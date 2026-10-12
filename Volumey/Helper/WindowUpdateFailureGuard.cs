using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Volumey.Helper;

/// <summary>
/// Follows up on a WPF window update failure that <see cref="App"/> ignores ("not enough quota", GitHub issues #58, #66;
/// "desktop composition disabled"), one instance per kind of failure:
/// reports it at most once per <see cref="LogInterval"/>, so a lasting failure can't flood the log (or Loggly),
/// and refreshes the app's windows once the failures stop, so a skipped update doesn't leave a window stale until it's moved.
/// </summary>
internal sealed class WindowUpdateFailureGuard
{
	internal static readonly TimeSpan LogInterval = TimeSpan.FromMinutes(1);
	private static readonly TimeSpan RefreshDelay = TimeSpan.FromSeconds(1);

	private readonly Func<DateTime> now;
	private DateTime? lastReport;
	private int unreported;
	private DispatcherTimer refreshTimer;

	internal WindowUpdateFailureGuard(Func<DateTime> now) => this.now = now;

	/// <summary>
	/// Counts a failure. Returns how many failures to report now (this one included), or 0 if it's only counted for the next report.
	/// </summary>
	internal int CountFailure()
	{
		unreported++;
		var time = now();
		if(lastReport is DateTime last && time - last < LogInterval)
			return 0;
		lastReport = time;
		int count = unreported;
		unreported = 0;
		return count;
	}

	/// <summary>
	/// Refreshes the visible windows <see cref="RefreshDelay"/> after the last failure. Each failure restarts the delay,
	/// so a lasting failure leads to at most one refresh per delay and never to a loop of immediate retries.
	/// </summary>
	internal void ScheduleRefresh(Dispatcher dispatcher)
	{
		if(refreshTimer == null)
		{
			refreshTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = RefreshDelay };
			refreshTimer.Tick += (s, e) =>
			{
				refreshTimer.Stop();
				RefreshWindows(Application.Current.Windows.OfType<Window>());
			};
		}
		refreshTimer.Stop();
		refreshTimer.Start();
	}

	internal static void RefreshWindows(IEnumerable<Window> windows)
	{
		foreach(Window window in windows)
		{
			if(!window.IsVisible)
				continue;
			var hwnd = new WindowInteropHelper(window).Handle;
			if(hwnd == IntPtr.Zero)
				continue;
			//a position "change" that changes nothing: WPF handles the resulting WM_WINDOWPOSCHANGED by updating the window's render target again
			PInvoke.SetWindowPos((HWND)hwnd, HWND.Null, 0, 0, 0, 0,
				SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
				SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
		}
	}
}
