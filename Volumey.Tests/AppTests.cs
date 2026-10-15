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

		//other errors, or the same error elsewhere, still crash the app
		Assert.IsFalse(App.IsWindowUpdateQuotaFailure(new WindowUpdateException(5, WpfStack)));
		Assert.IsFalse(App.IsWindowUpdateQuotaFailure(new WindowUpdateException(1816, "   at Volumey.Helper.NativeMethods.Something()")));
		Assert.IsFalse(App.IsWindowUpdateQuotaFailure(new System.InvalidOperationException()));
	}

	private sealed class CompositionException : System.Runtime.InteropServices.COMException
	{
		private readonly string stackTrace;

		public CompositionException(int hresult, string stackTrace) : base("composition", hresult) => this.stackTrace = stackTrace;

		public override string StackTrace => stackTrace;
	}

	//a real stack of this crash
	private const string WindowChromeStack = "   at Standard.NativeMethods.DwmExtendFrameIntoClientArea(IntPtr hwnd, MARGINS& pMarInset)\n" +
											 "   at System.Windows.Shell.WindowChromeWorker._ExtendGlassFrame()\n" +
											 "   at System.Windows.Shell.WindowChromeWorker._HandleDwmCompositionChanged(WM uMsg, IntPtr wParam, IntPtr lParam, Boolean& handled)";

	[TestMethod]
	public void OnlyTheWindowChromeCompositionFailureIsIgnored()
	{
		const int compositionDisabled = unchecked((int)0x80263001);
		Assert.IsTrue(App.IsCompositionChangeFailure(new CompositionException(compositionDisabled, WindowChromeStack)));
		//the same call can fail with another code
		Assert.IsTrue(App.IsCompositionChangeFailure(new CompositionException(unchecked((int)0xD0000701), WindowChromeStack)));

		//the same error elsewhere, or another exception there, still crashes the app
		Assert.IsFalse(App.IsCompositionChangeFailure(new CompositionException(compositionDisabled, "   at Volumey.Helper.NativeMethods.Something()")));
		Assert.IsFalse(App.IsCompositionChangeFailure(new TabletException(WindowChromeStack)));
		Assert.IsFalse(App.IsCompositionChangeFailure(new WindowUpdateException(1816, WpfStack)));
		Assert.IsFalse(App.IsWindowUpdateQuotaFailure(new CompositionException(compositionDisabled, WindowChromeStack)));
	}

	private sealed class PopupException : System.NullReferenceException
	{
		private readonly string stackTrace;
		public PopupException(string stackTrace) => this.stackTrace = stackTrace;
		public override string StackTrace => stackTrace;
	}

	private sealed class TabletException : System.InvalidOperationException
	{
		private readonly string stackTrace;
		public TabletException(string stackTrace) => this.stackTrace = stackTrace;
		public override string StackTrace => stackTrace;
	}

	//real stacks of these crashes
	private const string PopupStack = "   at System.Windows.Controls.Primitives.Popup.OnWindowResize(Object sender, AutoResizedEventArgs e)\n" +
									  "   at System.Windows.Interop.HwndSource.Resize(Size newSize)\n" +
									  "   at System.Windows.Interop.HwndSource.OnLayoutUpdated(Object obj, EventArgs args)";

	private const string TabletStack = "   at System.Collections.Generic.Dictionary`2.ValueCollection.Enumerator.MoveNext()\n" +
									   "   at System.Windows.Input.StylusWisp.WispLogic.RefreshTablets()\n" +
									   "   at System.Windows.Input.StylusWisp.WispLogic.OnTabletAdded(UInt32 wisptisIndex)";

	[TestMethod]
	public void OnlyThePopupsOwnResizeFailureIsIgnored()
	{
		Assert.IsTrue(App.IsPopupResizeFailure(new PopupException(PopupStack)));

		//a NullReferenceException of app code running in the same layout pass still crashes
		Assert.IsFalse(App.IsPopupResizeFailure(new PopupException("   at Volumey.View.MainView.OnSizeChanged()\n" + PopupStack)));
		Assert.IsFalse(App.IsPopupResizeFailure(new TabletException(PopupStack)));
		Assert.IsFalse(App.IsPopupResizeFailure(new PopupException(TabletStack)));
	}

	[TestMethod]
	public void OnlyTheTabletRefreshFailureIsIgnored()
	{
		Assert.IsTrue(App.IsTabletRefreshFailure(new TabletException(TabletStack)));

		Assert.IsFalse(App.IsTabletRefreshFailure(new TabletException("   at Volumey.Model.OutputDeviceModel.Something()")));
		Assert.IsFalse(App.IsTabletRefreshFailure(new PopupException(TabletStack)));
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
