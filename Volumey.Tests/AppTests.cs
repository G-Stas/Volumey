using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Volumey.Tests;

[TestClass]
public class AppTests
{
	private sealed class WindowUpdateException : Win32Exception
	{
		private readonly string stackTrace;

		public WindowUpdateException(int error, string stackTrace) : base(error) => this.stackTrace = stackTrace;

		public override string StackTrace => stackTrace;
	}

	private const string WpfStack = "   at System.Windows.Interop.HwndTarget.UpdateWindowSettings(Boolean enableRenderTarget, Nullable`1 channelSet)\n" +
									"   at System.Windows.Interop.HwndTarget.UpdateWindowPos(IntPtr lParam)";

	[TestMethod]
	public void OnlyTheWpfWindowUpdateQuotaFailureIsIgnored()
	{
		Assert.IsTrue(App.IsWindowUpdateQuotaFailure(new WindowUpdateException(1816, WpfStack)));

		//other errors, or the same error elsewhere, still crash the app as before
		Assert.IsFalse(App.IsWindowUpdateQuotaFailure(new WindowUpdateException(5, WpfStack)));
		Assert.IsFalse(App.IsWindowUpdateQuotaFailure(new WindowUpdateException(1816, "   at Volumey.Helper.NativeMethods.Something()")));
		Assert.IsFalse(App.IsWindowUpdateQuotaFailure(new System.InvalidOperationException()));
	}

	[TestMethod]
	public void IgnoredFailuresAreReportedAtMostOncePerInterval()
	{
		var time = new System.DateTime(2026, 10, 9, 12, 0, 0);
		var guard = new Volumey.Helper.WindowUpdateFailureGuard(() => time);

		Assert.AreEqual(1, guard.CountFailure(), "the first failure is reported right away");
		Assert.AreEqual(0, guard.CountFailure());
		time += System.TimeSpan.FromSeconds(30);
		Assert.AreEqual(0, guard.CountFailure());

		//the next report counts the failures it held back
		time += Volumey.Helper.WindowUpdateFailureGuard.LogInterval;
		Assert.AreEqual(3, guard.CountFailure());
		Assert.AreEqual(0, guard.CountFailure());
	}

	[STATestMethod]
	public void RefreshingAWindowDoesntMoveOrResizeIt()
	{
		var window = new System.Windows.Window
		{
			Left = 120,
			Top = 80,
			Width = 300,
			Height = 200,
			ShowActivated = false,
			ShowInTaskbar = false,
			WindowStartupLocation = System.Windows.WindowStartupLocation.Manual
		};
		window.Show();
		try
		{
			Volumey.Helper.WindowUpdateFailureGuard.RefreshWindows(new[] { window });

			Assert.AreEqual(120, window.Left);
			Assert.AreEqual(80, window.Top);
			Assert.AreEqual(300, window.ActualWidth);
			Assert.AreEqual(200, window.ActualHeight);
		}
		finally
		{
			window.Close();
		}
	}
}
