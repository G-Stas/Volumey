using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The App hotkeys page: the foreground app's hotkeys and per-app hotkeys, pressed on a test app that plays silence
/// </summary>
[TestClass]
public class AppHotkeysTests : UiTest
{
	private void OpenPage(JsonObject? settings = null)
	{
		StartApp(settings);
		Settings.Open("NavAppHotkeys", "AddAppButton");
	}

	private int VolumeStep => App.ReadSettings()["General"]?["VolumeStep"]?.GetValue<int>() ?? 1;

	private static JsonNode? SavedApp(JsonNode settings, string name)
		=> settings["Hotkeys"]?["Apps"]?.AsArray().FirstOrDefault(a => a?["ProcessName"]?.GetValue<string>() == name);

	/// <summary>Adds the running test app with the Add app dialog; a null hotkey's box stays empty</summary>
	private void AddInDialog(TestAudioApp testApp, Combo? up, Combo? down, Combo? mute = null)
	{
		Settings.Find("AddAppButton").AsButton().Invoke();
		var apps = Settings.Find("AddAppBox").AsComboBox();
		apps.Expand();
		var item = Retry.WhileNull(() => apps.Items.FirstOrDefault(i => i.Name == testApp.DisplayName), TimeSpan.FromSeconds(5)).Result
		           ?? throw new AssertFailedException($"{testApp.DisplayName} isn't in the dialog's list: {string.Join(", ", apps.Items.Select(i => i.Name))}");
		item.Select();
		apps.Collapse();
		if(up != null)
			Settings.TypeHotkey("AddAppUpBox", up);
		if(down != null)
			Settings.TypeHotkey("AddAppDownBox", down);
		if(mute != null)
			Settings.TypeHotkey("AddAppMuteBox", mute);
		Settings.Find("PrimaryButton").AsButton().Invoke();
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void ThePagesSettingsSurviveARestart()
	{
		using var testApp = Later(TestAudioApp.Start());
		OpenPage();
		var foregroundUp = Combo.F(13);
		var foregroundDown = Combo.F(14);
		var appUp = Combo.F(15);
		var appDown = Combo.F(16);
		Settings.TypeHotkey("ForegroundUpBox", foregroundUp);
		Settings.TypeHotkey("ForegroundDownBox", foregroundDown);
		Settings.SetSwitch("ForegroundToggle", true);
		AddInDialog(testApp, appUp, appDown);
		App.WaitForSettings(s => IsHotkey(s["Hotkeys"]?["ForegroundVolumeUp"], foregroundUp) && IsHotkey(s["Hotkeys"]?["ForegroundVolumeDown"], foregroundDown)
		                         && SavedApp(s, testApp.DisplayName) is { } a && IsHotkey(a["VolumeUp"], appUp) && IsHotkey(a["VolumeDown"], appDown),
			"the App hotkeys page's settings saved");

		Restart();
		Settings.Open("NavAppHotkeys", "AddAppButton");

		Assert.IsTrue(Eventually(() => new[] { foregroundUp, foregroundDown, appUp, appDown }.All(c => c.IsHeldByAnotherApp())),
			"the hotkeys aren't registered after the restart");
		Assert.IsTrue(Settings.IsOn("ForegroundToggle"), "the foreground switch is off after the restart");
		var row = AppRow(testApp.DisplayName)
		          ?? throw new AssertFailedException("The app's row is gone after the restart");
		row.Patterns.ExpandCollapse.PatternOrDefault?.Expand();
		var upBox = Retry.WhileNull(() => row.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit).And(cf.ByName(testApp.DisplayName + " volume up"))),
			TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException("No volume up box in the app's row");
		Assert.IsFalse(string.IsNullOrEmpty(upBox.AsTextBox().Text), "the app's volume up box is empty after the restart");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void TheAddAppDialogNeedsAnAppAndAHotkeyAndCancelSavesNothing()
	{
		using var testApp = Later(TestAudioApp.Start());
		OpenPage();
		var up = Combo.F(13);
		var down = Combo.F(14);
		bool AddEnabled() => Settings.Find("PrimaryButton").IsEnabled;

		Settings.Find("AddAppButton").AsButton().Invoke();
		Assert.IsFalse(AddEnabled(), "Add is enabled with nothing chosen");
		var apps = Settings.Find("AddAppBox").AsComboBox();
		apps.Expand();
		Retry.WhileNull(() => apps.Items.FirstOrDefault(i => i.Name == testApp.DisplayName), TimeSpan.FromSeconds(5)).Result!.Select();
		apps.Collapse();
		Assert.IsFalse(AddEnabled(), "Add is enabled without hotkeys");
		Settings.TypeHotkey("AddAppUpBox", up);
		Assert.IsTrue(Eventually(AddEnabled), "Add isn't enabled with an app and one hotkey");

		Settings.Find("CloseButton").AsButton().Invoke();
		Thread.Sleep(800);
		Assert.IsNull(SavedApp(App.ReadSettings(), testApp.DisplayName), "Cancel saved the app");
		Assert.IsFalse(up.IsHeldByAnotherApp(), "Cancel registered the hotkey");

		//once added, the app isn't offered again
		AddInDialog(testApp, up, down);
		App.WaitForSettings(s => SavedApp(s, testApp.DisplayName) != null, "the app added");
		Settings.Find("AddAppButton").AsButton().Invoke();
		var again = Settings.Find("AddAppBox").AsComboBox();
		again.Expand();
		Thread.Sleep(500);
		Assert.IsFalse(again.Items.Any(i => i.Name == testApp.DisplayName), "an app that has hotkeys is offered again");
		again.Collapse();
		Settings.Find("CloseButton").AsButton().Invoke();
	}

	/// <summary>A saved app's card: the element named after the app that expands (the list item around it has the same name)</summary>
	private AutomationElement? AppRow(string app)
		=> Settings.Find("AppList").FindAllDescendants(cf => cf.ByName(app)).FirstOrDefault(e => e.Patterns.ExpandCollapse.IsSupported);

	/// <summary>The saved app's row, expanded, and one of its hotkey boxes</summary>
	private AutomationElement AppBox(string app, string which)
	{
		var row = AppRow(app) ?? throw new AssertFailedException("No row for " + app);
		row.Patterns.ExpandCollapse.PatternOrDefault?.Expand();
		return Retry.WhileNull(() => row.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit).And(cf.ByName($"{app} volume {which}"))),
			TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException($"No volume {which} box for {app}");
	}

