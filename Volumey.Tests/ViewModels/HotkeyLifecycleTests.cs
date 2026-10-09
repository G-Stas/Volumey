using System;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests.ViewModels;

[TestClass]
public class HotkeyLifecycleTests : ViewModelTestBase
{
	private static readonly HotKey Up = new HotKey(Key.PageUp, ModifierKeys.Alt);
	private static readonly HotKey Down = new HotKey(Key.PageDown, ModifierKeys.Alt);

	[TestMethod]
	public void ADisposedDeviceIgnoresItsHotkeys()
	{
		ActivateHotkeys();
		var device = CreateDevice("1", "Speakers", out Mock<IAudioSessionVolume> masterVolume);
		Assert.IsTrue(device.SetVolumeHotkeys(Up, Down));

		device.Dispose();
		PressHotkey(Up);

		masterVolume.Verify(m => m.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
		//the view models release the hotkeys after the disposal, as they do on a default device switch
		device.ResetVolumeHotkeys();
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
	}

	[TestMethod]
	public void AnOutsideVolumeChangeShowsTheNewValueAndIsNotWrittenBack()
	{
		Settings.Notifications.ReactToAllVolumeChanges = true;
		var sessionVolume = new Mock<IAudioSessionVolume>();
		var sessionEvents = new Mock<IAudioSessionStateNotifications>();
		var process = CreateProcess("Spotify", 50);
		process.AddSession(new Volumey.Model.AudioSessionModel(false, 50, "1", default, default, default, Guid.Empty, sessionVolume.Object, sessionEvents.Object));
		var shown = new System.Collections.Generic.List<int>();
		var mediator = new Mock<IAudioProcessStateMediator>();
		mediator.Setup(m => m.NotifyAudioStateChange(It.IsAny<Volumey.Model.IManagedMasterAudioSession>()))
				.Callback<Volumey.Model.IManagedMasterAudioSession>(s => shown.Add(s.Volume));
		process.SetStateMediator(mediator.Object);

		//the Windows mixer sets the app to 30
		sessionEvents.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(30, false));

		Assert.AreEqual(30, process.Volume);
		CollectionAssert.AreEqual(new[] { 30 }, shown, "the OSD should show the new volume, once");
		sessionVolume.Verify(m => m.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
		sessionVolume.Verify(m => m.SetMute(It.IsAny<bool>()), Times.Never);
	}

	[TestMethod]
	public void ASavedForegroundPairThatFailsAtStartupShowsAnError()
	{
		Settings.Hotkeys.ForegroundVolumeUp = Up;
		Settings.Hotkeys.ForegroundVolumeDown = Down;
		//another app took the combination since the last start
		HotkeyManager.Setup(m => m.RegisterHotkey(Down)).Throws(new Win32Exception(1409));
		SetDefaultDevice(CreateDevice("1", "Speakers"));
		var vm = new ForegroundWindowVolumeViewModel(DeviceProvider.Object, Hotkeys, Notifications.Object);

		ActivateHotkeys();

		Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.VolumeReg], vm.ErrorMessage);
		//the first hotkey of the pair isn't left registered
		HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
		//and the pair stays saved for the next start
		Assert.AreEqual(Up, Settings.Hotkeys.ForegroundVolumeUp);
	}
}
