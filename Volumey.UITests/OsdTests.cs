using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The OSD: pills for outside volume changes ("react to all volume changes"), their display time and mute button, and the preview
/// on the Notifications page
/// </summary>
[TestClass]
public class OsdTests : UiTest
{
	private const string SampleApp = "Application";

	private static JsonObject Notifications(bool reactToAll, int displayTimeSeconds) => new()
	{
		["Notifications"] = new JsonObject
		{
			["Enabled"] = true,
			["ReactToAllVolumeChanges"] = reactToAll,
			["DisplayTimeSeconds"] = displayTimeSeconds,
			//away from the top-left corner, where the mouse may rest (a pill under the mouse stays open)
			["Position"] = "BottomRight"
		}
	};

	private bool PillShown(string name, int timeoutMs = 3000) => Eventually(() => App.FindOsdPill(name) != null, timeoutMs);

	private bool PillGone(string name, int timeoutMs) => Eventually(() => App.FindOsdPill(name) == null, timeoutMs);

	[TestMethod]
	[TestCategory("Audio")]
	public void AnOutsideChangeShowsTheAppsPillForTheDisplayTime()
	{
		using var testApp = Later(TestAudioApp.Start());
		StartMixer(Notifications(reactToAll: true, displayTimeSeconds: 1));
		string name = testApp.DisplayName;

		//like the Windows volume mixer
		Audio.SetSessionVolume(testApp.Process.Id, 40);
		Assert.IsTrue(PillShown(name), "no pill for the app after an outside change");
		Assert.IsTrue(PillGone(name, 4000), "the pill stayed longer than the display time (1 s)");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void WithoutReactToAllAnOutsideChangeShowsNothing()
	{
		using var testApp = Later(TestAudioApp.Start());
		StartMixer(Notifications(reactToAll: false, displayTimeSeconds: 1));

		Audio.SetSessionVolume(testApp.Process.Id, 40);
		Assert.IsFalse(PillShown(testApp.DisplayName, 1500), "a pill showed for an outside change with \"react to all\" off");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void ThePillsMuteButtonMutesTheApp()
	{
		using var testApp = Later(TestAudioApp.Start());
		StartMixer(Notifications(reactToAll: true, displayTimeSeconds: 10));
		string name = testApp.DisplayName;

		Audio.SetSessionVolume(testApp.Process.Id, 40);
		Assert.IsTrue(PillShown(name), "no pill for the app");
		var mute = App.FindOsdPill(name)!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Mute " + name)))
		           ?? throw new AssertFailedException("No mute button in the pill");
		mute.AsButton().Invoke();
		Assert.AreEqual(true, Audio.WaitFor(() => Audio.SessionMuted(testApp.Process.Id), true), "the pill's button didn't mute the app");
		Assert.AreEqual(40, testApp.Volume, "muting changed the app's volume");
	}

	[TestMethod]
	public void ShowOnScreenShowsTheSamplePill()
	{
		StartApp(Notifications(reactToAll: false, displayTimeSeconds: 1));
		Settings.Open("NavNotifications", "ShowPreviewButton");

		Settings.Find("ShowPreviewButton").AsButton().Invoke();
		Assert.IsTrue(PillShown(SampleApp), "Show on screen didn't show the sample pill");
		Assert.IsTrue(PillGone(SampleApp, 4000), "the sample pill stayed longer than the display time (1 s)");
	}

	[TestMethod]
	public void TheOsdSwitchIsSaved()
	{
		StartApp();
		Settings.Open("NavNotifications", "OsdToggle");

		Settings.SetSwitch("OsdToggle", true);
		App.WaitForSettings(s => s["Notifications"]?["Enabled"]?.GetValue<bool>() == true, "OSD notifications on");
		Settings.SetSwitch("OsdToggle", false);
		App.WaitForSettings(s => s["Notifications"]?["Enabled"]?.GetValue<bool>() == false, "OSD notifications off");
	}
}
