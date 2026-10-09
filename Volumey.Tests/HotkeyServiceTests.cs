using System.ComponentModel;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests;

[TestClass]
public class HotkeyServiceTests
{
	private static readonly HotKey Shared = new HotKey(Key.F7, ModifierKeys.Control);
	private static readonly HotKey Taken = new HotKey(Key.F8, ModifierKeys.Control);
	private static readonly HotKey Other = new HotKey(Key.F9, ModifierKeys.Control);

	private Mock<IHotkeyManager> manager;
	private HotkeyService service;

	[TestInitialize]
	public void Initialize()
	{
		manager = new Mock<IHotkeyManager>();
		//another app owns this combination (blocking mode)
		manager.Setup(m => m.RegisterHotkey(Taken)).Throws(new Win32Exception(1409));
		service = new HotkeyService();
		service.SetHotkeyManager(manager.Object);
	}

	[TestMethod]
	public void AFailedPairKeepsACombinationAnotherFeatureUses()
	{
		Assert.IsTrue(service.RegisterHotkey(Shared));

		//the pair takes the shared combination, then fails on the second hotkey
		Assert.IsFalse(service.RegisterHotkeysPair(Shared, Taken));

		manager.Verify(m => m.UnregisterHotkey(Shared), Times.Never);
		manager.Verify(m => m.UnregisterHotkey(Taken), Times.Never);
		//the other feature is still the one user: its release unregisters the combination
		service.UnregisterHotkey(Shared);
		manager.Verify(m => m.UnregisterHotkey(Shared), Times.Once);
	}

	[TestMethod]
	public void AFailedPairReleasesWhatItTook()
	{
		Assert.IsFalse(service.RegisterHotkeysPair(Other, Taken));

		manager.Verify(m => m.UnregisterHotkey(Other), Times.Once);
	}

	[TestMethod]
	public void AFailedRegistrationIsNotCounted()
	{
		Assert.IsTrue(service.RegisterHotkey(Other));
		//a second feature fails to register the combination with the manager
		manager.Setup(m => m.RegisterHotkey(Other)).Throws(new Win32Exception(1409));
		Assert.IsFalse(service.RegisterHotkey(Other));

		//so the first feature's release is the last one
		service.UnregisterHotkey(Other);
		manager.Verify(m => m.UnregisterHotkey(Other), Times.Once);
	}

	[TestMethod]
	public void AnOpenMixerHotkeyThatFailedDoesNothing()
	{
		int opened = 0;
		service.OpenHotkeyPressed += () => opened++;

		Assert.IsFalse(service.RegisterOpenMixerHotkey(Taken));
		manager.Raise(m => m.HotkeyPressed += null, Taken);

		Assert.AreEqual(0, opened);
	}

	[TestMethod]
	public void RegisteringWithoutAManagerFails()
	{
		var inactive = new HotkeyService();

		Assert.IsFalse(inactive.RegisterHotkey(Other));
		Assert.IsFalse(inactive.RegisterHotkeysPair(Other, Shared));
		inactive.UnregisterHotkey(Other);
	}

	[TestMethod]
	public void ForegroundAndMediaHotkeysCountAsTaken()
	{
		var settings = new VolumeySettings();
		settings.Hotkeys.ForegroundVolumeUp = Shared;
		settings.Hotkeys.System = [new SystemHotkeyEntry { SystemKey = Key.MediaPlayPause, Replacement = Other }];
		var previous = SettingsProvider.Settings;
		SettingsProvider.UseSettings(settings);
		try
		{
			Assert.AreEqual(ErrorMessageType.HotkeyExists, service.HotkeyIsValid(Shared));
			Assert.AreEqual(ErrorMessageType.HotkeyExists, service.HotkeyIsValid(Other));
			Assert.AreEqual(ErrorMessageType.None, service.HotkeyIsValid(Taken));
		}
		finally
		{
			SettingsProvider.UseSettings(previous);
		}
	}
}
