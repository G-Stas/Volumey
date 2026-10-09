using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Volumey.Tests
{
	[TestClass]
	public class AppTests
	{
		private sealed class WindowUpdateException : Win32Exception
		{
			private readonly string stackTrace;

			public WindowUpdateException(int error, string stackTrace) : base(error) => this.stackTrace = stackTrace;

			public override string StackTrace => this.stackTrace;
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
	}
}
