using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Model;

namespace Volumey.Tests;

[TestClass]
public class AudioSessionVisibilityTests
{
	private VolumeySettings previousSettings = null!;

	[TestInitialize]
	public void Initialize()
	{
		previousSettings = SettingsProvider.Settings;
		SettingsProvider.UseSettings(new VolumeySettings());
	}

	[TestCleanup]
	public void Cleanup() => SettingsProvider.UseSettings(previousSettings);

	[TestMethod]
	public void HidingAndRestoringASessionDoesNotWriteAudioState()
	{
		var volume = new Mock<IAudioSessionVolume>();
		var notifications = new Mock<IAudioSessionStateNotifications>();
		using var session = new AudioSessionModel(false, 50, "session-id", 42, "Demo", "demo.exe", Guid.Empty,
			volume.Object, notifications.Object);
		session.SetVisibilityDevice("device-id");

		session.HideCommand.Execute(null);
		Assert.IsTrue(session.IsHidden);
		session.RestoreCommand.Execute(null);
		Assert.IsFalse(session.IsHidden);

		volume.Verify(v => v.SetVolume(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
		volume.Verify(v => v.SetMute(It.IsAny<bool>(), It.IsAny<Guid>()), Times.Never);
	}
}
