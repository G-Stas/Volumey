using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests;

[TestClass]
public class ControlsTests
{
	[STATestMethod]
	public void AHotkeyBoxInheritsTheHotkeyServiceSetOnItsPage()
	{
		var service = new Mock<IHotkeyService>().Object;
		var page = new StackPanel();
		var card = new Border();
		var box = new HotKeyBox();
		card.Child = box;
		page.Children.Add(card);

		HotKeyBox.SetHotkeyService(page, service);

		Assert.AreSame(service, HotKeyBox.GetHotkeyService(box));
	}

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

	[STATestMethod]
	public void NumberBoxCommitsAClampedValueWhenItLosesFocus()
	{
		var box = new Wpf.Ui.Controls.NumberBox { Minimum = 1, Maximum = 100, Value = 5 };
		var scope = new StackPanel();
		System.Windows.Input.FocusManager.SetIsFocusScope(scope, true);
		scope.Children.Add(box);
		System.Windows.Input.FocusManager.SetFocusedElement(scope, box);
		box.Text = "150";

		NumberBoxCommit.TakeLogicalFocus(box);

		Assert.AreEqual(100d, box.Value, $"text {box.Text}");
		Assert.IsFalse(box.IsFocused);
	}

	[STATestMethod]
	public void NumberBoxShowsTheClampedValueWhenItEqualsTheCurrentOne()
	{
		//WPF-UI doesn't update the text when the clamped value doesn't change the value
		var box = new Wpf.Ui.Controls.NumberBox { Minimum = 1, Maximum = 100, Value = 100 };
		box.Text = "150";

		NumberBoxCommit.ShowValue(box);

		Assert.AreEqual("100", box.Text);
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
	public void MouseHotkeysAreEqualBySourceButtonAndModifiers()
	{
		var hotkey = HotKey.Mouse(MouseButtons.XButton1, ModifierKeys.Control);

		Assert.AreEqual(HotKey.Mouse(MouseButtons.XButton1, ModifierKeys.Control), hotkey);
		Assert.AreEqual(HotKey.Mouse(MouseButtons.XButton1, ModifierKeys.Control).GetHashCode(), hotkey.GetHashCode());
		Assert.AreNotEqual(HotKey.Mouse(MouseButtons.XButton2, ModifierKeys.Control), hotkey);
		Assert.AreNotEqual(HotKey.Mouse(MouseButtons.XButton1), hotkey);
		//a keyboard hotkey with no key and the same modifiers is another hotkey
		Assert.AreNotEqual(new HotKey(Key.None, ModifierKeys.Control), hotkey);
		Assert.AreEqual(InputSource.Mouse, hotkey.Source);
		Assert.AreEqual(Key.None, hotkey.Key);
		Assert.AreEqual(InputSource.Keyboard, new HotKey(Key.F5).Source);
		Assert.IsNull(new HotKey(Key.F5).Button);
	}

	[TestMethod]
	public void OnlyTheSideButtonsWorkWithoutAModifier()
	{
		Assert.IsFalse(MouseButtons.NeedsModifier(MouseButtons.XButton1));
		Assert.IsFalse(MouseButtons.NeedsModifier(MouseButtons.XButton2));
		foreach(var button in new[] { MouseButtons.Left, MouseButtons.Right, MouseButtons.Middle, MouseButtons.WheelUp, MouseButtons.WheelDown,
			         MouseButtons.WheelLeft, MouseButtons.WheelRight })
			Assert.IsTrue(MouseButtons.NeedsModifier(button), button);
		Assert.IsTrue(MouseButtons.IsWheel(MouseButtons.WheelLeft));
		Assert.IsFalse(MouseButtons.IsWheel(MouseButtons.Middle));
		Assert.IsTrue(MouseButtons.IsKnown(MouseButtons.WheelRight));
		Assert.IsFalse(MouseButtons.IsKnown("Button6"));
		Assert.IsFalse(MouseButtons.IsKnown(null));
	}

	[TestMethod]
	public void MouseHotkeyTextListsModifiersBeforeTheButton()
	{
		var parts = HotKey.Mouse(MouseButtons.WheelUp, ModifierKeys.Control | ModifierKeys.Windows).ToString().Split('+');

		Assert.HasCount(3, parts);
		Assert.AreEqual("Windows", parts[1]);
		Assert.AreEqual(MouseButtons.DisplayName(MouseButtons.WheelUp), parts[2]);
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
	public void AScreenReaderHearsWhatAnEmptyHotkeyBoxTakes()
	{
		//the empty box only shows a keyboard and a mouse icon
		var box = new HotKeyBox { PlaceholderText = "Press a button on keyboard or mouse" };

		Assert.AreEqual("Press a button on keyboard or mouse", System.Windows.Automation.AutomationProperties.GetHelpText(box));
	}

	[STATestMethod]
	public void HotkeyBoxShowsAMouseHotkeyWithTheButtonsName()
	{
		var box = new HotKeyBox();

		box.HotKey = HotKey.Mouse(MouseButtons.XButton1, ModifierKeys.Control);

		Assert.AreEqual(2, box.KeyNames.Count);
		Assert.AreEqual(MouseButtons.DisplayName(MouseButtons.XButton1), box.KeyNames[1]);
		Assert.AreNotEqual(MouseButtons.XButton1, box.KeyNames[1], "the name comes from the translations, not the saved name");
		StringAssert.EndsWith(box.Text, MouseButtons.DisplayName(MouseButtons.XButton1));
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
