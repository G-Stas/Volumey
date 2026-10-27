using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.DataProvider.Settings;
using Volumey.View;
using Volumey.View.Controls;

namespace Volumey.Tests;

[TestClass]
public class OsdPlacementTests
{
	[TestMethod]
	public void EveryPositionAndEdgeIndentUsesTheSelectedMonitorsPhysicalWorkArea()
	{
		//Simulated 1920x1080 at 100%, 1920x1080 at 125%, and 2560x1440 at 150%, with taskbar-reduced work areas.
		(double width, double height, double scale)[] monitors =
		{
			(1920, 1040, 1.0),
			(1920, 1020, 1.25),
			(2560, 1400, 1.5)
		};

		foreach(var monitor in monitors)
		foreach(var position in Enum.GetValues<NotificationPosition>())
		foreach(int horizontalIndent in new[] { NotificationSettings.MinIndent, NotificationSettings.MaxIndent })
		foreach(int verticalIndent in new[] { NotificationSettings.MinIndent, NotificationSettings.MaxIndent })
		{
			var margin = OsdPlacement.CalculateMargin(position, horizontalIndent, verticalIndent,
				monitor.width, monitor.height, monitor.scale, monitor.scale, 3);
			double widthDip = monitor.width / monitor.scale;
			double heightDip = monitor.height / monitor.scale;
			double expectedSide = Math.Max(0, Math.Min(horizontalIndent - OsdPill.ShadowMargin, widthDip - OsdPill.OuterWidth));
			double expectedVertical = Math.Max(0, Math.Min(verticalIndent - OsdPill.ShadowMargin, heightDip - 3 * OsdPill.OuterHeight));
			bool left = position is NotificationPosition.TopLeft or NotificationPosition.BottomLeft;
			bool center = position is NotificationPosition.TopCenter or NotificationPosition.BottomCenter;
			bool top = position is NotificationPosition.TopLeft or NotificationPosition.TopCenter or NotificationPosition.TopRight;

			Assert.AreEqual(left ? expectedSide : 0, margin.Left, $"{monitor.width}x{monitor.height} at {monitor.scale}x, {position}, horizontal {horizontalIndent}");
			Assert.AreEqual(!left && !center ? expectedSide : 0, margin.Right, $"{monitor.width}x{monitor.height} at {monitor.scale}x, {position}, horizontal {horizontalIndent}");
			Assert.AreEqual(top ? expectedVertical : 0, margin.Top, $"{position}, vertical {verticalIndent}, top");
			Assert.AreEqual(top ? 0 : expectedVertical, margin.Bottom, $"{position}, vertical {verticalIndent}, bottom");
		}
	}
}
