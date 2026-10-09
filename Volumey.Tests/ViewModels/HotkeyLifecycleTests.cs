using System;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests.ViewModels
{
	[TestClass]
	public class HotkeyLifecycleTests : ViewModelTestBase
	{
		private static readonly HotKey Up = new HotKey(Key.PageUp, ModifierKeys.Alt);
		private static readonly HotKey Down = new HotKey(Key.PageDown, ModifierKeys.Alt);

		[TestMethod]
		public void ADisposedDeviceIgnoresItsHotkeys()
		{
			ActivateHotkeys();
			var device = CreateDevice("1", "Speakers", out Mock<IAudioSessionVolume> masterVolume);
			Assert.IsTrue(device.SetVolumeHotkeys(Up, Down));

			device.Dispose();
			PressHotkey(Up);

			masterVolume.Verify(m => m.SetVolume(It.IsAny<int>(), ref It.Ref<Guid>.IsAny), Times.Never);
			//the view models release the hotkeys after the disposal, as they do on a default device switch
			device.ResetVolumeHotkeys();
			this.HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
		}

		[TestMethod]
		public void ASavedForegroundPairThatFailsAtStartupShowsAnError()
		{
			this.Settings.Hotkeys.ForegroundVolumeUp = Up;
			this.Settings.Hotkeys.ForegroundVolumeDown = Down;
			//another app took the combination since the last start
			this.HotkeyManager.Setup(m => m.RegisterHotkey(Down)).Throws(new Win32Exception(1409));
			SetDefaultDevice(CreateDevice("1", "Speakers"));
			var vm = new ForegroundWindowVolumeViewModel(this.DeviceProvider.Object, this.Hotkeys, this.Notifications.Object);

			ActivateHotkeys();

			Assert.AreEqual(ErrorDictionary.Instance[ErrorMessageType.VolumeReg], vm.ErrorMessage);
			//the first hotkey of the pair isn't left registered
			this.HotkeyManager.Verify(m => m.UnregisterHotkey(Up), Times.Once);
			//and the pair stays saved for the next start
			Assert.AreEqual(Up, this.Settings.Hotkeys.ForegroundVolumeUp);
		}
	}
}
