#nullable enable
using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Volumey.Helper;

/// <summary>
/// Reads the mouse wheel through raw input while the cursor is over the tray icon. Raw input is registered when the cursor
/// enters the icon and removed when it leaves it, so no global hook is needed
/// </summary>
internal sealed class TrayWheelInput(IntPtr hwnd)
{
	private static readonly VIRTUAL_KEY[] ModifierKeys =
		[
			VIRTUAL_KEY.VK_SHIFT,
			VIRTUAL_KEY.VK_CONTROL,
			VIRTUAL_KEY.VK_MENU,
			VIRTUAL_KEY.VK_LWIN,
			VIRTUAL_KEY.VK_RWIN
		];

	private readonly HWND window = (HWND)hwnd;
	private RECT iconRect;
	//the part of a wheel notch that wasn't applied yet (touchpads send small deltas)
	private int remainder;

	public bool IsActive { get; private set; }

	/// <param name="rect">The icon's rectangle in physical screen coordinates</param>
	public bool Start(RECT rect)
	{
		iconRect = rect;
		remainder = 0;
		return IsActive = Register(RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK, window);
	}

	public void Stop()
	{
		if(!IsActive)
			return;
		IsActive = false;
		remainder = 0;
		Register(RAWINPUTDEVICE_FLAGS.RIDEV_REMOVE, default);
	}

	/// <summary>
	/// Handles a WM_INPUT message. Unregisters when the cursor is outside of the icon.
	/// </summary>
	/// <returns>The number of full wheel notches to apply: positive up, negative down, 0 for none</returns>
	public int ReadSteps(IntPtr lParam)
	{
		if(!IsActive)
			return 0;
		if(!PInvoke.GetCursorPos(out var cursor) || !IsInsideIcon(cursor.X, cursor.Y))
		{
			Stop();
			return 0;
		}

		RAWINPUT input = default;
		Span<byte> buffer = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref input, 1));
		uint size = (uint)buffer.Length;
		uint copied = PInvoke.GetRawInputData(new HRAWINPUT(lParam), RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, buffer, ref size,
			(uint)Marshal.SizeOf<RAWINPUTHEADER>());
		if(copied == uint.MaxValue || input.header.dwType != (uint)RID_DEVICE_INFO_TYPE.RIM_TYPEMOUSE)
			return 0;

		var mouse = input.data.mouse.Anonymous.Anonymous;
		if((mouse.usButtonFlags & (uint)PInvoke.RI_MOUSE_WHEEL) == 0)
			return 0;

		//the wheel must not also fire wheel hotkeys such as Ctrl+Wheel
		if(IsModifierPressed())
			return 0;

		int wheelDelta = (int)PInvoke.WHEEL_DELTA;
		int total = remainder + (short)mouse.usButtonData;
		remainder = total % wheelDelta;
		return total / wheelDelta;
	}

	private bool IsInsideIcon(int x, int y)
		=> x >= iconRect.left && x < iconRect.right && y >= iconRect.top && y < iconRect.bottom;

	private static bool IsModifierPressed()
	{
		foreach(VIRTUAL_KEY key in ModifierKeys)
		{
			if((PInvoke.GetAsyncKeyState((int)key) & 0x8000) != 0)
				return true;
		}
		return false;
	}

	private static bool Register(RAWINPUTDEVICE_FLAGS flags, HWND target)
	{
		var device = new RAWINPUTDEVICE
		{
			usUsagePage = 0x01,
			usUsage = 0x02,
			dwFlags = flags,
			hwndTarget = target
		};
		return PInvoke.RegisterRawInputDevices([device], (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
	}
}
