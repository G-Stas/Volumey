using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// An app with several sessions (two processes of one exe, as two windows of a player): one row, the app slider shows the loudest
/// session and sets all of them, each session's own slider and the app's volume hotkeys change that session only. Only the test apps'
/// volumes change
/// </summary>
[TestClass]
public class AppSessionsTests : UiTest
{
	private const string ExeName = "VolumeyUiSessions";

	private AutomationElement Row(string app)
		=> Retry.WhileNull(() => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem).And(cf.ByName(app))),
			   TimeSpan.FromSeconds(10)).Result ?? throw new AssertFailedException($"No row for {app} in the mixer");

	/// <summary>The row's sliders named after the app: the app's own first, then (with the row opened) one per session</summary>
	private AutomationElement[] Sliders(string app)
		=> Row(app).FindAllDescendants(cf => cf.ByControlType(ControlType.Slider).And(cf.ByName(app)));

	private AutomationElement AppSlider(string app)
		=> Retry.WhileNull(() => Sliders(app).FirstOrDefault(), TimeSpan.FromSeconds(5)).Result
		   ?? throw new AssertFailedException($"No slider for {app}");

	private static int ValueOf(AutomationElement slider) => (int)Math.Round(slider.Patterns.RangeValue.Pattern.Value.Value);

	private void Open(string app)
	{
		var toggle = Row(app).FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(app)))?.Patterns.Toggle.Pattern
		             ?? throw new AssertFailedException($"No expand button for {app}");
		if(toggle.ToggleState.Value != ToggleState.On)
			toggle.Toggle();
	}

	/// <summary>The sessions' own volumes, as set from outside like the Windows mixer does; waits until the app slider shows the loudest</summary>
	private void SetSessions(string app, TestAudioApp first, int firstVolume, TestAudioApp second, int secondVolume)
	{
		Audio.SetSessionVolume(first.Process.Id, firstVolume);
		Audio.SetSessionVolume(second.Process.Id, secondVolume);
		int loudest = Math.Max(firstVolume, secondVolume);
		Assert.IsTrue(Eventually(() => ValueOf(AppSlider(app)) == loudest, 5000),
			$"the app slider shows {ValueOf(AppSlider(app))}, not the loudest session's {loudest}");
	}

	private static int[] Volumes(TestAudioApp first, TestAudioApp second)
		=> new[] { first.Volume ?? -1, second.Volume ?? -1 }.Order().ToArray();

	[TestMethod]
	[TestCategory("Audio")]
	public void TheAppSliderShowsTheLoudestSessionAndSetsEverySession()
	{
		var first = Later(TestAudioApp.Start(ExeName));
		var second = Later(TestAudioApp.Start(ExeName));
		StartMixer();
		string app = first.DisplayName;

		SetSessions(app, first, 30, second, 60);
		AppSlider(app).Patterns.RangeValue.Pattern.SetValue(40);

		Assert.AreEqual(40, Audio.WaitFor(() => first.Volume, 40), "the app slider didn't set the first session");
		Assert.AreEqual(40, Audio.WaitFor(() => second.Volume, 40), "the app slider didn't set the second session");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void ASessionsOwnSliderChangesThatSessionOnly()
	{
		var first = Later(TestAudioApp.Start(ExeName));
		var second = Later(TestAudioApp.Start(ExeName));
		StartMixer();
		string app = first.DisplayName;
		SetSessions(app, first, 70, second, 70);
		Open(app);
		Assert.IsTrue(Eventually(() => Sliders(app).Length == 3), "the opened row doesn't show the app slider and one per session");

		Sliders(app)[2].Patterns.RangeValue.Pattern.SetValue(20);

		Assert.IsTrue(Eventually(() => Volumes(first, second).SequenceEqual(new[] { 20, 70 })),
			$"one session should be at 20 and the other unchanged, they are at {string.Join(" and ", Volumes(first, second))}");
		Assert.AreEqual(70, ValueOf(AppSlider(app)), "the app slider should still show the loudest session");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppVolumeHotkeyStepsEverySessionOnItsOwn()
	{
		var first = Later(TestAudioApp.Start(ExeName));
		var second = Later(TestAudioApp.Start(ExeName));
		string app = first.DisplayName;
		var up = Combo.F(15);
		var down = Combo.F(16);
		StartMixer(new JsonObject
		{
			["Hotkeys"] = new JsonObject
			{
				["BlockInSystem"] = true,
				["Apps"] = new JsonArray(new JsonObject { ["ProcessName"] = app, ["VolumeUp"] = HotkeyJson(up), ["VolumeDown"] = HotkeyJson(down) })
			}
		});
		int step = App.ReadSettings()["General"]?["VolumeStep"]?.GetValue<int>() ?? 1;
		SetSessions(app, first, 60, second, 30);
		Assert.IsTrue(Eventually(() => up.IsHeldByAnotherApp()), "the app's volume up hotkey isn't registered");

		up.Press();

		Assert.IsTrue(Eventually(() => Volumes(first, second).SequenceEqual(new[] { 30 + step, 60 + step })),
			$"each session should change by {step}, they are at {string.Join(" and ", Volumes(first, second))}");

		down.Press();
		down.Press();

		Assert.IsTrue(Eventually(() => Volumes(first, second).SequenceEqual(new[] { 30 - step, 60 - step })),
			$"each session should change by {step} again, they are at {string.Join(" and ", Volumes(first, second))}");
	}
}
