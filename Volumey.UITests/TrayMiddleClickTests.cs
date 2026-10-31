using System.Text.Json.Nodes;
using FlaUI.Core.Input;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// A middle click on the tray icon with the "mute or unmute" action changes the real default device's mute (put back afterwards).
/// The "switch device" action isn't tested here: the UI tests never change the default device.
/// </summary>
[TestClass]
[TestCategory("Audio")]
public class TrayMiddleClickTests : UiTest
{
	private static JsonObject Seed(string action, bool osd) => new()
	{
		//window mode: the open mixer stays open when the taskbar is clicked
		["Window"] = new JsonObject { ["TrayMiddleClickAction"] = action, ["PopupMode"] = false },
		["Notifications"] = new JsonObject
		{
			["Enabled"] = osd,
			["DisplayTimeSeconds"] = 5,
			//away from the tray, where the pointer rests (a pill under the pointer stays open)
			["Position"] = "TopRight"
		}
	};

	private void Start(string action, bool osd, bool mixer = false)
	{
		if(!Audio.HasOutputDevice)
			Assert.Inconclusive("No output device");
		Later(Audio.KeepDeviceState());
		StartMixer(Seed(action, osd));
		if(!mixer)
			App.HideMixer();
	}

	private void MiddleClickTrayIcon()
	{
		Mouse.Click(App.TrayIcon().GetClickablePoint(), MouseButton.Middle);
		Wait.UntilInputIsProcessed();
	}

	private bool DevicePillShown(int timeoutMs) => Eventually(() => App.FindOsdPill(Audio.DefaultDeviceName) != null, timeoutMs);

	[TestMethod]
	public void TheMiddleClickMutesAndUnmutesTheDefaultDevice()
	{
		Start("ToggleMute", osd: false);
		bool start = Audio.DeviceMuted;

		MiddleClickTrayIcon();
		Assert.AreEqual(!start, Audio.WaitFor(() => Audio.DeviceMuted, !start), "the first middle click didn't change the mute");
		MiddleClickTrayIcon();
		Assert.AreEqual(start, Audio.WaitFor(() => Audio.DeviceMuted, start), "the second middle click didn't change it back");
		Assert.IsFalse(App.MixerIsVisible, "the middle click opened the mixer");
	}

	[TestMethod]
	[DataRow(true, DisplayName = "OSD notifications on")]
	[DataRow(false, DisplayName = "OSD notifications off")]
	public void TheMiddleClickShowsTheOsdOnlyWhenItsOn(bool osd)
	{
		Start("ToggleMute", osd);
		bool start = Audio.DeviceMuted;

		MiddleClickTrayIcon();
		Assert.AreEqual(!start, Audio.WaitFor(() => Audio.DeviceMuted, !start), "the middle click didn't change the mute");
		Assert.AreEqual(osd, DevicePillShown(osd ? 3000 : 1500), osd ? "no OSD with OSD notifications on" : "an OSD with OSD notifications off");
	}

	[TestMethod]
	public void WithTheMixerOpenTheMiddleClickMutesWithoutAnOsd()
	{
		Start("ToggleMute", osd: true, mixer: true);
		bool start = Audio.DeviceMuted;

		MiddleClickTrayIcon();
		Assert.AreEqual(!start, Audio.WaitFor(() => Audio.DeviceMuted, !start), "the middle click didn't change the mute with the mixer open");
		Assert.IsTrue(App.MixerIsVisible, "the mixer closed");
		Assert.IsFalse(DevicePillShown(1500), "an OSD showed while the mixer is open");
	}

	[TestMethod]
	public void DoNothingChangesNothing()
	{
		Start("None", osd: true);
		bool start = Audio.DeviceMuted;

		MiddleClickTrayIcon();
		Assert.IsFalse(Eventually(() => Audio.DeviceMuted != start, 1000), "the middle click changed the mute");
		Assert.IsFalse(DevicePillShown(500), "the middle click showed an OSD");
		Assert.IsFalse(App.MixerIsVisible, "the middle click opened the mixer");
	}
}
