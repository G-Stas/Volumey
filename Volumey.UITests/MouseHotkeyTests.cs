using System.Drawing;
using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// Mouse hotkeys: the hotkey box takes mouse buttons and the wheel, and saved mouse hotkeys change the real default device's
/// volume and mute (put back afterwards) in both hotkey modes. Buttons are pressed over the settings window's title bar,
/// where a middle click or a wheel step with Ctrl does nothing else; side buttons are only pressed over a hotkey box,
/// which takes them (elsewhere they would go back in the settings' navigation).
/// </summary>
[TestClass]
public class MouseHotkeyTests : UiTest
{
	//as the English UI names them (the tests run the app in English)
	private const string Back = "Back";
	private const string Forward = "Forward";
	private const string CtrlWheelUp = "Ctrl+Wheel up";
	private const string CtrlWheelRight = "Ctrl+Wheel right";
	private const string CtrlMiddle = "Ctrl+Middle click";

	private static JsonObject MouseHotkey(string button, string modifiers = "Control")
		=> new() { ["Key"] = "None", ["ModifierKeys"] = modifiers, ["Source"] = "Mouse", ["Button"] = button };

	private static bool IsMouseHotkey(JsonNode? node, string button, string modifiers)
		=> node?["Source"]?.GetValue<string>() == "Mouse" && node?["Button"]?.GetValue<string>() == button
		   && node?["ModifierKeys"]?.GetValue<string>() == modifiers;

	private string BoxText(string boxId) => Settings.Find(boxId).AsTextBox().Text;

	/// <summary>Focuses a hotkey box (keyboard focus only: a click would be a click on it) and puts the pointer over it</summary>
	private AutomationElement PointAt(string boxId)
	{
		Settings.Window.SetForeground();
		var box = Settings.Find(boxId);
		box.Focus();
		var r = box.BoundingRectangle;
		Mouse.MoveTo(new Point(r.Left + r.Width / 2, r.Top + r.Height / 2));
		Wait.UntilInputIsProcessed();
		return box;
	}

	/// <summary>The settings window's title bar, away from its buttons</summary>
	private Point TitleBar()
	{
		var r = Settings.Window.BoundingRectangle;
		return new Point(r.Left + r.Width / 3, r.Top + 16);
	}

	private static void WithCtrl(Action action)
	{
		using(Keyboard.Pressing(VirtualKeyShort.CONTROL))
		{
			action();
			Wait.UntilInputIsProcessed();
		}
		Wait.UntilInputIsProcessed();
	}

	private static void Click(MouseButton button)
	{
		Mouse.Click(button);
		Wait.UntilInputIsProcessed();
	}

	[TestMethod]
	public void TheBoxTakesSideButtonsAloneAndClicksAndTheWheelWithAModifier()
	{
		StartApp();
		Settings.Open("NavDevices", "DeviceMuteBox");

		PointAt("DeviceMuteBox");
		Click(MouseButton.XButton1);
		Assert.AreEqual(Back, BoxText("DeviceMuteBox"), "a side button alone isn't taken");
		Assert.IsNotNull(Settings.TryFind("DeviceMuteBox", TimeSpan.FromSeconds(1)), "the side button also went back in the settings");

		//a plain scroll and a plain middle click are just a scroll and a click
		Mouse.Scroll(1);
		Click(MouseButton.Middle);
		Assert.AreEqual(Back, BoxText("DeviceMuteBox"), "a click or scroll without a modifier was taken");

		PointAt("DeviceUpBox");
		WithCtrl(() => Mouse.Scroll(1));
		Assert.AreEqual(CtrlWheelUp, BoxText("DeviceUpBox"));

		PointAt("DeviceDownBox");
		WithCtrl(() => Mouse.HorizontalScroll(1));
		Assert.AreEqual(CtrlWheelRight, BoxText("DeviceDownBox"), "the horizontal wheel (no WPF event) isn't taken");

		PointAt("DeviceMuteBox");
		WithCtrl(() => Click(MouseButton.Middle));
		Assert.AreEqual(CtrlMiddle, BoxText("DeviceMuteBox"));

		//a side button replaces a typed key combination
		Settings.TypeHotkey("DeviceMuteBox", Combo.F(21));
		PointAt("DeviceMuteBox");
		Click(MouseButton.XButton2);
		Assert.AreEqual(Forward, BoxText("DeviceMuteBox"));
		Settings.LeaveBox();
	}

