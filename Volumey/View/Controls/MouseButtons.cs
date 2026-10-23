#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Volumey.Controls;

/// <summary>
/// The names of the mouse buttons and wheel directions a hotkey can use (<see cref="HotKey.Button"/>), as they are saved
/// </summary>
public static class MouseButtons
{
	public const string Left = "Left";
	public const string Right = "Right";
	public const string Middle = "Middle";
	/// <summary>The side button usually called Back</summary>
	public const string XButton1 = "XButton1";
	/// <summary>The side button usually called Forward</summary>
	public const string XButton2 = "XButton2";
	public const string WheelUp = "WheelUp";
	public const string WheelDown = "WheelDown";
	public const string WheelLeft = "WheelLeft";
	public const string WheelRight = "WheelRight";

	public static IReadOnlyList<string> All { get; } =
		new[] { Left, Right, Middle, XButton1, XButton2, WheelUp, WheelDown, WheelLeft, WheelRight };

	public static bool IsKnown(string? button) => button != null && All.Contains(button);

	/// <summary>
	/// Whether the button works as a hotkey only with a modifier key held: the clicks and the wheel are used all the time,
	/// so alone they would trigger on every click or scroll; the side buttons may be a hotkey alone
	/// </summary>
	public static bool NeedsModifier(string button) => button is not (XButton1 or XButton2);

	public static bool IsWheel(string button) => button is WheelUp or WheelDown or WheelLeft or WheelRight;

	/// <summary>The button's name in the UI language ("Back button"); the saved name if there's no translation</summary>
	public static string DisplayName(string button)
		=> Localization.TranslationSource.Instance[$"MouseButton_{button}"] is { Length: > 0 } name ? name : button;
}
