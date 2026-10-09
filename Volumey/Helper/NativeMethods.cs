using System;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.LibraryLoader;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Volumey.Helper;

/// <summary>
/// Helpers over the Win32 declarations that CsWin32 generates into <see cref="PInvoke"/> (the APIs are listed in NativeMethods.txt)
/// </summary>
static class NativeMethods
{
	/// <summary>
	/// Reads a string resource from a system DLL
	/// </summary>
	/// <param name="file">System DLL name without its path</param>
	/// <param name="number">Non-negative string resource ID</param>
	internal static string ExtractStringFromSystemDLL(string file, int number)
	{
		using var lib = PInvoke.LoadLibraryEx(file, LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_AS_IMAGE_RESOURCE);
		if(lib.IsInvalid)
			return string.Empty;
		Span<char> buffer = stackalloc char[256];
		int length = PInvoke.LoadString(lib, (uint)number, buffer, buffer.Length);
		return length > 0 ? buffer[..length].ToString() : string.Empty;
	}

	/// <summary>
	/// Flashes the window's caption and taskbar button until it comes to the foreground
	/// </summary>
	internal static void FlashWindow(IntPtr handle)
	{
		var info = new FLASHWINFO
		{
			cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<FLASHWINFO>(),
			hwnd = (HWND)handle,
			dwFlags = FLASHWINFO_FLAGS.FLASHW_ALL | FLASHWINFO_FLAGS.FLASHW_TIMERNOFG,
			uCount = uint.MaxValue,
			dwTimeout = 0
		};
		PInvoke.FlashWindowEx(in info);
	}

	/// <summary>
	/// The process ID of the foreground window, 0 if there is none
	/// </summary>
	internal static uint GetForegroundWindowProcessId()
	{
		HWND handle = PInvoke.GetForegroundWindow();
		return PInvoke.GetWindowThreadProcessId(handle, out uint procId) > 0 ? procId : 0;
	}

	/// <summary>
	/// Sends a press and a release of the key to the foreground window
	/// </summary>
	internal static void SimulateKeyPress(Key keyToSimulate)
	{
		var press = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
		press.Anonymous.ki.wVk = (VIRTUAL_KEY)KeyInterop.VirtualKeyFromKey(keyToSimulate);
		var release = press;
		release.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
		PInvoke.SendInput([press, release], System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
	}

	/// <summary>
	/// The full path of a process's executable, null if the process can't be opened (e.g. it runs elevated)
	/// </summary>
	/// <param name="error">The Win32 error when the path couldn't be read</param>
	internal static string GetProcessImagePath(int processId, out int error)
	{
		using var process = PInvoke.OpenProcess_SafeHandle(Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
			false, (uint)processId);
		if(process.IsInvalid)
		{
			error = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
			return null;
		}
		//paths can be longer than MAX_PATH; grow the buffer up to the longest possible path
		for(int size = 260; size <= 32768; size *= 2)
		{
			var buffer = new char[size];
			uint length = (uint)buffer.Length;
			if(PInvoke.QueryFullProcessImageName(process, Windows.Win32.System.Threading.PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref length))
			{
				error = 0;
				return new string(buffer, 0, (int)length);
			}
			error = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
			if(error != (int)WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
				return null;
		}
		error = (int)WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER;
		return null;
	}
}
