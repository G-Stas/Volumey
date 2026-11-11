#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Volumey.Services;

/// <summary>
/// One donation add-on of the Store listing. The title and the price come from the Store in the user's language and currency
/// </summary>
public sealed record SupportTier(string StoreId, string Title, string Price)
{
	/// <summary>
	/// The suggested tier: the dialog shows it as the accent button
	/// </summary>
	public bool IsHighlighted { get; init; }
}

public enum SupportPurchaseResult
{
	Succeeded,
	Cancelled,
	Failed
}

/// <summary>
/// The optional donation add-ons (Store-managed consumables, so a tier can be bought again). Only the Store build has any: elsewhere there are no tiers
/// </summary>
internal interface ISupportStore
{
	/// <summary>
	/// The add-ons the Store offers this user, cheapest first. Empty when the Store has none or the build isn't from the Store; throws when the Store can't be reached
	/// </summary>
	Task<IReadOnlyList<SupportTier>> GetTiersAsync();

	/// <summary>
	/// Opens the Store's purchase dialog over the window and, after a purchase, reports it as used up so the tier can be bought again. UI thread only
	/// </summary>
	Task<SupportPurchaseResult> PurchaseAsync(SupportTier tier, IntPtr windowHandle);

	/// <summary>
	/// Reports purchases that were never reported as used up (the app was closed during a purchase), which the Store would otherwise count as owned and not sell again
	/// </summary>
	Task FulfillPendingAsync(IReadOnlyList<SupportTier> tiers);
}
