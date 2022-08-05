using System;
using System.Reflection;
using System.Windows.Input;
using Moq;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Helper;
using Volumey.Model;
using Volumey.ViewModel.Settings;
using Xunit;

namespace Volumey.Tests
{
	public class AudioProcessModelTests
	{
		private AudioProcessModel proc;
		private Mock<IAudioSessionStateNotifications> sessionStateNotif;
		private Mock<IAudioSessionVolume> sessionVolumeMock;

		public AudioProcessModelTests()
		{
			this.sessionVolumeMock = new Mock<IAudioSessionVolume>();
			this.sessionStateNotif = new Mock<IAudioSessionStateNotifications>();

			this.proc = GetProcessMock("app", 50, false);
			
			var model = new AudioSessionModel(false, 50, "0", proc.ProcessId, default, default, sessionVolumeMock.Object,
				sessionStateNotif.Object);
			
			proc.AddSession(model);
		}

		[Fact]
		public void VolumeShouldBeChangedToCorrectValue()
		{
			var newVolume = this.proc.Volume + 1;

			this.sessionStateNotif.Raise(m => m.VolumeChanged += null,
				new object[] { new VolumeChangedEventArgs(newVolume, this.proc.IsMuted) });

			Assert.Equal(newVolume, this.proc.Volume);
		}

		[Fact]
		public void MuteStateShouldBeChanged()
		{
			var newMuteState = !this.proc.IsMuted;

			this.sessionStateNotif.Raise(m => m.VolumeChanged += null,
				new object[] { new VolumeChangedEventArgs(this.proc.Volume, newMuteState) });

			Assert.Equal(newMuteState, this.proc.IsMuted);
		}

		[Fact]
		public void HotkeyShouldIncrementVolume()
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
			this.sessionStateNotif.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, this.proc.IsMuted));
			Assert.Equal(newVolume, this.proc.Volume);
		}
		
		[Fact]
		public void RandomHotkeyShouldNotChangeVolume()
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
		public void FirstAddedSessionShouldSetAsTracked()
		{
			//arrange
			var model = new AudioSessionModel(false, 50, "0", proc.ProcessId, default, default, sessionVolumeMock.Object,
			                                  sessionStateNotif.Object);
			proc.Sessions.Clear();
			FieldInfo field = typeof(AudioProcessModel).GetField("_trackedSession", BindingFlags.NonPublic | BindingFlags.Instance);
			
			//act
			proc.AddSession(model);
			
			//assert
			AudioSessionModel session = field.GetValue(proc) as AudioSessionModel;
			Assert.Same(session, model);
		}

		[Fact]
		public void TrackedSessionStateChangesShouldReflectOnProcess()
		{
			//arrange
			var tracked = new AudioSessionModel(false, 50, "0", proc.ProcessId, default, default, sessionVolumeMock.Object,
			                                  sessionStateNotif.Object);
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
		public void ProcessStateChangesShouldReflectOnItsSessions()
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
		public void AdditionalSessionsShouldHaveSameStateAsProcess()
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

		internal static AudioSessionModel GetSessionMock(int volume, bool muteState, string id)
		{
			var sessionVolumeMock = new Mock<IAudioSessionVolume>();
			var sessionStateNotifications = new Mock<IAudioSessionStateNotifications>();
			return new AudioSessionModel(muteState, volume, id, default, default, default, sessionVolumeMock.Object, sessionStateNotifications.Object);
		}

		internal static AudioProcessModel GetProcessMock(string name, int volume, bool muteState)
		{
			return new AudioProcessModel(volume, muteState, name, default, default, default, null, null);
		}
	}
}