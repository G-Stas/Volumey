using System.Drawing;
using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The Notifications page measured on screen: each corner, the edge distances (boxes and sliders, which show the sample OSD where it
/// lands), Horizontal off for a centered OSD, and the OSD's screen. Distances are compared on the primary screen, assumed at 100%.
/// </summary>
[TestClass]
public class NotificationsPageTests : UiTest
{
	private const string SampleApp = "Application";

	/// <summary>The pill's shadow margin inside its element: the distances count to the pill itself (OsdPill.xaml)</summary>
	private const int ShadowMargin = 12;

	private void OpenPage(int horizontal = 30, int vertical = 30)
	{
		StartApp(new JsonObject
		{
			["Notifications"] = new JsonObject { ["Enabled"] = true, ["HorizontalIndent"] = horizontal, ["VerticalIndent"] = vertical, ["DisplayTimeSeconds"] = 5 }
		});
		Settings.Open("NavNotifications", "OsdToggle");
	}

	/// <summary>The sample pill's own rectangle (without its shadow margin) and the OSD's area, once it's shown</summary>
	private (Rectangle Pill, Rectangle Area) ShownSample()
	{
		AutomationElement? pill = null;
		Assert.IsTrue(Eventually(() => (pill = App.FindOsdPill(SampleApp)) != null, 5000), "the sample OSD didn't show");
		Thread.Sleep(400); //the fade-in
		var bounds = pill!.BoundingRectangle;
		var area = App.OsdWindow!.BoundingRectangle;
		return (Rectangle.FromLTRB((int)bounds.Left + ShadowMargin, (int)bounds.Top + ShadowMargin, (int)bounds.Right - ShadowMargin, (int)bounds.Bottom - ShadowMargin),
			Rectangle.FromLTRB((int)area.Left, (int)area.Top, (int)area.Right, (int)area.Bottom));
	}

	private void ShowSample()
	{
		Settings.Find("ShowPreviewButton").AsButton().Invoke();
	}

	private static void AssertNear(int expected, int actual, string what)
		=> Assert.IsTrue(Math.Abs(expected - actual) <= 2, $"{what}: {actual} px, expected {expected}");

	[TestMethod]
	public void EachCornerPlacesTheOsdAtTheDistancesFromItsEdges()
	{
		OpenPage(horizontal: 40, vertical: 60);
		foreach(var corner in new[] { "Top left", "Top center", "Top right", "Bottom left", "Bottom center", "Bottom right" })
		{
			Settings.Select(Settings.RadioNamed(corner));
			ShowSample();
			var (pill, area) = ShownSample();

			if(corner.EndsWith("left"))
				AssertNear(40, pill.Left - area.Left, corner + ", from the left edge");
			else if(corner.EndsWith("right"))
				AssertNear(40, area.Right - pill.Right, corner + ", from the right edge");
			else
				AssertNear(area.Left + area.Width / 2, pill.Left + pill.Width / 2, corner + ", center");
			if(corner.StartsWith("Top"))
				AssertNear(60, pill.Top - area.Top, corner + ", from the top edge");
			else
				AssertNear(60, area.Bottom - pill.Bottom, corner + ", from the bottom edge");
		}
	}

	[TestMethod]
	public void TheDistanceSlidersMoveTheOsdAndShowWhereItLands()
	{
		OpenPage();
		Settings.Select(Settings.RadioNamed("Bottom right"));

		//no Show on screen: changing a distance shows the sample by itself
		Settings.Find("HorizontalDistanceSlider").Patterns.RangeValue.Pattern.SetValue(120);
		var (pill, area) = ShownSample();
		AssertNear(120, area.Right - pill.Right, "from the right edge");
		Assert.AreEqual("120", Settings.Number("HorizontalDistanceBox"), "the box doesn't follow the slider");

		Settings.Find("VerticalDistanceSlider").Patterns.RangeValue.Pattern.SetValue(90);
		Assert.IsTrue(Eventually(() => Math.Abs(ShownSample().Area.Bottom - ShownSample().Pill.Bottom - 90) <= 2), "the OSD didn't move to 90 px from the bottom");
		Assert.AreEqual("90", Settings.Number("VerticalDistanceBox"), "the box doesn't follow the slider");
		App.WaitForSettings(s => s["Notifications"]?["HorizontalIndent"]?.GetValue<int>() == 120 && s["Notifications"]?["VerticalIndent"]?.GetValue<int>() == 90,
			"the distances saved");

		//and the other way: a typed distance moves the slider
		Settings.TypeNumber("HorizontalDistanceBox", 200);
		Assert.IsTrue(Eventually(() => (int)Settings.Find("HorizontalDistanceSlider").Patterns.RangeValue.Pattern.Value.Value == 200),
			"the slider doesn't follow the box");
	}

	[TestMethod]
	public void TheHorizontalDistanceIsOffForACenteredOsd()
	{
		OpenPage();
		Settings.Select(Settings.RadioNamed("Top center"));
		Assert.IsTrue(Eventually(() => !Settings.IsEnabled("HorizontalDistanceBox") && !Settings.IsEnabled("HorizontalDistanceSlider")),
			"the horizontal distance can be changed for a centered OSD");
		Assert.IsTrue(Settings.IsEnabled("VerticalDistanceSlider"), "the vertical distance is off too");

		Settings.Select(Settings.RadioNamed("Top left"));
		Assert.IsTrue(Eventually(() => Settings.IsEnabled("HorizontalDistanceBox") && Settings.IsEnabled("HorizontalDistanceSlider")),
			"the horizontal distance stayed off");
	}

	[TestMethod]
	public void TheChosenScreenIsWhereTheOsdShows()
	{
		if(Screens.MonitorCount < 2)
			Assert.Inconclusive("Only one screen");
		OpenPage();
		var screens = Settings.Find("OsdScreenBox").AsComboBox();
		screens.Expand();
		int other = Array.FindIndex(screens.Items, i => !i.IsSelected);
		screens.Collapse();
		string chosen = Settings.SelectItem("OsdScreenBox", other);
		App.WaitForSettings(s => s["Notifications"]?["ScreenName"] != null, "the screen saved");

		ShowSample();
		ShownSample();
		string monitor = Screens.MonitorName(VolumeyApp.Handle(App.OsdWindow!));

		Assert.IsTrue(chosen.Replace(@"\\.\", "").StartsWith(monitor + " "), $"the OSD showed on \"{monitor}\", not on \"{chosen}\"");
	}
}
