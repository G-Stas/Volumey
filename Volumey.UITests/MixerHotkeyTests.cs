using FlaUI.Core.Tools;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The Volume mixer page's open-mixer hotkey
/// </summary>
[TestClass]
public class MixerHotkeyTests : UiTest
{
	private void OpenPage()
	{
		StartApp();
		Settings.Open("NavMixer", "OpenHotkeyBox");
	}

	[TestMethod]
	public void TheOpenMixerHotkeyIsRegisteredTogglesTheMixerAndIsReleased()
	{
		OpenPage();
		var combo = Combo.F(13);

		Settings.TypeHotkey("OpenHotkeyBox", combo);
		Settings.SetSwitch("OpenHotkeyToggle", true);

		App.WaitForSettings(s => IsHotkey(s["Hotkeys"]?["OpenMixer"], combo), "the open-mixer hotkey saved");
		Assert.IsTrue(combo.IsHeldByAnotherApp(), "Volumey doesn't hold the hotkey");
		Assert.IsFalse(Settings.IsEnabled("OpenHotkeyBox"), "the box stays editable while the hotkey is on");

		//each press flips the mixer (whether it's shown now depends on the focus: a mixer the test showed never had it, so it didn't hide)
		bool shown = App.MixerIsVisible;
		combo.Press();
		Assert.IsTrue(Retry.WhileFalse(() => App.MixerIsVisible == !shown, TimeSpan.FromSeconds(3)).Result, "the first press didn't flip the mixer");
		combo.Press();
		Assert.IsTrue(Retry.WhileFalse(() => App.MixerIsVisible == shown, TimeSpan.FromSeconds(3)).Result, "the second press didn't flip it back");

		Settings.SetSwitch("OpenHotkeyToggle", false);
		App.WaitForSettings(s => s["Hotkeys"]?["OpenMixer"] == null, "the open-mixer hotkey removed");
		Assert.IsFalse(combo.IsHeldByAnotherApp(), "Volumey still holds the hotkey");
	}

	[TestMethod]
	public void AHotkeyAnotherAppHoldsIsRefused()
	{
		OpenPage();
		var combo = Combo.F(14);
		//typed first: while another app holds a combo (blocking mode), Windows delivers it as that app's hotkey and the box never sees the keys
		Settings.TypeHotkey("OpenHotkeyBox", combo);
		using var held = combo.HoldInThisProcess();

		Settings.Find("OpenHotkeyToggle").Patterns.Toggle.Pattern.Toggle();

		Assert.IsTrue(Settings.ShowsText(ErrorFailedToRegister), "no error shown");
		Assert.IsFalse(Settings.IsOn("OpenHotkeyToggle"), "the switch stayed on");
		Assert.IsNull(App.ReadSettings()["Hotkeys"]?["OpenMixer"], "a refused hotkey was saved");
	}

	[TestMethod]
	public void F12AloneIsRejected()
	{
		OpenPage();

		Settings.TypeHotkey("OpenHotkeyBox", new Combo(FlaUI.Core.WindowsAPI.VirtualKeyShort.F12, Ctrl: false, Alt: false, Shift: false));
		Settings.SetSwitch("OpenHotkeyToggle", true);

		Assert.IsTrue(Settings.ShowsText(ErrorF12), "no error shown");
		Assert.IsNull(App.ReadSettings()["Hotkeys"]?["OpenMixer"], "F12 was saved");
	}
}
