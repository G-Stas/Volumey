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
using Xunit;

namespace Volumey.Tests
{
	public class AudioProcessModelTests
	{
		private AudioProcessModel proc;
		private Mock<IAudioSessionStateNotifications> trackedSessionStateNotif;
		private Mock<IAudioSessionVolume> sessionVolumeMock;

		public AudioProcessModelTests()
		{
			this.sessionVolumeMock = new Mock<IAudioSessionVolume>();
			this.trackedSessionStateNotif = new Mock<IAudioSessionStateNotifications>();

			this.proc = GetProcessMock("app", 50, false);
			
			var model = new AudioSessionModel(false, 50, "0", proc.ProcessId, default, default, Guid.Empty, sessionVolumeMock.Object,
				trackedSessionStateNotif.Object);
			
			proc.AddSession(model);
		}
		
		[Fact]
		public void TrackedSessionMustChangeToAnotherSession()
		{
			//arrange
			var trackedSession = this.proc.Sessions[0];
			Assert.Single(proc.Sessions);

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

		[Fact]
		public void VolumeMustChangeToCorrectValue()
		{
			var newVolume = this.proc.Volume + 1;

			this.trackedSessionStateNotif.Raise(m => m.VolumeChanged += null,
				new object[] { new VolumeChangedEventArgs(newVolume, this.proc.IsMuted) });

			Assert.Equal(newVolume, this.proc.Volume);
		}

		[Fact]
		public void MuteStateMustChanged()
		{
			var newMuteState = !this.proc.IsMuted;

			this.trackedSessionStateNotif.Raise(m => m.VolumeChanged += null,
				new object[] { new VolumeChangedEventArgs(this.proc.Volume, newMuteState) });

			Assert.Equal(newMuteState, this.proc.IsMuted);
		}

		[Fact]
		public void HotkeyMustIncrementVolume()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			HotkeysControl.SetHotkeyManager(hManagerMock.Object);
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.B);
			this.proc.SetVolumeHotkeys(upHotkey, downHotkey);
			var newVolume = this.proc.Volume + HotkeysControl.VolumeStep;
			
			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, upHotkey);
			
