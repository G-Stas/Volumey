using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The device box of an opened app in the mixer: where Windows plays the app. A move only ever changes the app's own choice in Windows,
/// never the default device, and the test puts it back
/// </summary>
[TestClass]
public class AppOutputDeviceTests : UiTest
{
	private const string DefaultDevice = "Default device";

	//its own executable: Windows keeps a choice per executable, so a choice left over by a failed run can't reach the other tests' app
	private const string MovedApp = "VolumeyUiMovedApp";

	private AutomationElement OpenDeviceBox(string app)
	{
		var expand = Retry.WhileNull(() => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(app))),
			             TimeSpan.FromSeconds(10)).Result
		             ?? throw new AssertFailedException($"No row for {app} in the mixer");
		if(expand.Patterns.Toggle.Pattern.ToggleState.Value != ToggleState.On)
			expand.Patterns.Toggle.Pattern.Toggle();
		return Retry.WhileNull(() => App.Mixer.FindFirstDescendant(cf => cf.ByAutomationId("AppOutputDeviceBox").And(cf.ByName($"Play {app} on"))),
			       TimeSpan.FromSeconds(5)).Result
		       ?? throw new AssertFailedException($"Opening {app} showed no device box");
	}

	private static AutomationElement[] Items(AutomationElement box)
	{
		box.Patterns.ExpandCollapse.Pattern.Expand();
		return Retry.WhileEmpty(() => box.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)), TimeSpan.FromSeconds(5)).Result ?? [];
	}

	private static string? Selected(AutomationElement box)
		=> box.Patterns.Selection.Pattern.Selection.Value.FirstOrDefault()?.Name;

	private static void Pick(AutomationElement box, string device)
	{
		var item = Items(box).FirstOrDefault(i => i.Name == device) ?? throw new AssertFailedException($"{device} isn't in the device box");
		item.Patterns.SelectionItem.Pattern.Select();
		box.Patterns.ExpandCollapse.Pattern.Collapse();
	}

	/// <summary>A device to move test apps to: never a default device, and no other app plays there</summary>
	private static (string Id, string Name) OtherDevice()
	{
		var other = Audio.OtherOutputDevice();
		if(other == null)
			Assert.Inconclusive("No second output device that no app plays on: an app can't be moved");
		if(!Routes.Available)
			Assert.Inconclusive("Windows doesn't offer per-app output devices");
		return other.Value;
	}

	/// <summary>A test app that follows Windows' routing like a real player, under a fresh exe name (Windows keeps routes and
	/// per-device volumes per executable), put back on the default device after the test</summary>
	private TestAudioApp StartRoutingApp(string name)
	{
		var app = Later(TestAudioApp.Start($"{name}{Guid.NewGuid().ToString("N")[..6]}", routing: true));
		Later(Routes.ResetLater(app));
		return app;
	}

	private bool HasRow(string app) => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Slider).And(cf.ByName(app))) != null;

	private AutomationElement? Badge(string device) => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName(device)));

	/// <summary>Shows the device in the mixer through its device picker</summary>
	private void ShowInMixer(string device)
	{
		var picker = App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox).And(cf.ByName("Output device")))
		             ?? throw new AssertFailedException("The mixer has no device picker");
		picker.Patterns.ExpandCollapse.Pattern.Expand();
		var item = Retry.WhileNull(() => picker.FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(device))),
			           TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException($"{device} isn't in the device picker");
		item.Patterns.SelectionItem.Pattern.Select();
		picker.Patterns.ExpandCollapse.Pattern.Collapse();
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AMovedAppPlaysThereLeavesTheDefaultsListAndComesBackWithUndo()
	{
		var other = OtherDevice();
		var testApp = StartRoutingApp("VolumeyUiRouted");
		StartMixer();
		string app = testApp.DisplayName;
		string defaultDevice = Audio.DefaultDeviceName;

		Pick(OpenDeviceBox(app), other.Name);

		Assert.IsTrue(Eventually(() => Audio.PlaysOn(testApp.Process.Id) == other.Name, 5000), $"the app plays on {Audio.PlaysOn(testApp.Process.Id)}");
		Assert.AreEqual(other.Id, Routes.PinnedDevice(testApp.Process.Id), "Windows doesn't have the app on the chosen device");
		Assert.IsTrue(Eventually(() => !HasRow(app), 5000), "the app's row stayed in the default device's list");
		Assert.AreEqual(defaultDevice, Audio.DefaultDeviceName, "the default device changed");

		var undo = App.Mixer.FindFirstDescendant(cf => cf.ByAutomationId("UndoMoveButton")) ?? throw new AssertFailedException("No Undo");
		undo.Patterns.Invoke.Pattern.Invoke();

		Assert.IsTrue(Eventually(() => Audio.PlaysOn(testApp.Process.Id) == defaultDevice, 5000), $"Undo left the app on {Audio.PlaysOn(testApp.Process.Id)}");
		Assert.IsNull(Routes.PinnedDevice(testApp.Process.Id), "Windows still has the app on the other device");
		Assert.IsTrue(Eventually(() => HasRow(app), 5000), "the app's row didn't come back");
		Assert.IsNull(Badge(other.Name), "the device's name is still shown next to the app");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AMovedAppIsListedOnItsDeviceWithTheDevicesName()
	{
		var other = OtherDevice();
		var testApp = StartRoutingApp("VolumeyUiListed");
		StartMixer();
		string app = testApp.DisplayName;
		Pick(OpenDeviceBox(app), other.Name);
		Assert.IsTrue(Eventually(() => Audio.PlaysOn(testApp.Process.Id) == other.Name, 5000), "the app didn't move");

		ShowInMixer(other.Name);

		Assert.IsTrue(Eventually(() => HasRow(app), 5000), $"no row for the app on {other.Name}");
		Assert.IsNotNull(Retry.WhileNull(() => Badge(other.Name), TimeSpan.FromSeconds(3)).Result, "no device name next to the app");
		Assert.AreEqual(other.Name, Selected(OpenDeviceBox(app)));
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AMovedAppKeepsItsVolumeOnTheNewDevice()
	{
		var other = OtherDevice();
		var testApp = StartRoutingApp("VolumeyUiKeepsVolume");
		StartMixer();
		string app = testApp.DisplayName;
		var slider = Retry.WhileNull(() => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Slider).And(cf.ByName(app))),
			             TimeSpan.FromSeconds(10)).Result ?? throw new AssertFailedException("No row for the app");
		slider.Patterns.RangeValue.Pattern.SetValue(40);
		Assert.AreEqual(40, Audio.WaitFor(() => testApp.Volume, 40), "the slider didn't set the app's volume");

		Pick(OpenDeviceBox(app), other.Name);

		//Windows starts the app at its own volume for that device (a fresh executable: 100); Volumey copies the app's volume there
		Assert.AreEqual(40, Audio.WaitFor(() => Audio.SessionVolumeOn(other.Id, testApp.Process.Id), 40, 5000),
			"the app didn't keep its volume on the new device");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppGoesBackToItsDeviceWhenTheDeviceIsTurnedOnAgain()
	{
		var other = OtherDevice();
		var testApp = StartRoutingApp("VolumeyUiReturns");
		StartMixer();
		string defaultDevice = Audio.DefaultDeviceName;
		Pick(OpenDeviceBox(testApp.DisplayName), other.Name);
		Assert.IsTrue(Eventually(() => Audio.PlaysOn(testApp.Process.Id) == other.Name, 5000), "the app didn't move");

		using(Later(Routes.KeepDeviceEnabled(other.Id)))
		{
			Routes.SetDeviceEnabled(other.Id, false);
			Assert.IsTrue(Eventually(() => Audio.PlaysOn(testApp.Process.Id) == defaultDevice, 5000),
				$"with its device off the app plays on {Audio.PlaysOn(testApp.Process.Id)}");

			Routes.SetDeviceEnabled(other.Id, true);

			//Windows doesn't send the app back itself; Volumey sets its route again
			Assert.IsTrue(Eventually(() => Audio.PlaysOn(testApp.Process.Id) == other.Name, 8000),
				$"after its device came back the app plays on {Audio.PlaysOn(testApp.Process.Id)}");
		}
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppMovedOutsideVolumeyIsShownOnItsDevice()
	{
		var other = OtherDevice();
		var testApp = StartRoutingApp("VolumeyUiOutside");
		//as Windows' own volume mixer settings would, before Volumey starts
		Routes.Pin(testApp.Process.Id, other.Id);
		Assert.IsTrue(Eventually(() => Audio.PlaysOn(testApp.Process.Id) == other.Name, 5000), "the app didn't move");
		StartMixer();
		string app = testApp.DisplayName;

		Assert.IsTrue(Eventually(() => !HasRow(app), 5000), "the app has a row in the default device's list");
		ShowInMixer(other.Name);
		Assert.IsTrue(Eventually(() => HasRow(app), 5000), $"no row for the app on {other.Name}");
		Assert.AreEqual(other.Name, Selected(OpenDeviceBox(app)));
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AppHotkeysReachTheAppOnItsNewDevice()
	{
		var other = OtherDevice();
		var testApp = StartRoutingApp("VolumeyUiHotkeyFollows");
		string app = testApp.DisplayName;
		var down = Combo.F(21);
		StartMixer(new System.Text.Json.Nodes.JsonObject
		{
			["Hotkeys"] = new System.Text.Json.Nodes.JsonObject
			{
				["Apps"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
					{ ["ProcessName"] = app, ["VolumeUp"] = HotkeyJson(Combo.F(20)), ["VolumeDown"] = HotkeyJson(down) })
			}
		});
		Pick(OpenDeviceBox(app), other.Name);
		Assert.IsTrue(Eventually(() => Audio.PlaysOn(testApp.Process.Id) == other.Name, 5000), "the app didn't move");
		Assert.AreEqual(100, Audio.WaitFor(() => Audio.SessionVolumeOn(other.Id, testApp.Process.Id), 100, 5000));
		int step = App.ReadSettings()["General"]?["VolumeStep"]?.GetValue<int>() ?? 1;

		down.Press();

		Assert.AreEqual(100 - step, Audio.WaitFor(() => Audio.SessionVolumeOn(other.Id, testApp.Process.Id), 100 - step, 5000),
			"the hotkey didn't reach the app on its new device");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnOpenedAppListsTheDefaultDeviceAndEveryOutputDevice()
	{
		using var testApp = Later(TestAudioApp.Start());
		StartMixer();
		var box = OpenDeviceBox(testApp.DisplayName);
		Assert.AreEqual(DefaultDevice, Selected(box));

		var items = Items(box).Select(i => i.Name).ToArray();
		box.Patterns.ExpandCollapse.Pattern.Collapse();

		CollectionAssert.AreEqual(new[] { DefaultDevice }.Concat(Audio.OutputDeviceNames()).ToArray(), items);
		Assert.AreEqual(DefaultDevice, Selected(box), "closing the list changed the choice");
		Assert.IsNull(App.Mixer.FindFirstDescendant(cf => cf.ByAutomationId("UndoMoveButton")), "a notice showed without a move");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppMovedToAnotherDeviceComesBackWithUndo()
	{
		string defaultDevice = Audio.DefaultDeviceName;
		string? other = Audio.OutputDeviceNames().FirstOrDefault(name => name != defaultDevice);
		if(other == null)
			Assert.Inconclusive("Only one output device: an app can't be moved");
		using var testApp = Later(TestAudioApp.Start(MovedApp));
		StartMixer();
		string app = testApp.DisplayName;
		var box = OpenDeviceBox(app);
		try
		{
			Pick(box, other);

			var undo = Retry.WhileNull(() => App.Mixer.FindFirstDescendant(cf => cf.ByAutomationId("UndoMoveButton")), TimeSpan.FromSeconds(5)).Result
			           ?? throw new AssertFailedException("No notice with Undo after the move");
			Assert.IsTrue(Eventually(() => Selected(box) == other), $"the box doesn't show {other}: {Selected(box)}");
			//the test app keeps playing where it started (it opened that device itself), so its row stays, with the device's name
			Assert.IsNotNull(App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName(other))), "no badge with the device's name");
			Assert.AreEqual(defaultDevice, Audio.DefaultDeviceName, "the default device changed");

			undo.Patterns.Invoke.Pattern.Invoke();

			Assert.IsTrue(Eventually(() => Selected(box) == DefaultDevice, 5000), $"Undo didn't bring the app back: {Selected(box)}");
			Assert.IsTrue(Eventually(() => App.Mixer.FindFirstDescendant(cf => cf.ByAutomationId("UndoMoveButton")) == null), "the notice stayed after Undo");
		}
		finally
		{
			if(Selected(box) != DefaultDevice)
				Pick(box, DefaultDevice);
		}
	}
}
