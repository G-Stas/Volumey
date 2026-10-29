using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The mouse wheel over the tray icon changes the real default device's volume by the volume step (put back afterwards).
/// The wheel is only turned over the tray icon or the settings window's title bar, where it does nothing else.
/// </summary>
[TestClass]
[TestCategory("Audio")]
public class TrayWheelTests : UiTest
{
	private const int Step = 5;
	private const int Notch = 120;
	private const uint MouseEventWheel = 0x0800;

	[DllImport("user32.dll")]
	private static extern void mouse_event(uint flags, int dx, int dy, int data, UIntPtr extraInfo);

	private static JsonObject Seed(bool osd) => new()
	{
		["General"] = new JsonObject { ["VolumeStep"] = Step },
		["Notifications"] = new JsonObject
		{
			["Enabled"] = osd,
			["DisplayTimeSeconds"] = 5,
			//away from the tray, where the pointer rests (a pill under the pointer stays open)
			["Position"] = "TopRight"
		}
	};

	/// <summary>Turns the wheel by <paramref name="delta"/> (a notch is 120; touchpads send less)</summary>
	private static void Wheel(int delta)
	{
		mouse_event(MouseEventWheel, 0, 0, delta, UIntPtr.Zero);
		Wait.UntilInputIsProcessed();
	}

	/// <summary>
	/// Puts the pointer on the tray icon and moves it a little there, so Explorer tells the app that the pointer is over its icon
	/// </summary>
	private void PointAtTrayIcon()
	{
		var p = App.TrayIcon().GetClickablePoint();
		Mouse.MoveTo(p);
		Thread.Sleep(200);
		Mouse.MoveTo(new Point(p.X + 1, p.Y));
		Thread.Sleep(300);
	}

	/// <summary>The device volume away from the ends, so a step either way moves it</summary>
	private static int StartVolume()
	{
		int start = Audio.DeviceVolume;
		if(start < 2 * Step || start > 100 - 2 * Step)
		{
			start = 50;
			Audio.SetDeviceVolume(start);
		}
		return start;
	}

	private void Start(bool osd, bool mixer = false)
	{
		if(!Audio.HasOutputDevice)
			Assert.Inconclusive("No output device");
		Later(Audio.KeepDeviceState());
		StartMixer(Seed(osd));
		if(!mixer)
			App.HideMixer();
	}

	private bool DevicePillShown(int timeoutMs) => Eventually(() => App.FindOsdPill(Audio.DefaultDeviceName) != null, timeoutMs);

	[TestMethod]
	public void TheWheelOverTheIconChangesTheDeviceByTheStep()
	{
		Start(osd: false);
		int start = StartVolume();
		PointAtTrayIcon();

		Wheel(-Notch);
		Assert.AreEqual(start - Step, Audio.WaitFor(() => Audio.DeviceVolume, start - Step), "wheel down didn't lower the device by the step");
		Wheel(Notch);
		Assert.AreEqual(start, Audio.WaitFor(() => Audio.DeviceVolume, start), "wheel up didn't raise the device by the step");
	}

	[TestMethod]
	[DataRow(true, DisplayName = "OSD notifications on")]
	[DataRow(false, DisplayName = "OSD notifications off")]
	public void TheWheelShowsTheOsdOnlyWhenItsOn(bool osd)
	{
		Start(osd);
		int start = StartVolume();
		PointAtTrayIcon();

		Wheel(-Notch);
		Assert.AreEqual(start - Step, Audio.WaitFor(() => Audio.DeviceVolume, start - Step), "the wheel didn't change the device");
		Assert.AreEqual(osd, DevicePillShown(osd ? 3000 : 1500), osd ? "no OSD with OSD notifications on" : "an OSD with OSD notifications off");
	}

	[TestMethod]
	public void TheWheelWithAModifierChangesNothing()
	{
		Start(osd: false);
		int start = StartVolume();
		PointAtTrayIcon();

		using(Keyboard.Pressing(VirtualKeyShort.CONTROL))
			Wheel(-Notch);
		Assert.IsFalse(Eventually(() => Audio.DeviceVolume != start, 1000), "Ctrl+wheel over the icon changed the volume");
	}

	[TestMethod]
	public void TheWheelAwayFromTheIconChangesNothing()
	{
		if(!Audio.HasOutputDevice)
			Assert.Inconclusive("No output device");
		Later(Audio.KeepDeviceState());
		StartApp(Seed(osd: false));
		int start = StartVolume();
		PointAtTrayIcon();

		//the settings window's title bar, away from its buttons
		var r = Settings.Window.BoundingRectangle;
		Mouse.MoveTo(new Point(r.Left + r.Width / 3, r.Top + 16));
		Thread.Sleep(300);
		Wheel(-Notch);
		Assert.IsFalse(Eventually(() => Audio.DeviceVolume != start, 1000), "the wheel changed the volume after the pointer left the icon");
	}

	[TestMethod]
	public void SmallDeltasAddUpToOneStep()
	{
		Start(osd: false);
		int start = StartVolume();
		PointAtTrayIcon();

		for(int i = 0; i < 3; i++)
			Wheel(-Notch / 4);
		Assert.IsFalse(Eventually(() => Audio.DeviceVolume != start, 500), "part of a notch changed the volume");
		Wheel(-Notch / 4);
		Assert.AreEqual(start - Step, Audio.WaitFor(() => Audio.DeviceVolume, start - Step), "four quarters of a notch weren't one step");
	}

	[TestMethod]
	public void WithTheMixerOpenTheWheelChangesTheShownDeviceWithoutAnOsd()
	{
		Start(osd: true, mixer: true);
		int start = StartVolume();
		PointAtTrayIcon();

		Wheel(-Notch);
		Assert.AreEqual(start - Step, Audio.WaitFor(() => Audio.DeviceVolume, start - Step), "the wheel didn't change the device with the mixer open");
		Assert.IsTrue(App.MixerIsVisible, "the mixer closed");
		Assert.IsFalse(DevicePillShown(1500), "an OSD showed while the mixer is open");
	}
}
