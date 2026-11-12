#nullable enable
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
/// Follows up on a WPF window update failure that <see cref="App"/> ignores ("not enough quota",
/// "desktop composition disabled"), one instance per kind of failure:
/// reports it at most once per <see cref="LogInterval"/>, so a lasting failure can't flood the log (or Loggly), with the count held back
/// since the last report (also once the failures stop),
/// and refreshes the app's windows once the failures stop, so a skipped update doesn't leave a window stale until it's moved.
/// </summary>
internal sealed class WindowUpdateFailureGuard
{
	internal static readonly TimeSpan LogInterval = TimeSpan.FromMinutes(1);
	private static readonly TimeSpan RefreshDelay = TimeSpan.FromSeconds(1);

	private readonly Func<DateTime> now;
	private DateTime? lastReport;
	private int unreported;
	private DispatcherTimer? refreshTimer;

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
	/// The failures held back since the last report once <see cref="LogInterval"/> has passed since it, else 0.
	/// Taking them counts as a report
	/// </summary>
	internal int TakeDueFailures()
	{
		var time = now();
		if(unreported == 0 || (lastReport is DateTime last && time - last < LogInterval))
			return 0;
		lastReport = time;
		int count = unreported;
		unreported = 0;
		return count;
	}

	/// <summary>How long until the held-back failures can be reported; null if there are none</summary>
	internal TimeSpan? UntilReportDue()
	{
		if(unreported == 0)
			return null;
		var due = lastReport is DateTime last ? last + LogInterval - now() : TimeSpan.Zero;
		return due > TimeSpan.Zero ? due : TimeSpan.Zero;
	}

	private DispatcherTimer? reportTimer;

	/// <summary>
	/// Reports the failures held back by <see cref="CountFailure"/> once the interval since the last report has passed, so the last
	/// ones aren't lost when the failures stop. One pending report at a time.
	/// </summary>
	internal void ScheduleReport(Dispatcher dispatcher, Action<int> report)
	{
		if(UntilReportDue() is not TimeSpan due || reportTimer?.IsEnabled == true)
			return;
		var timer = reportTimer ??= new DispatcherTimer(DispatcherPriority.Background, dispatcher);
		timer.Interval = due;
		EventHandler? onTick = null;
		onTick = (s, e) =>
		{
			timer.Stop();
			timer.Tick -= onTick;
			int count = TakeDueFailures();
			if(count > 0)
				report(count);
		};
		timer.Tick += onTick;
		timer.Start();
	}

	/// <summary>
	/// Refreshes the visible windows <see cref="RefreshDelay"/> after the last failure. Each failure restarts the delay,
	/// so a lasting failure leads to at most one refresh per delay and never to a loop of immediate retries.
	/// </summary>
	internal void ScheduleRefresh(Dispatcher dispatcher)
	{
		if(refreshTimer == null)
		{
			var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = RefreshDelay };
			timer.Tick += (s, e) =>
			{
				timer.Stop();
				RefreshWindows(Application.Current.Windows.OfType<Window>());
			};
			refreshTimer = timer;
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