	private static JsonObject SavedAppSettings(string app, Combo up, Combo down, bool shareKeys = false) => new()
	{
		["Hotkeys"] = new JsonObject
		{
			["BlockInSystem"] = !shareKeys,
			["Apps"] = new JsonArray(new JsonObject { ["ProcessName"] = app, ["VolumeUp"] = HotkeyJson(up), ["VolumeDown"] = HotkeyJson(down) })
		}
	};

	/// <summary>Empties one of a saved app's hotkey boxes with Backspace</summary>
	private void EmptyBox(string app, string which)
	{
		var box = AppBox(app, which);
		Settings.Window.SetForeground();
		box.Focus();
		Keyboard.Type(VirtualKeyShort.BACK);
		Wait.UntilInputIsProcessed();
		Settings.LeaveBox();
	}

	[TestMethod]
	public void EmptyingAHotkeyKeepsTheOthersAndEmptyingTheLastRemovesTheApp()
	{
		//a saved app that isn't running: its hotkeys are still shown and changed in place
		const string app = "VolumeyUiNotRunning";
		var up = Combo.F(15);
		var down = Combo.F(16);
		OpenPage(SavedAppSettings(app, up, down));

		EmptyBox(app, "up");
		App.WaitForSettings(s => SavedApp(s, app) is { } a && a["VolumeUp"] == null && IsHotkey(a["VolumeDown"], down),
			"volume up emptied, volume down kept");
		Assert.IsTrue(string.IsNullOrEmpty(AppBox(app, "up").AsTextBox().Text), "the emptied box shows a hotkey again");

		EmptyBox(app, "down");
		App.WaitForSettings(s => SavedApp(s, app) == null, "the app removed with its last hotkey");
		Assert.IsTrue(Eventually(() => AppRow(app) == null), "the app's card is still shown");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppWithOnlyAMuteHotkeyIsMutedAndUnmuted()
	{
		using var testApp = Later(TestAudioApp.Start());
		OpenPage();
		var mute = Combo.F(18);

		AddInDialog(testApp, null, null, mute);

		App.WaitForSettings(s => SavedApp(s, testApp.DisplayName) is { } a && IsHotkey(a["Mute"], mute) && a["VolumeUp"] == null,
			"the app's mute hotkey saved");
		Assert.IsTrue(mute.IsHeldByAnotherApp(), "the mute hotkey isn't registered");
		mute.Press();
		Assert.AreEqual(true, Audio.WaitFor(() => testApp.Muted, true), "the mute hotkey didn't mute the app");
		mute.Press();
		Assert.AreEqual(false, Audio.WaitFor(() => testApp.Muted, false), "the mute hotkey didn't unmute the app");
	}

	[TestMethod]
	public void ARejectedChangeNamesTheHotkeyAndPutsTheOldOneBack()
	{
		const string app = "VolumeyUiNotRunning";
		var up = Combo.F(17);
		var down = Combo.F(18);
		var mediaKey = Combo.F(19);
		//shared mode: a duplicate reaches the box as keys and is rejected by the check, not by Windows
		var settings = SavedAppSettings(app, up, down, shareKeys: true);
		settings["Hotkeys"]!["System"] = new JsonArray(new JsonObject { ["SystemKey"] = "MediaPlayPause", ["Replacement"] = HotkeyJson(mediaKey) });
		OpenPage(settings);

		Settings.TypeHotkey(AppBox(app, "up"), mediaKey);

		Assert.IsTrue(Settings.ShowsText("F19: " + ErrorExists), "no error naming the duplicate");
		Assert.IsTrue(IsHotkey(SavedApp(App.ReadSettings(), app)?["VolumeUp"], up), "the duplicate was saved");
		Assert.IsTrue(Eventually(() => AppBox(app, "up").AsTextBox().Text.EndsWith("F17")), "the old hotkey isn't back in the box");
	}

	[TestMethod]
	public void TheForegroundBoxesAreLockedWhileTheSwitchIsOn()
	{
		OpenPage(new JsonObject
		{
			["Hotkeys"] = new JsonObject { ["ForegroundVolumeUp"] = HotkeyJson(Combo.F(19)), ["ForegroundVolumeDown"] = HotkeyJson(Combo.F(20)) }
		});

		Assert.IsTrue(Settings.IsOn("ForegroundToggle"));
		Assert.IsFalse(Settings.IsEnabled("ForegroundUpBox") || Settings.IsEnabled("ForegroundDownBox"), "the boxes can be edited while on");

		Settings.SetSwitch("ForegroundToggle", false);
		Assert.IsTrue(Eventually(() => Settings.IsEnabled("ForegroundUpBox") && Settings.IsEnabled("ForegroundDownBox")),
			"the boxes are still locked while off");
	}

	[TestMethod]
	public void ForegroundHotkeysAreRegisteredAndReleased()
	{
		OpenPage();
		var up = Combo.F(13);
		var down = Combo.F(14);

		Settings.TypeHotkey("ForegroundUpBox", up);
		Settings.TypeHotkey("ForegroundDownBox", down);
		Settings.SetSwitch("ForegroundToggle", true);

		App.WaitForSettings(s => IsHotkey(s["Hotkeys"]?["ForegroundVolumeUp"], up) && IsHotkey(s["Hotkeys"]?["ForegroundVolumeDown"], down),
			"the foreground hotkeys saved");
		Assert.IsTrue(up.IsHeldByAnotherApp() && down.IsHeldByAnotherApp(), "the foreground hotkeys aren't registered");

		Settings.SetSwitch("ForegroundToggle", false);
		App.WaitForSettings(s => s["Hotkeys"]?["ForegroundVolumeUp"] == null, "the foreground hotkeys removed");
		Assert.IsFalse(up.IsHeldByAnotherApp() || down.IsHeldByAnotherApp(), "the foreground hotkeys are still registered");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void TheForegroundHotkeysChangeOnlyTheAppInFront()
	{
		using var testApp = Later(TestAudioApp.Start("VolumeyUiForegroundApp", window: true));
		var up = Combo.F(21);
		var down = Combo.F(22);
		var mute = Combo.F(23);
		StartApp(new JsonObject
		{
			["Hotkeys"] = new JsonObject { ["ForegroundVolumeUp"] = HotkeyJson(up), ["ForegroundVolumeDown"] = HotkeyJson(down), ["ForegroundMute"] = HotkeyJson(mute) }
		});
		Assert.IsTrue(Eventually(() => up.IsHeldByAnotherApp() && down.IsHeldByAnotherApp()), "the saved foreground hotkeys aren't registered");
		int step = VolumeStep;

		//Volumey's settings window in front: the test app isn't the foreground app
		Settings.Window.SetForeground();
		down.Press();
		Thread.Sleep(500);
		Assert.AreEqual(100, testApp.Volume, "a foreground hotkey changed an app that isn't in front");

		testApp.BringToFront(App.Automation);
		down.Press();
		Assert.AreEqual(100 - step, Audio.WaitFor(() => testApp.Volume, 100 - step), "volume down didn't lower the app in front");
		up.Press();
		Assert.AreEqual(100, Audio.WaitFor(() => testApp.Volume, 100), "volume up didn't raise the app in front");
		mute.Press();
		Assert.AreEqual(true, Audio.WaitFor(() => testApp.Muted, true), "the mute hotkey didn't mute the app in front");
		mute.Press();
		Assert.AreEqual(false, Audio.WaitFor(() => testApp.Muted, false), "the mute hotkey didn't unmute the app in front");
	}

	[TestMethod]
	public void TheForegroundHotkeysMustDiffer()
	{
		OpenPage();
		var combo = Combo.F(15);
		Settings.TypeHotkey("ForegroundUpBox", combo);
		Settings.TypeHotkey("ForegroundDownBox", combo);

		Settings.SetSwitch("ForegroundToggle", true);

		Assert.IsTrue(Settings.ShowsText(ErrorDiffer), "no error shown");
		Assert.IsNull(App.ReadSettings()["Hotkeys"]?["ForegroundVolumeUp"], "the same hotkey was saved for both");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppAddedInTheDialogGetsHotkeysThatChangeItsVolume()
	{
		using var testApp = Later(TestAudioApp.Start());
		OpenPage();
		var up = Combo.F(16);
		var down = Combo.F(17);

		AddInDialog(testApp, up, down);

		App.WaitForSettings(s => SavedApp(s, testApp.DisplayName) is { } a && IsHotkey(a["VolumeUp"], up) && IsHotkey(a["VolumeDown"], down),
			"the app's hotkeys saved");
		Assert.IsTrue(up.IsHeldByAnotherApp() && down.IsHeldByAnotherApp(), "the app's hotkeys aren't registered");

		int step = VolumeStep;
		down.Press();
		Assert.AreEqual(100 - step, Audio.WaitFor(() => testApp.Volume, 100 - step), "volume down didn't lower the app");
		up.Press();
		Assert.AreEqual(100, Audio.WaitFor(() => testApp.Volume, 100), "volume up didn't raise the app");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void ASavedAppsHotkeyIsChangedInPlaceAndTheAppIsRemoved()
	{
		using var testApp = Later(TestAudioApp.Start());
		var up = Combo.F(18);
		var down = Combo.F(19);
		var newUp = Combo.F(20);
		OpenPage(new JsonObject
		{
			["Hotkeys"] = new JsonObject
			{
				["Apps"] = new JsonArray(new JsonObject { ["ProcessName"] = testApp.DisplayName, ["VolumeUp"] = HotkeyJson(up), ["VolumeDown"] = HotkeyJson(down) })
			}
		});
		Assert.IsTrue(up.IsHeldByAnotherApp(), "a saved app's hotkey isn't registered while the app runs");

		var row = AppRow(testApp.DisplayName) ?? throw new AssertFailedException("No row for the app");
		row.Patterns.ExpandCollapse.PatternOrDefault?.Expand();
		var upBox = Retry.WhileNull(() => row.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit).And(cf.ByName(testApp.DisplayName + " volume up"))),
			TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException("No volume up box in the app's row");
		Settings.TypeHotkey(upBox, newUp);

		App.WaitForSettings(s => IsHotkey(SavedApp(s, testApp.DisplayName)?["VolumeUp"], newUp), "the changed hotkey saved");
		Assert.IsTrue(newUp.IsHeldByAnotherApp(), "the new hotkey isn't registered");
		Assert.IsFalse(up.IsHeldByAnotherApp(), "the old hotkey is still registered");
		newUp.Press();
		Thread.Sleep(300);
		Assert.AreEqual(100, testApp.Volume, "the app isn't at 100% (volume up can't go higher)");

		var remove = row.FindFirstDescendant(cf => cf.ByName("Remove " + testApp.DisplayName)) ?? throw new AssertFailedException("No remove button");
		remove.AsButton().Invoke();
		App.WaitForSettings(s => SavedApp(s, testApp.DisplayName) == null, "the app removed");
		Assert.IsFalse(newUp.IsHeldByAnotherApp() || down.IsHeldByAnotherApp(), "a removed app's hotkeys are still registered");
	}
}
