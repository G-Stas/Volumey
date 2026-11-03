using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The volume lock of an opened app in the mixer (Off, Until closed, Always), on test apps that play silence: a locked volume is put
/// back after any change. Only the test apps' volumes change; a move only changes the test app's own choice in Windows
/// </summary>
[TestClass]
public class AppVolumeLockTests : UiTest
{
	private const string Off = "Off";
	private const string UntilClosed = "Until closed";
	private const string Always = "Always";

	private AutomationElement Row(string app)
		=> Retry.WhileNull(() => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem).And(cf.ByName(app))),
			   TimeSpan.FromSeconds(10)).Result
		   ?? throw new AssertFailedException($"No row for {app} in the mixer");

	private AutomationElement ExpandButton(string app)
		=> Row(app).FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(app)))
		   ?? throw new AssertFailedException($"No expand button for {app}");

	private void SetOpen(string app, bool open)
	{
		var toggle = ExpandButton(app).Patterns.Toggle.Pattern;
		if((toggle.ToggleState.Value == ToggleState.On) != open)
			toggle.Toggle();
	}

	private AutomationElement? FindLockBar(string app) => Row(app).FindFirstDescendant(cf => cf.ByAutomationId("AppVolumeLockBar"));

	private AutomationElement LockBar(string app)
	{
		SetOpen(app, true);
		return Retry.WhileNull(() => FindLockBar(app), TimeSpan.FromSeconds(5)).Result
		       ?? throw new AssertFailedException($"Opening {app} showed no lock bar");
	}

	private static string? LockMode(AutomationElement bar) => bar.Patterns.Selection.Pattern.Selection.Value.FirstOrDefault()?.Name;

	private void PickLock(string app, string mode)
	{
		var bar = LockBar(app);
		var part = bar.FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(mode)))
		           ?? throw new AssertFailedException($"The lock bar has no {mode}");
		part.Patterns.SelectionItem.Pattern.Select();
		Assert.IsTrue(Eventually(() => LockMode(bar) == mode), $"the lock bar shows {LockMode(bar)}, not {mode}");
	}

	private AutomationElement Slider(string app)
		=> Retry.WhileNull(() => Row(app).FindFirstDescendant(cf => cf.ByControlType(ControlType.Slider).And(cf.ByName(app))),
			   TimeSpan.FromSeconds(5)).Result
		   ?? throw new AssertFailedException($"No slider for {app}");

	/// <summary>The pill next to the app's name: with the row closed, the only text with the mode's word</summary>
	private AutomationElement? Pill(string app, string mode)
	{
		SetOpen(app, false);
		return Row(app).FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName(mode)));
	}

	private void SetAppVolume(string app, int volume) => Slider(app).Patterns.RangeValue.Pattern.SetValue(volume);

	private static JsonNode? SavedLock(JsonNode settings, string app)
		=> settings["General"]?["LockedAppVolumes"]?.AsArray().FirstOrDefault(e => e?["AppName"]?.GetValue<string>() == app);

	[TestMethod]
	[TestCategory("Audio")]
	public void UntilClosedHoldsTheVolumeShowsAPillAndGreysTheSlider()
	{
		var testApp = Later(TestAudioApp.Start("VolumeyUiLocked"));
		StartMixer();
		string app = testApp.DisplayName;
		SetAppVolume(app, 40);
		Assert.AreEqual(40, Audio.WaitFor(() => testApp.Volume, 40), "the slider didn't set the app's volume");

		PickLock(app, UntilClosed);

		Audio.SetSessionVolume(testApp.Process.Id, 80);
		Assert.AreEqual(40, Audio.WaitFor(() => testApp.Volume, 40, 5000), "a change from outside wasn't put back");
		Assert.IsNotNull(Pill(app, UntilClosed), "no pill next to the app's name");
		Assert.IsFalse(Slider(app).IsEnabled, "the slider of a locked app isn't greyed");
		Assert.IsNull(SavedLock(App.ReadSettings(), app), "an until-closed lock was saved");

		PickLock(app, Off);

		Assert.IsTrue(Slider(app).IsEnabled, "the slider stayed greyed after Off");
		Audio.SetSessionVolume(testApp.Process.Id, 80);
		Assert.AreEqual(80, Audio.WaitFor(() => testApp.Volume, 80, 2000), "the app stayed locked after Off");
		Assert.IsNull(Pill(app, UntilClosed), "the pill stayed after Off");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppLockedUntilClosedIsFreeWhenItStartsAgain()
	{
		const string exe = "VolumeyUiLockedOnce";
		var testApp = Later(TestAudioApp.Start(exe));
		StartMixer();
		string app = testApp.DisplayName;
		SetAppVolume(app, 40);
		Assert.AreEqual(40, Audio.WaitFor(() => testApp.Volume, 40));
		PickLock(app, UntilClosed);

		testApp.Dispose();
		Assert.IsTrue(Eventually(() => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem).And(cf.ByName(app))) == null, 10000),
			"the row stayed after the app exited");
		//it sets itself to 100% when it starts: a lock would put it back to 40
		var again = Later(TestAudioApp.Start(exe));

		Assert.AreEqual(100, Audio.WaitFor(() => again.Volume, 100, 2000), "the restarted app was locked again");
		Assert.AreEqual(Off, LockMode(LockBar(app)));
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AlwaysIsSavedAndLocksTheAppEachTimeItStarts()
	{
		const string exe = "VolumeyUiLockedAlways";
		var testApp = Later(TestAudioApp.Start(exe));
		StartMixer();
		string app = testApp.DisplayName;
		SetAppVolume(app, 40);
		Assert.AreEqual(40, Audio.WaitFor(() => testApp.Volume, 40));

		PickLock(app, Always);

		App.WaitForSettings(s => SavedLock(s, app)?["Volume"]?.GetValue<int>() == 40, "the lock saved at 40");
		Assert.IsNotNull(Pill(app, Always), "no pill next to the app's name");

		testApp.Dispose();
		Assert.IsTrue(Eventually(() => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem).And(cf.ByName(app))) == null, 10000),
			"the row stayed after the app exited");
		var again = Later(TestAudioApp.Start(exe, waitForFullVolume: false));

		Assert.AreEqual(40, Audio.WaitFor(() => again.Volume, 40, 5000), "the restarted app wasn't set to its locked volume");
		Assert.AreEqual(Always, LockMode(LockBar(app)));

		PickLock(app, Off);

		App.WaitForSettings(s => SavedLock(s, app) == null, "the saved lock removed");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AppHotkeysDontChangeALockedApp()
	{
		var testApp = Later(TestAudioApp.Start("VolumeyUiLockedHotkeys"));
		string app = testApp.DisplayName;
		var down = Combo.F(21);
		StartMixer(new JsonObject
		{
			["Hotkeys"] = new JsonObject
			{
				["Apps"] = new JsonArray(new JsonObject { ["ProcessName"] = app, ["VolumeUp"] = HotkeyJson(Combo.F(20)), ["VolumeDown"] = HotkeyJson(down) })
			}
		});
		int step = App.ReadSettings()["General"]?["VolumeStep"]?.GetValue<int>() ?? 1;
		Slider(app);
		down.Press();
		Assert.AreEqual(100 - step, Audio.WaitFor(() => testApp.Volume, 100 - step), "the hotkey didn't work before the lock");

		PickLock(app, UntilClosed);
		down.Press();
		down.Press();

		Thread.Sleep(500);
		Assert.AreEqual(100 - step, testApp.Volume, "the hotkey changed a locked app");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppLockedUntilClosedStaysLockedWhenItMovesToAnotherDevice()
	{
		var other = Audio.OtherOutputDevice();
		if(other == null)
			Assert.Inconclusive("No second output device that no app plays on: an app can't be moved");
		if(!Routes.Available)
			Assert.Inconclusive("Windows doesn't offer per-app output devices");
		var testApp = Later(TestAudioApp.Start($"VolumeyUiLockedMoved{Guid.NewGuid().ToString("N")[..6]}", routing: true));
		Later(Routes.ResetLater(testApp));
		StartMixer();
		string app = testApp.DisplayName;
		SetAppVolume(app, 40);
		Assert.AreEqual(40, Audio.WaitFor(() => testApp.Volume, 40));
		PickLock(app, UntilClosed);

		var box = Retry.WhileNull(() => Row(app).FindFirstDescendant(cf => cf.ByAutomationId("AppOutputDeviceBox")), TimeSpan.FromSeconds(5)).Result
		          ?? throw new AssertFailedException("The opened app has no device box");
		box.Patterns.ExpandCollapse.Pattern.Expand();
		var item = Retry.WhileNull(() => box.FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(other.Value.Name))),
			           TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException($"{other.Value.Name} isn't in the device box");
		item.Patterns.SelectionItem.Pattern.Select();
		box.Patterns.ExpandCollapse.Pattern.Collapse();
		Assert.IsTrue(Eventually(() => Audio.PlaysOn(testApp.Process.Id) == other.Value.Name, 5000), "the app didn't move");

		Audio.SetSessionVolumeOn(other.Value.Id, testApp.Process.Id, 80);

		Assert.AreEqual(40, Audio.WaitFor(() => Audio.SessionVolumeOn(other.Value.Id, testApp.Process.Id), 40, 5000),
			"a change on the new device wasn't put back");
		var picker = App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox).And(cf.ByName("Output device")))
		             ?? throw new AssertFailedException("The mixer has no device picker");
		picker.Patterns.ExpandCollapse.Pattern.Expand();
		var device = Retry.WhileNull(() => picker.FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(other.Value.Name))),
			             TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException($"{other.Value.Name} isn't in the device picker");
		device.Patterns.SelectionItem.Pattern.Select();
		picker.Patterns.ExpandCollapse.Pattern.Collapse();
		Assert.IsTrue(Eventually(() => LockMode(LockBar(app)) == UntilClosed, 5000), "the moved app's row doesn't show its lock");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void TheSystemSoundsHaveNoLock()
	{
		StartMixer();
		const string systemSounds = "System Sounds";
		if(App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem).And(cf.ByName(systemSounds))) == null)
			Assert.Inconclusive("No System Sounds row on the default device");

		var toggle = ExpandButton(systemSounds).Patterns.Toggle.Pattern;
		toggle.Toggle();

		Thread.Sleep(300);
		Assert.IsNull(FindLockBar(systemSounds), "the System Sounds show a lock bar");
		if(toggle.ToggleState.Value == ToggleState.On)
			toggle.Toggle();
	}
}