	[TestMethod]
	public void ATypedMouseHotkeyIsSavedAndShownAfterARestart()
	{
		StartApp();
		Settings.Open("NavDevices", "DeviceMuteBox");

		PointAt("DeviceMuteBox");
		WithCtrl(() => Click(MouseButton.Middle));
		Settings.LeaveBox();
		Settings.SetSwitch("DeviceMuteToggle", true);

		App.WaitForSettings(s => IsMouseHotkey(s["Hotkeys"]?["DeviceMute"], "Middle", "Control"), "the mouse hotkey saved");

		Restart();
		Settings.Open("NavDevices", "DeviceMuteBox");
		Assert.AreEqual(CtrlMiddle, BoxText("DeviceMuteBox"), "the box doesn't show the mouse hotkey after a restart");
		Assert.IsTrue(Settings.IsOn("DeviceMuteToggle"));
	}

	[TestMethod]
	[TestCategory("Audio")]
	[DataRow(true, DisplayName = "blocking mode")]
	[DataRow(false, DisplayName = "shared mode")]
	public void MouseHotkeysChangeTheDevicesVolumeAndMute(bool blockKeys)
	{
		if(!Audio.HasOutputDevice)
			Assert.Inconclusive("No output device");
		Later(Audio.KeepDeviceState());
		StartApp(new JsonObject
		{
			["Hotkeys"] = new JsonObject
			{
				["BlockInSystem"] = blockKeys,
				["DeviceVolumeUp"] = MouseHotkey("WheelUp"),
				["DeviceVolumeDown"] = MouseHotkey("WheelDown"),
				["DeviceMute"] = MouseHotkey("Middle"),
			},
		});
		int step = App.ReadSettings()["General"]?["VolumeStep"]?.GetValue<int>() ?? 1;
		int start = Audio.DeviceVolume;
		if(start < step || start > 100 - step)
		{
			start = 50;
			Audio.SetDeviceVolume(start);
		}
		bool muted = Audio.DeviceMuted;
		Settings.Window.SetForeground();
		Mouse.MoveTo(TitleBar());
		Wait.UntilInputIsProcessed();

		WithCtrl(() => Mouse.Scroll(-1));
		Assert.AreEqual(start - step, Audio.WaitFor(() => Audio.DeviceVolume, start - step), "Ctrl+wheel down didn't lower the device");
		WithCtrl(() => Mouse.Scroll(1));
		Assert.AreEqual(start, Audio.WaitFor(() => Audio.DeviceVolume, start), "Ctrl+wheel up didn't raise the device");

		//a wheel step without Ctrl isn't the hotkey
		Mouse.Scroll(-1);
		Wait.UntilInputIsProcessed();
		Assert.IsFalse(Eventually(() => Audio.DeviceVolume != start, 500), "a plain wheel step changed the volume");

		WithCtrl(() => Click(MouseButton.Middle));
		Assert.AreEqual(!muted, Audio.WaitFor(() => Audio.DeviceMuted, !muted), "Ctrl+middle click didn't flip mute");
		WithCtrl(() => Click(MouseButton.Middle));
		Assert.AreEqual(muted, Audio.WaitFor(() => Audio.DeviceMuted, muted), "the second Ctrl+middle click didn't flip mute back");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void MouseHotkeysKeepWorkingAfterTheHotkeyModeChanges()
	{
		if(!Audio.HasOutputDevice)
			Assert.Inconclusive("No output device");
		Later(Audio.KeepDeviceState());
		StartApp(new JsonObject
		{
			["Hotkeys"] = new JsonObject { ["BlockInSystem"] = true, ["DeviceMute"] = MouseHotkey("Middle") },
		});
		bool muted = Audio.DeviceMuted;

		//the keyboard's hook starts next to the mouse's: the one input hook is replaced
		Settings.Open("NavGeneral", "ShareHotkeysRadio");
		Settings.Select(Settings.Find("ShareHotkeysRadio"));
		App.WaitForSettings(s => s["Hotkeys"]?["BlockInSystem"]?.GetValue<bool>() == false, "shared mode saved");

		//the hook is replaced on a background thread once the changes settle
		Thread.Sleep(1500);
		Settings.Window.SetForeground();
		Mouse.MoveTo(TitleBar());
		Wait.UntilInputIsProcessed();
		WithCtrl(() => Click(MouseButton.Middle));
		Assert.AreEqual(!muted, Audio.WaitFor(() => Audio.DeviceMuted, !muted), "the mouse hotkey stopped working after switching to shared mode");
		WithCtrl(() => Click(MouseButton.Middle));
		Assert.AreEqual(muted, Audio.WaitFor(() => Audio.DeviceMuted, muted), "the second press didn't flip mute back");
	}
}
