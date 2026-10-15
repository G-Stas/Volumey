using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The mixer's own controls: app rows (slider, mute button) on a test app that plays silence, and the device's slider and mute button
/// </summary>
[TestClass]
public class MixerTests : UiTest
{
	private AutomationElement? FindSlider(string name)
		=> App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Slider).And(cf.ByName(name)));

	private AutomationElement AppSlider(string name)
		=> Retry.WhileNull(() => FindSlider(name), TimeSpan.FromSeconds(10)).Result
		   ?? throw new AssertFailedException($"No row for {name} in the mixer");

	private AutomationElement MuteButton(string name, bool muted)
	{
		string buttonName = (muted ? "Unmute " : "Mute ") + name;
		return Retry.WhileNull(() => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(buttonName))),
			       TimeSpan.FromSeconds(5)).Result
		       ?? throw new AssertFailedException($"No \"{buttonName}\" button in the mixer");
	}

	private static int SliderValue(AutomationElement slider) => (int)Math.Round(slider.Patterns.RangeValue.Pattern.Value.Value);

	private static void SetSlider(AutomationElement slider, int value) => slider.Patterns.RangeValue.Pattern.SetValue(value);

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppWithTwoSessionsExpandsAndEachSessionHasItsOwnVolume()
	{
		//two processes of one exe: Volumey groups their sessions into one app (by the exe's path)
		using var first = Later(TestAudioApp.Start("VolumeyUiTwoSessions"));
		using var second = Later(TestAudioApp.Start("VolumeyUiTwoSessions"));
		StartMixer();
		string app = first.DisplayName;
		var appSlider = AppSlider(app);
		Assert.AreEqual(1, App.Mixer.FindAllDescendants(cf => cf.ByControlType(ControlType.Slider).And(cf.ByName(app))).Length, "the two sessions got two rows");

		var expand = App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(app)))
		             ?? throw new AssertFailedException("No expand button for the app");
		expand.Patterns.Toggle.Pattern.Toggle();
		//the sessions are named like the app (the exe's description): the app's slider comes first, then one per session
		AutomationElement[] Sliders() => App.Mixer.FindAllDescendants(cf => cf.ByControlType(ControlType.Slider).And(cf.ByName(app)));
		if(!Eventually(() => Sliders().Length == 3, 5000))
			throw new AssertFailedException($"Expanding didn't show the two sessions ({Sliders().Length} sliders named {app})");
		var sessions = Sliders().Skip(1).ToArray();

		SetSlider(sessions[0], 40);
		Assert.IsTrue(Eventually(() => new[] { first.Volume, second.Volume }.Count(v => v == 40) == 1), "one session's slider didn't change only its session");
		Assert.IsTrue(new[] { first.Volume, second.Volume }.Contains(100), "the other session changed too");

		SetSlider(appSlider, 70);
		Assert.IsTrue(Eventually(() => first.Volume == 70 && second.Volume == 70), $"the app's slider didn't set both sessions: {first.Volume}, {second.Volume}");

		expand.Patterns.Toggle.Pattern.Toggle();
		Assert.IsTrue(Eventually(() => Sliders().Length == 1), "collapsing didn't hide the sessions");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppGetsARowWhenItStartsAndLosesItWhenItExits()
	{
		StartMixer();
		var testApp = Later(TestAudioApp.Start());

		string name = testApp.DisplayName;
		AppSlider(name);

		testApp.Dispose();
		Assert.IsTrue(Eventually(() => FindSlider(name) == null, 10000), "the row stayed after the app exited");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppsSliderChangesItsVolumeAndFollowsOutsideChanges()
	{
		using var testApp = Later(TestAudioApp.Start());
		StartMixer();
		var slider = AppSlider(testApp.DisplayName);
		Assert.AreEqual(100, SliderValue(slider), "the slider doesn't show the app's volume");

		SetSlider(slider, 35);
		Assert.AreEqual(35, Audio.WaitFor(() => testApp.Volume, 35), "the slider didn't change the app's volume");

		//like the Windows volume mixer
		Audio.SetSessionVolume(testApp.Process.Id, 60);
		Assert.IsTrue(Eventually(() => SliderValue(slider) == 60), $"the slider didn't follow an outside change: {SliderValue(slider)}");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void AnAppsMuteButtonMutesAndUnmutesIt()
	{
		using var testApp = Later(TestAudioApp.Start());
		StartMixer();
		AppSlider(testApp.DisplayName);

		MuteButton(testApp.DisplayName, muted: false).AsButton().Invoke();
		Assert.AreEqual(true, Audio.WaitFor(() => Audio.SessionMuted(testApp.Process.Id), true), "the app wasn't muted");

		//the button is named after what it does now
		MuteButton(testApp.DisplayName, muted: true).AsButton().Invoke();
		Assert.AreEqual(false, Audio.WaitFor(() => Audio.SessionMuted(testApp.Process.Id), false), "the app wasn't unmuted");
		Assert.AreEqual(100, testApp.Volume, "muting changed the app's volume");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void TheDeviceSliderAndMuteButtonChangeTheDevice()
	{
		using var deviceState = Later(Audio.KeepDeviceState());
		StartMixer();
		var slider = Retry.WhileNull(() => App.Mixer.FindFirstDescendant(cf => cf.ByAutomationId("DeviceVolumeSlider")), TimeSpan.FromSeconds(10)).Result
		             ?? throw new AssertFailedException("No device slider in the mixer");
		int volume = Audio.DeviceVolume;
		Assert.AreEqual(volume, SliderValue(slider), "the slider doesn't show the device's volume");

		int target = volume >= 50 ? volume - 10 : volume + 10;
		SetSlider(slider, target);
		Assert.AreEqual(target, Audio.WaitFor(() => Audio.DeviceVolume, target), "the slider didn't change the device's volume");

		//like the Windows volume flyout
		Audio.SetDeviceVolume(volume);
		Assert.IsTrue(Eventually(() => SliderValue(slider) == volume), $"the slider didn't follow an outside change: {SliderValue(slider)}");

		bool muted = Audio.DeviceMuted;
		var mute = App.Mixer.FindFirstDescendant(cf => cf.ByAutomationId("DeviceMuteButton")) ?? throw new AssertFailedException("No device mute button");
		mute.AsButton().Invoke();
		Assert.AreEqual(!muted, Audio.WaitFor(() => Audio.DeviceMuted, !muted), "the mute button didn't change the device's mute");
		mute.AsButton().Invoke();
		Assert.AreEqual(muted, Audio.WaitFor(() => Audio.DeviceMuted, muted), "the second press didn't change it back");
	}
}
