using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace Volumey.UITests.Infrastructure;

/// <summary>
/// The settings window: its pages, hotkey boxes, switches and error texts, found by the automation IDs of the XAML
/// </summary>
internal sealed class SettingsWindow
{
	private static readonly TimeSpan FindTimeout = TimeSpan.FromSeconds(5);

	private readonly VolumeyApp app;

	public Window Window { get; private set; }

	public SettingsWindow(VolumeyApp app, Window window)
	{
		this.app = app;
		Window = window;
	}

	/// <summary>
	/// Opens a page: WPF-UI's navigation items expose no automation pattern, so this is a real click
	/// </summary>
	/// <param name="navId">NavGeneral, NavMixer, NavAppHotkeys, NavDevices, NavMediaKeys, NavNotifications, NavAbout</param>
	/// <param name="readyId">an element of the page, to wait for</param>
	public void Open(string navId, string readyId)
	{
		Window.SetForeground();
		Find(navId).Click();
		Find(readyId);
	}

	public AutomationElement Find(string automationId)
		=> TryFind(automationId, FindTimeout) ?? throw new InvalidOperationException($"No element {automationId} in the settings window");

	public AutomationElement? TryFind(string automationId, TimeSpan timeout)
		=> Retry.WhileNull(() => Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), timeout).Result;

	/// <summary>
	/// Types a combination into a hotkey box, the way a person does (the box records the keys as they're pressed)
	/// </summary>
	public void TypeHotkey(string boxId, Combo combo) => TypeHotkey(Find(boxId), combo);

	public void TypeHotkey(AutomationElement box, Combo combo)
	{
		Window.SetForeground();
		box.Focus();
		combo.Press();
		LeaveBox();
	}

	/// <summary>
	/// Empties a hotkey box (Backspace)
	/// </summary>
	public void ClearHotkey(string boxId)
	{
		Window.SetForeground();
		Find(boxId).Focus();
		Keyboard.Type(VirtualKeyShort.BACK);
		Wait.UntilInputIsProcessed();
		LeaveBox();
	}

	/// <summary>
	/// Moves the keyboard focus off a hotkey box: while a box has it, Volumey ignores its hotkeys (IHotkeyService.IsEditingHotkey)
	/// </summary>
	public void LeaveBox()
	{
		try
		{
			Find("NavGeneral").Focus();
		}
		catch(InvalidOperationException)
		{
			//a dialog is open (the navigation can't take the focus then): move on to its next control
			Keyboard.Type(VirtualKeyShort.TAB);
		}
		Wait.UntilInputIsProcessed();
	}

	public bool IsOn(string toggleId) => Find(toggleId).Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On;

	public void SetSwitch(string toggleId, bool on)
	{
		var toggle = Find(toggleId);
		if((toggle.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On) != on)
			toggle.Patterns.Toggle.Pattern.Toggle();
		Retry.WhileFalse(() => IsOn(toggleId) == on, FindTimeout);
	}

	public bool IsEnabled(string automationId) => Find(automationId).IsEnabled;

	/// <summary>
	/// Types a number into a number box and commits it with Enter, the way a person does
	/// </summary>
	public void TypeNumber(string boxId, int value)
	{
		Window.SetForeground();
		Find(boxId).Focus();
		Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
		Keyboard.Type(value.ToString());
		Keyboard.Type(VirtualKeyShort.RETURN);
		Wait.UntilInputIsProcessed();
		LeaveBox();
	}

	/// <summary>The text of a number box</summary>
	public string Number(string boxId) => Find(boxId).Patterns.Value.Pattern.Value.Value;

	/// <summary>
	/// Selects a combo box's item by its position; returns its name (to compare with <see cref="SelectedName"/> later)
	/// </summary>
	public string SelectItem(string comboId, int index)
	{
		var combo = Find(comboId).AsComboBox();
		combo.Expand();
		var item = Retry.WhileNull(() => combo.Items.ElementAtOrDefault(index), FindTimeout).Result
		           ?? throw new InvalidOperationException($"{comboId} has no item {index}");
		string name = item.Name;
		item.Select();
		combo.Collapse();
		return name;
	}

	public string? SelectedName(string comboId) => Find(comboId).AsComboBox().SelectedItem?.Name;

	/// <summary>A radio button, found by its name (the corner pickers panel has no automation ID in the tree)</summary>
	public AutomationElement RadioNamed(string name)
		=> Retry.WhileNull(() => Window.FindFirstDescendant(cf => cf.ByControlType(ControlType.RadioButton).And(cf.ByName(name))), FindTimeout).Result
		   ?? throw new InvalidOperationException($"No radio button {name} in the settings window");

	public void Select(AutomationElement radio) => radio.Patterns.SelectionItem.Pattern.Select();

	public static bool IsSelected(AutomationElement radio) => radio.Patterns.SelectionItem.Pattern.IsSelected.Value;

	/// <summary>
	/// A text shown in the window (e.g. an error under a hotkey box), within <paramref name="timeoutMs"/>
	/// </summary>
	/// <summary>
	/// The message under a hotkey box ("Ctrl+K: Used by another app"): the box exposes it as its help text
	/// </summary>
	public bool BoxSays(AutomationElement box, string text, int timeoutMs = 3000)
		=> Retry.WhileFalse(() => box.Properties.HelpText.ValueOrDefault?.Contains(text) == true, TimeSpan.FromMilliseconds(timeoutMs)).Result;

	public bool ShowsText(string text, int timeoutMs = 3000)
		=> Retry.WhileFalse(() => Window.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Any(t => t.Name.Contains(text)),
			TimeSpan.FromMilliseconds(timeoutMs)).Result;

	/// <summary>
	/// The text of an element inside the card or row that holds <paramref name="automationId"/>, e.g. the error under a box
	/// </summary>
	public IEnumerable<string> TextsNear(string automationId)
	{
		var parent = Find(automationId).Parent;
		return parent?.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Select(t => t.Name) ?? Enumerable.Empty<string>();
	}

	/// <summary>
	/// Looks the window up again (after a restart)
	/// </summary>
	public void Refresh() => Window = app.OpenSettings().Window;
}
