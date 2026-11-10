#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
#if(STORE)
using Windows.Services.Store;
#endif

namespace Volumey.Services;

/// <summary>
/// <see cref="ISupportStore"/> over the Store's Store-managed consumable add-ons (Windows.Services.Store).
/// A dev run can show fake tiers with <c>VOLUMEY_FAKE_SUPPORT=1</c> (buying one always succeeds); the other builds have no tiers
/// </summary>
internal sealed class SupportStore : ISupportStore
{
	private static readonly ILog Logger = LogManager.GetLogger(typeof(SupportStore));

	/// <summary>
	/// The product IDs of the donation add-ons start with this; the app's other add-ons are left out
	/// </summary>
	internal const string DonationPrefix = "Donation";

	/// <summary>
	/// The most tiers the dialog shows
	/// </summary>
	internal const int MaxTiers = 5;

	/// <summary>
	/// An add-on as the Store describes it: <paramref name="ProductId"/> is the ID set in Partner Center, <paramref name="StoreId"/> the one the Store made
	/// </summary>
	internal sealed record StoreItem(string? ProductId, string StoreId, string Title, string Price, string? BasePrice);

	/// <summary>
	/// The donation add-ons among the app's add-ons, cheapest first (the same price by product ID), at most <see cref="MaxTiers"/>
	/// </summary>
	internal static IReadOnlyList<SupportTier> PickTiers(IEnumerable<StoreItem> items)
		=> items
			.Where(i => i.ProductId?.StartsWith(DonationPrefix, StringComparison.OrdinalIgnoreCase) == true)
			.OrderBy(i => ParsePrice(i.BasePrice))
			.ThenBy(i => i.ProductId, StringComparer.Ordinal)
			.Take(MaxTiers)
			.Select(i => new SupportTier(i.StoreId, i.Title, i.Price))
			.ToList();

	/// <summary>
	/// The number in a price the Store wrote, whatever its currency symbol and separators ("4.99", "4,99", "1,234.50", "1.234,50", "2 290"), to order tiers by price.
	/// A last separator followed by one or two digits is the decimal point, otherwise the separators only group digits. An unreadable price sorts last
	/// </summary>
	internal static decimal ParsePrice(string? text)
	{
		if(text == null)
			return decimal.MaxValue;
		var kept = new StringBuilder();
		foreach(var c in text)
			if(char.IsDigit(c) || c is '.' or ',')
				kept.Append(c);
		var digits = kept.ToString();
		int last = digits.LastIndexOfAny(['.', ',']);
		string number;
		if(last >= 0 && digits.Length - last - 1 is 1 or 2)
			number = digits[..last].Replace(".", "").Replace(",", "") + "." + digits[(last + 1)..];
		else
			number = digits.Replace(".", "").Replace(",", "");
		return decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : decimal.MaxValue;
	}

#if(STORE)
	public async Task<IReadOnlyList<SupportTier>> GetTiersAsync()
	{
		//the Store's name for a Store-managed consumable
		var result = await StoreContext.GetDefault().GetAssociatedStoreProductsAsync(["Consumable"]);
		//a failure throws, so the view model tries again; no add-ons is an answer
		if(result.ExtendedError != null)
			throw result.ExtendedError;
		return PickTiers(result.Products.Values.Select(p =>
			new StoreItem(p.InAppOfferToken, p.StoreId, p.Title, p.Price.FormattedPrice, p.Price.FormattedBasePrice)));
	}

	public async Task<SupportPurchaseResult> PurchaseAsync(SupportTier tier, IntPtr windowHandle)
	{
		var context = StoreContext.GetDefault();
		//a desktop app has to tell the Store which window to show its dialog over
		WinRT.Interop.InitializeWithWindow.Initialize(context, windowHandle);
		var result = await context.RequestPurchaseAsync(tier.StoreId);
		if(result.ExtendedError != null)
			Logger.Warn($"The Store's purchase dialog for {tier.StoreId} ended with {result.Status}", result.ExtendedError);
		switch(result.Status)
		{
			case StorePurchaseStatus.Succeeded:
				await FulfillAsync(context, tier.StoreId);
				return SupportPurchaseResult.Succeeded;
			case StorePurchaseStatus.AlreadyPurchased:
				//an earlier purchase wasn't used up; it is now, so the next click can buy
				await FulfillAsync(context, tier.StoreId);
				return SupportPurchaseResult.Failed;
			case StorePurchaseStatus.NotPurchased:
				return SupportPurchaseResult.Cancelled;
			default:
				return SupportPurchaseResult.Failed;
		}
	}

	public async Task FulfillPendingAsync(IReadOnlyList<SupportTier> tiers)
	{
		var context = StoreContext.GetDefault();
		foreach(var tier in tiers)
			await FulfillAsync(context, tier.StoreId);
	}

	//there is nothing to hand over for a donation: the balance is reported as used up at once
	private static async Task FulfillAsync(StoreContext context, string storeId)
	{
		var balance = await context.GetConsumableBalanceRemainingAsync(storeId);
		if(balance.Status != StoreConsumableStatus.Succeeded)
		{
			Logger.Warn($"The Store didn't return the balance of {storeId}: {balance.Status}", balance.ExtendedError);
			return;
		}
		if(balance.BalanceRemaining == 0)
			return;
		var report = await context.ReportConsumableFulfillmentAsync(storeId, balance.BalanceRemaining, Guid.NewGuid());
		if(report.Status != StoreConsumableStatus.Succeeded)
			Logger.Warn($"The Store didn't accept the fulfillment of {storeId}: {report.Status}", report.ExtendedError);
	}
#elif(DEBUG)
	public Task<IReadOnlyList<SupportTier>> GetTiersAsync()
		=> Task.FromResult<IReadOnlyList<SupportTier>>(Environment.GetEnvironmentVariable("VOLUMEY_FAKE_SUPPORT") == "long"
			? [new("1", "A very long tier name that goes on and on to see how it wraps", "₸12 345,67"), new("2", "Großzügige Unterstützung für die Entwicklung von Volumey", "€1.234,50"), new("3", "Short", "$9.99")]
			: Environment.GetEnvironmentVariable("VOLUMEY_FAKE_SUPPORT") == "1"
			? [new("1", "Supporter 1", "$2.99"), new("2", "Supporter 2", "$4.99"), new("3", "Supporter 3", "$9.99")]
			: []);

	public Task<SupportPurchaseResult> PurchaseAsync(SupportTier tier, IntPtr windowHandle)
		=> Task.FromResult(SupportPurchaseResult.Succeeded);

	public Task FulfillPendingAsync(IReadOnlyList<SupportTier> tiers) => Task.CompletedTask;
#else
	public Task<IReadOnlyList<SupportTier>> GetTiersAsync() => Task.FromResult<IReadOnlyList<SupportTier>>([]);

	public Task<SupportPurchaseResult> PurchaseAsync(SupportTier tier, IntPtr windowHandle) => Task.FromResult(SupportPurchaseResult.Failed);

	public Task FulfillPendingAsync(IReadOnlyList<SupportTier> tiers) => Task.CompletedTask;
#endif
}
