using System.Diagnostics;
using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The Volume mixer page's switches seen on the mixer (pop-up mode, always on top, screen, device row), the volume limit and app
/// volume sync on a test app, and the mixer window itself: second start, losing focus, remembered size, the device picker,
/// the Sound settings button, and where the settings window opens. Never switches the default device.
/// </summary>
[TestClass]
public class MixerPageTests : UiTest
{
	private static JsonObject Mode(bool popup) => new() { ["Window"] = new JsonObject { ["PopupMode"] = popup } };

	private void OpenPage(JsonObject? settings = null)
	{
		StartApp(settings);
		Settings.Open("NavMixer", "PopupToggle");
	}

	/// <summary>Shows the mixer and returns it once it has settled (it's placed after it shows)</summary>
	private Window ShownMixer()
	{
		if(!App.MixerIsVisible)
			App.ShowMixer();
		var mixer = Retry.WhileNull(() => App.FindWindow("Volumey"), TimeSpan.FromSeconds(5)).Result
		            ?? throw new AssertFailedException("The mixer didn't show");
		Thread.Sleep(500);
		return mixer;
	}

	private static bool CanResize(Window window) => window.Patterns.Transform.PatternOrDefault?.CanResize.ValueOrDefault == true;

	[TestMethod]
	public void PopupModeSwitchesTheMixerRightAway()
	{
		OpenPage(Mode(popup: false));
		Assert.IsTrue(CanResize(ShownMixer()), "window mode isn't resizable");

		Settings.SetSwitch("PopupToggle", true);
		App.WaitForSettings(s => s["Window"]?["PopupMode"]?.GetValue<bool>() == true, "pop-up mode saved");
		Assert.IsFalse(CanResize(ShownMixer()), "the pop-up is resizable: still window mode");

		Settings.SetSwitch("PopupToggle", false);
		App.WaitForSettings(s => s["Window"]?["PopupMode"]?.GetValue<bool>() == false, "window mode saved");
		Assert.IsTrue(CanResize(ShownMixer()), "back in window mode, the mixer isn't resizable");
	}

	[TestMethod]
	public void AlwaysOnTopMakesTheMixerTopmost()
	{
		OpenPage(Mode(popup: false));
		Assert.IsFalse(Screens.IsTopmost(VolumeyApp.Handle(ShownMixer())), "the mixer is topmost before the switch");

		Settings.SetSwitch("AlwaysOnTopToggle", true);
		Assert.IsTrue(Eventually(() => Screens.IsTopmost(VolumeyApp.Handle(ShownMixer()))), "the mixer isn't topmost");

		Settings.SetSwitch("AlwaysOnTopToggle", false);
		Assert.IsTrue(Eventually(() => !Screens.IsTopmost(VolumeyApp.Handle(ShownMixer()))), "the mixer stayed topmost");
	}

