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

	/// <summary>A monitor: its work area in physical pixels and its scale (1.25 = 125%)</summary>
	public readonly record struct Monitor(Rectangle WorkArea, double Scale, bool IsPrimary);

	public static List<Monitor> All()
	{
		using var scope = PhysicalPixels();
		var monitors = new List<Monitor>();
		EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
		{
			var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
			if(GetMonitorInfo(monitor, ref info) && GetDpiForMonitor(monitor, 0 /*MDT_EFFECTIVE_DPI*/, out uint dpi, out _) == 0)
			{
				var r = info.rcWork;
				monitors.Add(new Monitor(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom), dpi / 96.0, (info.dwFlags & 1) != 0));
			}
			return true;
		}, IntPtr.Zero);
		return monitors;
	}

	/// <summary>The window's place in physical pixels</summary>
	public static Rectangle WindowRect(IntPtr hwnd)
	{
		using var scope = PhysicalPixels();
		if(!GetWindowRect(hwnd, out var r))
			throw new InvalidOperationException("GetWindowRect failed");
		return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
	}

	/// <summary>
	/// Until disposed, this thread's window positions, cursor positions and UI Automation rectangles are physical pixels on every monitor
	/// (otherwise Windows scales them for monitors that aren't at the primary's scale)
	/// </summary>
	public static IDisposable PhysicalPixels() => new DpiScope(SetThreadDpiAwarenessContext(new IntPtr(-4))); //PER_MONITOR_AWARE_V2

	private sealed class DpiScope(IntPtr previous) : IDisposable
	{
		public void Dispose() => SetThreadDpiAwarenessContext(previous);
	}

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

	private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);

	[DllImport("user32.dll")]
	private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

	[DllImport("shcore.dll")]
	private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

	[DllImport("user32.dll")]
	private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
