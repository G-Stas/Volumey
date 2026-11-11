using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The Store build's support heart in the mixer and its dialog; the Debug build shows fake tiers with VOLUMEY_FAKE_SUPPORT
/// (Supporter 1 to 3). The Store's own purchase window isn't covered
/// </summary>
[TestClass]
public class SupportTests : UiTest
{
	private static readonly IDictionary<string, string> FakeTiers = new Dictionary<string, string> { ["VOLUMEY_FAKE_SUPPORT"] = "1" };

	//long names and prices in other formats
	private static readonly IDictionary<string, string> LongTiers = new Dictionary<string, string> { ["VOLUMEY_FAKE_SUPPORT"] = "long" };

	private AutomationElement? Heart() => App.Mixer.FindFirstDescendant(cf => cf.ByAutomationId("SupportButton"));

	private AutomationElement? Text(string name) => App.Mixer.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName(name)));

	private AutomationElement? CloseButton() => App.Mixer.FindFirstDescendant(cf => cf.ByAutomationId("CloseButton"));

	private void OpenDialog()
	{
		var heart = Retry.WhileNull(Heart, TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException("No heart in the mixer footer");
		heart.AsButton().Invoke();
		Assert.IsNotNull(Retry.WhileNull(() => Text("Support Volumey"), TimeSpan.FromSeconds(5)).Result, "No support dialog");
	}

	private AutomationElement? Tier(string title)
		=> App.Mixer.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault(b => b.Name.Contains(title));

	[TestMethod]
	public void WithoutAddOnsThereIsNoHeart()
	{
		StartMixer();
		Assert.IsTrue(App.MixerIsVisible, "the mixer didn't open");
		Assert.IsNull(Heart(), "the heart shows although the Store offers no add-ons");
	}

	[TestMethod]
	public void TheHeartOpensTheDialogWithTheTiersAndNotNowClosesIt()
	{
		StartMixer(environment: FakeTiers);
		var heart = Retry.WhileNull(Heart, TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException("No heart in the mixer footer");
		heart.AsButton().Invoke();

		Assert.IsNotNull(Retry.WhileNull(() => Text("Support Volumey"), TimeSpan.FromSeconds(5)).Result, "No support dialog title");
		foreach(var title in new[] { "Supporter 1", "Supporter 2", "Supporter 3" })
			Assert.IsTrue(Tier(title)?.IsEnabled, $"{title} can't be bought");
		Assert.IsNotNull(Text("$4.99"), "the Store's price isn't shown");

		var notNow = App.Mixer.FindFirstDescendant(cf => cf.ByAutomationId("CloseButton")) ?? throw new AssertFailedException("No Not now button");
		notNow.AsButton().Invoke();
		Assert.IsTrue(Eventually(() => Text("Support Volumey") == null), "Not now didn't close the dialog");
	}

	[TestMethod]
	public void TheDialogFitsThePopup()
	{
		StartMixer(new System.Text.Json.Nodes.JsonObject { ["Window"] = new System.Text.Json.Nodes.JsonObject { ["PopupMode"] = true } }, FakeTiers);
		App.HideMixer();
		App.ShowMixer();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible), "the popup didn't open");
		var heart = Retry.WhileNull(Heart, TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException("No heart in the popup");
		heart.AsButton().Invoke();

		var text = Retry.WhileNull(() => Text("Support Volumey"), TimeSpan.FromSeconds(5)).Result ?? throw new AssertFailedException("No support dialog");
		var price = Text("$4.99") ?? throw new AssertFailedException("No price");
		var notNow = App.Mixer.FindFirstDescendant(cf => cf.ByAutomationId("CloseButton")) ?? throw new AssertFailedException("No Not now button");
		//the prices stay inside the dialog: they aren't cut off at the popup's edge
		Assert.IsTrue(price.BoundingRectangle.Right <= notNow.BoundingRectangle.Right,
			$"the price ({price.BoundingRectangle}) runs past the dialog's buttons ({notNow.BoundingRectangle})");
		Assert.IsTrue(text.BoundingRectangle.Left >= App.Mixer.BoundingRectangle.Left);
	}

	[TestMethod]
	public void TheTrayItemOpensTheHiddenMixerWithTheDialog()
	{
		StartMixer(environment: FakeTiers);
		Assert.IsNotNull(Retry.WhileNull(Heart, TimeSpan.FromSeconds(5)).Result, "the add-ons weren't read");
		App.HideMixer();

		App.TrayMenuItem("Support Volumey").Patterns.Invoke.Pattern.Invoke();
		Assert.IsTrue(Eventually(() => App.MixerIsVisible, 5000), "the tray item didn't open the mixer");
		Assert.IsNotNull(Retry.WhileNull(() => Text("Support Volumey"), TimeSpan.FromSeconds(5)).Result, "the tray item didn't open the dialog");
		Assert.IsNotNull(Tier("Supporter 2"), "the dialog has no tiers");
	}

	[TestMethod]
	public void WithoutAddOnsTheTrayMenuHasNoSupportItem()
	{
		StartMixer();
		//opens the menu
		App.TrayMenuItem("Open mixer");
		var support = App.FindOpenMenuItem("Support Volumey");
		Keyboard.Press(VirtualKeyShort.ESCAPE);
		Assert.IsNull(support, "the tray menu offers support although the Store has no add-ons");
	}

	[TestMethod]
	public void BuyingATierTurnsTheDialogIntoAThankYou()
	{
		StartMixer(environment: FakeTiers);
		OpenDialog();
		(Tier("Supporter 2") ?? throw new AssertFailedException("No tier")).AsButton().Invoke();

		Assert.IsNotNull(Retry.WhileNull(() => Text("Thank you!"), TimeSpan.FromSeconds(5)).Result, "no thank-you after the purchase");
		Assert.IsNull(Tier("Supporter 1"), "the tiers are still shown after the purchase");
		var close = CloseButton() ?? throw new AssertFailedException("No close button");
		Assert.AreEqual("Close", close.Name);

		close.AsButton().Invoke();
		Assert.IsTrue(Eventually(() => Text("Thank you!") == null), "Close didn't close the dialog");
		//the next time it offers the tiers again
		OpenDialog();
		Assert.IsNotNull(Tier("Supporter 1"), "the tiers aren't offered again");
	}

	[TestMethod]
	public void LongNamesWrapAndThePricesStayInside()
	{
		StartMixer(environment: LongTiers);
		OpenDialog();
		var notNow = CloseButton() ?? throw new AssertFailedException("No Not now button");
		foreach(var price in new[] { "₸12 345,67", "€1.234,50", "$9.99" })
		{
			var text = Text(price) ?? throw new AssertFailedException($"No price {price}");
			Assert.IsTrue(text.BoundingRectangle.Right <= notNow.BoundingRectangle.Right,
				$"{price} ({text.BoundingRectangle}) runs past the dialog ({notNow.BoundingRectangle})");
		}
		var name = Text("A very long tier name that goes on and on to see how it wraps") ?? throw new AssertFailedException("No long name");
		var itsPrice = Text("₸12 345,67")!;
		Assert.IsTrue(name.BoundingRectangle.Right <= itsPrice.BoundingRectangle.Left, "the long name runs into its price");
		Assert.IsTrue(name.BoundingRectangle.Height > itsPrice.BoundingRectangle.Height * 1.5, "the long name doesn't wrap");
	}
}