	[TestMethod]
	public void TheChosenScreenIsWhereThePopupOpens()
	{
		if(Screens.MonitorCount < 2)
			Assert.Inconclusive("Only one screen");
		OpenPage();
		var screens = Settings.Find("ScreenBox").AsComboBox();
		screens.Expand();
		int other = Array.FindIndex(screens.Items, i => !i.IsSelected);
		screens.Collapse();
		string chosen = Settings.SelectItem("ScreenBox", other);
		App.WaitForSettings(s => s["Window"]?["ScreenName"] != null, "the screen saved");

		App.HideMixer();
		string monitor = Screens.MonitorName(VolumeyApp.Handle(ShownMixer()));

		//the list item's name is the screen's device name and size: "\\.\DISPLAY2 (1920x1080)"
		Assert.IsTrue(chosen.Replace(@"\\.\", "").StartsWith(monitor + " "), $"the pop-up opened on \"{monitor}\", not on \"{chosen}\"");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void TheDeviceRowMovesBelowTheApps()
	{
		using var testApp = Later(TestAudioApp.Start());
		OpenPage();
		double DeviceTop() => ShownMixer().FindFirstDescendant(cf => cf.ByAutomationId("DeviceVolumeSlider"))!.BoundingRectangle.Top;
		double AppTop() => ShownMixer().FindFirstDescendant(cf => cf.ByControlType(ControlType.Slider).And(cf.ByName(testApp.DisplayName)))!
		                               .BoundingRectangle.Top;
		Assert.IsTrue(Eventually(() => DeviceTop() < AppTop(), 5000), "the device row isn't above the apps");

		Settings.SetSwitch("DeviceBottomToggle", true);
		App.WaitForSettings(s => s["Window"]?["DeviceViewAtTheBottom"]?.GetValue<bool>() == true, "device at the bottom saved");
		Assert.IsTrue(Eventually(() => DeviceTop() > AppTop()), "the device row isn't below the apps");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void TheVolumeLimitLowersANewApp()
	{
		OpenPage();
		Settings.TypeNumber("VolumeLimitBox", 30);
		Settings.SetSwitch("VolumeLimitToggle", true);
		App.WaitForSettings(s => s["General"]?["VolumeLimit"]?.GetValue<int>() == 30 && s["General"]?["VolumeLimitEnabled"]?.GetValue<bool>() == true,
			"the volume limit saved");

		//a new exe name: Windows starts a session it doesn't know at 100%, and the app leaves it there (--no-reset)
		using var testApp = Later(TestAudioApp.Start("VolumeyUiLimit" + Guid.NewGuid().ToString("N")[..8], atFullVolume: false));

		Assert.AreEqual(30, Audio.WaitFor(() => testApp.Volume, 30, 5000), "a new app at 100% wasn't lowered to the limit");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AppVolumeSyncFollowsTheDeviceVolume()
	{
		//sync sets every app on the device to the device's volume, other apps too: all of them are put back afterwards
		Later(Audio.KeepDeviceState());
		Later(Audio.KeepSessionVolumes());
		using var testApp = Later(TestAudioApp.Start());
		OpenPage();
		//stopped first in the cleanup: putting the device volume back mustn't sync the apps again
		Later(new Cleanup(() => App.Kill()));

		Settings.SetSwitch("SyncToggle", true);
		App.WaitForSettings(s => s["General"]?["AppVolumeSyncEnabled"]?.GetValue<bool>() == true, "sync saved");
		int target = Audio.DeviceVolume == 40 ? 60 : 40;
		Audio.SetDeviceVolume(target);

		Assert.AreEqual(target, Audio.WaitFor(() => testApp.Volume, target, 5000), "the app didn't follow the device volume");
	}

	private sealed class Cleanup(Action action) : IDisposable
	{
		public void Dispose() => action();
	}

	[TestMethod]
	public void StartingTheAppAgainOpensThePopup()
	{
		StartMixer(Mode(popup: true));
		App.HideMixer();

		Assert.IsTrue(App.StartSecondInstance(), "the second instance didn't exit");

		Assert.IsTrue(Eventually(() => App.MixerIsVisible, 5000), "the pop-up didn't open");
		Assert.IsFalse(CanResize(App.Mixer), "a window opened instead of the pop-up");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void ThePopupHidesWhenAnotherWindowIsActivated()
	{
		using var testApp = Later(TestAudioApp.Start("VolumeyUiFocusApp", window: true));
		StartMixer(Mode(popup: true));
		//opened like a person does: a pop-up shown by the test's message never gets the focus, so it can't lose it
		App.HideMixer();
		App.TrayIcon().Click();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible, 5000), "the tray click didn't open the pop-up");
		Thread.Sleep(500);

		testApp.BringToFront(App.Automation);

		Assert.IsTrue(Eventually(() => !App.MixerIsVisible, 5000), "the pop-up stayed open");
	}

	[TestMethod]
	public void WindowModesSizeIsRemembered()
	{
		StartMixer(new JsonObject { ["Window"] = new JsonObject { ["PopupMode"] = false, ["Width"] = 400, ["Height"] = 500 } });
		var mixer = ShownMixer();
		//the window opens by the tray, at the screen's edge: grown there it reaches into the next screen, and on one at another scale
		//Windows changes its size again
		using(Screens.PhysicalPixels())
		{
			var workArea = Screens.WorkArea(VolumeyApp.Handle(mixer));
			mixer.Patterns.Transform.Pattern.Move(workArea.X + 100, workArea.Y + 100);
		}
		mixer.Patterns.Transform.Pattern.Resize(480, 600);
		Assert.IsTrue(Eventually(() => (int)App.Mixer.BoundingRectangle.Width == 480), $"the window wasn't resized: {App.Mixer.BoundingRectangle}");

		//the size is saved when the app exits (like the position), so it quits the way a person does
		App.ExitByTray();
		App.Restart();
		var size = ShownMixer().BoundingRectangle;

		Assert.AreEqual(480, (int)size.Width, "width after the restart");
		Assert.AreEqual(600, (int)size.Height, "height after the restart");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void TheMixersDevicePickerShowsAnotherDeviceWithoutMakingItTheDefault()
	{
		if(Audio.OutputDeviceNames().Count < 2)
			Assert.Inconclusive("Only one output device");
		using var testApp = Later(TestAudioApp.Start());
		string defaultId = Audio.DefaultDeviceId;
		StartMixer();
		var mixer = ShownMixer();
		AutomationElement? AppRow() => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Slider).And(cf.ByName(testApp.DisplayName)));
		Assert.IsTrue(Eventually(() => AppRow() != null, 5000), "the test app has no row on the default device");
		string defaultName = mixer.FindFirstDescendant(cf => cf.ByAutomationId("DeviceVolumeSlider"))!.Name;

		var picker = mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox).And(cf.ByName("Output device")))!.AsComboBox();
		//real clicks, not a selection through UI Automation: the list is a popup, and the window's drag handling sees its presses too
		picker.Click();
		Assert.IsTrue(Eventually(() => picker.ExpandCollapseState == ExpandCollapseState.Expanded), "the device list didn't open");
		var other = picker.Items.First(i => !i.IsSelected);
		other.Click();

		Assert.IsTrue(Eventually(() => App.Mixer.FindFirstDescendant(cf => cf.ByAutomationId("DeviceVolumeSlider"))!.Name != defaultName),
			"the mixer still shows the default device");
		Assert.IsTrue(Eventually(() => AppRow() == null), "the test app (on the default device) is still listed");
		Assert.AreEqual(defaultId, Audio.DefaultDeviceId, "picking a device in the mixer changed the default device");
	}

