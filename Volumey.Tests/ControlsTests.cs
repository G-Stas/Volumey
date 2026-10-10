using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Controls;
using Volumey.Model;

namespace Volumey.Tests;

[TestClass]
public class ControlsTests
{
	[TestMethod]
	public void DigitsOnlyLetsOnlyDigitsIn()
	{
		Assert.IsTrue(DigitsOnly.IsAllowed("5"));
		Assert.IsTrue(DigitsOnly.IsAllowed("100"));
		Assert.IsFalse(DigitsOnly.IsAllowed("-"));
		Assert.IsFalse(DigitsOnly.IsAllowed("-1"));
		Assert.IsFalse(DigitsOnly.IsAllowed("1.5"));
		Assert.IsFalse(DigitsOnly.IsAllowed("1e2"));
		Assert.IsFalse(DigitsOnly.IsAllowed("٣")); //non-ASCII digits
		Assert.IsFalse(DigitsOnly.IsAllowed(""));
	}

	[TestMethod]
	public void HotkeysWithSameKeysAreEqual()
	{
		var hotkey = new HotKey(Key.F5, ModifierKeys.Control | ModifierKeys.Shift);

		Assert.AreEqual(new HotKey(Key.F5, ModifierKeys.Shift | ModifierKeys.Control), hotkey);
		Assert.AreEqual(new HotKey(Key.F5, ModifierKeys.Control | ModifierKeys.Shift).GetHashCode(), hotkey.GetHashCode());
		Assert.AreNotEqual(new HotKey(Key.F5, ModifierKeys.Control), hotkey);
		Assert.AreNotEqual(new HotKey(Key.F6, ModifierKeys.Control | ModifierKeys.Shift), hotkey);
		Assert.IsFalse(hotkey.Equals(null));
		Assert.IsFalse(hotkey.Equals("F5"));
	}

	[TestMethod]
	public void HotkeyTextListsModifiersBeforeTheKey()
	{
		//modifier names come from the keyboard layout, so only the structure is checked
		var parts = new HotKey(Key.F5, ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Windows).ToString().Split('+');

		Assert.HasCount(5, parts);
		Assert.AreEqual("Windows", parts[3]);
		Assert.AreEqual("F5", parts[4]);
		Assert.AreEqual("F5", new HotKey(Key.F5).ToString());
		Assert.AreEqual("BrowserBack", new HotKey(Key.BrowserBack).ToString());
	}

	[STATestMethod]
	public void HotkeyBoxShowsTheHotkey()
	{
		var box = new HotKeyBox();

		box.HotKey = new HotKey(Key.F5);
		Assert.AreEqual("F5", box.Text);

		box.HotKey = new HotKey(Key.None);
		Assert.AreEqual(string.Empty, box.Text);

		box.HotKey = null;
		Assert.AreEqual(string.Empty, box.Text);
	}

	[STATestMethod]
	public void ClearingHotkeyBoxTextRemovesTheHotkey()
	{
		var box = new HotKeyBox();
		box.ApplyTemplate();
		box.OnApplyTemplate();
		box.HotKey = new HotKey(Key.F5);

		box.Text = string.Empty;

		Assert.IsNull(box.HotKey);
	}

	[TestMethod]
	public void SystemHotkeysAreEqualBySystemKey()
	{
		var playPause = new SystemHotkey(Key.MediaPlayPause, new HotKey(Key.F5));

		Assert.AreEqual(new SystemHotkey(Key.MediaPlayPause, new HotKey(Key.F6)), playPause);
		Assert.AreEqual(new SystemHotkey(Key.MediaPlayPause).GetHashCode(), playPause.GetHashCode());
		Assert.AreNotEqual(new SystemHotkey(Key.MediaNextTrack, new HotKey(Key.F5)), playPause);
		Assert.AreEqual("MediaPlayPause\tF5", playPause.ToString());
		Assert.AreEqual("MediaPlayPause", new SystemHotkey(Key.MediaPlayPause).ToString());
	}
}
