using System.Drawing;
using System.Text.Json.Nodes;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// "Remember the mixer's position" (Volume mixer page): window mode and the dragged popup open where they were left only while it's on.
/// The saved position is in physical pixels; drags are compared in this process's own coordinates.
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
			["Width"] = 400
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
		Thread.Sleep(500); //it's moved after it shows
		//the saved position is in physical pixels, which UI Automation scales for this process when the primary screen isn't at 100%
		Assert.AreEqual(new Point(Left, Top), Screens.WindowRect(VolumeyApp.Handle(App.Mixer)).Location);
	}

	[TestMethod]
	public void WindowModeOpensByTheTrayWhenNotRemembered()
	{
		StartMixer(WindowMode(remember: false));
		var position = MixerPosition();
		Assert.AreNotEqual(new Point(Left, Top), position, "the saved position was used although it isn't remembered");
		Assert.AreEqual(400, App.Mixer.BoundingRectangle.Width, "the window mode's saved width wasn't used");
	}

	[TestMethod]
	public void ADraggedPopupOpensWhereItWasDropped() => DragPopupBy("Apps", remember: true);

	[TestMethod]
	public void WindowModeIsDraggedByASpotThatIsntAControl()
	{
		//the window mode can be dragged whether its position is remembered or not
		StartMixer(WindowMode(remember: false));
		var before = MixerPosition();

		var grip = Retry.WhileNull(() => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName("Apps"))),
			TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException("No \"Apps\" header in the mixer");
		var from = grip.BoundingRectangle.Center();
		Mouse.Drag(from, new Point(from.X - 300, from.Y - 200));
		Wait.UntilInputIsProcessed();

		var after = MixerPosition();
		Assert.AreEqual(new Point(before.X - 300, before.Y - 200), after, "the window didn't follow the drag");

		//the title bar still moves it too
		var title = App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName("Volumey")))
		            ?? throw new AssertFailedException("No title in the title bar");
		from = title.BoundingRectangle.Center();
		Mouse.Drag(from, new Point(from.X + 100, from.Y + 50));
		Wait.UntilInputIsProcessed();
		Assert.AreEqual(new Point(after.X + 100, after.Y + 50), MixerPosition(), "the title bar doesn't move the window anymore");
	}

	/// <summary>
	/// Inside the apps list, whose ScrollViewer takes mouse presses itself; dragging works while
	/// the position isn't remembered too, the popup just opens by the tray again
	/// </summary>
	[TestMethod]
	[TestCategory("Audio")]
	public void ThePopupIsDraggedByAnAppsNameAlsoWhenNotRemembered()
	{
		using var testApp = Later(TestAudioApp.Start());
		DragPopupBy(testApp.DisplayName, remember: false);
	}

	/// <summary>
	/// The mixer's position is kept in physical pixels: WPF's Left/Top depend on the scale of the screen the window is on, so a popup dropped
	/// on a screen at another scale than the one the hidden window starts on would open off after a restart. Needs a screen at another scale
	/// than the primary one
	/// </summary>
	[TestMethod]
	public void APopupDroppedOnAScreenAtAnotherScaleOpensThereAfterARestart() => DropOnAScreenAtAnotherScaleAndRestart(popup: true);

	[TestMethod]
	public void WindowModeMovedToAScreenAtAnotherScaleOpensThereAfterARestart() => DropOnAScreenAtAnotherScaleAndRestart(popup: false);

	private void DropOnAScreenAtAnotherScaleAndRestart(bool popup)
	{
		var monitors = Screens.All();
		double primaryScale = monitors.First(m => m.IsPrimary).Scale;
		if(monitors.FirstOrDefault(m => m.Scale != primaryScale) is not { WorkArea.Width: > 0 } other)
		{
			Assert.Inconclusive("Needs a screen at another scale than the primary one");
			return;
		}

		StartMixer(new JsonObject { ["Window"] = new JsonObject { ["PopupMode"] = popup, ["RememberPosition"] = true } });
		App.HideMixer();
		App.ShowMixer();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible), "the mixer didn't open");

		Rectangle dropped;
		using(Screens.PhysicalPixels())
		{
			var grip = Retry.WhileNull(() => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName("Apps"))),
				TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException("No \"Apps\" header in the mixer");
			var from = grip.BoundingRectangle.Center();
			var target = new Point(other.WorkArea.Left + other.WorkArea.Width / 3, other.WorkArea.Top + other.WorkArea.Height / 4);
			Mouse.Drag(from, target);
			Wait.UntilInputIsProcessed();
			Thread.Sleep(500);
			dropped = Screens.WindowRect(VolumeyApp.Handle(App.Mixer));
		}
		Assert.IsTrue(other.WorkArea.Contains(dropped.Location), $"the mixer wasn't dropped on the other screen ({dropped})");

		//exiting saves the position; the new hidden window starts on the primary screen
		App.ExitByTray();
		App.Restart();
		App.HideMixer();
		App.ShowMixer();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible), "the mixer didn't open after the restart");
		Thread.Sleep(500);
		var reopened = Screens.WindowRect(VolumeyApp.Handle(App.Mixer));
		Assert.AreEqual(dropped.Location, reopened.Location, "the mixer didn't open where it was dropped");
	}

	/// <param name="text">a text in the popup that isn't a control: where it's grabbed</param>
	/// <param name="remember">"Remember the mixer's position": the popup opens where it was dropped, else where it was before</param>
	private void DragPopupBy(string text, bool remember)
	{
		StartMixer(new JsonObject { ["Window"] = new JsonObject { ["PopupMode"] = true, ["RememberPosition"] = remember } });
		//the app shows a normal window at its first start; a second start opens the popup
		App.HideMixer();
		App.ShowMixer();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible), "the mixer didn't open");
		var before = MixerPosition();

		var grip = Retry.WhileNull(() => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName(text))),
			TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException($"No \"{text}\" in the popup");
		var from = grip.BoundingRectangle.Center();
		Mouse.Drag(from, new Point(from.X - 300, from.Y - 200));
		Wait.UntilInputIsProcessed();
		var dropped = MixerPosition();
		Assert.AreNotEqual(before, dropped, "the popup didn't move");

		App.HideMixer();
		App.ShowMixer();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible), "the popup didn't open again");
		if(remember)
			Assert.AreEqual(dropped, MixerPosition(), "the mixer didn't open where it was dropped");
		else
			Assert.AreEqual(before, MixerPosition(), "the popup didn't open by the tray again");
	}
}
