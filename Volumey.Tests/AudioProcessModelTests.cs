using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
	public class AudioProcessModelTests
	{
		private readonly HotkeyService hotkeys = new HotkeyService();
		private AudioProcessModel proc;
		private Mock<IAudioSessionStateNotifications> trackedSessionStateNotif;
		private Mock<IAudioSessionVolume> sessionVolumeMock;

		public AudioProcessModelTests()
		{
			this.sessionVolumeMock = new Mock<IAudioSessionVolume>();
			this.trackedSessionStateNotif = new Mock<IAudioSessionStateNotifications>();

			this.proc = GetProcessMock("app", 50, false, this.hotkeys);
			
			var model = new AudioSessionModel(false, 50, "0", proc.ProcessId, default, default, Guid.Empty, sessionVolumeMock.Object,
				trackedSessionStateNotif.Object);
			
			proc.AddSession(model);
		}
		
		[TestMethod]
		public void TrackedSessionMustChangeToAnotherSession()
		{
			//arrange
			var trackedSession = this.proc.Sessions[0];
			Assert.ContainsSingle(proc.Sessions);

			var additionalSession = GetSessionMock(12, true, proc.ProcessId.ToString());
			proc.AddSession(additionalSession);

			MethodInfo endedSessionHandler = typeof(OutputDeviceModel).GetMethod("ProcessEndedSession",
																				 BindingFlags.NonPublic | BindingFlags.Instance);

			//act
			
			//Simulate that the handler was called from the background thread because normally it invokes by events
			Task.Run(() =>
						 {
							 var task = (Task)endedSessionHandler.Invoke(this.proc, new object[] { trackedSession });
							 task.Wait();
						 }).ContinueWith(t =>
			{
				//assert
				Assert.DoesNotContain(trackedSession, this.proc.Sessions);
				trackedSessionStateNotif.Verify(n => n.Dispose(), Times.Once);
			});
		}

		[TestMethod]
		public void VolumeMustChangeToCorrectValue()
		{
			var newVolume = this.proc.Volume + 1;

			this.trackedSessionStateNotif.Raise(m => m.VolumeChanged += null,
				new object[] { new VolumeChangedEventArgs(newVolume, this.proc.IsMuted) });

			Assert.AreEqual(newVolume, this.proc.Volume);
		}

		[TestMethod]
		public void MuteStateMustChanged()
		{
			var newMuteState = !this.proc.IsMuted;

			this.trackedSessionStateNotif.Raise(m => m.VolumeChanged += null,
				new object[] { new VolumeChangedEventArgs(this.proc.Volume, newMuteState) });

			Assert.AreEqual(newMuteState, this.proc.IsMuted);
		}

		[TestMethod]
		public void HotkeyMustIncrementVolume()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.B);
			this.proc.SetVolumeHotkeys(upHotkey, downHotkey);
			var newVolume = this.proc.Volume + this.hotkeys.VolumeStep;
			
			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, upHotkey);
			
			//assert
			//verify the call to external API is made
			this.sessionVolumeMock.Verify(m => m.SetVolume(newVolume, GuidValue.Internal.VolumeGUID), Times.AtLeastOnce);
			//simualate callback from external API
			this.trackedSessionStateNotif.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, this.proc.IsMuted));
			Assert.AreEqual(newVolume, this.proc.Volume);
		}
		
		[TestMethod]
		public void RandomHotkeyMustNotChangeVolume()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.B);
			this.proc.SetVolumeHotkeys(upHotkey, downHotkey);
			
			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, new HotKey(Key.C));
			
			//assert
			//verify increment/decrement volume calls to external API are not made
			this.sessionVolumeMock.Verify(m => m.SetVolume(this.proc.Volume + this.hotkeys.VolumeStep, GuidValue.Internal.Empty), Times.Never);
			this.sessionVolumeMock.Verify(m => m.SetVolume(this.proc.Volume - this.hotkeys.VolumeStep, GuidValue.Internal.Empty), Times.Never);
		}

		[TestMethod]
		public void FirstAddedSessionMustSetAsTracked()
		{
			//arrange
			var model = new AudioSessionModel(false, 50, "0", proc.ProcessId, default, default, Guid.Empty, sessionVolumeMock.Object,
			                                  trackedSessionStateNotif.Object);
			proc.Sessions.Clear();
			FieldInfo field = typeof(AudioProcessModel).GetField("_trackedSession", BindingFlags.NonPublic | BindingFlags.Instance);
			
			//act
			proc.AddSession(model);
			
			//assert
			AudioSessionModel session = field.GetValue(proc) as AudioSessionModel;
			Assert.AreSame(session, model);
		}

		[TestMethod]
		public void TrackedSessionStateChangesMustReflectOnProcess()
		{
			//arrange
			var tracked = new AudioSessionModel(false, 50, "0", proc.ProcessId, default, default, Guid.Empty, sessionVolumeMock.Object,
			                                  trackedSessionStateNotif.Object);
			proc.Sessions.Clear();
			proc.Volume = 0;
			proc.IsMuted = false;
			proc.AddSession(tracked);
			
			//act
			tracked.Volume = 75;
			tracked.IsMuted = true;

			//assert
			Assert.AreEqual(proc.Volume, tracked.Volume);
			Assert.AreEqual(proc.IsMuted, tracked.IsMuted);
		}

		[TestMethod]
		public void ProcessStateChangesMustReflectOnItsSessions()
		{
			//arrange
			proc.Sessions.Clear();
			var additionalSession = GetSessionMock(12, true, proc.ProcessId.ToString());
			var additionalSession1 = GetSessionMock(25, true, proc.ProcessId.ToString());
			proc.AddSession(additionalSession);
			proc.AddSession(additionalSession1);
			
			//act
			proc.Volume = 99;
			proc.IsMuted = false;

			//assert
			Assert.AreEqual(proc.Volume, additionalSession.Volume);
			Assert.AreEqual(proc.Volume, additionalSession1.Volume);
			Assert.AreEqual(proc.IsMuted, additionalSession.IsMuted);
			Assert.AreEqual(proc.IsMuted, additionalSession1.IsMuted);
		}

		[TestMethod]
		public void AdditionalSessionsMustHaveSameStateAsProcess()
		{
			//arrange
			var additionalSession = GetSessionMock(12, true, proc.ProcessId.ToString());
			var additionalSession1 = GetSessionMock(25, true, proc.ProcessId.ToString());
			
			//verify tracked session is set before adding more sessions
			Assert.ContainsSingle(proc.Sessions);
			
			//act 
			proc.AddSession(additionalSession);
			proc.AddSession(additionalSession1);
			
			//assert
			Assert.AreEqual(proc.Volume, additionalSession.Volume);
			Assert.AreEqual(proc.Volume, additionalSession1.Volume);
			Assert.AreEqual(proc.IsMuted, additionalSession.IsMuted);
			Assert.AreEqual(proc.IsMuted, additionalSession1.IsMuted);
		}

		[TestMethod]
		public void VolumeMustNotExceedMaxValue()
		{
			//arrange 
			var additionalSession = GetSessionMock(12, true, proc.ProcessId.ToString());
			proc.AddSession(additionalSession);
			int maxVolume = 100;
			proc.Volume = maxVolume;

			//act
			proc.Volume += 5;

			//assert 
			Assert.AreEqual(maxVolume, proc.Volume);
		}
		
		[TestMethod]
		public void VolumeMustNotBeLessThanMinValue()
		{
			//arrange 
			var additionalSession = GetSessionMock(12, true, proc.ProcessId.ToString());
			proc.AddSession(additionalSession);
			int minVolume = 0;
			proc.Volume = minVolume;

			//act
			proc.Volume -= 5;

			//assert 
			Assert.AreEqual(minVolume, proc.Volume);
		}
		
		[TestMethod]
		public void VolumeHotkeysMustReset()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(hManagerMock.Object);
			HotKey upHotkey = new HotKey(Key.Z);
			HotKey downHotkey = new HotKey(Key.X);

			FieldInfo volumeUpField = typeof(AudioProcessModel).GetField("_volumeUp", BindingFlags.NonPublic | BindingFlags.Instance);
			FieldInfo volumeDownField = typeof(AudioProcessModel).GetField("_volumeDown", BindingFlags.NonPublic | BindingFlags.Instance);
			
			this.proc.SetVolumeHotkeys(upHotkey, downHotkey);
			
			//act
			this.proc.ResetVolumeHotkeys();
			
			//arrange
			//verify hotkey manager is called twice to unregister the required hotkeys in system
			hManagerMock.Verify(m => m.UnregisterHotkey(upHotkey), Times.Once);
			hManagerMock.Verify(m => m.UnregisterHotkey(downHotkey), Times.Once);
			
			HotKey actualVolumeUpValue = (HotKey)volumeUpField.GetValue(this.proc);
			HotKey actualVolumeDownValue = (HotKey)volumeDownField.GetValue(this.proc);
			
			Assert.IsNull(actualVolumeUpValue);
			Assert.IsNull(actualVolumeDownValue);
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
			var result = this.proc.SetVolumeHotkeys(up, down);
			
			//assert
			Assert.IsFalse(result);
		}
		
		[TestMethod]
		public void StateMediatorMustReset()
		{
			SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges = false;
			var stateMediatorMock = new Mock<IAudioProcessStateMediator>();
			this.proc.SetStateMediator(stateMediatorMock.Object);
			
			Assert.AreEqual(stateMediatorMock.Object, this.proc.StateNotificationMediator);
			
			this.proc.ResetStateMediator();
			
			Assert.IsNull(this.proc.StateNotificationMediator);
		}
		
		[TestMethod]
		public void StateMediatorMustNotReset_IfReactingToAnyVolumeChangesEnabled()
		{
			SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges = true;
			var stateMediatorMock = new Mock<IAudioProcessStateMediator>();
			this.proc.SetStateMediator(stateMediatorMock.Object);
			
			Assert.AreEqual(stateMediatorMock.Object, this.proc.StateNotificationMediator);
			
			this.proc.ResetStateMediator();
			
			Assert.AreEqual(stateMediatorMock.Object, this.proc.StateNotificationMediator);
		}
		
		[TestMethod]
		public void MuteCommandInvertsMuteState()
		{
			//arrange
			var muteState = this.proc.IsMuted;
			
			//act
			this.proc.MuteCommand.Execute(null);
			
			//assert
			Assert.AreEqual(!muteState, this.proc.IsMuted);
		}

		internal static AudioSessionModel GetSessionMock(int volume, bool muteState, string id)
		{
			var sessionVolumeMock = new Mock<IAudioSessionVolume>();
			var sessionStateNotifications = new Mock<IAudioSessionStateNotifications>();
			return new AudioSessionModel(muteState, volume, id, default, default, default, Guid.Empty, sessionVolumeMock.Object, sessionStateNotifications.Object);
		}

		internal static AudioProcessModel GetProcessMock(string name, int volume, bool muteState, IHotkeyService hotkeys = null)
		{
			return new AudioProcessModel(volume, muteState, name, default, default, default, null, hotkeys ?? new HotkeyService());
		}
	}
}