using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The Devices page besides its hotkeys: the device list, "Set as default" (only looked at, never clicked: the tests must not switch
/// the default device), a saved device that isn't connected, keep balance, the balance sliders, exclusive mode and the format.
/// Device settings are Windows' own, so each test puts them back as they were.
/// </summary>
[TestClass]
public class DevicesPageTests : UiTest
{
	private void OpenPage(JsonObject? settings = null)
	{
		StartApp(settings);
		Settings.Open("NavDevices", "DeviceBox");
	}

	private void RequireDevice()
	{
		if(!Audio.HasOutputDevice)
			Assert.Inconclusive("No output device");
		Later(Audio.KeepDeviceState());
	}

	private bool SetAsDefaultShown() => Settings.TryFind("SetAsDefaultButton", TimeSpan.FromMilliseconds(500)) is { IsOffscreen: false };

	[TestMethod]
	[TestCategory("Audio")]
	public void TheListHasEveryDeviceAndSetAsDefaultOnlyForAnotherOne()
	{
		RequireDevice();
		string defaultId = Audio.DefaultDeviceId;
		OpenPage();
		var list = Settings.Find("DeviceBox").AsComboBox();
		list.Expand();
		var names = list.Items.Select(i => i.Name).ToList();
		var tagged = list.Items.Where(i => i.Properties.ItemStatus.ValueOrDefault == "Default").ToList();
		list.Collapse();
		Assert.AreEqual(1, tagged.Count, "not exactly one device is tagged Default");
		Assert.AreEqual(Audio.DefaultDeviceName, tagged[0].Name, "the wrong device is tagged Default");

		foreach(var device in Audio.OutputDeviceNames())
			Assert.IsTrue(names.Any(n => n.StartsWith(device)), $"{device} isn't listed: {string.Join(", ", names)}");
		Assert.IsFalse(SetAsDefaultShown(), "Set as default is shown for the default device");

		if(names.Count < 2)
			Assert.Inconclusive("Only one output device: Set as default can't be shown");
		int other = Array.FindIndex(Settings.Find("DeviceBox").AsComboBox().Items, i => !i.IsSelected);
		Settings.SelectItem("DeviceBox", other);
		Assert.IsTrue(Eventually(SetAsDefaultShown), "Set as default isn't shown for another device");
		Assert.AreEqual(defaultId, Audio.DefaultDeviceId, "selecting a device changed the default device");
	}

	[TestMethod]
	public void ASavedDeviceThatIsntConnectedIsListedAsNotConnected()
	{
		const string name = "Volumey UI test device";
		OpenPage(new JsonObject
		{
			["Hotkeys"] = new JsonObject
			{
				["DefaultDevices"] = new JsonArray(new JsonObject
				{
					["Hotkey"] = HotkeyJson(Combo.F(13)),
					["DeviceId"] = "{0.0.0.00000000}.{00000000-0000-0000-0000-000000000000}",
					["DeviceName"] = name
				})
			}
		});

		var list = Settings.Find("DeviceBox").AsComboBox();
		list.Expand();
		var item = list.Items.FirstOrDefault(i => i.Name == name);
		Assert.IsNotNull(item, "the saved device isn't listed");
		//the tag next to its name, exposed as the item's status
		Assert.AreEqual("Not connected", item.Properties.ItemStatus.ValueOrDefault, "it isn't marked as not connected");
		int index = Array.IndexOf(list.Items, item);
		list.Collapse();
		Settings.SelectItem("DeviceBox", index);

		Assert.IsFalse(SetAsDefaultShown(), "Set as default is shown for an unplugged device");
		Assert.IsNull(Settings.TryFind("AllowExclusiveToggle", TimeSpan.FromMilliseconds(500)) is { IsOffscreen: false } t ? t : null,
			"audio features are shown for an unplugged device");
		Assert.IsTrue(Settings.Find("DeviceHotkeyBox").AsTextBox().Text.EndsWith("F13"), "its saved hotkey isn't shown");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void KeepBalanceMutesInsteadOfGoingTo0()
	{
		RequireDevice();
		var up = Combo.F(14);
		var down = Combo.F(15);
		OpenPage(new JsonObject
		{
			["Hotkeys"] = new JsonObject
			{
				["DeviceVolumeUp"] = HotkeyJson(up),
				["DeviceVolumeDown"] = HotkeyJson(down),
				["PreventResettingVolumeBalance"] = true
			}
		});
		Assert.IsTrue(Eventually(() => down.IsHeldByAnotherApp()), "the device hotkeys aren't registered");
		Audio.SetDeviceVolume(1);
		Later(new Cleanup(() => App.Kill()));
		if(Audio.DeviceMuted)
			Assert.Inconclusive("The device is muted");

		//volume step 1: down from 1 would reach 0, which resets the balance in Windows
		down.Press();
		Assert.IsTrue(Audio.WaitFor(() => Audio.DeviceMuted, true), "volume down at 1 didn't mute");
		Assert.AreEqual(1, Audio.DeviceVolume, "the volume went to 0");

		up.Press();
		Assert.IsFalse(Audio.WaitFor(() => Audio.DeviceMuted, false), "volume up didn't unmute");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void TheBalanceSlidersSetTheChannels()
	{
		RequireDevice();
		if(Audio.ChannelVolumes.Length < 2)
			Assert.Inconclusive("The default device has one channel");
		Audio.SetDeviceVolume(80);
		OpenPage();

		var left = Settings.Find("BalanceLeft");
		left.Patterns.RangeValue.Pattern.SetValue(40);
		Assert.IsTrue(Eventually(() => Math.Abs(Audio.ChannelVolumes[0] - 40) <= 1), $"the left channel is at {Audio.ChannelVolumes[0]}, not 40");

		var right = Settings.Find("BalanceRight");
		right.Patterns.RangeValue.Pattern.SetValue(70);
		Assert.IsTrue(Eventually(() => Math.Abs(Audio.ChannelVolumes[1] - 70) <= 1), $"the right channel is at {Audio.ChannelVolumes[1]}, not 70");
		Assert.IsTrue(Math.Abs(Audio.ChannelVolumes[0] - 40) <= 1, "setting the right channel moved the left one");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void ExclusiveModeIsChangedThroughThePageAndSurvivesARestart()
	{
		RequireDevice();
		OpenPage();
		bool initial = Settings.IsOn("AllowExclusiveToggle");
		try
		{
			Settings.SetSwitch("AllowExclusiveToggle", !initial);
			//priority only applies while exclusive control is allowed
			Assert.AreEqual(!initial, Settings.IsEnabled("ExclusivePriorityToggle"), "exclusive priority doesn't follow the switch");

			Restart();
			Settings.Open("NavDevices", "AllowExclusiveToggle");
			Assert.AreEqual(!initial, Settings.IsOn("AllowExclusiveToggle"), "Windows didn't keep the change");
		}
		finally
		{
			if(!App.HasExited && Settings.IsOn("AllowExclusiveToggle") != initial)
				Settings.SetSwitch("AllowExclusiveToggle", initial);
		}
		Assert.AreEqual(initial, Settings.IsOn("AllowExclusiveToggle"), "the setting wasn't put back");
	}

	[TestMethod]
	[TestCategory("Audio")]
	public void TheFormatIsChangedAndPutBack()
	{
		RequireDevice();
		var before = Audio.DeviceFormat;
		OpenPage();
		var formats = Settings.TryFind("DeviceFormatBox", TimeSpan.FromSeconds(3))?.AsComboBox();
		if(formats == null || formats.IsOffscreen)
			Assert.Inconclusive("The default device offers no format choice");
		string original = Settings.SelectedName("DeviceFormatBox")!;
		formats.Expand();
		int other = Array.FindIndex(formats.Items, i => !i.IsSelected);
		formats.Collapse();
		if(other < 0)
			Assert.Inconclusive("The default device supports one format");
		try
		{
			string chosen = Settings.SelectItem("DeviceFormatBox", other);
			//Windows reconfigures the device and Volumey rebuilds its model, so the page reads the new format
			Assert.IsTrue(Eventually(() => Audio.DeviceFormat != before, 10000), $"Windows still has {before} after choosing {chosen}");
			Assert.IsTrue(Eventually(() => Settings.SelectedName("DeviceFormatBox") == chosen, 10000), "the page doesn't show the new format");
		}
		finally
		{
			Thread.Sleep(1000);
			formats = Settings.Find("DeviceFormatBox").AsComboBox();
			formats.Expand();
			int back = Array.FindIndex(formats.Items, i => i.Name == original);
			formats.Collapse();
			if(back >= 0 && Settings.SelectedName("DeviceFormatBox") != original)
				Settings.SelectItem("DeviceFormatBox", back);
		}
		Assert.IsTrue(Eventually(() => Audio.DeviceFormat == before, 10000), $"the format wasn't put back to {before}: {Audio.DeviceFormat}");
	}

	private sealed class Cleanup(Action action) : IDisposable
	{
		public void Dispose() => action();
	}
}
