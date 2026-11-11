using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Services;

namespace Volumey.Tests;

[TestClass]
public class SupportStoreTests
{
	[TestMethod]
	[DataRow("4.99", 4.99)]
	[DataRow("4,99", 4.99)]
	[DataRow("$4.99", 4.99)]
	[DataRow("€ 4,99", 4.99)]
	[DataRow("1,234.50", 1234.5)]
	[DataRow("1.234,50", 1234.5)]
	[DataRow("2 290", 2290)]
	[DataRow("2,290", 2290)]
	[DataRow("₸2 290", 2290)]
	[DataRow("500", 500)]
	[DataRow("9.9", 9.9)]
	public void APriceIsReadWhateverItsFormat(string text, double expected)
		=> Assert.AreEqual((decimal)expected, SupportStore.ParsePrice(text));

	[TestMethod]
	[DataRow(null)]
	[DataRow("")]
	[DataRow("free")]
	public void AnUnreadablePriceSortsLast(string text)
		=> Assert.AreEqual(decimal.MaxValue, SupportStore.ParsePrice(text));

	[TestMethod]
	public void TheTiersOfOneMarketKeepTheirPriceOrder()
	{
		string[] prices = ["9.99", "2.99", "4.99"];
		CollectionAssert.AreEqual(new[] { "2.99", "4.99", "9.99" }, prices.OrderBy(SupportStore.ParsePrice).ToArray());
	}

	private static SupportStore.StoreItem Item(string productId, string title, string price) => new(productId, "id-" + title, title, price, price);

	[TestMethod]
	public void OnlyTheDonationAddOnsAreTiers()
	{
		var tiers = SupportStore.PickTiers([Item("DonationCoffee", "Coffee", "$4.99"), Item("ThemePack", "Themes", "$1.99"), Item(null, "Unknown", "$0.99"), Item("donationThanks", "Thanks", "$2.99")]);
		CollectionAssert.AreEqual(new[] { "Thanks", "Coffee" }, tiers.Select(t => t.Title).ToArray());
	}

	[TestMethod]
	public void TiersAreOrderedByPrice()
	{
		var tiers = SupportStore.PickTiers([Item("Donation3", "Generous support", "5 400,00 ₸"), Item("Donation1", "Say thanks", "1 600,00 ₸"), Item("Donation2", "Buy a coffee", "2 675,00 ₸")]);
		CollectionAssert.AreEqual(new[] { "Say thanks", "Buy a coffee", "Generous support" }, tiers.Select(t => t.Title).ToArray());
	}

	[TestMethod]
	public void AtMostFiveTiersAreShown()
	{
		var tiers = SupportStore.PickTiers(Enumerable.Range(1, 8).Select(i => Item("Donation" + i, "Tier " + i, "$" + i)));
		Assert.AreEqual(SupportStore.MaxTiers, tiers.Count);
		Assert.AreEqual("Tier 1", tiers[0].Title);
	}

	[TestMethod]
	public void TiersOfTheSamePriceAreOrderedByProductId()
	{
		var tiers = SupportStore.PickTiers([Item("DonationB", "Alpha", "$5"), Item("DonationA", "Zulu", "$5")]);
		CollectionAssert.AreEqual(new[] { "Zulu", "Alpha" }, tiers.Select(t => t.Title).ToArray());
	}
}