	[TestMethod]
	public void TheSoundSettingsButtonOpensWindowsSoundSettings()
	{
		bool wasOpen = Process.GetProcessesByName("SystemSettings").Length > 0;
		StartMixer();

		ShownMixer().FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Sound settings")))!.AsButton().Invoke();

		Assert.IsTrue(Eventually(() => Process.GetProcessesByName("SystemSettings").Length > 0, 10000), "Windows Settings didn't open");
		//only closed when the test opened it: a Settings app that was already open belongs to the user
		if(!wasOpen)
			foreach(var p in Process.GetProcessesByName("SystemSettings"))
				using(p)
					p.Kill();
	}

	[TestMethod]
	public void TheSettingsWindowOpensCenteredOnTheMixersScreenAndClosesToHide()
	{
		StartApp();
		var area = Screens.MonitorBounds(VolumeyApp.Handle(App.Mixer));
		var bounds = Settings.Window.BoundingRectangle;
		double dx = bounds.Left + bounds.Width / 2 - (area.Left + area.Width / 2.0);
		double dy = bounds.Top + bounds.Height / 2 - (area.Top + area.Height / 2.0);
		Assert.IsTrue(Math.Abs(dx) < 50 && Math.Abs(dy) < 50, $"the settings window isn't centered on the mixer's screen (off by {dx}, {dy})");

		Settings.Window.Patterns.Window.Pattern.Close();
		Assert.IsTrue(Eventually(() => App.FindWindow("Volumey settings") == null), "the settings window didn't close");
		Assert.IsNotNull(App.OpenSettings(), "the settings window didn't open again");
	}
}
