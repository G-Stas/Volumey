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

	// A1
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

	// A2
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
