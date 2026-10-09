using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Moq;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;
using Volumey.ViewModel.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Volumey.Tests
{
	[TestClass]
	public class MasterSessionModelTests
	{
		private readonly HotkeyService hotkeys = new HotkeyService();
		private MasterSessionModel model;
		private Mock<IMasterVolumeNotificationHandler> mVolumeNotifMock;
		
		private OutputDeviceModel deviceOwner;
		private Mock<IDevice> iDeviceMock;
		private Mock<IDeviceStateNotificationHandler> deviceStateMock;
		private Mock<IAudioSessionVolume> sessionVolumeMock;

		public MasterSessionModelTests()
		{
			this.mVolumeNotifMock = new Mock<IMasterVolumeNotificationHandler>();
			this.sessionVolumeMock = new Mock<IAudioSessionVolume>();
			
			this.model = new MasterSessionModel("speakers", "speakers", 70, 
				false, "id", null, this.sessionVolumeMock.Object, mVolumeNotifMock.Object, this.hotkeys);

			this.deviceStateMock = new Mock<IDeviceStateNotificationHandler>();
			this.iDeviceMock = new Mock<IDevice>();
			
			this.iDeviceMock.Setup(m => m.GetId()).Returns("111");
			
			this.deviceOwner = new OutputDeviceModel(this.iDeviceMock.Object, this.deviceStateMock.Object,
				new Mock<ISessionProvider>().Object, this.model, new List<AudioProcessModel>());
		}

		[TestMethod]
		public void VolumeShouldBeChangedToCorrectValue()
		{
			var newVolume = this.model.Volume + 1;

			this.mVolumeNotifMock.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, false));
			
			Assert.AreEqual(this.model.Volume, newVolume);
		}

		[TestMethod]
		public void MuteStateMustChange()
		{
			var newMuteState = !this.model.IsMuted;

			this.mVolumeNotifMock.Raise(m => m.VolumeChanged += null,
				new VolumeChangedEventArgs(this.model.Volume, newMuteState));

			Assert.AreEqual(this.model.IsMuted, newMuteState);
		}

		[TestMethod]
		public void IconPathChangedEvent_IconSourceShouldBeChanged()
		{
			var newIcon = SystemIcons.WinLogo;

			this.iDeviceMock.Setup(m => m.GetIcon()).Returns(newIcon);
			this.deviceStateMock.Raise(m => m.IconPathChanged += null, this.deviceOwner.Id);
			
			Assert.AreEqual(this.model.Icon, newIcon);
		}

		[TestMethod]
		public void NameChangedEvent_NamePropertiesShouldBeChanged()
		{
			var newFriendlyName = "speakers";
			var newDeviceDesc = "speakers-speakers";

			this.iDeviceMock.Setup(m => m.GetFriendlyName()).Returns(newFriendlyName);
			this.iDeviceMock.Setup(m => m.GetDeviceDesc()).Returns(newDeviceDesc);

			this.deviceStateMock.Raise(m => m.NameChanged += null, this.deviceOwner.Id);

			Assert.AreEqual(this.model.DeviceFriendlyName, newFriendlyName);
			Assert.AreEqual(this.model.DeviceDesc, newDeviceDesc);
		}
		
		[TestMethod]
		public void HotkeyMustIncrementVolume()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.B);
			this.model.SetVolumeHotkeys(upHotkey, downHotkey);
			var newVolume = this.model.Volume + this.hotkeys.VolumeStep;
			
			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, upHotkey);
			
			//assert
			//verify the call to external API is made
			this.sessionVolumeMock.Verify(m => m.SetVolume(newVolume, GuidValue.Internal.Empty), Times.AtLeastOnce);
			//simualate callback from external API
			this.mVolumeNotifMock.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, this.model.IsMuted));
			Assert.AreEqual(newVolume, this.model.Volume);
		}
		
		[TestMethod]
		public void HotkeyMustDecrementVolume()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.B);
			this.model.SetVolumeHotkeys(upHotkey, downHotkey);
			var newVolume = this.model.Volume - this.hotkeys.VolumeStep;
			
			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, downHotkey);
			
			//assert
			//verify the call to external API is made
			this.sessionVolumeMock.Verify(m => m.SetVolume(newVolume, GuidValue.Internal.Empty), Times.AtLeastOnce);
			//simualate callback from external API
			this.mVolumeNotifMock.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, this.model.IsMuted));
			Assert.AreEqual(newVolume, this.model.Volume);
		}

		[TestMethod]
		public void HotkeyMustMuteVolume()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			HotKey muteKey = new HotKey(Key.F4);
			this.model.SetMuteHotkey(muteKey);
			bool muteState = this.model.IsMuted;
			
			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, muteKey);
			
			//assert
			this.sessionVolumeMock.Verify(m => m.SetMute(!muteState), Times.Once);
			this.mVolumeNotifMock.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(this.model.Volume, !muteState));
			Assert.AreEqual(!muteState, this.model.IsMuted);
		}
		
		[TestMethod]
		public void RandomHotkeyMustNotMuteVolume()
		{
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			HotKey muteKey = new HotKey(Key.F4);
			this.model.SetMuteHotkey(muteKey);
			bool muteState = this.model.IsMuted;
			
			hManagerMock.Raise(m => m.HotkeyPressed += null, new HotKey(Key.Space));
			
			this.sessionVolumeMock.Verify(m => m.SetMute(!muteState), Times.Never);
			Assert.AreEqual(muteState, this.model.IsMuted);
		}

		[TestMethod]
		public void SetMuteReturnsFalse_IfHotkeyManagerFails()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			HotKey muteKey = new HotKey(Key.F4);
			hManagerMock.Setup(hm => hm.RegisterHotkey(muteKey)).Throws(new COMException());
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			
			//act
			var result = this.model.SetMuteHotkey(muteKey);
			
			//assert
			Assert.IsFalse(result);
		}
		
		[TestMethod]
		public void SetVolumeHotkeysReturnsFalse_IfHotkeyManagerFails()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			HotKey up = new HotKey(Key.V);
			HotKey down = new HotKey(Key.B);
			hManagerMock.Setup(hm => hm.RegisterHotkey(up)).Throws(new COMException());
			hManagerMock.Setup(hm => hm.RegisterHotkey(down)).Throws(new COMException());
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			
			//act
			var result = this.model.SetVolumeHotkeys(up, down);
			
			//assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void StateMediatorMustReset()
		{
			SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges = false;
			var stateMediatorMock = new Mock<IAudioProcessStateMediator>();
			this.model.SetStateMediator(stateMediatorMock.Object);
			
			Assert.AreEqual(stateMediatorMock.Object, this.model.StateNotificationMediator);
			
			this.model.ResetStateMediator();
			
			Assert.IsNull(this.model.StateNotificationMediator);
		}
		
		[TestMethod]
		public void StateMediatorMustNotReset_IfReactingToAnyVolumeChangesEnabled()
		{
			SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges = true;
			var stateMediatorMock = new Mock<IAudioProcessStateMediator>();
			this.model.SetStateMediator(stateMediatorMock.Object);
			
			Assert.AreEqual(stateMediatorMock.Object, this.model.StateNotificationMediator);
			
			this.model.ResetStateMediator();
			
			Assert.AreEqual(stateMediatorMock.Object, this.model.StateNotificationMediator);
		}

		[TestMethod]
		public void SessionMustMuteToPreventResettingVolumeBalance_IfEnabled()
		{
			//arrange
			SettingsProvider.Settings.Hotkeys.PreventResettingVolumeBalance = true;
			this.model.Volume = 1;
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.B);
			this.model.SetVolumeHotkeys(upHotkey, downHotkey);
			
			var volume = this.model.Volume;
			var muteState = this.model.IsMuted;

			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, downHotkey);

			//assert
			this.sessionVolumeMock.Verify(m => m.SetVolume(volume - SettingsProvider.Settings.General.VolumeStep, GuidValue.Internal.VolumeGUID), Times.Never);
			this.sessionVolumeMock.Verify(m => m.SetMute(!muteState), Times.Once);

			this.mVolumeNotifMock.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(volume, !muteState));
			Assert.AreEqual(!muteState, this.model.IsMuted);
		}
		
		[TestMethod]
		public void RandomHotkeyMustNotChangeVolume()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.B);
			this.model.SetVolumeHotkeys(upHotkey, downHotkey);
			
			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, new HotKey(Key.C));
			
			//assert
			//verify increment/decrement volume calls to external API are not made
			this.sessionVolumeMock.Verify(m => m.SetVolume(this.model.Volume + this.hotkeys.VolumeStep, GuidValue.Internal.Empty), Times.Never);
			this.sessionVolumeMock.Verify(m => m.SetVolume(this.model.Volume - this.hotkeys.VolumeStep, GuidValue.Internal.Empty), Times.Never);
		}

		[TestMethod]
		public void VolumeHotkeysMustBeSet()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			HotKey upHotkey = new HotKey(Key.A);
			HotKey downHotkey = new HotKey(Key.B);

			FieldInfo volumeUpField = typeof(MasterSessionModel).GetField("volumeUp", BindingFlags.NonPublic | BindingFlags.Instance);
			FieldInfo volumeDownField = typeof(MasterSessionModel).GetField("volumeDown", BindingFlags.NonPublic | BindingFlags.Instance);
			
			//act
			this.model.SetVolumeHotkeys(upHotkey, downHotkey);
			
			//assert
			Assert.IsTrue(this.model.AnyHotkeyRegistered);
			
			//verify hotkey manager is called twice to register the required hotkeys in system
			hManagerMock.Verify(m => m.RegisterHotkey(upHotkey), Times.Once);
			hManagerMock.Verify(m => m.RegisterHotkey(downHotkey), Times.Once);

			HotKey actualVolumeUpValue = (HotKey)volumeUpField.GetValue(this.model);
			HotKey actualVolumeDownValue = (HotKey)volumeDownField.GetValue(this.model);
			
			Assert.AreEqual(upHotkey, actualVolumeUpValue);
			Assert.AreEqual(downHotkey, actualVolumeDownValue);
		}

		[TestMethod]
		public void MuteHotkeyMustBeSet()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			HotKey muteKey = new HotKey(Key.F1);

			FieldInfo muteKeyField = typeof(MasterSessionModel).GetField("muteKey", BindingFlags.NonPublic | BindingFlags.Instance);
			
			//act
			this.model.SetMuteHotkey(muteKey);
			
			//assert
			Assert.IsTrue(this.model.AnyHotkeyRegistered);
			
			//verify the hotkey manager is actually called to unregister the hotkey in system
			hManagerMock.Verify(m => m.RegisterHotkey(muteKey), Times.Once);

			HotKey actualKeyValue = (HotKey)muteKeyField.GetValue(this.model);
			Assert.AreEqual(muteKey, actualKeyValue);
		}

		[TestMethod]
		public void MuteHotkeyMustReset()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			HotKey muteKey = new HotKey(Key.F2);
			this.model.SetMuteHotkey(muteKey);
			FieldInfo muteKeyField = typeof(MasterSessionModel).GetField("muteKey", BindingFlags.NonPublic | BindingFlags.Instance);
			
			//act
			this.model.ResetMuteHotkey();
			
			//assert
			
			//verify the hotkey manager is actually called to unregister the hotkey in system
			hManagerMock.Verify(m => m.UnregisterHotkey(muteKey), Times.Once);

			HotKey actualKeyValue = (HotKey)muteKeyField.GetValue(this.model);
			
			Assert.IsNull(actualKeyValue);
		}

		[TestMethod]
		public void VolumeHotkeysMustReset()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			HotKey upHotkey = new HotKey(Key.Z);
			HotKey downHotkey = new HotKey(Key.X);

			FieldInfo volumeUpField = typeof(MasterSessionModel).GetField("volumeUp", BindingFlags.NonPublic | BindingFlags.Instance);
			FieldInfo volumeDownField = typeof(MasterSessionModel).GetField("volumeDown", BindingFlags.NonPublic | BindingFlags.Instance);
			
			this.model.SetVolumeHotkeys(upHotkey, downHotkey);
			
			//act
			this.model.ResetVolumeHotkeys();
			
			//arrange
			//verify hotkey manager is called twice to unregister the required hotkeys in system
			hManagerMock.Verify(m => m.UnregisterHotkey(upHotkey), Times.Once);
			hManagerMock.Verify(m => m.UnregisterHotkey(downHotkey), Times.Once);
			
			HotKey actualVolumeUpValue = (HotKey)volumeUpField.GetValue(this.model);
			HotKey actualVolumeDownValue = (HotKey)volumeDownField.GetValue(this.model);
			
			Assert.IsNull(actualVolumeUpValue);
			Assert.IsNull(actualVolumeDownValue);
		}

		[TestMethod]
		public void MuteCommandInvertsMuteState()
		{
			//arrange
			var muteState = this.model.IsMuted;
			
			//act
			this.model.MuteCommand.Execute(null);
			
			//assert
			Assert.AreEqual(!muteState, this.model.IsMuted);
		}

		internal static MasterSessionModel GetMasterMock(string name, int volume, bool muteState, string id, Icon icon, IHotkeyService hotkeys = null)
		{
			var sessionVolumeMock = new Mock<IAudioSessionVolume>();
			var masterVolumeHandler = new Mock<IMasterVolumeNotificationHandler>();

			return new MasterSessionModel
			(name, name, volume, muteState, id,
				icon, sessionVolumeMock.Object, masterVolumeHandler.Object, hotkeys ?? new HotkeyService());
		}
	}
}