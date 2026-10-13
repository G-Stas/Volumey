using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The Store build's update dialog in the mixer, shown by the Debug build's VOLUMEY_SHOW_UPDATE_DIALOG
/// </summary>
[TestClass]
public class UpdateDialogTests : UiTest
{
	private const string Message = "An update is available. Do you want to update the app?";

	private AutomationElement? Find(string? automationId = null, string? name = null)
		=> App.Mixer.FindFirstDescendant(cf => automationId != null ? cf.ByAutomationId(automationId) : cf.ByControlType(ControlType.Text).And(cf.ByName(name!)));

	[TestMethod]
	public void TheUpdateDialogFitsThePopupAndLaterClosesIt()
	{
		StartMixer(new JsonObject { ["Window"] = new JsonObject { ["PopupMode"] = true } },
			new Dictionary<string, string> { ["VOLUMEY_SHOW_UPDATE_DIALOG"] = "1" });
		//the dialog opens in the window shown first; it stays in the mixer when it opens again as the (narrower) popup
		App.HideMixer();
		App.ShowMixer();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible), "the popup didn't open");

		var text = Retry.WhileNull(() => Find(name: Message), TimeSpan.FromSeconds(5)).Result
		           ?? throw new AssertFailedException("No update dialog in the mixer");
		var later = Find("CloseButton") ?? throw new AssertFailedException("No Later button");
		Assert.IsNotNull(Find("PrimaryButton"), "No OK button");
		//its text had a fixed width that ran past the dialog in the popup (the text was cut off)
		Assert.IsTrue(text.BoundingRectangle.Right <= later.BoundingRectangle.Right,
			$"the text ({text.BoundingRectangle}) runs past the dialog's buttons ({later.BoundingRectangle})");

		later.AsButton().Invoke();
		Assert.IsTrue(Eventually(() => Find(name: Message) == null), "Later didn't close the dialog");
	}
}
