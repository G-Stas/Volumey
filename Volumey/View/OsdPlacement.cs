#nullable enable
using System;
using System.Windows;
using Volumey.DataProvider.Settings;
using Volumey.View.Controls;

namespace Volumey.View;

/// <summary>Calculates the pill panel's offsets in WPF layout units for a screen work area.</summary>
internal static class OsdPlacement
{
	internal static Thickness CalculateMargin(NotificationPosition position, int horizontalIndent, int verticalIndent,
		double workAreaWidthPixels, double workAreaHeightPixels, double dpiScaleX, double dpiScaleY, int pillCount)
	{
		bool top = position is NotificationPosition.TopLeft or NotificationPosition.TopCenter or NotificationPosition.TopRight;
		bool centered = position is NotificationPosition.TopCenter or NotificationPosition.BottomCenter;
		bool left = position is NotificationPosition.TopLeft or NotificationPosition.BottomLeft;
		//Distances are to the pill itself, including the room for its shadow. Centered positions ignore the side indent.
		//On a small screen shrink the inset so the pills remain visible.
		double workAreaWidth = workAreaWidthPixels / dpiScaleX;
		double workAreaHeight = workAreaHeightPixels / dpiScaleY;
		double horizontal = centered ? 0 : Clamp(horizontalIndent - OsdPill.ShadowMargin, workAreaWidth - OsdPill.OuterWidth);
		double vertical = Clamp(verticalIndent - OsdPill.ShadowMargin, workAreaHeight - pillCount * OsdPill.OuterHeight);
		return new Thickness(left ? horizontal : 0, top ? vertical : 0, left ? 0 : horizontal, top ? 0 : vertical);
	}

	private static double Clamp(double value, double room) => Math.Max(0, Math.Min(value, room));
}
