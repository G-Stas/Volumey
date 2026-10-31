#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Volumey.DataProvider;
using Volumey.Localization;
using Volumey.Model;
using Volumey.Services;

namespace Volumey.ViewModel;

public sealed partial class DeviceProviderViewModel : ObservableObject
{
	[ObservableProperty]
	public partial OutputDeviceModel? SelectedDevice { get; set; }

	[ObservableProperty]
	public partial OutputDeviceModel? DefaultDevice { get; set; }

	public IDeviceProvider DeviceProvider { get; }

	private readonly IAppRouting? appRouting;

	/// <summary>The notice at the bottom of the mixer after a move; null when none is shown</summary>
	[ObservableProperty]
	public partial MixerNotice? Notice { get; set; }

	private Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

	internal DeviceProviderViewModel(IDeviceProvider deviceProvider, IAppShell shell, IAppRouting? appRouting = null)
	{
		DeviceProvider = deviceProvider;
		this.appRouting = appRouting;
		DeviceProvider.DeviceDisabled += OnDeviceDisabled;
		DeviceProvider.DefaultDeviceChanged += OnDefaultDeviceSwitched;
		DeviceProvider.DeviceReplaced += OnDeviceReplaced;

		if(!DeviceProvider.NoOutputDevices)
			SelectedDevice = DefaultDevice = DeviceProvider.DefaultDevice;

		shell.Exit += () => DeviceProvider.Dispose();
	}

	private void OnDefaultDeviceSwitched(OutputDeviceModel? newDevice)
	{
		//update default device property and selected device property if default device was the selected device in app
		dispatcher.Invoke(() =>
		{
			if(SelectedDevice == DefaultDevice)
			{
				SelectedDevice = DefaultDevice = newDevice;
				return;
			}
			DefaultDevice = newDevice;
		});
	}

	private void OnDeviceReplaced(OutputDeviceModel oldDevice, OutputDeviceModel newDevice)
	{
		//the default device is handled by OnDefaultDeviceSwitched, which comes first
		dispatcher.Invoke(() =>
		{
			if(SelectedDevice == oldDevice)
				SelectedDevice = newDevice;
		});
	}

	private void OnDeviceDisabled(OutputDeviceModel device)
	{
		dispatcher.Invoke(() =>
		{
			if(SelectedDevice == device)
			{
				if(device != DefaultDevice)
					SelectedDevice = DefaultDevice;

				//set both properties to null
				//if defaultDevice was the selected device in application
				//and OnDeviceProviderPropertyChanged event handler will set reference of a new default device object to both properties
				else
					SelectedDevice = DefaultDevice = null;
			}
		});
	}

	/// <summary>Whether apps can be moved to other devices (asks Windows on the first call)</summary>
	public bool CanMoveApps => appRouting?.IsAvailable == true;

	/// <summary>The items of the app's device box and the index of its current choice</summary>
	internal (IReadOnlyList<AppOutputOption> Options, int Selected) OutputOptionsOf(AudioProcessModel app)
	{
		var options = AppOutputOptions.Build(DeviceProvider.ActiveDevices.Select(d => (d.Id, d.Master.Name)).ToList(), DefaultDeviceName,
			app.Route, app.RouteDeviceName);
		return (options, AppOutputOptions.IndexOf(options, app.Route));
	}

	private string DefaultDeviceName => DeviceProvider.DefaultDevice?.Master.Name ?? string.Empty;

	//the last move with Undo: the app and where it was set to play before (the device's name for a failure notice)
	private (AudioProcessModel App, AppRoute Previous, string PreviousName)? lastMove;

	/// <summary>
	/// Moves the app to the picked device, unless it's the current choice, and shows the notice for the outcome
	/// </summary>
	internal async Task PickOutputAsync(AudioProcessModel app, AppOutputOption option)
	{
		AppRoute to = option.DeviceId == null ? new AppRoute.FollowsDefault() : new AppRoute.Pinned(option.DeviceId);
		if(appRouting == null || to == app.Route)
			return;
		string targetName = option.DeviceId == null ? option.Detail ?? string.Empty : option.Name;
		string previousName = app.Route is AppRoute.Pinned ? app.RouteDeviceName ?? string.Empty : DefaultDeviceName;
		switch(await appRouting.MoveAsync(app, to))
		{
			case MoveOutcome.Moved moved:
				lastMove = (app, moved.Previous, previousName);
				Notice = new MixerNotice(Format("Mixer_MovedTo", app.Name, targetName), canUndo: true);
				break;
			case MoveOutcome.Refused:
				Notice = new MixerNotice(Format("Mixer_MoveFailed", app.Name, targetName), canUndo: false);
				break;
			//a device that left is no longer in the list; an app that exited has no row
		}
	}

	/// <summary>Moves the app of the last move back to where it was set to play before</summary>
	[RelayCommand]
	private async Task UndoMoveAsync()
	{
		if(appRouting == null || lastMove is not var (app, previous, previousName))
			return;
		lastMove = null;
		Notice = null;
		switch(await appRouting.MoveAsync(app, previous))
		{
			case MoveOutcome.Refused or MoveOutcome.DeviceGone:
				Notice = new MixerNotice(Format("Mixer_MoveFailed", app.Name, previousName), canUndo: false);
				break;
		}
	}

	private static string Format(string key, params object[] values)
		=> string.Format(TranslationSource.Instance[key] ?? string.Empty, values);

	/// <summary>Reads every app's route again: Windows tells nobody about changes made outside Volumey</summary>
	internal void OnMixerOpened() => appRouting?.RefreshAll();

	/// <summary>Shows the default device in the mixer, e.g. when its device picker is hidden</summary>
	internal void ShowDefaultDevice() => SelectedDevice = DefaultDevice;

	[RelayCommand]
	private void SetDefaultDevice()
	{
		if(SelectedDevice == null)
			return;
		DeviceProvider.SetDefaultDevice(SelectedDevice.Id);
	}

	[RelayCommand]
	private void SetDefaultDeviceTray(object param)
	{
		if(param is OutputDeviceModel model)
			SetAsDefaultDevice(model.Id);
	}

	private void SetAsDefaultDevice(string deviceId)
	{
		if(string.IsNullOrEmpty(deviceId))
			return;

		if(DeviceProvider.DefaultDevice != null && DeviceProvider.DefaultDevice.CompareId(deviceId))
			return;

		DeviceProvider.SetDefaultDevice(deviceId);
	}

	/// <summary>
	/// Switches the default output device to the one after it in the active devices list. It counts from a switch
	/// Windows hasn't reported yet, so quick repeats keep moving on
	/// </summary>
	internal void SwitchToNextDevice()
	{
		var activeDevices = DeviceProvider.ActiveDevices;
		if(activeDevices.Count <= 1)
			return;

		string? currentId = DeviceProvider.RequestedDefaultDeviceId ?? DeviceProvider.DefaultDevice?.Id;
		var current = currentId == null ? null : activeDevices.FirstOrDefault(d => d.CompareId(currentId));
		//IndexOf(null) is -1: without a known default device the first one is next
		int nextIndex = (activeDevices.IndexOf(current!) + 1) % activeDevices.Count;
		SetAsDefaultDevice(activeDevices[nextIndex].Id);
	}
}
