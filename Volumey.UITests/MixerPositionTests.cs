using System.Drawing;
using System.Text.Json.Nodes;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// "Remember the mixer's position" (Volume mixer page): window mode and the dragged popup open where they were left only while it's on.
/// Positions are compared on the primary screen, which these tests assume to be at 100%.
/// </summary>
[TestClass]
public class MixerPositionTests : UiTest
{
	private const int Left = 200;
	private const int Top = 150;

	private static JsonObject WindowMode(bool remember) => new()
	{
		["Window"] = new JsonObject
		{
			["PopupMode"] = false,
			["RememberPosition"] = remember,
			["Left"] = Left,
			["Top"] = Top,
			["Width"] = 400,
			["Height"] = 500
		}
	};

	/// <summary>The mixer's position, once it has been placed (it's moved after it shows)</summary>
	private Point MixerPosition()
	{
		Thread.Sleep(500);
		var bounds = App.Mixer.BoundingRectangle;
		return new Point(bounds.Left, bounds.Top);
	}

	[TestMethod]
	public void WindowModeOpensWhereItWasLeftWhileRemembered()
	{
		StartMixer(WindowMode(remember: true));
		Assert.AreEqual(new Point(Left, Top), MixerPosition());
	}

	[TestMethod]
	public void WindowModeOpensByTheTrayWhenNotRemembered()
	{
		StartMixer(WindowMode(remember: false));
		var position = MixerPosition();
		Assert.AreNotEqual(new Point(Left, Top), position, "the saved position was used although it isn't remembered");
		Assert.AreEqual(400, App.Mixer.BoundingRectangle.Width, "the window mode's saved size wasn't used");
	}

	[TestMethod]
	public void ADraggedPopupOpensWhereItWasDropped()
	{
		StartMixer(new JsonObject { ["Window"] = new JsonObject { ["PopupMode"] = true, ["RememberPosition"] = true } });
		//the app shows a normal window at its first start; a second start opens the popup
		App.HideMixer();
		App.ShowMixer();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible), "the popup didn't open");
		var before = MixerPosition();

		//by a spot that isn't a control: the "Apps" header
		var header = Retry.WhileNull(() => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName("Apps"))),
			TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException("No \"Apps\" header in the popup");
		var from = header.BoundingRectangle.Center();
		Mouse.Drag(from, new Point(from.X - 300, from.Y - 200));
		Wait.UntilInputIsProcessed();
		var dropped = MixerPosition();
		Assert.AreNotEqual(before, dropped, "the popup didn't move");

		App.HideMixer();
		App.ShowMixer();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible), "the popup didn't open again");
		Assert.AreEqual(dropped, MixerPosition(), "the popup didn't open where it was dropped");
	}
}
