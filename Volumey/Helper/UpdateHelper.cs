using System;
using Windows.Services.Store;
using System.Threading.Tasks;

namespace Volumey.Helper
{
	internal static class UpdateHelper
	{
		internal static async Task<bool> CheckIfUpdateIsAvailable()
		{
			var sContext = StoreContext.GetDefault();
			var updates = await sContext.GetAppAndOptionalStorePackageUpdatesAsync();
			return updates.Count > 0;
		}
		
		internal static bool IsWindows11()
			=> Environment.OSVersion.Version.Build >= 22000;
	}
}