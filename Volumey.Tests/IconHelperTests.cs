using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Helper;

namespace Volumey.Tests
{
	[TestClass]
	public class IconHelperTests
	{
		[TestMethod]
		public void DisposingOneGenericIconDoesntBreakTheOthers()
		{
			//the shared generic icon used to be handed out and destroyed by the first app model that was disposed
			var first = IconHelper.CreateGenericExeIcon();
			var second = IconHelper.CreateGenericExeIcon();
			Assert.IsNotNull(first);
			Assert.AreNotSame(first, second);

			first.Dispose();

			Assert.IsNotNull(second.GetAsImageSource(), "the second copy's handle was destroyed with the first");
			Assert.IsNotNull(IconHelper.CreateGenericExeIcon().GetAsImageSource());
			second.Dispose();
		}

		[TestMethod]
		public void IconsAreReadFromResourcePathsAndFiles()
		{
			string shell32 = Path.Combine(Environment.SystemDirectory, "shell32.dll");

			using var fromResource = IconHelper.GetFromResourcePath("%SystemRoot%\\System32\\shell32.dll,3");
			using var fromFile = IconHelper.GetFromResourcePath(Path.Combine(Environment.SystemDirectory, "notepad.exe"));
			using var fromDll = IconHelper.GetFromDll(shell32, 3);

			Assert.IsNotNull(fromResource?.GetAsImageSource());
			Assert.IsNotNull(fromFile?.GetAsImageSource());
			Assert.IsNotNull(fromDll?.GetAsImageSource());
		}

		[TestMethod]
		public void BrokenIconPathsGiveNoIcon()
		{
			Assert.IsNull(IconHelper.GetFromResourcePath(null));
			Assert.IsNull(IconHelper.GetFromResourcePath(""));
			Assert.IsNull(IconHelper.GetFromResourcePath(@"C:\does\not\exist.dll,5"));
			Assert.IsNull(IconHelper.GetFromResourcePath("not a path,abc"));
		}
	}
}
