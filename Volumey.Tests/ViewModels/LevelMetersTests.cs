using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Model;
using Volumey.ViewModel;

namespace Volumey.Tests.ViewModels;

[TestClass]
public class LevelMetersTests : ViewModelTestBase
{
	private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1 / 30.0);

	[TestMethod]
	public void APeakShowsAtOnceAndFallsOverHalfASecond()
	{
		Assert.AreEqual(0.7, LevelMeters.Next(0.2, 0.7, Frame), 1e-6);
		//no new peak: 0.8 of the level per 1/30 s
		Assert.AreEqual(0.8, LevelMeters.Next(1, 0, Frame), 1e-6);
		Assert.AreEqual(0.64, LevelMeters.Next(1, 0, Frame * 2), 1e-6);
		Assert.AreEqual(0.5, LevelMeters.Next(1, 0.5, Frame * 10), 1e-6, "a peak above what is left wins");
		Assert.AreEqual(0, LevelMeters.Next(1, 0, TimeSpan.FromSeconds(1)), "a second later it shows nothing");
		Assert.AreEqual(1, LevelMeters.Next(0, 1.3, Frame), "never more than the full fill");
	}

	[TestMethod]
	public void SessionsAppsAndTheDeviceGetTheirLevels()
	{
		var loud = Session(0.6, volume: 50);
		var quiet = Session(0.3, volume: 50);
		var player = CreateProcess("player");
		player.AddSession(loud);
		player.AddSession(quiet);
		var browser = CreateProcess("browser");
		browser.AddSession(Session(0.2, volume: 100));
		var device = CreateDevice("1", "Speakers", player, browser);

		LevelMeters.Read(device, Frame);

		Assert.AreEqual(0.6, loud.Level, 1e-6);
		Assert.AreEqual(0.3, quiet.Level, 1e-6);
		Assert.AreEqual(0.6, player.Level, 1e-6, "an app shows its loudest session");
		Assert.AreEqual(0.2, browser.Level, 1e-6);
		//the player at 50% (0.3) is louder than the browser at 100% (0.2)
		Assert.AreEqual(0.3, device.Master.Level, 1e-6, "the device shows its loudest app at that app's volume");
	}

	[TestMethod]
	public void MutedOnesShowNothing()
	{
		//sessions follow their app's mute state (AddSession), so a muted app's sessions are muted too
		var app = CreateProcess("app");
		app.AddSession(Session(0.4, volume: 100));
		var mutedApp = CreateProcess("muted");
		mutedApp.AddSession(Session(0.8, volume: 100));
		mutedApp.IsMuted = true;
		var device = CreateDevice("1", "Speakers", app, mutedApp);

		LevelMeters.Read(device, Frame);

		Assert.AreEqual(0, mutedApp.Sessions[0].Level);
		Assert.AreEqual(0, mutedApp.Level);
		Assert.AreEqual(0.4, device.Master.Level, 1e-6, "a muted app doesn't count for the device");

		device.Master.IsMuted = true;
		LevelMeters.Read(device, Frame);
		Assert.AreEqual(0, device.Master.Level);
	}

	[TestMethod]
	public void ALevelFallsBetweenReadings()
	{
		var peak = 0.5f;
		var volume = new Mock<IAudioSessionVolume>();
		volume.Setup(m => m.GetPeak()).Returns(() => peak);
		var session = new AudioSessionModel(false, 100, "s", default, "s", "s", Guid.Empty, volume.Object, new Mock<IAudioSessionStateNotifications>().Object);
		var app = CreateProcess("app");
		app.AddSession(session);
		var device = CreateDevice("1", "Speakers", app);

		LevelMeters.Read(device, Frame);
		peak = 0;
		LevelMeters.Read(device, Frame);

		Assert.AreEqual(0.4, session.Level, 1e-6);
		Assert.AreEqual(0.4, app.Level, 1e-6);
	}

	private static AudioSessionModel Session(double peak, int volume)
	{
		var sessionVolume = new Mock<IAudioSessionVolume>();
		sessionVolume.Setup(m => m.GetPeak()).Returns((float)peak);
		return new AudioSessionModel(false, volume, Guid.NewGuid().ToString(), default, "session", Guid.NewGuid().ToString(), Guid.NewGuid(),
			sessionVolume.Object, new Mock<IAudioSessionStateNotifications>().Object);
	}
}
