using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlaUI.Core.Definitions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

[TestClass]
public class Issue80GeometryEvidenceTests : UiTest
{
	[TestMethod]
	public void RegularWindowRestoresExpandedHeightAtLowerRightAfterRestart()
	{
		StartMixer(new JsonObject
		{
			["Window"] = new JsonObject
			{
				["PopupMode"] = false,
				["RememberPosition"] = true,
				["Width"] = 460
			}
		});
		var window = App.Mixer;
		var transform = window.Patterns.Transform.Pattern;
		using(Screens.PhysicalPixels())
		{
			var work = Screens.WorkArea(VolumeyApp.Handle(window));
			int height = (int)(work.Height * 0.55);
			transform.Resize(460, height);
			transform.Move(work.Right - 470, work.Bottom - height - 10);
			Assert.IsTrue(Eventually(() =>
			{
				var bounds = Screens.WindowRect(VolumeyApp.Handle(App.Mixer));
				return bounds.Width == 460 && Math.Abs(bounds.Height - height) <= 1
				       && Math.Abs(bounds.Right - (work.Right - 10)) <= 1
				       && Math.Abs(bounds.Bottom - (work.Bottom - 10)) <= 1;
			}, 5000), "the mixer wasn't expanded at the lower right of its monitor");
			var before = Screens.WindowRect(VolumeyApp.Handle(App.Mixer));
			CaptureIfRequested(VolumeyApp.Handle(App.Mixer), "before.png");
			App.ExitByTray();
			App.Restart();
			var after = Screens.WindowRect(VolumeyApp.Handle(App.Mixer));
			Assert.AreEqual(before.Location, after.Location, "position after restart");
			Assert.AreEqual(before.Size, after.Size, "expanded size after restart");
			Assert.AreEqual(work, Screens.WorkArea(VolumeyApp.Handle(App.Mixer)), "monitor work area after restart");
			CaptureIfRequested(VolumeyApp.Handle(App.Mixer), "after.png");
			WriteStateIfRequested(work, before, after);
		}
	}

	private void CaptureIfRequested(IntPtr hwnd, string name)
	{
		if(Environment.GetEnvironmentVariable("VOLUMEY_ISSUE80_EVIDENCE_DIR") is not { Length: > 0 } directory)
			return;
		Directory.CreateDirectory(directory);
		var bounds = Screens.WindowRect(hwnd);
		using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
		using var graphics = Graphics.FromImage(bitmap);
		IntPtr hdc = graphics.GetHdc();
		try
		{
			Assert.IsTrue(PrintWindow(hwnd, hdc, 2), "PrintWindow couldn't capture the mixer");
		}
		finally
		{
			graphics.ReleaseHdc(hdc);
		}
		bitmap.Save(Path.Combine(directory, name), ImageFormat.Png);
	}

	private void WriteStateIfRequested(Rectangle work, Rectangle before, Rectangle after)
	{
		if(Environment.GetEnvironmentVariable("VOLUMEY_ISSUE80_EVIDENCE_DIR") is not { Length: > 0 } directory)
			return;
		var state = new
		{
			Monitor = Screens.MonitorName(VolumeyApp.Handle(App.Mixer)),
			WorkArea = new { work.X, work.Y, work.Width, work.Height },
			Before = new { before.X, before.Y, before.Width, before.Height, before.Right, before.Bottom },
			After = new { after.X, after.Y, after.Width, after.Height, after.Right, after.Bottom },
			Settings = App.ReadSettings()["Window"]?.DeepClone()
		};
		File.WriteAllText(Path.Combine(directory, "state.json"), JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
}
