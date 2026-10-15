using System.Diagnostics;
using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The tray icon and its menu. The Output device list is only looked at: picking a device would switch the default device.
/// They work without an output device too (the icon is found by its "no device" tooltip then).
/// </summary>
[TestClass]
public class TrayTests : UiTest
{
	private static JsonObject Popup => new() { ["Window"] = new JsonObject { ["PopupMode"] = true } };

	private static void CloseMenu()
	{
		Keyboard.Type(VirtualKeyShort.ESCAPE);
		Keyboard.Type(VirtualKeyShort.ESCAPE);
		Wait.UntilInputIsProcessed();
	}

	[TestMethod]
	public void ClickingTheTrayIconOpensAndClosesThePopup()
	{
		StartMixer(Popup);
		App.HideMixer();

		App.TrayIcon().Click();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible, 5000), "a click didn't open the pop-up");

		App.TrayIcon().Click();
		Assert.IsTrue(Eventually(() => !App.MixerIsVisible, 5000), "a second click didn't close the pop-up");
	}

	[TestMethod]
	public void TheTrayMenuOpensTheMixerAndTheSettings()
	{
		StartMixer();
		App.HideMixer();

		App.TrayMenuItem("Open mixer").Patterns.Invoke.Pattern.Invoke();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible, 5000), "Open mixer didn't open it");

		App.TrayMenuItem("Settings").Patterns.Invoke.Pattern.Invoke();
		Assert.IsTrue(Eventually(() => App.FindWindow("Volumey settings") != null, 5000), "Settings didn't open the settings window");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void TheTrayMenuListsEveryOutputDevice()
	{
		string defaultId = Audio.DefaultDeviceId;
		StartMixer();
		var devices = App.TrayMenuItem("Output device");
		devices.Patterns.ExpandCollapse.Pattern.Expand();

		var listed = (Retry.WhileEmpty(() => devices.FindAllChildren(cf => cf.ByControlType(ControlType.MenuItem)), TimeSpan.FromSeconds(5)).Result ?? Array.Empty<AutomationElement>())
		                  .Select(i => i.Name).ToList();
		CloseMenu();

		CollectionAssert.AreEquivalent(Audio.OutputDeviceNames(), listed, "the menu doesn't list the active output devices");
		Assert.AreEqual(defaultId, Audio.DefaultDeviceId, "opening the list changed the default device");
	}

	[TestMethod]
	public void TheTrayMenuOpensTheSoundControlPanel()
	{
		StartMixer();
		var desktop = App.Automation.GetDesktop();
		bool OpenDialog(out AutomationElement? dialog)
		{
			dialog = desktop.FindAllChildren(cf => cf.ByClassName("#32770"))
			                .FirstOrDefault(w => Process.GetProcessById(w.Properties.ProcessId.Value).ProcessName == "rundll32");
			return dialog != null;
		}
		bool wasOpen = OpenDialog(out _);

		App.TrayMenuItem("Sound control panel").Patterns.Invoke.Pattern.Invoke();

		AutomationElement? opened = null;
		Assert.IsTrue(Eventually(() => OpenDialog(out opened), 10000), "the Sound control panel didn't open");
		//only closed when the test opened it
		if(!wasOpen)
			opened!.AsWindow().Close();
	}

	[TestMethod]
	public void TheTrayMenusSoundSettingsOpensWindowsSettings()
	{
		bool wasOpen = Process.GetProcessesByName("SystemSettings").Length > 0;
		StartMixer();

		App.TrayMenuItem("Sound settings").Patterns.Invoke.Pattern.Invoke();

		Assert.IsTrue(Eventually(() => Process.GetProcessesByName("SystemSettings").Length > 0, 10000), "Windows Settings didn't open");
		if(!wasOpen)
			foreach(var p in Process.GetProcessesByName("SystemSettings"))
				using(p)
					p.Kill();
	}

	[TestMethod]
	public void ExitQuitsTheApp()
	{
		StartMixer();

		App.ExitByTray();

		Assert.IsTrue(App.HasExited, "the app is still running");
	}
}
