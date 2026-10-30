using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
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
using MouseButton = SharpHook.Data.MouseButton;

namespace Volumey.Tests;

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

	private static MouseHookEventArgs MousePress(MouseButton button, EventMask mask)
	{
		var data = new UioHookEvent { Type = EventType.MousePressed, Mask = mask };
		data.Mouse.Button = button;
		return new MouseHookEventArgs(data);
	}

	private static MouseWheelHookEventArgs Wheel(MouseWheelScrollDirection direction, short rotation, EventMask mask)
	{
		var data = new UioHookEvent { Type = EventType.MouseWheel, Mask = mask };
		data.Wheel.Direction = direction;
		data.Wheel.Rotation = rotation;
		return new MouseWheelHookEventArgs(data);
	}

	[TestMethod]
	public void MouseEventsAreConvertedToHotkeys()
	{
		Assert.AreEqual(HotKey.Mouse(MouseButtons.XButton1), MouseHotkeyManager.ToHotkey(MouseButton.Button4, EventMask.None));
		Assert.AreEqual(HotKey.Mouse(MouseButtons.XButton2, ModifierKeys.Control), MouseHotkeyManager.ToHotkey(MouseButton.Button5, EventMask.LeftCtrl));
		Assert.AreEqual(HotKey.Mouse(MouseButtons.Middle, ModifierKeys.Shift), MouseHotkeyManager.ToHotkey(MouseButton.Button3, EventMask.RightShift));
		Assert.AreEqual(HotKey.Mouse(MouseButtons.Left, ModifierKeys.Alt), MouseHotkeyManager.ToHotkey(MouseButton.Button1, EventMask.LeftAlt));
		Assert.AreEqual(HotKey.Mouse(MouseButtons.Right), MouseHotkeyManager.ToHotkey(MouseButton.Button2, EventMask.None));
		Assert.IsNull(MouseHotkeyManager.ToHotkey(MouseButton.NoButton, EventMask.None));

		Assert.AreEqual(HotKey.Mouse(MouseButtons.WheelUp, ModifierKeys.Control), MouseHotkeyManager.ToHotkey(MouseWheelScrollDirection.Vertical, 1, EventMask.LeftCtrl));
		Assert.AreEqual(HotKey.Mouse(MouseButtons.WheelDown, ModifierKeys.Control), MouseHotkeyManager.ToHotkey(MouseWheelScrollDirection.Vertical, -1, EventMask.LeftCtrl));
		//libuiohook on Windows: positive is up and left (checked with simulated wheel input)
		Assert.AreEqual(HotKey.Mouse(MouseButtons.WheelLeft), MouseHotkeyManager.ToHotkey(MouseWheelScrollDirection.Horizontal, 1, EventMask.None));
		Assert.AreEqual(HotKey.Mouse(MouseButtons.WheelRight), MouseHotkeyManager.ToHotkey(MouseWheelScrollDirection.Horizontal, -1, EventMask.None));
		Assert.IsNull(MouseHotkeyManager.ToHotkey(MouseWheelScrollDirection.Vertical, 0, EventMask.None));
	}

	[TestMethod]
	public void OnlyRegisteredMouseHotkeysAreRaised()
	{
		var hook = new Mock<IGlobalHook>();
		using var mouse = new MouseHotkeyManager(new InputHookHost(() => hook.Object, start: false));
		var pressed = new List<HotKey>();
		mouse.HotkeyPressed += pressed.Add;
		var back = HotKey.Mouse(MouseButtons.XButton1);
		var ctrlWheelUp = HotKey.Mouse(MouseButtons.WheelUp, ModifierKeys.Control);
		mouse.RegisterHotkey(back);
		mouse.RegisterHotkey(ctrlWheelUp);

		hook.Raise(m => m.MousePressed += null, hook.Object, MousePress(MouseButton.Button4, EventMask.None));
		hook.Raise(m => m.MousePressed += null, hook.Object, MousePress(MouseButton.Button4, EventMask.LeftCtrl));
		hook.Raise(m => m.MousePressed += null, hook.Object, MousePress(MouseButton.Button1, EventMask.None));
		hook.Raise(m => m.MouseWheel += null, hook.Object, Wheel(MouseWheelScrollDirection.Vertical, 1, EventMask.LeftCtrl));
		hook.Raise(m => m.MouseWheel += null, hook.Object, Wheel(MouseWheelScrollDirection.Vertical, 1, EventMask.None));

		CollectionAssert.AreEqual(new[] { back, ctrlWheelUp }, pressed);
	}

	[TestMethod]
	public void TheHookWatchesOnlyWhatItsUsersNeed()
	{
		var hooks = new List<Mock<IGlobalHook>>();
		var host = new InputHookHost(() =>
		{
			var hook = new Mock<IGlobalHook>();
			hooks.Add(hook);
			return hook.Object;
		}, start: false);
		Assert.IsNull(host.Running);

		var keyboard = new SharpHookHotkeyManager(host);
		Assert.AreEqual(GlobalHookType.Keyboard, host.Running);

		//libuiohook runs one hook per process: the keyboard's is replaced by one for both
		var mouse = new MouseHotkeyManager(host);
		Assert.AreEqual(GlobalHookType.All, host.Running);
		Assert.HasCount(2, hooks);
		hooks[0].Verify(h => h.Dispose(), Times.Once);

		//the blocking hotkey mode doesn't need the keyboard hook
		keyboard.Dispose();
		Assert.AreEqual(GlobalHookType.Mouse, host.Running);

		mouse.Dispose();
		Assert.IsNull(host.Running);
		hooks[2].Verify(h => h.Dispose(), Times.Once);
	}

	[TestMethod]
	public void KeysStillArriveWhileTheMouseIsWatchedToo()
	{
		var hooks = new List<Mock<IGlobalHook>>();
		var host = new InputHookHost(() =>
		{
			var hook = new Mock<IGlobalHook>();
			hooks.Add(hook);
			return hook.Object;
		}, start: false);
		using var keyboard = new SharpHookHotkeyManager(host);
		using var mouse = new MouseHotkeyManager(host);
		var pressed = new List<HotKey>();
		keyboard.HotkeyPressed += pressed.Add;
		keyboard.RegisterHotkey(CtrlShiftF5);

		hooks[^1].Raise(m => m.KeyPressed += null, hooks[^1].Object, KeyPress(VkF5, EventMask.LeftCtrl | EventMask.LeftShift));

		Assert.AreEqual(CtrlShiftF5, Assert.ContainsSingle(pressed));
	}

	[TestMethod]
	public void DisposingStopsTheHook()
	{
		var hook = new Mock<IGlobalHook>();
		var manager = new SharpHookHotkeyManager(hook.Object, start: false);

		manager.Dispose();

		hook.Verify(m => m.Dispose(), Times.Once);
	}

	/// <summary>
	/// A hook that runs like SharpHook's: Run blocks until the hook is stopped, stopping fails as many times as asked,
	/// and Dispose stops a running hook once
	/// </summary>
	private static Mock<IGlobalHook> RunningHook(int failedStops)
	{
		var hook = new Mock<IGlobalHook>();
		var stopped = new ManualResetEventSlim();
		bool running = false;
		bool disposed = false;
		void StopNative()
		{
			if(failedStops-- > 0)
				throw new HookException(UioHookResult.Failure, "Failed stopping the global hook");
			stopped.Set();
		}
		hook.SetupGet(h => h.IsRunning).Returns(() => running);
		hook.Setup(h => h.Run(It.IsAny<GlobalHookType>())).Callback(() =>
		{
			running = true;
			hook.Raise(h => h.HookEnabled += null, hook.Object, new HookEventArgs(new UioHookEvent { Type = EventType.HookEnabled }));
			stopped.Wait();
			running = false;
		});
		hook.Setup(h => h.Stop()).Callback(() =>
		{
			if(disposed)
				throw new ObjectDisposedException(nameof(IGlobalHook));
			if(running)
				StopNative();
		});
		hook.Setup(h => h.Dispose()).Callback(() =>
		{
			if(disposed)
				return;
			disposed = true;
			if(running)
				StopNative();
		});
		return hook;
	}

	private static void WaitFor(Func<bool> condition)
	{
		var timeout = Stopwatch.StartNew();
		while(!condition() && timeout.Elapsed < TimeSpan.FromSeconds(10))
			Thread.Sleep(10);
	}

	[TestMethod]
	public void AHookThatFailsToStopIsStoppedOnTheNextTry()
	{
		var hooks = new List<Mock<IGlobalHook>>();
		var host = new InputHookHost(() =>
		{
			var hook = RunningHook(failedStops: hooks.Count == 0 ? 2 : 0);
			hooks.Add(hook);
			return hook.Object;
		}, start: true);
		using var keyboard = new SharpHookHotkeyManager(host);
		WaitFor(() => host.Running == GlobalHookType.Keyboard);

		using var mouse = new MouseHotkeyManager(host);
		WaitFor(() => host.Running == GlobalHookType.All);

		Assert.AreEqual(GlobalHookType.All, host.Running);
		hooks[0].Verify(h => h.Stop(), Times.Exactly(3));
		hooks[0].Verify(h => h.Dispose(), Times.Once);
		hooks[1].Verify(h => h.Run(GlobalHookType.All), Times.Once);
	}

	[TestMethod]
	public void AHookThatNeverStopsDoesNotKeepTheNextOneFromStarting()
	{
		var hooks = new List<Mock<IGlobalHook>>();
		var host = new InputHookHost(() =>
		{
			var hook = RunningHook(failedStops: hooks.Count == 0 ? int.MaxValue : 0);
			hooks.Add(hook);
			return hook.Object;
		}, start: true);
		using var keyboard = new SharpHookHotkeyManager(host);
		WaitFor(() => host.Running == GlobalHookType.Keyboard);

		using var mouse = new MouseHotkeyManager(host);
		WaitFor(() => host.Running == GlobalHookType.All);

		Assert.AreEqual(GlobalHookType.All, host.Running);
		hooks[1].Verify(h => h.Run(GlobalHookType.All), Times.Once);
	}

	[TestMethod]
	public void SwitchingManagersMovesTheHotkeys()
	{
		var first = new Mock<IHotkeyManager>();
		var registered = new List<HotKey>();
		first.Setup(m => m.RegisterHotkey(It.IsAny<HotKey>())).Callback<HotKey>(registered.Add);
		first.Setup(m => m.GetRegisteredHotkeys()).Returns(registered);
		int activations = 0;
		hotkeys.Activated += () => activations++;
		hotkeys.SetHotkeyManager(first.Object);
		var up = new HotKey(Key.Up, ModifierKeys.Control);
		var down = new HotKey(Key.Down, ModifierKeys.Control);
		hotkeys.RegisterHotkeysPair(up, down);
		var pressed = new List<HotKey>();
		hotkeys.HotkeyPressed += pressed.Add;

		var second = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(second.Object);

		second.Verify(m => m.RegisterHotkey(up), Times.Once);
		second.Verify(m => m.RegisterHotkey(down), Times.Once);
		first.Verify(m => m.UnregisterHotkey(up), Times.Once);
		first.Verify(m => m.Dispose(), Times.Once);
		Assert.AreEqual(1, activations, "the view models register their hotkeys only once");

		first.Raise(m => m.HotkeyPressed += null, up);
		second.Raise(m => m.HotkeyPressed += null, down);
		Assert.AreEqual(down, Assert.ContainsSingle(pressed));

		//the user counts survive the switch: unregistering reaches the new manager
		hotkeys.UnregisterHotkeysPair(up, down);
		second.Verify(m => m.UnregisterHotkey(up), Times.Once);
	}

	[TestMethod]
	public void AHotkeyThatFailsToMoveDoesNotStopTheOthers()
	{
		var first = new Mock<IHotkeyManager>();
		var up = new HotKey(Key.Up, ModifierKeys.Control);
		var down = new HotKey(Key.Down, ModifierKeys.Control);
		first.Setup(m => m.GetRegisteredHotkeys()).Returns(new[] { up, down });
		hotkeys.SetHotkeyManager(first.Object);
		var second = new Mock<IHotkeyManager>();
		second.Setup(m => m.RegisterHotkey(up)).Throws(new InvalidOperationException("taken by another app"));

		hotkeys.SetHotkeyManager(second.Object);

		second.Verify(m => m.RegisterHotkey(down), Times.Once);
	}

	[TestMethod]
	public void HotkeysDoNothingWhileOneIsBeingTyped()
	{
		var manager = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(manager.Object);
		var pressed = new List<HotKey>();
		hotkeys.HotkeyPressed += pressed.Add;

		hotkeys.IsEditingHotkey = true;
		manager.Raise(m => m.HotkeyPressed += null, CtrlShiftF5);
		hotkeys.IsEditingHotkey = false;
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
