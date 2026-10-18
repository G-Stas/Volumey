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

	//keys as the other UI tests press them (Ctrl+Alt+Shift with an F-key no keyboard has), next to the mouse hotkeys
	private static readonly Combo OpenMixerKeys = Combo.F(23);

	private static JsonObject HotkeysWith(bool blockKeys, params (string name, JsonNode value)[] hotkeys)
	{
		var section = new JsonObject { ["BlockInSystem"] = blockKeys };
		foreach(var (name, value) in hotkeys)
			section[name] = value;
		return new JsonObject { ["Hotkeys"] = section };
	}

	/// <summary>Presses with the pointer over the settings window's title bar</summary>
	private void AtTitleBar(Action press)
	{
		Settings.Window.SetForeground();
		Mouse.MoveTo(TitleBar());
		Wait.UntilInputIsProcessed();
		press();
	}

	/// <summary>The hook is replaced on a background thread once the changes settle</summary>
	private static void LetTheHookSettle() => Thread.Sleep(1000);

	private void SwitchMode(bool blockKeys)
	{
		Settings.Open("NavGeneral", "ShareHotkeysRadio");
		Settings.Select(Settings.Find(blockKeys ? "BlockHotkeysRadio" : "ShareHotkeysRadio"));
		App.WaitForSettings(s => s["Hotkeys"]?["BlockInSystem"]?.GetValue<bool>() == blockKeys, blockKeys ? "blocking mode saved" : "shared mode saved");
		LetTheHookSettle();
	}

	/// <summary>Ctrl+middle click mutes and unmutes the device (the device mute hotkey)</summary>
	private void AssertCtrlMiddleFlipsMute(string when)
	{
		bool muted = Audio.DeviceMuted;
		AtTitleBar(() => WithCtrl(() => Click(MouseButton.Middle)));
		Assert.AreEqual(!muted, Audio.WaitFor(() => Audio.DeviceMuted, !muted), $"Ctrl+middle click didn't flip mute {when}");
		AtTitleBar(() => WithCtrl(() => Click(MouseButton.Middle)));
		Assert.AreEqual(muted, Audio.WaitFor(() => Audio.DeviceMuted, muted), $"the second Ctrl+middle click didn't flip mute back {when}");
	}

	/// <summary>The open-mixer keys show or hide the mixer, and a second press puts it back</summary>
	private void AssertOpenMixerKeysWork(string when)
	{
		bool shown = App.MixerIsVisible;
		OpenMixerKeys.Press();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible == !shown), $"the open-mixer keys didn't work {when}");
		OpenMixerKeys.Press();
		Eventually(() => App.MixerIsVisible == shown);
	}

	/// <summary>The app's visible windows without a title: a context menu is one (UI Automation doesn't list it at the top)</summary>
	private int UntitledWindows() => App.VisibleWindows().Count(w => w.Title.Length == 0);

	private void RequireAudio()
	{
		if(!Audio.HasOutputDevice)
			Assert.Inconclusive("No output device");
		Later(Audio.KeepDeviceState());
	}

	[TestMethod]
	public void PlainLeftAndRightClicksOnABoxAreJustClicksAndOpenNoMenu()
	{
		StartApp();
		Settings.Open("NavDevices", "DeviceMuteBox");

		PointAt("DeviceMuteBox");
		int untitled = UntitledWindows();
		Click(MouseButton.Left);
		Click(MouseButton.Right);
		Assert.AreEqual(string.Empty, BoxText("DeviceMuteBox"), "a plain click was taken as a hotkey");
		//a hotkey box has no Copy/Cut/Paste menu
		Assert.IsFalse(Eventually(() => UntitledWindows() > untitled, 700), "a plain right click opened a menu");

		PointAt("DeviceMuteBox");
		WithCtrl(() => Click(MouseButton.Right));
		Assert.AreEqual("Ctrl+Right click", BoxText("DeviceMuteBox"), "Ctrl+right click isn't taken");
		Assert.IsFalse(Eventually(() => UntitledWindows() > untitled, 700), "a right click the box took opened a menu");
		Settings.LeaveBox();
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void ASideButtonAloneWorksAsAHotkey()
	{
		RequireAudio();
		StartApp(HotkeysWith(true, ("DeviceMute", MouseHotkey("XButton1", "None"))));
		bool muted = Audio.DeviceMuted;

		AtTitleBar(() => Click(MouseButton.XButton1));
		Assert.AreEqual(!muted, Audio.WaitFor(() => Audio.DeviceMuted, !muted), "the Back button didn't flip mute");
		AtTitleBar(() => Click(MouseButton.XButton1));
		Assert.AreEqual(muted, Audio.WaitFor(() => Audio.DeviceMuted, muted), "the second Back didn't flip mute back");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void MouseHotkeysKeepWorkingAfterSwitchingToBlockingMode()
	{
		RequireAudio();
		StartApp(HotkeysWith(false, ("DeviceMute", MouseHotkey("Middle"))));
		AssertCtrlMiddleFlipsMute("in shared mode");

		//keys and mouse -> the mouse only
		SwitchMode(blockKeys: true);

		AssertCtrlMiddleFlipsMute("after switching to blocking mode");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void KeysAndMouseHotkeysKeepWorkingThroughSeveralModeSwitches()
	{
		RequireAudio();
		StartApp(HotkeysWith(true, ("DeviceMute", MouseHotkey("Middle")), ("OpenMixer", HotkeyJson(OpenMixerKeys))));
		AssertCtrlMiddleFlipsMute("in blocking mode");
		AssertOpenMixerKeysWork("in blocking mode");

		foreach(bool blockKeys in new[] { false, true, false, true })
		{
			SwitchMode(blockKeys);
			string mode = blockKeys ? "after switching to blocking mode" : "after switching to shared mode";
			AssertCtrlMiddleFlipsMute(mode);
			AssertOpenMixerKeysWork(mode);
		}
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AddingTheFirstMouseHotkeyInSharedModeKeepsTheKeysWorking()
	{
		//the hook watches only the keys until the mouse hotkey is switched on: then it's replaced by one for both
		RequireAudio();
		StartApp(HotkeysWith(false, ("OpenMixer", HotkeyJson(OpenMixerKeys))));
		AssertOpenMixerKeysWork("before the mouse hotkey");
		Settings.Open("NavDevices", "DeviceMuteBox");

		PointAt("DeviceMuteBox");
		WithCtrl(() => Click(MouseButton.Middle));
		Settings.LeaveBox();
		Settings.SetSwitch("DeviceMuteToggle", true);
		App.WaitForSettings(s => IsMouseHotkey(s["Hotkeys"]?["DeviceMute"], "Middle", "Control"), "the mouse hotkey saved");
		LetTheHookSettle();

		AssertCtrlMiddleFlipsMute("after it was switched on");
		AssertOpenMixerKeysWork("after the mouse hotkey was switched on");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void RemovingTheLastMouseHotkeyStopsItAndKeepsTheKeysWorking()
	{
		RequireAudio();
		StartApp(HotkeysWith(false, ("DeviceMute", MouseHotkey("Middle")), ("OpenMixer", HotkeyJson(OpenMixerKeys))));
		Settings.Open("NavDevices", "DeviceMuteToggle");
		AssertCtrlMiddleFlipsMute("while it's on");

		//keys and mouse -> the keys only
		Settings.SetSwitch("DeviceMuteToggle", false);
		App.WaitForSettings(s => s["Hotkeys"]?["DeviceMute"] == null, "the mouse hotkey removed");
		LetTheHookSettle();

		bool muted = Audio.DeviceMuted;
		AtTitleBar(() => WithCtrl(() => Click(MouseButton.Middle)));
		Assert.IsFalse(Eventually(() => Audio.DeviceMuted != muted, 1000), "the switched-off mouse hotkey still mutes");
		AssertOpenMixerKeysWork("after the last mouse hotkey was switched off");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void ASavedMouseHotkeyWorksRightAfterARestartInSharedMode()
	{
		RequireAudio();
		StartApp(HotkeysWith(false, ("DeviceMute", MouseHotkey("Middle")), ("OpenMixer", HotkeyJson(OpenMixerKeys))));

		Restart();
		LetTheHookSettle();

		AssertCtrlMiddleFlipsMute("after the restart");
		AssertOpenMixerKeysWork("after the restart");
	}

	[TestMethod]
	public void TheOpenMixerHotkeyCanBeAMouseButton()
	{
		StartApp(HotkeysWith(true, ("OpenMixer", MouseHotkey("Middle", "Control, Shift"))));
		LetTheHookSettle();
		bool shown = App.MixerIsVisible;

		void CtrlShiftMiddle()
		{
			using(Keyboard.Pressing(VirtualKeyShort.SHIFT))
				WithCtrl(() => Click(MouseButton.Middle));
		}

		AtTitleBar(CtrlShiftMiddle);
		Assert.IsTrue(Eventually(() => App.MixerIsVisible == !shown), "Ctrl+Shift+middle click didn't flip the mixer");
		AtTitleBar(CtrlShiftMiddle);
		Assert.IsTrue(Eventually(() => App.MixerIsVisible == shown), "the second press didn't flip it back");
	}

	[TestMethod]
	public void AMouseHotkeyUsedElsewhereIsRefused()
	{
		StartApp(HotkeysWith(true, ("DeviceMute", MouseHotkey("Middle"))));
		Settings.Open("NavMixer", "OpenHotkeyBox");

		PointAt("OpenHotkeyBox");
		WithCtrl(() => Click(MouseButton.Middle));
		Settings.LeaveBox();
		Settings.Find("OpenHotkeyToggle").Patterns.Toggle.Pattern.Toggle();

		Assert.IsTrue(Settings.ShowsText(ErrorExists), "no error for a mouse hotkey the device mute already uses");
		Assert.IsNull(App.ReadSettings()["Hotkeys"]?["OpenMixer"], "the same mouse hotkey was saved twice");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppsVolumeHotkeysCanBeTheWheel()
	{
		using var testApp = Later(TestAudioApp.Start());
		StartApp();
		Settings.Open("NavAppHotkeys", "AddAppButton");
		int step = App.ReadSettings()["General"]?["VolumeStep"]?.GetValue<int>() ?? 1;

		Settings.Find("AddAppButton").AsButton().Invoke();
		var apps = Settings.Find("AddAppBox").AsComboBox();
		apps.Expand();
		var item = FlaUI.Core.Tools.Retry.WhileNull(() => apps.Items.FirstOrDefault(i => i.Name == testApp.DisplayName), TimeSpan.FromSeconds(5)).Result
		           ?? throw new AssertFailedException($"{testApp.DisplayName} isn't in the dialog's list");
		item.Select();
		apps.Collapse();
		PointAt("AddAppUpBox");
		WithCtrl(() => Mouse.Scroll(1));
		PointAt("AddAppDownBox");
		WithCtrl(() => Mouse.Scroll(-1));
		Assert.AreEqual(CtrlWheelUp, BoxText("AddAppUpBox"), "the dialog's box didn't take Ctrl+wheel up");
		Settings.Find("PrimaryButton").AsButton().Invoke();
		App.WaitForSettings(s => s["Hotkeys"]?["Apps"]?.AsArray().Any(a => IsMouseHotkey(a?["VolumeUp"], "WheelUp", "Control")
		                                                                       && IsMouseHotkey(a?["VolumeDown"], "WheelDown", "Control")) == true,
			"the app's mouse hotkeys saved");
		LetTheHookSettle();

		AtTitleBar(() => WithCtrl(() => Mouse.Scroll(-1)));
		Assert.AreEqual(100 - step, Audio.WaitFor(() => testApp.Volume, 100 - step), "Ctrl+wheel down didn't lower the app");
		AtTitleBar(() => WithCtrl(() => Mouse.Scroll(1)));
		Assert.AreEqual(100, Audio.WaitFor(() => testApp.Volume, 100), "Ctrl+wheel up didn't raise the app");
	}
}
