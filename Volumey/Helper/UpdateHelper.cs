#nullable enable
using System;
using System.Reflection;
using System.Threading.Tasks;
using AutoUpdaterDotNET;
using Windows.ApplicationModel;
using Windows.Services.Store;

namespace Volumey.Helper;

internal static class UpdateHelper
{
	private const string UpdateFeed = "https://raw.githubusercontent.com/G-Stas/Volumey/main/LatestUpdateInfo.xml";
	internal const string StorePage = "ms-windows-store://pdp/?productid=9MZCQ03MX0S3";

	/// <summary>
	/// The app's version: the package's in the Store build, the assembly's otherwise
	/// </summary>
	internal static Version AppVersion
	{
		get
		{
#if(STORE)
			try
			{
				var ver = Package.Current.Id.Version;
				return new Version(ver.Major, ver.Minor, ver.Build, ver.Revision);
			}
			catch { }
#endif
			return Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
		}
	}

	/// <summary>
	/// Store build: whether the Store has an update for the app
	/// </summary>
	internal static async Task<bool> CheckIfUpdateIsAvailable()
	{
		var sContext = StoreContext.GetDefault();
		var updates = await sContext.GetAppAndOptionalStorePackageUpdatesAsync();
		return updates.Count > 0;
	}

	/// <summary>
	/// Non-Store build: AutoUpdater.NET reads the feed on GitHub and offers an update in its own window
	/// </summary>
	/// <param name="userAsked">From the About page: also says when there's no update or the check failed</param>
	internal static void StartAutoUpdater(bool userAsked)
	{
		AutoUpdater.ShowSkipButton = false;
		AutoUpdater.ReportErrors = userAsked;
		AutoUpdater.LetUserSelectRemindLater = false;
		AutoUpdater.RemindLaterTimeSpan = RemindLaterFormat.Days;
		AutoUpdater.RemindLaterAt = 1;
		AutoUpdater.Start(UpdateFeed);
	}
}
