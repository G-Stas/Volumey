using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.ViewModel.Settings;
using Volumey.Model;

namespace Volumey.Tests;

[TestClass]
public class SessionActivityTests
{
	private static AudioSessionModel Session(Mock<IAudioSessionStateNotifications> notifications)
		=> new(false, 50, "s", 100, "app", @"C:\Apps\app.exe", Guid.Empty, new Mock<IAudioSessionVolume>().Object, notifications.Object);

	private static AudioProcessModel Process()
		=> new(50, false, "app", 100, @"C:\Apps\app.exe", null, null, new HotkeyService());

	// U15
	[TestMethod]
	public void ASessionThatStartsPlayingWhileItIsReadStaysPlaying()
	{
		var notifications = new Mock<IAudioSessionStateNotifications>();
		var session = Session(notifications);

		//registered for Windows' events, then Windows reports the start before the state read returns its older answer
		notifications.Raise(n => n.Activated += null);
		session.ApplyInitialState(false);

		Assert.IsTrue(session.IsActive);
	}

	// U15
	[TestMethod]
	public void TheInitialStateIsTakenWhenNoChangeCameFirst()
	{
		var session = Session(new Mock<IAudioSessionStateNotifications>());
		session.ApplyInitialState(true);
		Assert.IsTrue(session.IsActive);
	}

	// U15
	[TestMethod]
	public void TheAppHearsOnlyRealActivityChanges()
	{
		var notifications = new Mock<IAudioSessionStateNotifications>();
		var session = Session(notifications);
		var app = Process();
		app.AddSession(session);
		int changes = 0;
		app.ActivityChanged += _ => changes++;

		notifications.Raise(n => n.Activated += null);
		notifications.Raise(n => n.Activated += null);
		Assert.AreEqual(1, changes);
		Assert.IsTrue(app.PlaysNow);

		notifications.Raise(n => n.Deactivated += null);
		notifications.Raise(n => n.Deactivated += null);
		Assert.AreEqual(2, changes);
		Assert.IsFalse(app.PlaysNow);
	}

	// U15
	[TestMethod]
	public void APlayingSessionJoiningTheAppIsAnActivityChange()
	{
		var session = Session(new Mock<IAudioSessionStateNotifications>());
		session.ApplyInitialState(true);
		var app = Process();
		int changes = 0;
		app.ActivityChanged += _ => changes++;

		app.AddSession(session);

		Assert.AreEqual(1, changes);
		Assert.IsTrue(app.PlaysNow);
	}
}
