#nullable enable
namespace Volumey.Controls;

/// <summary>
/// What a hotkey is pressed on. Keyboard is 0: a hotkey saved without a source (every 2.0 hotkey) loads as a keyboard hotkey.
/// </summary>
public enum InputSource
{
	Keyboard = 0,
	Mouse = 1,
}
