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
		app.RunLockWork = work => work();
		app.AddSession(new AudioSessionModel(false, 50, "0", app.ProcessId, default, default, Guid.Empty, sessionVolume.Object,
			sessionNotifications.Object));
	}

	private void ChangeOutsideVolumey(int volume)
		=> sessionNotifications.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(volume, false));

	[TestMethod]
	public void LockingSetsTheSessionsToTheLockedVolume()
	{
		app.LockVolume(30);

		Assert.IsTrue(app.IsVolumeLocked);
		Assert.AreEqual(30, app.Volume);
		Assert.AreEqual(30, app.Sessions[0].Volume);
		sessionVolume.Verify(v => v.SetVolume(30, It.IsAny<Guid>()), Times.Once);
	}

	[TestMethod]
	public void ALockedVolumeCantBeSet()
	{
		app.LockVolume(30);

		app.Volume = 80;
		app.SetVolume(90, notify: false, Guid.Empty);

		Assert.AreEqual(30, app.Volume);
		sessionVolume.Verify(v => v.SetVolume(80, It.IsAny<Guid>()), Times.Never);
		sessionVolume.Verify(v => v.SetVolume(90, It.IsAny<Guid>()), Times.Never);
	}

	[TestMethod]
	public void AChangeMadeOutsideVolumeyIsPutBack()
	{
		app.LockVolume(30);

		ChangeOutsideVolumey(100);

		Assert.AreEqual(30, app.Volume);
		Assert.AreEqual(30, app.Sessions[0].Volume);
		//the lock itself, then the change put back
		sessionVolume.Verify(v => v.SetVolume(30, It.IsAny<Guid>()), Times.Exactly(2));
	}

	[TestMethod]
	public void EveryChangeIsPutBackNoMatterHowLateItComes()
	{
		app.LockVolume(30);

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
		app.RunLockWork = queued.Add;
		app.LockVolume(30);
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
		app.RunLockWork = queued.Add;
		app.LockVolume(30);

		ChangeOutsideVolumey(30);

		Assert.IsEmpty(queued);
	}

	[TestMethod]
	public void ASessionAddedToALockedAppGetsTheVolumeAndIsHeld()
	{
		var secondVolume = new Mock<IAudioSessionVolume>();
		var secondNotifications = new Mock<IAudioSessionStateNotifications>();
		app.LockVolume(30);

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
		locked.LockVolume(20);
		var volume = new Mock<IAudioSessionVolume>();

		locked.AddSession(new AudioSessionModel(false, 100, "0", locked.ProcessId, default, default, Guid.Empty, volume.Object,
			new Mock<IAudioSessionStateNotifications>().Object));

		Assert.AreEqual(20, locked.Volume);
		Assert.AreEqual(20, locked.Sessions[0].Volume);
		volume.Verify(v => v.SetVolume(20, It.IsAny<Guid>()), Times.Once);
	}

	[TestMethod]
	public void UnlockingFreesTheVolume()
	{
		app.LockVolume(30);
		app.UnlockVolume();

		Assert.IsFalse(app.IsVolumeLocked);
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
		app.LockVolume(30);

		app.RestoreVolumeBeforeLock();

		Assert.IsFalse(app.IsVolumeLocked);
		Assert.AreEqual(50, app.Volume);
		Assert.AreEqual(50, app.Sessions[0].Volume);
	}

	[TestMethod]
	public void LockingAgainDoesNotReplaceTheVolumeBeforeTheLock()
	{
		app.LockVolume(30);
		app.LockVolume(10);

		app.RestoreVolumeBeforeLock();

		Assert.AreEqual(50, app.Volume);
	}

	[TestMethod]
	public void RestoringAnUnlockedAppDoesNothing()
	{
		app.Volume = 70;
		sessionVolume.Invocations.Clear();

		app.RestoreVolumeBeforeLock();

		Assert.AreEqual(70, app.Volume);
		sessionVolume.Verify(v => v.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
	}

	[TestMethod]
	public void ALockedVolumeIsClamped()
	{
		app.LockVolume(250);
		Assert.AreEqual(100, app.Volume);

		app.LockVolume(-5);
		Assert.AreEqual(0, app.Volume);
	}

	[TestMethod]
	public void TheLockStateIsReported()
	{
		var changed = new List<string>();
		app.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		app.LockVolume(30);
		app.UnlockVolume();

		CollectionAssert.Contains(changed, nameof(AudioProcessModel.IsVolumeLocked));
		Assert.AreEqual(2, changed.FindAll(name => name == nameof(AudioProcessModel.IsVolumeLocked)).Count);
		CollectionAssert.Contains(changed, nameof(AudioProcessModel.Volume));
	}

	[TestMethod]
	public void TheLockModeFollowsTheLock()
	{
		Assert.AreEqual(VolumeLockMode.None, app.LockMode);

		app.LockVolume(30);
		Assert.AreEqual(VolumeLockMode.Session, app.LockMode);

		app.LockVolume(30, persistent: true);
		Assert.AreEqual(VolumeLockMode.Persistent, app.LockMode);
		Assert.IsTrue(app.IsVolumeLocked);

		app.UnlockVolume();
		Assert.AreEqual(VolumeLockMode.None, app.LockMode);
	}

	[TestMethod]
	public void TheLockModeChangeIsReported()
	{
		var changed = new List<string>();
		app.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		app.LockVolume(30);
		app.LockVolume(30, persistent: true);
		app.UnlockVolume();

		Assert.AreEqual(3, changed.FindAll(name => name == nameof(AudioProcessModel.LockMode)).Count);
	}

	[TestMethod]
	public void TheLockButtonAsksTheOwnerOfTheLocks()
	{
		AudioProcessModel requested = null;
		app.VolumeLockRequested += model => requested = model;

		app.ToggleVolumeLockCommand.Execute(null);

		Assert.AreSame(app, requested);
		Assert.IsFalse(app.IsVolumeLocked, "the model doesn't lock itself: the owner keeps the list");
	}

	[TestMethod]
	public void ADisposedAppWritesNothing()
	{
		var queued = new List<Action>();
		app.RunLockWork = queued.Add;
		app.LockVolume(30);
		ChangeOutsideVolumey(100);
		app.Dispose();
		sessionVolume.Invocations.Clear();

		queued[0]();

		sessionVolume.Verify(v => v.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
	}
}
