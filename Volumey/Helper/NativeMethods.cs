using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Windows.Wdk.System.SystemInformation;
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
	/// The full path of a process's executable: read from the process, else from the system's process list
	/// (<see cref="GetProcessImagePathFromSystem"/>). Null if neither has it (e.g. the process has exited)
	/// </summary>
	/// <param name="error">The Win32 error when the process couldn't be read (the path may still come from the process list)</param>
	internal static string GetProcessPath(int processId, out int error)
		=> GetProcessImagePath(processId, out error) ?? GetProcessImagePathFromSystem(processId);

	/// <summary>
	/// A process's name from its executable's path, the way <see cref="System.Diagnostics.Process.ProcessName"/> forms it:
	/// the file name without ".exe" (other extensions stay). App hotkeys are saved by this name
	/// </summary>
	internal static string ProcessNameFromPath(string path)
	{
		string file = System.IO.Path.GetFileName(path);
		return file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
	}

	/// <summary>
	/// The full path of a process's executable, null if the process can't be opened. It asks for limited query rights only,
	/// which elevated apps of the same user also give; other users' processes and games guarded by anti-cheat refuse them
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

	[StructLayout(LayoutKind.Sequential)]
	private struct SYSTEM_PROCESS_ID_INFORMATION
	{
		public nint ProcessId;
		public UNICODE_STRING ImageName;
	}

	//SystemProcessIdInformation: not in the SDK's documentation, but in Windows since Vista (Process Explorer and System Informer use it)
	private const SYSTEM_INFORMATION_CLASS SystemProcessIdInformation = (SYSTEM_INFORMATION_CLASS)88;
	private static readonly NTSTATUS StatusInfoLengthMismatch = new(unchecked((int)0xC0000004));

	/// <summary>
	/// The full path of a process's executable from the system's process list, without opening the process, so also for processes
	/// that can't be opened (other users', protected ones, games guarded by anti-cheat). Null if the process isn't in the list
	/// (it has exited) or its volume has no drive letter
	/// </summary>
	internal static unsafe string GetProcessImagePathFromSystem(int processId)
	{
		var name = new char[260];
		//the second attempt has the size the first one asked for
		for(int attempt = 0; attempt < 2; attempt++)
		{
			fixed(char* buffer = name)
			{
				var info = new SYSTEM_PROCESS_ID_INFORMATION
				{
					ProcessId = processId,
					ImageName = new UNICODE_STRING { MaximumLength = (ushort)(name.Length * sizeof(char)), Buffer = buffer }
				};
				NTSTATUS status = Windows.Wdk.PInvoke.NtQuerySystemInformation(SystemProcessIdInformation, &info, (uint)sizeof(SYSTEM_PROCESS_ID_INFORMATION), null);
				if(status.SeverityCode == NTSTATUS.Severity.Success)
					return ToDosPath(new string(buffer, 0, info.ImageName.Length / sizeof(char)), DeviceOfDrive);
				if(status != StatusInfoLengthMismatch || info.ImageName.MaximumLength <= name.Length * sizeof(char))
					return null;
				name = new char[info.ImageName.MaximumLength / sizeof(char)];
			}
		}
		return null;
	}

	/// <summary>
	/// A path in the kernel's form ("\Device\HarddiskVolume3\Games\app.exe") with its drive letter instead ("C:\Games\app.exe");
	/// null if no drive letter's device holds it
	/// </summary>
	/// <param name="deviceOfDrive">The device of a drive ("C:" → "\Device\HarddiskVolume3"), null for a drive that doesn't exist</param>
	internal static string ToDosPath(string ntPath, Func<string, string> deviceOfDrive)
	{
		for(char letter = 'A'; letter <= 'Z'; letter++)
		{
			string drive = $"{letter}:";
			string device = deviceOfDrive(drive);
			if(!string.IsNullOrEmpty(device) && ntPath.Length > device.Length && ntPath[device.Length] == '\\'
			   && ntPath.StartsWith(device, StringComparison.OrdinalIgnoreCase))
				return string.Concat(drive, ntPath.AsSpan(device.Length));
		}
		return null;
	}

	private static string DeviceOfDrive(string drive)
	{
		//a list of names, the current one first, each ending with a null character
		Span<char> target = stackalloc char[260];
		uint length = PInvoke.QueryDosDevice(drive, target);
		if(length == 0)
			return null;
		var names = target[..(int)length];
		int end = names.IndexOf('\0');
		return (end < 0 ? names : names[..end]).ToString();
	}
}
