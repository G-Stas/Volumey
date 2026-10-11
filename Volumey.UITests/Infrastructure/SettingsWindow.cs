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
	/// A text shown in the window (e.g. an error under a hotkey box), within <paramref name="timeoutMs"/>
	/// </summary>
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
