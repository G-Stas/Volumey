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

	private AutomationElement? FindRow(string app)
		=> App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem).And(cf.ByName(app)));

	private AutomationElement Row(string app)
		=> Retry.WhileNull(() => FindRow(app), TimeSpan.FromSeconds(10)).Result ?? throw new AssertFailedException($"No row for {app} in the mixer");

	/// <summary>Picks an item of a drop-down (the mixer's device picker, an app's device box) by its name</summary>
	private static void PickFrom(AutomationElement comboBox, string item)
	{
		comboBox.Patterns.ExpandCollapse.Pattern.Expand();
		var found = Retry.WhileNull(() => comboBox.FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(item))),
			            TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException($"{item} isn't in the list");
		found.Patterns.SelectionItem.Pattern.Select();
		comboBox.Patterns.ExpandCollapse.Pattern.Collapse();
	}

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
		Assert.IsTrue(Eventually(() => FindRow(app) == null, 10000),
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
		Assert.IsTrue(Eventually(() => FindRow(app) == null, 10000),
			"the row stayed after the app exited");
		var again = Later(TestAudioApp.Start(exe, volume: TestAudioApp.StartVolume.FullNotWaited));

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
		PickFrom(box, other.Value.Name);
		Assert.IsTrue(Eventually(() => Audio.PlaysOn(testApp.Process.Id) == other.Value.Name, 5000), "the app didn't move");

		Audio.SetSessionVolumeOn(other.Value.Id, testApp.Process.Id, 80);

		Assert.AreEqual(40, Audio.WaitFor(() => Audio.SessionVolumeOn(other.Value.Id, testApp.Process.Id), 40, 5000),
			"a change on the new device wasn't put back");
		var picker = App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox).And(cf.ByName("Output device")))
		             ?? throw new AssertFailedException("The mixer has no device picker");
		PickFrom(picker, other.Value.Name);
		Assert.IsTrue(Eventually(() => LockMode(LockBar(app)) == UntilClosed, 5000), "the moved app's row doesn't show its lock");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void TheSystemSoundsHaveNoLock()
	{
		StartMixer();
		//named in the Windows display language, which the app saves once it has read the System Sounds' session
		string? SavedName() => App.ReadSettings()["General"]?["SystemSoundsName"]?.GetValue<string>();
		if(!Eventually(() => SavedName() is { } name && FindRow(name) != null, 5000))
			Assert.Inconclusive("No System Sounds row on the default device");
		string systemSounds = SavedName() ?? throw new AssertFailedException("The System Sounds' name is gone from the settings");

		var toggle = ExpandButton(systemSounds).Patterns.Toggle.Pattern;
		toggle.Toggle();

		Thread.Sleep(300);
		Assert.IsNull(FindLockBar(systemSounds), "the System Sounds show a lock bar");
		if(toggle.ToggleState.Value == ToggleState.On)
			toggle.Toggle();
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void EverySessionOfALockedAppIsHeldAndANewOneStartsAtTheLockedVolume()
	{
		//processes of one exe: Volumey groups their sessions into one app (by the exe's path)
		const string exe = "VolumeyUiLockedSessions";
		var first = Later(TestAudioApp.Start(exe));
		var second = Later(TestAudioApp.Start(exe));
		StartMixer();
		string app = first.DisplayName;
		SetAppVolume(app, 40);
		Assert.IsTrue(Eventually(() => first.Volume == 40 && second.Volume == 40), $"the slider didn't set both sessions: {first.Volume}, {second.Volume}");
		PickLock(app, UntilClosed);

		Audio.SetSessionVolume(second.Process.Id, 80);

		Assert.AreEqual(40, Audio.WaitFor(() => second.Volume, 40, 5000), "a change to the second session wasn't put back");
		Assert.AreEqual(40, first.Volume, "the first session moved");

		//it sets itself to 100% when it starts
		var third = Later(TestAudioApp.Start(exe, volume: TestAudioApp.StartVolume.FullNotWaited));

		Assert.AreEqual(40, Audio.WaitFor(() => third.Volume, 40, 5000), "a session added to the locked app isn't at the locked volume");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AlwaysHoldsTheAppAgainAfterVolumeyRestarts()
	{
		var testApp = Later(TestAudioApp.Start("VolumeyUiLockedRestart"));
		StartMixer();
		string app = testApp.DisplayName;
		SetAppVolume(app, 40);
		Assert.AreEqual(40, Audio.WaitFor(() => testApp.Volume, 40));
		PickLock(app, Always);
		App.WaitForSettings(s => SavedLock(s, app) != null, "the lock saved");

		App.Restart();

		Assert.AreEqual(Always, LockMode(LockBar(app)), "the lock didn't come back after the restart");
		Audio.SetSessionVolume(testApp.Process.Id, 80);
		Assert.AreEqual(40, Audio.WaitFor(() => testApp.Volume, 40, 5000), "a change from outside wasn't put back after the restart");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void UntilClosedEndsWhenVolumeyRestarts()
	{
		var testApp = Later(TestAudioApp.Start("VolumeyUiLockedUntilRestart"));
		StartMixer();
		string app = testApp.DisplayName;
		SetAppVolume(app, 40);
		Assert.AreEqual(40, Audio.WaitFor(() => testApp.Volume, 40));
		PickLock(app, UntilClosed);

		App.Restart();

		Assert.AreEqual(Off, LockMode(LockBar(app)), "the app is still locked after the restart");
		Assert.IsTrue(Slider(app).IsEnabled, "the slider is still greyed after the restart");
		Audio.SetSessionVolume(testApp.Process.Id, 80);
		Assert.AreEqual(80, Audio.WaitFor(() => testApp.Volume, 80, 2000), "a change from outside was put back after the restart");
	}
}