			//assert
			//verify the call to external API is made
			this.sessionVolumeMock.Verify(m => m.SetVolume(newVolume, ref GuidValue.Internal.VolumeGUID), Times.AtLeastOnce);
			//simualate callback from external API
			this.trackedSessionStateNotif.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, this.proc.IsMuted));
			Assert.Equal(newVolume, this.proc.Volume);
		}
		
		[Fact]
		public void RandomHotkeyMustNotChangeVolume()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			HotkeysControl.SetHotkeyManager(hManagerMock.Object);
			var upHotkey = new HotKey(Key.A);
			var downHotkey = new HotKey(Key.B);
			this.proc.SetVolumeHotkeys(upHotkey, downHotkey);
			
			//act
			hManagerMock.Raise(m => m.HotkeyPressed += null, new HotKey(Key.C));
			
			//assert
			//verify increment/decrement volume calls to external API are not made
			this.sessionVolumeMock.Verify(m => m.SetVolume(this.proc.Volume + HotkeysControl.VolumeStep, ref GuidValue.Internal.Empty), Times.Never);
			this.sessionVolumeMock.Verify(m => m.SetVolume(this.proc.Volume - HotkeysControl.VolumeStep, ref GuidValue.Internal.Empty), Times.Never);
		}

		[Fact]
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
			Assert.Same(session, model);
		}

		[Fact]
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
			Assert.Equal(proc.Volume, tracked.Volume);
			Assert.Equal(proc.IsMuted, tracked.IsMuted);
		}

		[Fact]
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
			Assert.Equal(proc.Volume, additionalSession.Volume);
			Assert.Equal(proc.Volume, additionalSession1.Volume);
			Assert.Equal(proc.IsMuted, additionalSession.IsMuted);
			Assert.Equal(proc.IsMuted, additionalSession1.IsMuted);
		}

		[Fact]
		public void AdditionalSessionsMustHaveSameStateAsProcess()
		{
			//arrange
			var additionalSession = GetSessionMock(12, true, proc.ProcessId.ToString());
			var additionalSession1 = GetSessionMock(25, true, proc.ProcessId.ToString());
			
			//verify tracked session is set before adding more sessions
			Assert.Single(proc.Sessions);
			
			//act 
			proc.AddSession(additionalSession);
			proc.AddSession(additionalSession1);
			
			//assert
			Assert.Equal(proc.Volume, additionalSession.Volume);
			Assert.Equal(proc.Volume, additionalSession1.Volume);
			Assert.Equal(proc.IsMuted, additionalSession.IsMuted);
			Assert.Equal(proc.IsMuted, additionalSession1.IsMuted);
		}

		[Fact]
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
			Assert.Equal(maxVolume, proc.Volume);
		}
		
		[Fact]
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
			Assert.Equal(minVolume, proc.Volume);
		}
		
		[Fact]
		public void VolumeHotkeysMustReset()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			HotkeysControl.SetHotkeyManager(hManagerMock.Object);
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
			
			Assert.Null(actualVolumeUpValue);
			Assert.Null(actualVolumeDownValue);
		}
		
		[Fact]
		public void SetVolumeHotkeysReturnsFalse_IfHotkeyManagerFails()
		{
			//arrange
			var hManagerMock = new Mock<IHotkeyManager>();
			HotKey up = new HotKey(Key.V);
			HotKey down = new HotKey(Key.B);
			hManagerMock.Setup(hm => hm.RegisterHotkey(up)).Throws(new COMException());
			hManagerMock.Setup(hm => hm.RegisterHotkey(down)).Throws(new COMException());
			HotkeysControl.SetHotkeyManager(hManagerMock.Object);
			
			//act
			var result = this.proc.SetVolumeHotkeys(up, down);
			
			//assert
			Assert.False(result);
		}
		
		[Fact]
		public void StateMediatorMustReset()
		{
			SettingsProvider.NotificationsSettings.ReactToAllVolumeChanges = false;
			var stateMediatorMock = new Mock<IAudioProcessStateMediator>();
			this.proc.SetStateMediator(stateMediatorMock.Object);
			
			Assert.Equal(stateMediatorMock.Object, this.proc.StateNotificationMediator);
			
			this.proc.ResetStateMediator();
			
			Assert.Null(this.proc.StateNotificationMediator);
		}
		
		[Fact]
		public void StateMediatorMustNotReset_IfReactingToAnyVolumeChangesEnabled()
		{
			SettingsProvider.NotificationsSettings.ReactToAllVolumeChanges = true;
			var stateMediatorMock = new Mock<IAudioProcessStateMediator>();
			this.proc.SetStateMediator(stateMediatorMock.Object);
			
			Assert.Equal(stateMediatorMock.Object, this.proc.StateNotificationMediator);
			
			this.proc.ResetStateMediator();
			
			Assert.Equal(stateMediatorMock.Object, this.proc.StateNotificationMediator);
		}
		
		[Fact]
		public void MuteCommandInvertsMuteState()
		{
			//arrange
			var muteState = this.proc.IsMuted;
			
			//act
			this.proc.MuteCommand.Execute(null);
			
			//assert
			Assert.Equal(!muteState, this.proc.IsMuted);
		}

		internal static AudioSessionModel GetSessionMock(int volume, bool muteState, string id)
		{
			var sessionVolumeMock = new Mock<IAudioSessionVolume>();
			var sessionStateNotifications = new Mock<IAudioSessionStateNotifications>();
			return new AudioSessionModel(muteState, volume, id, default, default, default, Guid.Empty, sessionVolumeMock.Object, sessionStateNotifications.Object);
		}

		internal static AudioProcessModel GetProcessMock(string name, int volume, bool muteState)
		{
			return new AudioProcessModel(volume, muteState, name, default, default, default, null, null);
		}
	}
}