using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Nucs.JsonSettings;
using SharpHook;
using SharpHook.Data;
using Volumey.Controls;
using Volumey.DataProvider.Settings;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests
{
	[TestClass]
	public class HotkeyManagerTests
	{
		private const ushort VkF5 = 0x74;
		private const ushort VkLeftCtrl = 0xA2;
		private const ushort VkLeftWin = 0x5B;

		private static readonly HotKey CtrlShiftF5 = new HotKey(Key.F5, ModifierKeys.Control | ModifierKeys.Shift);

		private readonly HotkeyService hotkeys = new HotkeyService();

		private static KeyboardHookEventArgs KeyPress(ushort virtualKey, EventMask mask)
		{
			var data = new UioHookEvent { Type = EventType.KeyPressed, Mask = mask };
			data.Keyboard.RawCode = virtualKey;
			return new KeyboardHookEventArgs(data);
		}

		[TestMethod]
		public void KeyEventsAreConvertedToHotkeys()
		{
			Assert.AreEqual(CtrlShiftF5, SharpHookHotkeyManager.ToHotkey(VkF5, EventMask.LeftCtrl | EventMask.RightShift));
			Assert.AreEqual(new HotKey(Key.F5, ModifierKeys.Alt | ModifierKeys.Windows), SharpHookHotkeyManager.ToHotkey(VkF5, EventMask.RightAlt | EventMask.LeftMeta));
			Assert.AreEqual(new HotKey(Key.F5), SharpHookHotkeyManager.ToHotkey(VkF5, EventMask.NumLock | EventMask.CapsLock));
		}

		[TestMethod]
		public void ModifierKeysAloneAreNotHotkeys()
		{
			Assert.IsNull(SharpHookHotkeyManager.ToHotkey(VkLeftCtrl, EventMask.LeftCtrl));
			Assert.IsNull(SharpHookHotkeyManager.ToHotkey(VkLeftWin, EventMask.LeftMeta));
		}

		[TestMethod]
		public void OnlyRegisteredHotkeysAreRaised()
		{
			var hook = new Mock<IGlobalHook>();
			using var manager = new SharpHookHotkeyManager(hook.Object, start: false);
			var pressed = new List<HotKey>();
			manager.HotkeyPressed += pressed.Add;
			manager.RegisterHotkey(CtrlShiftF5);

			hook.Raise(m => m.KeyPressed += null, hook.Object, KeyPress(VkF5, EventMask.LeftCtrl | EventMask.LeftShift));
			hook.Raise(m => m.KeyPressed += null, hook.Object, KeyPress(VkF5, EventMask.LeftCtrl));
			manager.UnregisterHotkey(CtrlShiftF5);
			hook.Raise(m => m.KeyPressed += null, hook.Object, KeyPress(VkF5, EventMask.LeftCtrl | EventMask.LeftShift));

			Assert.AreEqual(CtrlShiftF5, Assert.ContainsSingle(pressed));
			Assert.AreEqual(0, manager.RegisteredHotkeysCount);
		}

		[TestMethod]
		public void DisposingStopsTheHook()
		{
			var hook = new Mock<IGlobalHook>();
			var manager = new SharpHookHotkeyManager(hook.Object, start: false);

			manager.Dispose();

			hook.Verify(m => m.Dispose(), Times.Once);
		}

		[TestMethod]
		public void SwitchingManagersMovesTheHotkeys()
		{
			var first = new Mock<IHotkeyManager>();
			var registered = new List<HotKey>();
			first.Setup(m => m.RegisterHotkey(It.IsAny<HotKey>())).Callback<HotKey>(registered.Add);
			first.Setup(m => m.GetRegisteredHotkeys()).Returns(registered);
			int activations = 0;
			this.hotkeys.Activated += () => activations++;
			this.hotkeys.SetHotkeyManager(first.Object);
			var up = new HotKey(Key.Up, ModifierKeys.Control);
			var down = new HotKey(Key.Down, ModifierKeys.Control);
			this.hotkeys.RegisterHotkeysPair(up, down);
			var pressed = new List<HotKey>();
			this.hotkeys.HotkeyPressed += pressed.Add;

			var second = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(second.Object);

			second.Verify(m => m.RegisterHotkey(up), Times.Once);
			second.Verify(m => m.RegisterHotkey(down), Times.Once);
			first.Verify(m => m.UnregisterHotkey(up), Times.Once);
			first.Verify(m => m.Dispose(), Times.Once);
			Assert.AreEqual(1, activations, "the view models register their hotkeys only once");

			first.Raise(m => m.HotkeyPressed += null, up);
			second.Raise(m => m.HotkeyPressed += null, down);
			Assert.AreEqual(down, Assert.ContainsSingle(pressed));

			//the user counts survive the switch: unregistering reaches the new manager
			this.hotkeys.UnregisterHotkeysPair(up, down);
			second.Verify(m => m.UnregisterHotkey(up), Times.Once);
		}

		[TestMethod]
		public void AHotkeyThatFailsToMoveDoesNotStopTheOthers()
		{
			var first = new Mock<IHotkeyManager>();
			var up = new HotKey(Key.Up, ModifierKeys.Control);
			var down = new HotKey(Key.Down, ModifierKeys.Control);
			first.Setup(m => m.GetRegisteredHotkeys()).Returns(new[] { up, down });
			this.hotkeys.SetHotkeyManager(first.Object);
			var second = new Mock<IHotkeyManager>();
			second.Setup(m => m.RegisterHotkey(up)).Throws(new InvalidOperationException("taken by another app"));

			this.hotkeys.SetHotkeyManager(second.Object);

			second.Verify(m => m.RegisterHotkey(down), Times.Once);
		}

		[TestMethod]
		public void HotkeysDoNothingWhileOneIsBeingTyped()
		{
			var manager = new Mock<IHotkeyManager>();
			this.hotkeys.SetHotkeyManager(manager.Object);
			var pressed = new List<HotKey>();
			this.hotkeys.HotkeyPressed += pressed.Add;

			this.hotkeys.IsEditingHotkey = true;
			manager.Raise(m => m.HotkeyPressed += null, CtrlShiftF5);
			this.hotkeys.IsEditingHotkey = false;
			manager.Raise(m => m.HotkeyPressed += null, CtrlShiftF5);

			Assert.ContainsSingle(pressed);
		}

		[TestMethod]
		public void HotkeysBlockByDefaultAlsoInOlderSettingsFiles()
		{
			Assert.IsTrue(new VolumeySettings().Hotkeys.BlockInSystem);

			string dir = Path.Combine(Path.GetTempPath(), "VolumeyTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			try
			{
				string file = Path.Combine(dir, "settings.json");
				File.WriteAllText(file, "{ \"Version\": 1, \"Hotkeys\": { \"PreventResettingVolumeBalance\": true } }");
				var loaded = JsonSettings.Configure<VolumeySettings>(file).LoadNow();
				Assert.IsTrue(loaded.Hotkeys.PreventResettingVolumeBalance);
				Assert.IsTrue(loaded.Hotkeys.BlockInSystem);
			}
			finally
			{
				Directory.Delete(dir, true);
			}
		}
	}
}
