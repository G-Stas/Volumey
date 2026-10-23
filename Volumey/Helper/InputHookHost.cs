#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using SharpHook;
using SharpHook.Data;

namespace Volumey.Helper;

/// <summary>
/// The app's one low-level input hook (SharpHook). libuiohook keeps its state in globals, so a process can run only one hook:
/// the keyboard hotkeys of the shared hotkey mode and the mouse hotkeys both listen here. The hook only watches what its users
/// need (keys, the mouse, or both) and is replaced when that changes, which happens only when hotkeys are added or removed.
/// It never blocks input: other apps get every key and button too.
/// </summary>
internal sealed class InputHookHost
{
	internal static InputHookHost Shared { get; } = new InputHookHost(() => new EventLoopGlobalHook(useBackgroundThreadForEventLoop: true), start: true);

	/// <summary>Changes are gathered for this long before the hook is replaced: the app registers its hotkeys one by one at start</summary>
	private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(150);


	/// <summary>How long a hook may take to start or to stop</summary>
	private static readonly TimeSpan HookTimeout = TimeSpan.FromSeconds(3);

	private static ILog? logger;
	private static ILog Logger => logger ??= LogManager.GetLogger(typeof(InputHookHost));

	private readonly Func<IGlobalHook> createHook;
	private readonly bool start;
	private readonly Lock sync = new Lock();
	private readonly SemaphoreSlim changed = new SemaphoreSlim(0);
	private Task? worker;
	private int keyboardUsers;
	private int mouseUsers;

	//only the worker (or Apply when not starting hooks) changes these
	private IGlobalHook? hook;
	private Thread? hookThread;
	private ManualResetEventSlim? hookEnabled;
	private GlobalHookType? running;

	/// <summary>Raised on the hook's thread</summary>
	public event EventHandler<KeyboardHookEventArgs>? KeyPressed;

	/// <summary>Raised on the hook's thread</summary>
	public event EventHandler<MouseHookEventArgs>? MousePressed;

	/// <summary>Raised on the hook's thread</summary>
	public event EventHandler<MouseWheelHookEventArgs>? MouseWheel;

	/// <param name="createHook">A new, not yet running hook; one per start</param>
	/// <param name="start">Runs the hooks on a background thread; false: the hook is replaced at once and not run
	/// (tests raise the events of a fake hook)</param>
	internal InputHookHost(Func<IGlobalHook> createHook, bool start)
	{
		this.createHook = createHook;
		this.start = start;
	}

	/// <summary>The type of the hook now, null while there's none</summary>
	internal GlobalHookType? Running
	{
		get
		{
			lock(sync)
				return running;
		}
	}

	/// <summary>Watches the keyboard or the mouse until the returned object is disposed</summary>
	/// <param name="input"><see cref="GlobalHookType.Keyboard"/> or <see cref="GlobalHookType.Mouse"/></param>
	internal IDisposable Use(GlobalHookType input)
	{
		lock(sync)
		{
			if(input == GlobalHookType.Mouse)
				mouseUsers++;
			else
				keyboardUsers++;
		}
		Changed();
		return new Lease(this, input);
	}

	private void Release(GlobalHookType input)
	{
		lock(sync)
		{
			if(input == GlobalHookType.Mouse)
				mouseUsers = Math.Max(0, mouseUsers - 1);
			else
				keyboardUsers = Math.Max(0, keyboardUsers - 1);
		}
		Changed();
	}

	private GlobalHookType? Wanted()
	{
		lock(sync)
		{
			return (keyboardUsers > 0, mouseUsers > 0) switch
			{
				(true, true) => GlobalHookType.All,
				(true, false) => GlobalHookType.Keyboard,
				(false, true) => GlobalHookType.Mouse,
				_ => null,
			};
		}
	}

	private void Changed()
	{
		if(!start)
		{
			lock(sync)
				Apply(Wanted());
			return;
		}
		changed.Release();
		lock(sync)
			worker ??= Task.Run(WorkAsync);
	}

	/// <summary>Replaces the hook one change at a time, off the UI thread (starting and stopping a hook takes a moment)</summary>
	private async Task WorkAsync()
	{
		while(true)
		{
			await changed.WaitAsync().ConfigureAwait(false);
			await Task.Delay(Settle).ConfigureAwait(false);
			while(changed.CurrentCount > 0)
				await changed.WaitAsync().ConfigureAwait(false);
			try
			{
				Apply(Wanted());
			}
			catch(Exception e)
			{
				Logger.Error("Failed to change the input hook", e);
			}
		}
	}

	private void Apply(GlobalHookType? wanted)
	{
		if(wanted == running)
			return;
		Stop();
		if(wanted is not { } type)
			return;

		var newHook = createHook();
		newHook.KeyPressed += OnKeyPressed;
		newHook.MousePressed += OnMousePressed;
		newHook.MouseWheel += OnMouseWheel;
		hook = newHook;
		lock(sync)
			running = type;
		if(!start)
			return;

		var enabled = new ManualResetEventSlim();
		newHook.HookEnabled += (_, _) => enabled.Set();
		hookEnabled = enabled;
		//Run on an own thread, not RunAsync: RunAsync's task completes before its thread unsets the hook's callback in libuiohook,
		//which is one for the whole process, so a hook started right after it would lose its callback and get no input
		hookThread = new Thread(() =>
		{
			try
			{
				newHook.Run(type);
			}
			catch(Exception e)
			{
				Logger.Error($"The input hook ({type}) failed", e);
			}
		})
		{
			IsBackground = true,
			Name = $"Input hook ({type})",
		};
		hookThread.Start();
		if(!enabled.Wait(HookTimeout))
			Logger.Warn($"The input hook ({type}) didn't start in time");
	}

	/// <summary>
	/// Stops the hook and waits until its thread has ended, which is when libuiohook is free for the next one.
	/// A hook disposed before it has started isn't stopped (SharpHook only stops a running hook), so a starting hook is waited for first.
	/// </summary>
	private void Stop()
	{
		if(hook == null)
			return;
		hook.KeyPressed -= OnKeyPressed;
		hook.MousePressed -= OnMousePressed;
		hook.MouseWheel -= OnMouseWheel;
		hookEnabled?.Wait(HookTimeout);
		hook.Dispose();
		if(hookThread != null && !hookThread.Join(HookTimeout))
			Logger.Warn("The input hook didn't stop in time");
		hook = null;
		hookThread = null;
		hookEnabled?.Dispose();
		hookEnabled = null;
		lock(sync)
			running = null;
	}

	private void OnKeyPressed(object? sender, KeyboardHookEventArgs e) => KeyPressed?.Invoke(this, e);

	private void OnMousePressed(object? sender, MouseHookEventArgs e) => MousePressed?.Invoke(this, e);

	private void OnMouseWheel(object? sender, MouseWheelHookEventArgs e) => MouseWheel?.Invoke(this, e);

	private sealed class Lease : IDisposable
	{
		private InputHookHost? host;
		private readonly GlobalHookType input;

		public Lease(InputHookHost host, GlobalHookType input)
		{
			this.host = host;
			this.input = input;
		}

		public void Dispose()
		{
			Interlocked.Exchange(ref host, null)?.Release(input);
		}
	}
}
