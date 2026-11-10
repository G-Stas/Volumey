#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using log4net;
using Volumey.Helper;
using Volumey.Services;

namespace Volumey.ViewModel;

/// <summary>
/// The optional donation (Store build): the heart in the mixer and the tray menu item exist only while the Store offers add-ons,
/// and open the support dialog. UI thread only
/// </summary>
public sealed partial class SupportViewModel : ObservableObject
{
	private static readonly ILog Logger = LogManager.GetLogger(typeof(SupportViewModel));

	private readonly ISupportStore store;
	private IntPtr windowHandle;

	/// <summary>
	/// The add-ons as the Store listed them last
	/// </summary>
	public ObservableCollection<SupportTier> Tiers { get; } = [];

	/// <summary>
	/// Whether there is anything to offer: false in the builds outside the Store, in markets without the add-ons and while the Store can't be reached
	/// </summary>
	[ObservableProperty]
	public partial bool IsAvailable { get; private set; }

	/// <summary>
	/// The window shows this to open the dialog
	/// </summary>
	public event Action? DialogRequested;

	/// <summary>
	/// How long to wait before each new try when the Store can't be reached; the reading stops after the last one
	/// </summary>
	private static readonly TimeSpan[] DefaultRetryDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)];

	/// <param name="read">Whether to start reading the add-ons right away, in parallel with the rest of the startup (tests call <see cref="RefreshAsync"/> or <see cref="ReadWithRetriesAsync"/>)</param>
	internal SupportViewModel(ISupportStore store, bool read = true)
	{
		this.store = store;
		if(read)
			ReadWithRetriesAsync(DefaultRetryDelays).Forget(Logger, "Failed to read the support add-ons");
	}

	/// <summary>
	/// Reads the add-ons and tries again after each delay while the Store can't be reached. A Store that answers with no add-ons isn't retried.
	/// Once they're read, reports the purchases that were left unreported. Resumes on the UI thread
	/// </summary>
	internal async Task ReadWithRetriesAsync(IReadOnlyList<TimeSpan> retryDelays)
	{
		for(int attempt = 0; ; attempt++)
		{
			try
			{
				await RefreshAsync();
			}
			catch(Exception e)
			{
				if(attempt >= retryDelays.Count)
				{
					Logger.Warn("The Store didn't return the support add-ons, giving up", e);
					return;
				}
				Logger.Info($"The Store didn't return the support add-ons, trying again in {retryDelays[attempt]}: {e.Message}");
				await Task.Delay(retryDelays[attempt]);
				continue;
			}
			//a purchase the app never reported as used up (it closed during the purchase) would keep the Store from selling that tier again
			try
			{
				var tiers = Tiers.ToList();
				await Task.Run(() => store.FulfillPendingAsync(tiers));
			}
			catch(Exception e)
			{
				Logger.Warn("Failed to report the unused support purchases", e);
			}
			return;
		}
	}

	/// <summary>
	/// The window the Store's purchase dialog opens over
	/// </summary>
	internal void Attach(IntPtr hwnd) => windowHandle = hwnd;

	internal async Task RefreshAsync()
	{
		//the Store call runs on a pool thread: it never holds the UI thread
		var tiers = await Task.Run(store.GetTiersAsync);
		Tiers.Clear();
		//the middle one (the cheaper of the two middle ones) is the suggested tier
		int suggested = (tiers.Count - 1) / 2;
		for(int i = 0; i < tiers.Count; i++)
			Tiers.Add(tiers[i] with { IsHighlighted = i == suggested });
		IsAvailable = tiers.Count > 0;
	}

	[RelayCommand]
	private void OpenDialog() => DialogRequested?.Invoke();

	/// <summary>
	/// Opens the Store's purchase dialog for the tier
	/// </summary>
	internal async Task<SupportPurchaseResult> PurchaseAsync(SupportTier tier)
	{
		SupportPurchaseResult result;
		try
		{
			result = await store.PurchaseAsync(tier, windowHandle);
		}
		catch(Exception e)
		{
			Logger.Error($"Failed to buy the add-on {tier.StoreId}", e);
			return SupportPurchaseResult.Failed;
		}
		return result;
	}
}
