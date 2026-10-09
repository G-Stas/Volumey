using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using log4net;
using Volumey.Model;

namespace Volumey.Helper
{
	/// <summary>
	/// Dev-build-only logging for the "foreground app" hotkeys in games (GitHub issues #63, #88): when a press reaches the app,
	/// how long it waited, and what the foreground window lookup sees. The calls are removed from Release builds
	/// (<see cref="ConditionalAttribute"/>), so nothing is sent to Loggly.
	/// </summary>
	internal static class HotkeyDiagnostics
	{
		private static readonly ILog Logger = LogManager.GetLogger("Volumey.HotkeyDiagnostics");

		/// <summary>
		/// A WM_HOTKEY message is being handled; <paramref name="messageTime"/> is its GetMessageTime value
		/// </summary>
		[Conditional("DEBUG")]
		internal static void WmHotkeyReceived(int hotkeyId, int messageTime)
			=> Logger.Debug($"WM_HOTKEY id {hotkeyId}, waited {unchecked(Environment.TickCount - messageTime)} ms in the message queue");

		/// <summary>
		/// The keyboard hook (non-blocking mode) matched a hotkey; <paramref name="hookTimestamp"/> is a <see cref="Stopwatch"/> timestamp taken on the hook thread
		/// </summary>
		[Conditional("DEBUG")]
		internal static void HookHotkeyHandled(object hotkey, long hookTimestamp)
			=> Logger.Debug($"Hook hotkey [{hotkey}] reached the UI thread after {Stopwatch.GetElapsedTime(hookTimestamp).TotalMilliseconds:0} ms");

		[Conditional("DEBUG")]
		internal static void ForegroundHotkeyPressed(object hotkey)
			=> Logger.Debug($"Foreground hotkey [{hotkey}] pressed, foreground window: {DescribeForegroundWindow()}");

		[Conditional("DEBUG")]
		internal static void ForegroundLookup(uint foregroundPid, string processName, string filePath, Exception pathError,
			IEnumerable<AudioProcessModel> mixerApps, AudioProcessModel match, double elapsedMs)
		{
			string path = pathError != null ? $"path not readable ({pathError.GetType().Name}: {pathError.Message})" : $"path [{filePath}]";
			string apps = string.Join(", ", mixerApps.Select(p => $"{p.RawProcessName}({p.ProcessId})"));
			string result = match == null ? "NO MATCH" : $"matched [{match.RawProcessName}]({match.ProcessId}) by {MatchedBy(match, foregroundPid, processName, filePath)}";
			Logger.Debug($"Foreground lookup: pid {foregroundPid} [{processName}], {path}; mixer apps: {apps}; {result}; took {elapsedMs:0.0} ms");
		}

		[Conditional("DEBUG")]
		internal static void ForegroundVolumeSet(AudioProcessModel process, int volume)
			=> Logger.Debug($"Foreground app [{process.RawProcessName}] volume set to {volume}");

		private static string MatchedBy(AudioProcessModel match, uint pid, string processName, string filePath)
		{
			if(match.ProcessId == pid)
				return "pid";
			if(match.RawProcessName.Equals(processName))
				return "process name";
			return match.FilePath.Equals(filePath) ? "file path" : "?";
		}

		private static string DescribeForegroundWindow()
		{
			try
			{
				IntPtr hwnd = GetForegroundWindow();
				if(hwnd == IntPtr.Zero)
					return "none";
				GetWindowThreadProcessId(hwnd, out uint pid);
				var className = new StringBuilder(256);
				GetClassName(hwnd, className, className.Capacity);

				string fullscreen = "?";
				if(GetWindowRect(hwnd, out RECT window))
				{
					var monitor = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
					if(GetMonitorInfo(MonitorFromWindow(hwnd, 2 /*MONITOR_DEFAULTTONEAREST*/), ref monitor))
					{
						var m = monitor.rcMonitor;
						fullscreen = window.Left <= m.Left && window.Top <= m.Top && window.Right >= m.Right && window.Bottom >= m.Bottom ? "yes" : "no";
					}
				}
				return $"pid {pid}, class [{className}], covers its monitor: {fullscreen}";
			}
			catch(Exception e)
			{
				return $"failed to describe ({e.Message})";
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT { public int Left, Top, Right, Bottom; }

		[StructLayout(LayoutKind.Sequential)]
		private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

		[DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
		[DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);
		[DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
		[DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
		[DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
		[DllImport("user32.dll")] internal static extern int GetMessageTime();
	}
}
