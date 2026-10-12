using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Volumey.UITests.Infrastructure;

/// <summary>
/// Catches the media keys Volumey sends (a media key hotkey simulates the real key) with a low-level keyboard hook and swallows them,
/// so the owner's music player never gets them. Installed after Volumey started, the hook is called before any other app's.
/// </summary>
internal sealed class MediaKeyCatcher : IDisposable
{
	public const int NextTrack = 0xB0, PreviousTrack = 0xB1, Stop = 0xB2, PlayPause = 0xB3;

	private const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x100, WM_SYSKEYDOWN = 0x104, WM_QUIT = 0x12;

	private readonly ConcurrentQueue<int> pressed = new();
	private readonly Thread thread;
	private readonly HookProc proc;
	private uint threadId;

	public MediaKeyCatcher()
	{
		proc = OnKey;
		using var installed = new ManualResetEventSlim();
		IntPtr hook = IntPtr.Zero;
		thread = new Thread(() =>
		{
			threadId = GetCurrentThreadId();
			hook = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(null), 0);
			installed.Set();
			if(hook == IntPtr.Zero)
				return;
			//the hook is called on this thread while it waits for messages
			while(GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
			UnhookWindowsHookEx(hook);
		}) { IsBackground = true };
		thread.Start();
		installed.Wait();
		if(hook == IntPtr.Zero)
			throw new InvalidOperationException($"Couldn't install the keyboard hook: error {Marshal.GetLastWin32Error()}");
	}

	/// <summary>Waits until the media key was pressed (and swallowed); false after the timeout</summary>
	public bool WaitFor(int virtualKey, int timeoutMs = 3000)
	{
		var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		do
		{
			if(pressed.Contains(virtualKey))
				return true;
			Thread.Sleep(50);
		} while(DateTime.UtcNow < end);
		return false;
	}

	private IntPtr OnKey(int code, IntPtr message, IntPtr data)
	{
		if(code >= 0)
		{
			int vk = Marshal.ReadInt32(data);
			if(vk is >= NextTrack and <= PlayPause)
			{
				if((int)message is WM_KEYDOWN or WM_SYSKEYDOWN)
					pressed.Enqueue(vk);
				//swallowed: no other hook or app gets it
				return 1;
			}
		}
		return CallNextHookEx(IntPtr.Zero, code, message, data);
	}

	public void Dispose()
	{
		PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
		thread.Join(2000);
	}

	private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr SetWindowsHookEx(int hook, HookProc proc, IntPtr module, uint threadId);

	[DllImport("user32.dll")]
	private static extern bool UnhookWindowsHookEx(IntPtr hook);

	[DllImport("user32.dll")]
	private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

	[DllImport("user32.dll")]
	private static extern int GetMessage(out MSG message, IntPtr hwnd, uint min, uint max);

	[DllImport("user32.dll")]
	private static extern bool PostThreadMessage(uint threadId, int message, IntPtr wParam, IntPtr lParam);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr GetModuleHandle(string? name);

	[StructLayout(LayoutKind.Sequential)]
	private struct MSG
	{
		public IntPtr hwnd;
		public uint message;
		public IntPtr wParam;
		public IntPtr lParam;
		public uint time;
		public int x, y;
	}
}
