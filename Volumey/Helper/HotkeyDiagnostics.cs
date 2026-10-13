using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using log4net;
using Volumey.Model;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Volumey.Helper;

/// <summary>
/// Dev-build-only logging for the "foreground app" hotkeys in games: when a press reaches the app,
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
			HWND hwnd = PInvoke.GetForegroundWindow();
			if(hwnd.IsNull)
				return "none";
			PInvoke.GetWindowThreadProcessId(hwnd, out uint pid);
			Span<char> classBuffer = stackalloc char[256];
			string className = classBuffer[..PInvoke.GetClassName(hwnd, classBuffer)].ToString();

			string fullscreen = "?";
			if(PInvoke.GetWindowRect(hwnd, out RECT window))
			{
				var monitor = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
				if(PInvoke.GetMonitorInfo(PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST), ref monitor))
				{
					var m = monitor.rcMonitor;
					fullscreen = window.left <= m.left && window.top <= m.top && window.right >= m.right && window.bottom >= m.bottom ? "yes" : "no";
				}
			}
			return $"pid {pid}, class [{className}], covers its monitor: {fullscreen}";
		}
		catch(Exception e)
		{
			return $"failed to describe ({e.Message})";
		}
	}

	/// <summary>
	/// The time of the message being handled (GetMessageTime), for <see cref="WmHotkeyReceived"/>
	/// </summary>
	internal static int GetMessageTime() => PInvoke.GetMessageTime();
}
