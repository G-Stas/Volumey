using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests;

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
		mVolumeNotifMock = new Mock<IMasterVolumeNotificationHandler>();
		sessionVolumeMock = new Mock<IAudioSessionVolume>();

		model = new MasterSessionModel("speakers", "speakers", 70,
			false, "id", null, sessionVolumeMock.Object, mVolumeNotifMock.Object, hotkeys);

		deviceStateMock = new Mock<IDeviceStateNotificationHandler>();
		iDeviceMock = new Mock<IDevice>();

		iDeviceMock.Setup(m => m.GetId()).Returns("111");

		deviceOwner = new OutputDeviceModel(iDeviceMock.Object, deviceStateMock.Object,
			new Mock<ISessionProvider>().Object, model, new List<AudioProcessModel>());
	}

	[TestMethod]
	public void VolumeShouldBeChangedToCorrectValue()
	{
		var newVolume = model.Volume + 1;

		mVolumeNotifMock.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, false));

		Assert.AreEqual(model.Volume, newVolume);
		//Windows already has the outside change
		sessionVolumeMock.Verify(m => m.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
	}

	[TestMethod]
	public void MuteStateMustChange()
	{
		var newMuteState = !model.IsMuted;

		mVolumeNotifMock.Raise(m => m.VolumeChanged += null,
			new VolumeChangedEventArgs(model.Volume, newMuteState));

		Assert.AreEqual(model.IsMuted, newMuteState);
		sessionVolumeMock.Verify(m => m.SetMute(It.IsAny<bool>(), It.IsAny<Guid>()), Times.Never);
	}

	[TestMethod]
	public void IconPathChangedEvent_IconSourceShouldBeChanged()
	{
		var newIcon = SystemIcons.WinLogo;

		iDeviceMock.Setup(m => m.GetIcon()).Returns(newIcon);
		deviceStateMock.Raise(m => m.IconPathChanged += null, deviceOwner.Id);

		Assert.AreEqual(model.Icon, newIcon);
	}

	[TestMethod]
	public void NameChangedEvent_NamePropertiesShouldBeChanged()
	{
		var newFriendlyName = "speakers";
		var newDeviceDesc = "speakers-speakers";

		iDeviceMock.Setup(m => m.GetFriendlyName()).Returns(newFriendlyName);
		iDeviceMock.Setup(m => m.GetDeviceDesc()).Returns(newDeviceDesc);

		deviceStateMock.Raise(m => m.NameChanged += null, deviceOwner.Id);

		Assert.AreEqual(model.DeviceFriendlyName, newFriendlyName);
		Assert.AreEqual(model.DeviceDesc, newDeviceDesc);
	}

	[TestMethod]
	public void HotkeyMustIncrementVolume()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		var upHotkey = new HotKey(Key.A);
		var downHotkey = new HotKey(Key.B);
		model.SetVolumeHotkeys(upHotkey, downHotkey);
		var newVolume = model.Volume + hotkeys.VolumeStep;

		//act
		hManagerMock.Raise(m => m.HotkeyPressed += null, upHotkey);

		//assert
		//verify the call to external API is made
		sessionVolumeMock.Verify(m => m.SetVolume(newVolume, GuidValue.Internal.Empty), Times.AtLeastOnce);
		//simualate callback from external API
		mVolumeNotifMock.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, model.IsMuted));
		Assert.AreEqual(newVolume, model.Volume);
	}

	[TestMethod]
	public void HotkeyMustDecrementVolume()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		var upHotkey = new HotKey(Key.A);
		var downHotkey = new HotKey(Key.B);
		model.SetVolumeHotkeys(upHotkey, downHotkey);
		var newVolume = model.Volume - hotkeys.VolumeStep;

		//act
		hManagerMock.Raise(m => m.HotkeyPressed += null, downHotkey);

		//assert
		//verify the call to external API is made
		sessionVolumeMock.Verify(m => m.SetVolume(newVolume, GuidValue.Internal.Empty), Times.AtLeastOnce);
		//simualate callback from external API
		mVolumeNotifMock.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, model.IsMuted));
		Assert.AreEqual(newVolume, model.Volume);
	}

	[TestMethod]
	public void HotkeyMustMuteVolume()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		HotKey muteKey = new HotKey(Key.F4);
		model.SetMuteHotkey(muteKey);
		bool muteState = model.IsMuted;

		//act
		hManagerMock.Raise(m => m.HotkeyPressed += null, muteKey);

		//assert
		sessionVolumeMock.Verify(m => m.SetMute(!muteState, It.IsAny<System.Guid>()), Times.Once);
		mVolumeNotifMock.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(model.Volume, !muteState));
		Assert.AreEqual(!muteState, model.IsMuted);
	}

	[TestMethod]
	public void RandomHotkeyMustNotMuteVolume()
	{
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		HotKey muteKey = new HotKey(Key.F4);
		model.SetMuteHotkey(muteKey);
		bool muteState = model.IsMuted;

		hManagerMock.Raise(m => m.HotkeyPressed += null, new HotKey(Key.Space));

		sessionVolumeMock.Verify(m => m.SetMute(!muteState, It.IsAny<System.Guid>()), Times.Never);
		Assert.AreEqual(muteState, model.IsMuted);
	}

	[TestMethod]
	public void SetMuteReturnsFalse_IfHotkeyManagerFails()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		HotKey muteKey = new HotKey(Key.F4);
		hManagerMock.Setup(hm => hm.RegisterHotkey(muteKey)).Throws(new COMException());
		hotkeys.SetHotkeyManager(hManagerMock.Object);

		//act
		var result = model.SetMuteHotkey(muteKey);

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
		hotkeys.SetHotkeyManager(hManagerMock.Object);

		//act
		var result = model.SetVolumeHotkeys(up, down);

		//assert
		Assert.IsFalse(result);
	}

	[TestMethod]
	public void StateMediatorMustReset()
	{
		SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges = false;
		var stateMediatorMock = new Mock<IAudioProcessStateMediator>();
		model.SetStateMediator(stateMediatorMock.Object);

		Assert.AreEqual(stateMediatorMock.Object, model.StateNotificationMediator);

		model.ResetStateMediator();

		Assert.IsNull(model.StateNotificationMediator);
	}

	[TestMethod]
	public void StateMediatorMustNotReset_IfReactingToAnyVolumeChangesEnabled()
	{
		SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges = true;
		var stateMediatorMock = new Mock<IAudioProcessStateMediator>();
		model.SetStateMediator(stateMediatorMock.Object);

		Assert.AreEqual(stateMediatorMock.Object, model.StateNotificationMediator);

		model.ResetStateMediator();

		Assert.AreEqual(stateMediatorMock.Object, model.StateNotificationMediator);
	}

	[TestMethod]
	public void SessionMustMuteToPreventResettingVolumeBalance_IfEnabled()
	{
		//arrange
		SettingsProvider.Settings.Hotkeys.PreventResettingVolumeBalance = true;
		model.Volume = 1;
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		var upHotkey = new HotKey(Key.A);
		var downHotkey = new HotKey(Key.B);
		model.SetVolumeHotkeys(upHotkey, downHotkey);

		var volume = model.Volume;
		var muteState = model.IsMuted;

		//act
		hManagerMock.Raise(m => m.HotkeyPressed += null, downHotkey);

		//assert
		sessionVolumeMock.Verify(m => m.SetVolume(volume - SettingsProvider.Settings.General.VolumeStep, GuidValue.Internal.VolumeGUID), Times.Never);
		sessionVolumeMock.Verify(m => m.SetMute(!muteState, It.IsAny<System.Guid>()), Times.Once);

		mVolumeNotifMock.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(volume, !muteState));
		Assert.AreEqual(!muteState, model.IsMuted);
	}

	[TestMethod]
	public void RandomHotkeyMustNotChangeVolume()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		var upHotkey = new HotKey(Key.A);
		var downHotkey = new HotKey(Key.B);
		model.SetVolumeHotkeys(upHotkey, downHotkey);

		//act
		hManagerMock.Raise(m => m.HotkeyPressed += null, new HotKey(Key.C));

		//assert
		//verify increment/decrement volume calls to external API are not made
		sessionVolumeMock.Verify(m => m.SetVolume(model.Volume + hotkeys.VolumeStep, GuidValue.Internal.Empty), Times.Never);
		sessionVolumeMock.Verify(m => m.SetVolume(model.Volume - hotkeys.VolumeStep, GuidValue.Internal.Empty), Times.Never);
	}

	[TestMethod]
	public void VolumeHotkeysMustBeSet()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		HotKey upHotkey = new HotKey(Key.A);
		HotKey downHotkey = new HotKey(Key.B);

		FieldInfo volumeUpField = typeof(MasterSessionModel).GetField("volumeUp", BindingFlags.NonPublic | BindingFlags.Instance);
		FieldInfo volumeDownField = typeof(MasterSessionModel).GetField("volumeDown", BindingFlags.NonPublic | BindingFlags.Instance);

		//act
		model.SetVolumeHotkeys(upHotkey, downHotkey);

		//assert
		Assert.IsTrue(model.AnyHotkeyRegistered);

		//verify hotkey manager is called twice to register the required hotkeys in system
		hManagerMock.Verify(m => m.RegisterHotkey(upHotkey), Times.Once);
		hManagerMock.Verify(m => m.RegisterHotkey(downHotkey), Times.Once);

		HotKey actualVolumeUpValue = (HotKey)volumeUpField.GetValue(model);
		HotKey actualVolumeDownValue = (HotKey)volumeDownField.GetValue(model);

		Assert.AreEqual(upHotkey, actualVolumeUpValue);
		Assert.AreEqual(downHotkey, actualVolumeDownValue);
	}

	[TestMethod]
	public void MuteHotkeyMustBeSet()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		HotKey muteKey = new HotKey(Key.F1);

		FieldInfo muteKeyField = typeof(MasterSessionModel).GetField("muteKey", BindingFlags.NonPublic | BindingFlags.Instance);

		//act
		model.SetMuteHotkey(muteKey);

		//assert
		Assert.IsTrue(model.AnyHotkeyRegistered);

		//verify the hotkey manager is actually called to unregister the hotkey in system
		hManagerMock.Verify(m => m.RegisterHotkey(muteKey), Times.Once);

		HotKey actualKeyValue = (HotKey)muteKeyField.GetValue(model);
		Assert.AreEqual(muteKey, actualKeyValue);
	}

	[TestMethod]
	public void MuteHotkeyMustReset()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		HotKey muteKey = new HotKey(Key.F2);
		model.SetMuteHotkey(muteKey);
		FieldInfo muteKeyField = typeof(MasterSessionModel).GetField("muteKey", BindingFlags.NonPublic | BindingFlags.Instance);

		//act
		model.ResetMuteHotkey();

		//assert

		//verify the hotkey manager is actually called to unregister the hotkey in system
		hManagerMock.Verify(m => m.UnregisterHotkey(muteKey), Times.Once);

		HotKey actualKeyValue = (HotKey)muteKeyField.GetValue(model);

		Assert.IsNull(actualKeyValue);
	}

	[TestMethod]
	public void VolumeHotkeysMustReset()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		HotKey upHotkey = new HotKey(Key.Z);
		HotKey downHotkey = new HotKey(Key.X);

		FieldInfo volumeUpField = typeof(MasterSessionModel).GetField("volumeUp", BindingFlags.NonPublic | BindingFlags.Instance);
		FieldInfo volumeDownField = typeof(MasterSessionModel).GetField("volumeDown", BindingFlags.NonPublic | BindingFlags.Instance);

		model.SetVolumeHotkeys(upHotkey, downHotkey);

		//act
		model.ResetVolumeHotkeys();

		//arrange
		//verify hotkey manager is called twice to unregister the required hotkeys in system
		hManagerMock.Verify(m => m.UnregisterHotkey(upHotkey), Times.Once);
		hManagerMock.Verify(m => m.UnregisterHotkey(downHotkey), Times.Once);

		HotKey actualVolumeUpValue = (HotKey)volumeUpField.GetValue(model);
		HotKey actualVolumeDownValue = (HotKey)volumeDownField.GetValue(model);

		Assert.IsNull(actualVolumeUpValue);
		Assert.IsNull(actualVolumeDownValue);
	}

	[TestMethod]
	public void AnEchoMustBeIgnoredWhileWritesArePending()
	{
		//arrange
		var volumeMock = new Mock<IAudioSessionVolume>();
		var handlerMock = new Mock<IMasterVolumeNotificationHandler>();
		var master = new MasterSessionModel("dev", "desc", 50, false, "id", null, volumeMock.Object, handlerMock.Object, new HotkeyService());
		master.SetVolume(60, notify: false, GuidValue.Internal.Empty);

		//act: an old echo arrives while a write is still pending
		volumeMock.SetupGet(v => v.HasPendingWrites).Returns(true);
		handlerMock.Raise(h => h.VolumeChanged += null, new VolumeChangedEventArgs(55, false));

		//assert
		Assert.AreEqual(60, master.Volume);

		//nothing pending anymore: a change from outside is taken over again
		volumeMock.SetupGet(v => v.HasPendingWrites).Returns(false);
		handlerMock.Raise(h => h.VolumeChanged += null, new VolumeChangedEventArgs(40, false));
		Assert.AreEqual(40, master.Volume);
	}

	[TestMethod]
	public void HoldingAHotkeyWithASlowDeviceMustNotLoseSteps()
	{
		//arrange
		var blockingVolume = new BlockingVolume();
		var handlerMock = new Mock<IMasterVolumeNotificationHandler>();
		var managerMock = new Mock<IHotkeyManager>();
		var service = new HotkeyService();
		service.SetHotkeyManager(managerMock.Object);
		var upHotkey = new HotKey(Key.A);
		var downHotkey = new HotKey(Key.B);
		var master = new MasterSessionModel("dev", "desc", 50, false, "id", null, blockingVolume, handlerMock.Object, service);
		master.SetVolumeHotkeys(upHotkey, downHotkey);

		//act: press several times while the device blocks
		for(int i = 0; i < 3; i++)
			managerMock.Raise(m => m.HotkeyPressed += null, upHotkey);

		//assert
		int expected = 50 + 3 * service.VolumeStep;
		Assert.AreEqual(expected, master.Volume);

		//the echo of the first write arrives while the others are still pending
		handlerMock.Raise(h => h.VolumeChanged += null, new VolumeChangedEventArgs(50 + service.VolumeStep, false));
		Assert.AreEqual(expected, master.Volume);

		//release the device: the last write must be the final value
		blockingVolume.Release();
		blockingVolume.WaitIdle();
		Assert.AreEqual(expected, blockingVolume.Writes[^1]);
		Assert.AreEqual(expected, master.Volume);
	}

	[TestMethod]
	public void AFailedWriteMustShowTheDeviceState()
	{
		//arrange
		var volumeMock = new Mock<IAudioSessionVolume>();
		var handlerMock = new Mock<IMasterVolumeNotificationHandler>();
		var master = new MasterSessionModel("dev", "desc", 50, false, "id", null, volumeMock.Object, handlerMock.Object, new HotkeyService());
		master.SetVolume(60, notify: false, GuidValue.Internal.Empty);

		//act: the worker reports that the device kept its previous state
		volumeMock.Raise(v => v.WritesCompleted += null, new Exception("write failed"), new VolumeChangedEventArgs(50, false));

		//assert
		Assert.AreEqual(50, master.Volume);
	}

	[TestMethod]
	public void ADeviceChangeMadeWhileWritingMustBeTakenOverWhenTheWritesComplete()
	{
		//arrange
		var volumeMock = new Mock<IAudioSessionVolume>();
		var handlerMock = new Mock<IMasterVolumeNotificationHandler>();
		var master = new MasterSessionModel("dev", "desc", 50, false, "id", null, volumeMock.Object, handlerMock.Object, new HotkeyService());

		//act: every write succeeded, but the device was muted meanwhile (its notification was dropped)
		volumeMock.Raise(v => v.WritesCompleted += null, (Exception)null, new VolumeChangedEventArgs(50, true));

		//assert
		Assert.IsTrue(master.IsMuted);
	}

	[TestMethod]
	public void TheDeviceStateMustNotBeTakenOverWhileNewerWritesArePending()
	{
		//arrange
		var volumeMock = new Mock<IAudioSessionVolume>();
		var handlerMock = new Mock<IMasterVolumeNotificationHandler>();
		var master = new MasterSessionModel("dev", "desc", 50, false, "id", null, volumeMock.Object, handlerMock.Object, new HotkeyService());
		master.SetVolume(60, notify: false, GuidValue.Internal.Empty);

		//act: the writes completed with an old state, but a newer write is already queued
		volumeMock.SetupGet(v => v.HasPendingWrites).Returns(true);
		volumeMock.Raise(v => v.WritesCompleted += null, (Exception)null, new VolumeChangedEventArgs(50, true));

		//assert
		Assert.AreEqual(60, master.Volume);
		Assert.IsFalse(master.IsMuted);
	}

	[TestMethod]
	public void MuteCommandInvertsMuteState()
	{
		//arrange
		var muteState = model.IsMuted;

		//act
		model.MuteCommand.Execute(null);

		//assert
		Assert.AreEqual(!muteState, model.IsMuted);
	}

	/// <summary>
	/// A fake volume whose writes block until <see cref="Release"/> is called
	/// </summary>
	private sealed class BlockingVolume : IAudioSessionVolume
	{
		private readonly ManualResetEventSlim gate = new();
		private int pending;
		private Task chain = Task.CompletedTask;

		public List<int> Writes { get; } = new();

		public bool HasPendingWrites => Volatile.Read(ref pending) > 0;

		public float GetVolume() => 0f;

		public bool GetMute() => false;

		public void SetMute(bool newState, Guid context) { }

		public void SetVolume(int newVolume, Guid context)
		{
			Interlocked.Increment(ref pending);
			chain = chain.ContinueWith(_ =>
			{
				gate.Wait();
				lock(Writes)
					Writes.Add(newVolume);
				Interlocked.Decrement(ref pending);
			});
		}

		public void Release() => gate.Set();

		public void WaitIdle() => chain.Wait(TimeSpan.FromSeconds(5));
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
