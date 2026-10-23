#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using SharpHook;
using SharpHook.Data;
using Volumey.Controls;

namespace Volumey.Helper;

/// <summary>
/// Provides global hotkeys through a low-level keyboard hook (SharpHook).
/// Doesn't block registered hotkey combinations: other apps still get the keys.
/// The hook doesn't see keys while a window of an elevated (administrator) app is in focus.
/// </summary>
internal sealed class SharpHookHotkeyManager : IHotkeyManager
{
	public event Action<HotKey>? HotkeyPressed;

	private readonly HashSet<HotKey> registeredHotkeys = new HashSet<HotKey>();
	private readonly Lock sync = new Lock();
	private readonly InputHookHost host;
	private readonly IDisposable keyboard;
	private readonly SynchronizationContext? syncContext;

	/// <summary>
	/// Starts watching the keyboard with the app's input hook. Create it on the UI thread: hotkeys are raised there.
	/// </summary>
	internal SharpHookHotkeyManager() : this(InputHookHost.Shared) { }

	internal SharpHookHotkeyManager(IGlobalHook hook, bool start) : this(new InputHookHost(() => hook, start)) { }

	internal SharpHookHotkeyManager(InputHookHost host)
	{
		this.host = host;
		syncContext = SynchronizationContext.Current;
		host.KeyPressed += OnKeyPressed;
		keyboard = host.Use(GlobalHookType.Keyboard);
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

	/// <summary>
	/// Registers a hotkey. Never fails: a hook can't tell whether another app uses the same combination.
	/// </summary>
	public void RegisterHotkey(HotKey hotkey)
	{
		if(hotkey == null)
			throw new NullReferenceException("Hotkey is null");
		lock(sync)
			registeredHotkeys.Add(hotkey);
	}

	public void UnregisterHotkey(HotKey hotkey)
	{
		if(hotkey == null)
			throw new NullReferenceException("Hotkey is null");
		lock(sync)
			registeredHotkeys.Remove(hotkey);
	}

	//runs on the hook's event loop thread: only match the keys here, the work happens on the UI thread
	private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
	{
		var hotkey = ToHotkey(e.Data.RawCode, e.RawEvent.Mask);
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

	/// <summary>
	/// Converts a key press to a hotkey. Returns null for a modifier key pressed on its own.
	/// </summary>
	/// <param name="virtualKeyCode">On Windows the raw code of a key event is its virtual-key code</param>
	internal static HotKey? ToHotkey(ushort virtualKeyCode, EventMask mask)
	{
		var key = KeyInterop.KeyFromVirtualKey(virtualKeyCode);
		switch(key)
		{
			case Key.None:
			case Key.LeftShift:
			case Key.RightShift:
			case Key.LeftCtrl:
			case Key.RightCtrl:
			case Key.LeftAlt:
			case Key.RightAlt:
			case Key.LWin:
			case Key.RWin:
			case Key.System:
				return null;
		}

		return new HotKey(key, ToModifiers(mask));
	}

	/// <summary>The modifier keys held during a hook event</summary>
	internal static ModifierKeys ToModifiers(EventMask mask)
	{
		var modifiers = ModifierKeys.None;
		if((mask & EventMask.Alt) != 0)
			modifiers |= ModifierKeys.Alt;
		if((mask & EventMask.Ctrl) != 0)
			modifiers |= ModifierKeys.Control;
		if((mask & EventMask.Shift) != 0)
			modifiers |= ModifierKeys.Shift;
		if((mask & EventMask.Meta) != 0)
			modifiers |= ModifierKeys.Windows;
		return modifiers;
	}

	public void Dispose()
	{
		host.KeyPressed -= OnKeyPressed;
		keyboard.Dispose();
		HotkeyPressed = null;
	}
}
