using System.Drawing;
using System.Runtime.InteropServices;

namespace Volumey.UITests.Infrastructure;

/// <summary>
/// Win32 facts about a window: the monitor it's on, whether it's topmost
/// </summary>
internal static class Screens
{
	/// <summary>The device name of the monitor a window is on, without "\\.\" (e.g. "DISPLAY2", as Volumey's screen lists show it)</summary>
	public static string MonitorName(IntPtr hwnd) => Info(hwnd).szDevice.Replace(@"\\.\", "");

	/// <summary>The monitor's whole area, in physical pixels</summary>
	public static Rectangle MonitorBounds(IntPtr hwnd)
	{
		var r = Info(hwnd).rcMonitor;
		return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
	}

	/// <summary>The monitor's work area (without the taskbar), in physical pixels</summary>
	public static Rectangle WorkArea(IntPtr hwnd)
	{
		var r = Info(hwnd).rcWork;
		return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
	}

	public static int MonitorCount => GetSystemMetrics(80); //SM_CMONITORS

	public static bool IsTopmost(IntPtr hwnd) => (GetWindowLongPtr(hwnd, -20).ToInt64() & 0x8) != 0; //GWL_EXSTYLE, WS_EX_TOPMOST

	private static MONITORINFOEX Info(IntPtr hwnd)
	{
		var monitor = MonitorFromWindow(hwnd, 2); //MONITOR_DEFAULTTONEAREST
		var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
		if(!GetMonitorInfo(monitor, ref info))
			throw new InvalidOperationException("GetMonitorInfo failed");
		return info;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int Left, Top, Right, Bottom;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct MONITORINFOEX
	{
		public int cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public uint dwFlags;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string szDevice;
	}

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int index);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
}
