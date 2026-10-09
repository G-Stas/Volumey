using System;
using System.Threading.Tasks;
using Windows.Services.Store;

namespace Volumey.Helper;

internal static class UpdateHelper
{
	internal static async Task<bool> CheckIfUpdateIsAvailable()
	{
		var sContext = StoreContext.GetDefault();
		var updates = await sContext.GetAppAndOptionalStorePackageUpdatesAsync();
		return updates.Count > 0;
	}
}
