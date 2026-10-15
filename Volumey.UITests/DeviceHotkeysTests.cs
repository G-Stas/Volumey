using FlaUI.Core.AutomationElements;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The Devices page's hotkeys. They change the real default device's volume and mute (put back afterwards);
/// the "make default" hotkey is only registered, never pressed: the tests must not switch the default device.
/// </summary>
[TestClass]
[TestCategory("Audio")]
public class DeviceHotkeysTests : UiTest
{
	private void OpenPage()
	{
		if(!Audio.HasOutputDevice)
			Assert.Inconclusive("No output device");
		Later(Audio.KeepDeviceState());
		StartApp();
		Settings.Open("NavDevices", "DeviceUpBox");
	}

	private int VolumeStep => App.ReadSettings()["General"]?["VolumeStep"]?.GetValue<int>() ?? 1;

	[TestMethod]
	public void TheDeviceVolumeHotkeysChangeTheDefaultDevicesVolume()
	{
		OpenPage();
		var up = Combo.F(13);
		var down = Combo.F(14);
		Settings.TypeHotkey("DeviceUpBox", up);
		Settings.TypeHotkey("DeviceDownBox", down);
		Settings.SetSwitch("DeviceVolumeToggle", true);

		App.WaitForSettings(s => IsHotkey(s["Hotkeys"]?["DeviceVolumeUp"], up) && IsHotkey(s["Hotkeys"]?["DeviceVolumeDown"], down),
			"the device volume hotkeys saved");
		Assert.IsTrue(up.IsHeldByAnotherApp() && down.IsHeldByAnotherApp(), "the device volume hotkeys aren't registered");

		int start = Audio.DeviceVolume;
		int step = VolumeStep;
		//away from the ends, so both directions move
		if(start < step || start > 100 - step)
		{
			start = 50;
			Audio.SetDeviceVolume(start);
		}
		down.Press();
		Assert.AreEqual(start - step, Audio.WaitFor(() => Audio.DeviceVolume, start - step), "volume down didn't lower the device");
		up.Press();
		Assert.AreEqual(start, Audio.WaitFor(() => Audio.DeviceVolume, start), "volume up didn't raise the device");

		Settings.SetSwitch("DeviceVolumeToggle", false);
		App.WaitForSettings(s => s["Hotkeys"]?["DeviceVolumeUp"] == null, "the device volume hotkeys removed");
		Assert.IsFalse(up.IsHeldByAnotherApp() || down.IsHeldByAnotherApp(), "the device volume hotkeys are still registered");
	}

	[TestMethod]
	public void TheMuteHotkeyMutesAndUnmutes()
	{
		OpenPage();
		var mute = Combo.F(15);
		Settings.TypeHotkey("DeviceMuteBox", mute);
		Settings.SetSwitch("DeviceMuteToggle", true);
		App.WaitForSettings(s => IsHotkey(s["Hotkeys"]?["DeviceMute"], mute), "the mute hotkey saved");

		bool muted = Audio.DeviceMuted;
		mute.Press();
		Assert.AreEqual(!muted, Audio.WaitFor(() => Audio.DeviceMuted, !muted), "the first press didn't flip mute");
		mute.Press();
		Assert.AreEqual(muted, Audio.WaitFor(() => Audio.DeviceMuted, muted), "the second press didn't flip mute back");
	}

	[TestMethod]
	public void ThePagesSettingsSurviveARestart()
	{
		//nothing is pressed: the device's volume, mute and default stay as they are. Format, exclusive mode and balance
		//are the device's own state in Windows, not Volumey's settings, and changing them reconfigures the device: left out
		OpenPage();
		var up = Combo.F(17);
		var down = Combo.F(18);
		var mute = Combo.F(19);
		var makeDefault = Combo.F(20);
		Settings.TypeHotkey("DeviceUpBox", up);
		Settings.TypeHotkey("DeviceDownBox", down);
		Settings.SetSwitch("DeviceVolumeToggle", true);
		Settings.TypeHotkey("DeviceMuteBox", mute);
		Settings.SetSwitch("DeviceMuteToggle", true);
		Settings.SetSwitch("KeepBalanceToggle", true);
		Settings.TypeHotkey("DeviceHotkeyBox", makeDefault);
		Settings.SetSwitch("DeviceHotkeyToggle", true);
		App.WaitForSettings(s => IsHotkey(s["Hotkeys"]?["DeviceVolumeUp"], up) && IsHotkey(s["Hotkeys"]?["DeviceVolumeDown"], down)
		                         && IsHotkey(s["Hotkeys"]?["DeviceMute"], mute)
		                         && s["Hotkeys"]?["PreventResettingVolumeBalance"]?.GetValue<bool>() == true
		                         && s["Hotkeys"]?["DefaultDevices"]?.AsArray().Any(d => IsHotkey(d?["Hotkey"], makeDefault)) == true,
			"the Devices page's settings saved");

		Restart();
		Settings.Open("NavDevices", "DeviceUpBox");

		Assert.IsTrue(Eventually(() => up.IsHeldByAnotherApp() && down.IsHeldByAnotherApp() && mute.IsHeldByAnotherApp() && makeDefault.IsHeldByAnotherApp()),
			"the hotkeys aren't registered after the restart");
		foreach(var toggle in new[] { "DeviceVolumeToggle", "DeviceMuteToggle", "KeepBalanceToggle", "DeviceHotkeyToggle" })
			Assert.IsTrue(Settings.IsOn(toggle), toggle + " is off after the restart");
		foreach(var box in new[] { "DeviceUpBox", "DeviceDownBox", "DeviceMuteBox", "DeviceHotkeyBox" })
			Assert.IsFalse(string.IsNullOrEmpty(Settings.Find(box).AsTextBox().Text), box + " is empty after the restart");
	}

	[TestMethod]
	public void TheMakeDefaultHotkeyIsRegisteredAndReleased()
	{
		OpenPage();
		var combo = Combo.F(16);
		Settings.TypeHotkey("DeviceHotkeyBox", combo);
		Settings.SetSwitch("DeviceHotkeyToggle", true);

		App.WaitForSettings(s => s["Hotkeys"]?["DefaultDevices"]?.AsArray().Any(d => IsHotkey(d?["Hotkey"], combo)) == true, "the hotkey saved");
		Assert.IsTrue(combo.IsHeldByAnotherApp(), "the hotkey isn't registered");

		Settings.SetSwitch("DeviceHotkeyToggle", false);
		App.WaitForSettings(s => s["Hotkeys"]?["DefaultDevices"]?.AsArray().Count == 0, "the hotkey removed");
		Assert.IsFalse(combo.IsHeldByAnotherApp(), "the hotkey is still registered");
	}
}
