using System.Text.Json.Nodes;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The General page's hotkey mode: blocking (RegisterHotKey, other apps don't get the keys) and shared (a keyboard hook that only listens)
/// </summary>
[TestClass]
public class HotkeyModeTests : UiTest
{
	[TestMethod]
	public void SwitchingTheModeMovesTheHotkeysAndTheyKeepWorking()
	{
		var combo = Combo.F(21);
		StartApp(new JsonObject { ["Hotkeys"] = new JsonObject { ["OpenMixer"] = HotkeyJson(combo) } });
		Settings.Open("NavGeneral", "ShareHotkeysRadio");
		Assert.IsTrue(combo.IsHeldByAnotherApp(), "a saved hotkey isn't registered in blocking mode");

		Settings.Find("ShareHotkeysRadio").Patterns.SelectionItem.Pattern.Select();
		App.WaitForSettings(s => s["Hotkeys"]?["BlockInSystem"]?.GetValue<bool>() == false, "shared mode saved");
		Assert.IsTrue(Eventually(() => !combo.IsHeldByAnotherApp()), "shared mode still blocks the keys for other apps");

		//the hook still sees the keys
		bool shown = App.MixerIsVisible;
		combo.Press();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible == !shown), "the hotkey doesn't work in shared mode");
		combo.Press();
		Eventually(() => App.MixerIsVisible == shown);

		Settings.Window.SetForeground();
		Settings.Find("BlockHotkeysRadio").Patterns.SelectionItem.Pattern.Select();
		App.WaitForSettings(s => s["Hotkeys"]?["BlockInSystem"]?.GetValue<bool>() == true, "blocking mode saved");
		Assert.IsTrue(Eventually(combo.IsHeldByAnotherApp), "the hotkey isn't registered again in blocking mode");
	}

	[TestMethod]
	public void InSharedModeAHotkeyAnotherAppHoldsStillWorks()
	{
		//a hook can't be refused: another app's RegisterHotKey on the same keys doesn't stop it from being saved
		var combo = Combo.F(22);
		StartApp(new JsonObject { ["Hotkeys"] = new JsonObject { ["BlockInSystem"] = false } });
		Settings.Open("NavMixer", "OpenHotkeyBox");
		Settings.TypeHotkey("OpenHotkeyBox", combo);
		using var held = combo.HoldInThisProcess();

		Settings.SetSwitch("OpenHotkeyToggle", true);

		App.WaitForSettings(s => IsHotkey(s["Hotkeys"]?["OpenMixer"], combo), "the hotkey saved");
		Assert.IsFalse(Settings.ShowsText(ErrorFailedToRegister, 1000), "an error is shown in shared mode");
	}
}
