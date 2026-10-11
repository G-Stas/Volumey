using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using log4net;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Localization;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// The About page: the version and build, the update check, and the links (the support links come from GitHub, <see cref="DataProvider.SupportLinks"/>)
/// </summary>
public partial class AboutViewModel : ObservableObject
{
	internal const string SourceCodeUrl = "https://github.com/G-Stas/Volumey";
	internal const string IssuesUrl = "https://github.com/G-Stas/Volumey/issues";

	private static readonly ILog Logger = LogManager.GetLogger(typeof(AboutViewModel));

	private readonly SupportLinks supportLinks;

	public string Version { get; } = UpdateHelper.AppVersion.ToString();

	/// <summary>
	/// The localization key of the build's name
	/// </summary>
	public string BuildKey { get; } =
#if(STORE)
		"About_StoreBuild";
#elif(DEBUG)
		"About_DevBuild";
#else
		"About_GitHubBuild";
#endif

	/// <summary>
	/// The build's name in the current language ("GitHub release", "Microsoft Store")
	/// </summary>
	public string BuildName => TranslationSource.Instance[BuildKey];

	public ObservableCollection<SupportLink> SupportLinks => supportLinks.Links;

	/// <summary>
	/// What the update check found (Store build; the GitHub build's updater has its own window), empty otherwise
	/// </summary>
	[ObservableProperty]
	public partial string UpdateStatus { get; private set; } = string.Empty;

	private string updateStatusKey;

	internal AboutViewModel(SupportLinks supportLinks)
	{
		this.supportLinks = supportLinks;
		supportLinks.RefreshAsync().Forget(Logger, "Failed to read the support links");
		TranslationSource.Instance.PropertyChanged += (_, _) =>
		{
			SetUpdateStatus(updateStatusKey);
			OnPropertyChanged(nameof(BuildName));
		};
	}

	[RelayCommand]
	private async Task CheckForUpdates()
	{
#if(STORE)
		SetUpdateStatus("About_Checking");
		try
		{
			if(await UpdateHelper.CheckIfUpdateIsAvailable())
			{
				SetUpdateStatus("About_UpdateInStore");
				await Windows.System.Launcher.LaunchUriAsync(new Uri(UpdateHelper.StorePage));
			}
			else
				SetUpdateStatus("About_UpToDate");
		}
		catch(Exception e)
		{
			Logger.Error("Failed to check for an update", e);
			SetUpdateStatus("About_UpdateFailed");
		}
#else
		try
		{
			UpdateHelper.StartAutoUpdater(userAsked: true);
		}
		catch(Exception e)
		{
			Logger.Error("Failed to check for an update", e);
			SetUpdateStatus("About_UpdateFailed");
		}
		await Task.CompletedTask;
#endif
	}

	private void SetUpdateStatus(string key)
	{
		updateStatusKey = key;
		UpdateStatus = key == null ? string.Empty : TranslationSource.Instance[key];
	}

	[RelayCommand]
	private Task OpenSourceCode() => OpenWebPage(SourceCodeUrl);

	[RelayCommand]
	private Task OpenIssues() => OpenWebPage(IssuesUrl);

	[RelayCommand]
	private Task OpenSupportLink(SupportLink link) => OpenWebPage(link?.Url);

	internal static async Task OpenWebPage(string url)
	{
		if(string.IsNullOrEmpty(url))
			return;
		try
		{
			await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
		}
		catch(Exception e)
		{
			Logger.Warn($"Couldn't open {url}", e);
		}
	}
}
