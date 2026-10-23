#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using SharpHook;
using SharpHook.Data;
using Volumey.Controls;

namespace Volumey.Helper;

/// <summary>
/// Mouse hotkeys through the app's input hook: buttons on press (once per press, a held button doesn't repeat) and each wheel step.
/// Never blocks: the app under the pointer gets the click or scroll too. The hook doesn't see the mouse while a window of an
/// elevated (administrator) app is in focus.
/// </summary>
internal sealed class MouseHotkeyManager : IHotkeyManager
{
	public event Action<HotKey>? HotkeyPressed;

	private readonly HashSet<HotKey> registeredHotkeys = new HashSet<HotKey>();
	private readonly Lock sync = new Lock();
	private readonly InputHookHost host;
	private readonly IDisposable mouse;
	private readonly SynchronizationContext? syncContext;

	/// <summary>
	/// Starts watching the mouse with the app's input hook. Create it on the UI thread: hotkeys are raised there.
	/// </summary>
	internal MouseHotkeyManager() : this(InputHookHost.Shared) { }

	internal MouseHotkeyManager(InputHookHost host)
	{
		this.host = host;
		syncContext = SynchronizationContext.Current;
		host.MousePressed += OnMousePressed;
		host.MouseWheel += OnMouseWheel;
		mouse = host.Use(GlobalHookType.Mouse);
	}

	public int RegisteredHotkeysCount
	{
		get
		{
			lock(sync)
				return registeredHotkeys.Count;
		}
	}

	public IReadOnlyList<HotKey> GetRegisteredHotkeys()
	{
		lock(sync)
			return registeredHotkeys.ToArray();
	}

	/// <summary>Never fails: other apps can't register mouse buttons</summary>
	public void RegisterHotkey(HotKey hotkey)
	{
		ArgumentNullException.ThrowIfNull(hotkey);
		lock(sync)
			registeredHotkeys.Add(hotkey);
	}

	public void UnregisterHotkey(HotKey hotkey)
	{
		ArgumentNullException.ThrowIfNull(hotkey);
		lock(sync)
			registeredHotkeys.Remove(hotkey);
	}

	//on the hook's thread: only match here, the work happens on the UI thread
	private void OnMousePressed(object? sender, MouseHookEventArgs e)
		=> Raise(ToHotkey(e.Data.Button, e.RawEvent.Mask));

	private void OnMouseWheel(object? sender, MouseWheelHookEventArgs e)
		=> Raise(ToHotkey(e.Data.Direction, e.Data.Rotation, e.RawEvent.Mask));

	private void Raise(HotKey? hotkey)
	{
		if(hotkey == null)
			return;
		lock(sync)
		{
			if(!registeredHotkeys.Contains(hotkey))
				return;
		}

		long received = Stopwatch.GetTimestamp();
		if(syncContext != null)
			syncContext.Post(_ =>
			{
				HotkeyDiagnostics.HookHotkeyHandled(hotkey, received);
				HotkeyPressed?.Invoke(hotkey);
			}, null);
		else
			HotkeyPressed?.Invoke(hotkey);
	}

	/// <summary>A button press as a hotkey with the held modifiers; null for a button a hotkey can't use</summary>
	internal static HotKey? ToHotkey(MouseButton button, EventMask mask)
	{
		string? name = button switch
		{
			MouseButton.Button1 => MouseButtons.Left,
			MouseButton.Button2 => MouseButtons.Right,
			MouseButton.Button3 => MouseButtons.Middle,
			MouseButton.Button4 => MouseButtons.XButton1,
			MouseButton.Button5 => MouseButtons.XButton2,
			_ => null,
		};
		return name == null ? null : HotKey.Mouse(name, SharpHookHotkeyManager.ToModifiers(mask));
	}

	/// <summary>A wheel step as a hotkey with the held modifiers; null for a step without rotation</summary>
	/// <param name="rotation">As libuiohook reports it on Windows: positive is up (vertical) and left (horizontal)</param>
	internal static HotKey? ToHotkey(MouseWheelScrollDirection direction, short rotation, EventMask mask)
	{
		if(rotation == 0)
			return null;
		string name = direction == MouseWheelScrollDirection.Horizontal
			? rotation > 0 ? MouseButtons.WheelLeft : MouseButtons.WheelRight
			: rotation > 0 ? MouseButtons.WheelUp : MouseButtons.WheelDown;
		return HotKey.Mouse(name, SharpHookHotkeyManager.ToModifiers(mask));
	}

	public void Dispose()
	{
		host.MousePressed -= OnMousePressed;
		host.MouseWheel -= OnMouseWheel;
		mouse.Dispose();
		HotkeyPressed = null;
	}
}
