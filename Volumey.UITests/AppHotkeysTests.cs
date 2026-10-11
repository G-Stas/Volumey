using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
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

		Settings.Find("AddAppButton").AsButton().Invoke();
		var apps = Settings.Find("AddAppBox").AsComboBox();
		apps.Expand();
		var item = Retry.WhileNull(() => apps.Items.FirstOrDefault(i => i.Name == testApp.DisplayName), TimeSpan.FromSeconds(5)).Result
		           ?? throw new AssertFailedException($"{testApp.DisplayName} isn't in the dialog's list: {string.Join(", ", apps.Items.Select(i => i.Name))}");
		item.Select();
		apps.Collapse();
		Settings.TypeHotkey("AddAppUpBox", up);
		Settings.TypeHotkey("AddAppDownBox", down);
		Settings.Find("PrimaryButton").AsButton().Invoke();

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

		var row = Settings.Find("AppList").FindFirstDescendant(cf => cf.ByName(testApp.DisplayName)) ?? throw new AssertFailedException("No row for the app");
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
