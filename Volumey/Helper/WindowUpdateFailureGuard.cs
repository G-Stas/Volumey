using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Volumey.Helper
{
	/// <summary>
	/// Follows up on WPF's ignored "not enough quota" window update failure (GitHub issues #58, #66, see <see cref="App"/>):
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
			this.unreported++;
			var time = this.now();
			if(this.lastReport is DateTime last && time - last < LogInterval)
				return 0;
			this.lastReport = time;
			int count = this.unreported;
			this.unreported = 0;
			return count;
		}

		/// <summary>
		/// Refreshes the visible windows <see cref="RefreshDelay"/> after the last failure. Each failure restarts the delay,
		/// so a lasting failure leads to at most one refresh per delay and never to a loop of immediate retries.
		/// </summary>
		internal void ScheduleRefresh(Dispatcher dispatcher)
		{
			if(this.refreshTimer == null)
			{
				this.refreshTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = RefreshDelay };
				this.refreshTimer.Tick += (s, e) =>
				{
					this.refreshTimer.Stop();
					RefreshWindows(Application.Current.Windows.OfType<Window>());
				};
			}
			this.refreshTimer.Stop();
			this.refreshTimer.Start();
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
				NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
					NativeMethods.SetWindowPosFlags.NoMove | NativeMethods.SetWindowPosFlags.NoSize | NativeMethods.SetWindowPosFlags.NoZOrder |
					NativeMethods.SetWindowPosFlags.NoActivate | NativeMethods.SetWindowPosFlags.FrameChanged);
			}
		}
	}
}
