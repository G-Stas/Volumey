using System.Text.Json.Nodes;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// Settings changed on a page are saved and show the same after a restart. Left out on purpose: start with Windows and the Start menu
/// (real, if dev-named, autostart entries), the volume limit and app volume sync switches (they change real apps' volumes).
/// </summary>
[TestClass]
public class SettingsRestartTests : UiTest
{
	/// <summary>Selects the first item that isn't selected now; returns its name</summary>
	private string SelectAnother(string comboId)
	{
		string? current = Settings.SelectedName(comboId);
		for(int i = 0; ; i++)
		{
			string name = Settings.SelectItem(comboId, i);
			if(name != current)
				return name;
		}
	}

	private static string? Saved(JsonNode s, string section, string name) => s[section]?[name]?.ToString();

	[TestMethod]
	public void TheGeneralPageSurvivesARestart()
	{
		StartApp();
		Settings.Open("NavGeneral", "ThemeBox");

		string theme = SelectAnother("ThemeBox");
		Settings.SetSwitch("TrayIconToggle", true);
		Settings.Select(Settings.Find("ShareHotkeysRadio"));
		Settings.TypeNumber("VolumeStepBox", 7);
		App.WaitForSettings(s => Saved(s, "General", "Theme") != "System" && Saved(s, "Window", "DisplayDeviceIconAtTray") == "true"
		                         && Saved(s, "Hotkeys", "BlockInSystem") == "false" && Saved(s, "General", "VolumeStep") == "7",
			"the General page's settings saved");

		Restart();
		Settings.Open("NavGeneral", "ThemeBox");
		Assert.AreEqual(theme, Settings.SelectedName("ThemeBox"), "theme");
		Assert.IsTrue(Settings.IsOn("TrayIconToggle"), "tray icon");
		Assert.IsTrue(SettingsWindow.IsSelected(Settings.Find("ShareHotkeysRadio")), "hotkey mode");
		Assert.AreEqual("7", Settings.Number("VolumeStepBox"), "volume step");
	}

	[TestMethod]
	public void TheVolumeMixerPageSurvivesARestart()
	{
		StartApp();
		Settings.Open("NavMixer", "PopupToggle");

		Settings.SetSwitch("PopupToggle", true);
		Settings.SetSwitch("AlwaysOnTopToggle", true);
		Settings.SetSwitch("DeviceBottomToggle", true);
		Settings.TypeNumber("VolumeLimitBox", 30);
		App.WaitForSettings(s => Saved(s, "Window", "PopupMode") == "true" && Saved(s, "Window", "AlwaysOnTop") == "true"
		                         && Saved(s, "Window", "DeviceViewAtTheBottom") == "true" && Saved(s, "General", "VolumeLimit") == "30",
			"the Volume mixer page's settings saved");

		Restart();
		Settings.Open("NavMixer", "PopupToggle");
		Assert.IsTrue(Settings.IsOn("PopupToggle"), "pop-up mode");
		Assert.IsTrue(Settings.IsOn("AlwaysOnTopToggle"), "always on top");
		Assert.IsTrue(Settings.IsOn("DeviceBottomToggle"), "device at the bottom");
		Assert.AreEqual("30", Settings.Number("VolumeLimitBox"), "volume limit");
	}

	[TestMethod]
	public void TheNotificationsPageSurvivesARestart()
	{
		StartApp();
		Settings.Open("NavNotifications", "OsdToggle");

		Settings.SetSwitch("OsdToggle", true);
		Settings.SetSwitch("ReactAllToggle", true);
		Settings.Select(Settings.RadioNamed("Bottom right"));
		Settings.TypeNumber("HorizontalDistanceBox", 40);
		Settings.TypeNumber("VerticalDistanceBox", 60);
		string time = SelectAnother("DisplayTimeBox");
		App.WaitForSettings(s => Saved(s, "Notifications", "Enabled") == "true" && Saved(s, "Notifications", "ReactToAllVolumeChanges") == "true"
		                         && Saved(s, "Notifications", "Position") == "BottomRight" && Saved(s, "Notifications", "HorizontalIndent") == "40"
		                         && Saved(s, "Notifications", "VerticalIndent") == "60" && Saved(s, "Notifications", "DisplayTimeSeconds") != "3",
			"the Notifications page's settings saved");

		Restart();
		Settings.Open("NavNotifications", "OsdToggle");
		Assert.IsTrue(Settings.IsOn("OsdToggle"), "OSD notifications");
		Assert.IsTrue(Settings.IsOn("ReactAllToggle"), "react to all volume changes");
		Assert.IsTrue(SettingsWindow.IsSelected(Settings.RadioNamed("Bottom right")), "corner");
		Assert.AreEqual("40", Settings.Number("HorizontalDistanceBox"), "horizontal distance");
		Assert.AreEqual("60", Settings.Number("VerticalDistanceBox"), "vertical distance");
		Assert.AreEqual(time, Settings.SelectedName("DisplayTimeBox"), "display time");
	}
}
