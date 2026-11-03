using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Model;

namespace Volumey.Tests;

[TestClass]
public class AppVolumeLockTests
{
	private Mock<IAudioSessionVolume> sessionVolume;
	private Mock<IAudioSessionStateNotifications> sessionNotifications;
	private AudioProcessModel app;

	[TestInitialize]
	public void Initialize()
	{
		sessionVolume = new Mock<IAudioSessionVolume>();
		sessionNotifications = new Mock<IAudioSessionStateNotifications>();
		app = AudioProcessModelTests.GetProcessMock("app", 50, false);
		//the changes are put back at once, so the tests don't wait for the thread pool
		app.Lock.RunWork = work => work();
		app.AddSession(new AudioSessionModel(false, 50, "0", app.ProcessId, default, default, Guid.Empty, sessionVolume.Object,
			sessionNotifications.Object));
	}

	private static void Hold(AudioProcessModel model, int volume, VolumeLockMode mode = VolumeLockMode.UntilClosed, int volumeBefore = 50)
		=> model.Lock.Hold(new VolumeLock(mode, volume, volumeBefore));

	private void ChangeOutsideVolumey(int volume)
		=> sessionNotifications.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(volume, false));

	[TestMethod]
	public void LockingSetsTheSessionsToTheLockedVolume()
	{
		Hold(app, 30);

		Assert.IsTrue(app.Lock.IsOn);
		Assert.AreEqual(30, app.Volume);
		Assert.AreEqual(30, app.Sessions[0].Volume);
		sessionVolume.Verify(v => v.SetVolume(30, It.IsAny<Guid>()), Times.Once);
	}

	[TestMethod]
	public void ALockedVolumeCantBeSet()
	{
		Hold(app, 30);

		app.Volume = 80;
		app.SetVolume(90, notify: false, Guid.Empty);

		Assert.AreEqual(30, app.Volume);
		sessionVolume.Verify(v => v.SetVolume(80, It.IsAny<Guid>()), Times.Never);
		sessionVolume.Verify(v => v.SetVolume(90, It.IsAny<Guid>()), Times.Never);
	}

	[TestMethod]
	public void AChangeMadeOutsideVolumeyIsPutBack()
	{
		Hold(app, 30);

		ChangeOutsideVolumey(100);

		Assert.AreEqual(30, app.Volume);
		Assert.AreEqual(30, app.Sessions[0].Volume);
		//the lock itself, then the change put back
		sessionVolume.Verify(v => v.SetVolume(30, It.IsAny<Guid>()), Times.Exactly(2));
	}

	[TestMethod]
	public void EveryChangeIsPutBackNoMatterHowLateItComes()
	{
		Hold(app, 30);

		foreach(int volume in new[] { 100, 10, 77 })
		{
			ChangeOutsideVolumey(volume);
			Assert.AreEqual(30, app.Sessions[0].Volume, $"after a change to {volume}");
		}
	}

	[TestMethod]
	public void ChangesThatArriveBeforeThePassRunsAreFixedByOnePass()
	{
		var queued = new List<Action>();
		app.Lock.RunWork = queued.Add;
		Hold(app, 30);
		Assert.IsEmpty(queued, "the lock's own writes don't start a pass");

		ChangeOutsideVolumey(100);
		ChangeOutsideVolumey(90);

		//the app's volume doesn't follow the session meanwhile
		Assert.AreEqual(30, app.Volume);
		Assert.HasCount(1, queued);

		queued[0]();
		Assert.AreEqual(30, app.Sessions[0].Volume);

		//a change after the pass starts another one
		ChangeOutsideVolumey(60);
		Assert.HasCount(2, queued);
	}

	[TestMethod]
	public void TheLockedVolumeNeedsNoWork()
	{
		var queued = new List<Action>();
		app.Lock.RunWork = queued.Add;
		Hold(app, 30);

		ChangeOutsideVolumey(30);

		Assert.IsEmpty(queued);
	}

	[TestMethod]
	public void ASessionAddedToALockedAppGetsTheVolumeAndIsHeld()
	{
		var secondVolume = new Mock<IAudioSessionVolume>();
		var secondNotifications = new Mock<IAudioSessionStateNotifications>();
		Hold(app, 30);

		app.AddSession(new AudioSessionModel(false, 100, "1", app.ProcessId, default, default, Guid.Empty, secondVolume.Object,
			secondNotifications.Object));

		Assert.AreEqual(30, app.Sessions[1].Volume);
		secondNotifications.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(100, false));
		Assert.AreEqual(30, app.Sessions[1].Volume);
		secondVolume.Verify(v => v.SetVolume(30, It.IsAny<Guid>()), Times.Exactly(2));
	}

	[TestMethod]
	public void TheFirstSessionOfALockedAppIsSetToTheVolumeToo()
	{
		var locked = AudioProcessModelTests.GetProcessMock("locked", 50, false);
		Hold(locked, 20);
		var volume = new Mock<IAudioSessionVolume>();

		locked.AddSession(new AudioSessionModel(false, 100, "0", locked.ProcessId, default, default, Guid.Empty, volume.Object,
			new Mock<IAudioSessionStateNotifications>().Object));

		Assert.AreEqual(20, locked.Volume);
		Assert.AreEqual(20, locked.Sessions[0].Volume);
		volume.Verify(v => v.SetVolume(20, It.IsAny<Guid>()), Times.Once);
	}

	[TestMethod]
	public void ReleasingFreesTheVolume()
	{
		Hold(app, 30);
		app.Lock.Release();

		Assert.IsFalse(app.Lock.IsOn);
		Assert.AreEqual(30, app.Volume, "without restoring, the app keeps the volume it was held at");
		app.Volume = 80;
		Assert.AreEqual(80, app.Volume);

		//no longer held
		sessionVolume.Invocations.Clear();
		ChangeOutsideVolumey(10);
		sessionVolume.Verify(v => v.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
		Assert.AreEqual(10, app.Sessions[0].Volume);
	}

	[TestMethod]
	public void RestoringGivesBackTheVolumeBeforeTheLock()
	{
		Hold(app, 30, volumeBefore: 65);

		app.Lock.Release(restore: true);

		Assert.IsFalse(app.Lock.IsOn);
		Assert.AreEqual(65, app.Volume);
		Assert.AreEqual(65, app.Sessions[0].Volume);
	}

	[TestMethod]
	public void RestoringAnUnlockedAppDoesNothing()
	{
		app.Volume = 70;
		sessionVolume.Invocations.Clear();

		app.Lock.Release(restore: true);

		Assert.AreEqual(70, app.Volume);
		sessionVolume.Verify(v => v.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
	}

	[TestMethod]
	public void ALockedVolumeIsClamped()
	{
		Hold(app, 250);
		Assert.AreEqual(100, app.Volume);

		Hold(app, -5);
		Assert.AreEqual(0, app.Volume);
	}

	[TestMethod]
	public void TheLockStateIsReported()
	{
		var changed = new List<string>();
		var volumeChanged = 0;
		app.Lock.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
		app.PropertyChanged += (_, e) => volumeChanged += e.PropertyName == nameof(AudioProcessModel.Volume) ? 1 : 0;

		Hold(app, 30);
		app.Lock.Release();

		Assert.AreEqual(2, changed.FindAll(name => name == nameof(AppVolumeLock.IsOn)).Count);
		Assert.AreEqual(1, volumeChanged);
	}

	[TestMethod]
	public void TheModeFollowsTheLock()
	{
		Assert.AreEqual(VolumeLockMode.Off, app.Lock.Mode);

		Hold(app, 30);
		Assert.AreEqual(VolumeLockMode.UntilClosed, app.Lock.Mode);

		Hold(app, 30, VolumeLockMode.Always);
		Assert.AreEqual(VolumeLockMode.Always, app.Lock.Mode);
		Assert.IsTrue(app.Lock.IsOn);

		app.Lock.Release();
		Assert.AreEqual(VolumeLockMode.Off, app.Lock.Mode);
	}

	[TestMethod]
	public void TheModeChangeIsReported()
	{
		var changed = new List<string>();
		app.Lock.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		Hold(app, 30);
		Hold(app, 30, VolumeLockMode.Always);
		app.Lock.Release();

		Assert.AreEqual(3, changed.FindAll(name => name == nameof(AppVolumeLock.Mode)).Count);
	}

	[TestMethod]
	public void PickingAModeAsksTheOwnerOfTheLocks()
	{
		AudioProcessModel requested = null;
		VolumeLockMode requestedMode = VolumeLockMode.Off;
		app.Lock.Requested += (model, mode) => (requested, requestedMode) = (model, mode);

		app.Lock.Mode = VolumeLockMode.Always;

		Assert.AreSame(app, requested);
		Assert.AreEqual(VolumeLockMode.Always, requestedMode);
		Assert.IsFalse(app.Lock.IsOn, "the lock doesn't hold itself: the owner keeps the list");
		Assert.AreEqual(VolumeLockMode.Off, app.Lock.Mode);
	}

	[TestMethod]
	public void ADisposedAppWritesNothing()
	{
		var queued = new List<Action>();
		app.Lock.RunWork = queued.Add;
		Hold(app, 30);
		ChangeOutsideVolumey(100);
		app.Dispose();
		sessionVolume.Invocations.Clear();

		queued[0]();

		sessionVolume.Verify(v => v.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
	}
}
