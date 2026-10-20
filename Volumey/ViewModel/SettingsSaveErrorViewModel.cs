#nullable enable
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Volumey.DataProvider.Settings;

namespace Volumey.ViewModel;

/// <summary>
/// The settings window's error bar: shown while the settings file can't be written, again when the reason changes after it was closed
/// </summary>
public sealed partial class SettingsSaveErrorViewModel : ObservableObject
{
	public SettingsSaveErrorViewModel(Dispatcher dispatcher)
	{
		Show(SettingsSaveStatus.Current);
		SettingsSaveStatus.Changed += failure => dispatcher.InvokeAsync(() => Show(failure));
	}

	[ObservableProperty]
	public partial SaveFailure Failure { get; private set; }

	[ObservableProperty]
	public partial bool IsOpen { get; set; }

	private void Show(SaveFailure failure)
	{
		Failure = failure;
		IsOpen = failure != SaveFailure.None;
	}
}
