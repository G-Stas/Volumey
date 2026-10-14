using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using log4net;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.ViewModel.Settings;

public sealed class AppVolumeSyncViewModel : ObservableObject
{
	private int MasterVolume { get; set; } = 100;

	private bool isOn;
	public bool IsOn
	{
		get => isOn;
		set
		{
			if(isOn == value)
				return;
			isOn = value;
			if(isOn)
			{
				deviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
				SetDevice(deviceProvider.DefaultDevice);
			}
			else
			{
				deviceProvider.DefaultDeviceChanged -= OnDefaultDeviceChanged;
				SetDevice(null);
			}
			var general = SettingsProvider.Settings.General;
			if(general.AppVolumeSyncEnabled != value)
			{
				if(value)
					Logger.Info("Synchronising app volume to master volume on change.");
				general.AppVolumeSyncEnabled = value;
			}
			OnPropertyChanged();
		}
	}

	private readonly IDeviceProvider deviceProvider;
	private OutputDeviceModel defaultDevice;

	private static readonly ILog Logger = LogManager.GetLogger(typeof(AppVolumeSyncViewModel));

	/// <summary>
	/// Runs a queued sync later; the sync reads the device volume when it runs
	/// </summary>
	private readonly Action<Action> postSync;
	private int syncQueued;

	internal AppVolumeSyncViewModel(IDeviceProvider deviceProvider) : this(deviceProvider, null) { }

	/// <param name="postSync">Runs a queued sync; the UI thread's dispatcher at background priority by default</param>
	internal AppVolumeSyncViewModel(IDeviceProvider deviceProvider, Action<Action> postSync)
	{
		this.deviceProvider = deviceProvider;
		this.postSync = postSync ?? PostToDispatcher;
		IsOn = SettingsProvider.Settings.General.AppVolumeSyncEnabled;
	}

	//after input and rendering: the device volume changes of a slider drag that arrive meanwhile are synced once
	private static void PostToDispatcher(Action sync)
	{
		var dispatcher = Application.Current?.Dispatcher;
		if(dispatcher == null)
			sync();
		else
			dispatcher.InvokeAsync(sync, DispatcherPriority.Background);
	}

	/// <summary>
	/// Follows the volume and the new processes of the device, and stops following the previous one.
	/// </summary>
	private void SetDevice(OutputDeviceModel device)
	{
		if(defaultDevice != null)
		{
			defaultDevice.ProcessCreated -= OnProcessCreated;
			defaultDevice.Master.PropertyChanged -= OnMasterPropertyChanged;
		}
		defaultDevice = device;
		if(device != null)
		{
			MasterVolume = device.Master.Volume;
			device.ProcessCreated += OnProcessCreated;
			device.Master.PropertyChanged += OnMasterPropertyChanged;
		}
	}

	private async Task SyncSessionVolume()
	{
		if(IsOn && defaultDevice is OutputDeviceModel device)
		{
			foreach(var process in await device.GetImmutableProcessesAsync())
			{
				if(process.Volume != MasterVolume)
				{
					process.Volume = MasterVolume;
				}
			}
		}
	}

	private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
	{
		SetDevice(newDevice);
		if(newDevice != null)
			SyncSessionVolume().Forget(Logger, "Failed to sync the app volumes with the new default device");
	}

	private void OnProcessCreated(AudioProcessModel newProcess)
	{
		if(IsOn && newProcess.Volume != MasterVolume)
			newProcess.Volume = MasterVolume;
	}

	private void OnMasterPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if(sender is MasterSessionModel masterSession && e.PropertyName == nameof(MasterSessionModel.Volume))
		{
			MasterVolume = masterSession.Volume;
			//each app's volume is written once for the changes that come before the queued sync runs
			if(Interlocked.Exchange(ref syncQueued, 1) == 0)
				postSync(RunQueuedSync);
		}
	}

	private void RunQueuedSync()
	{
		Volatile.Write(ref syncQueued, 0);
		SyncSessionVolume().Forget(Logger, "Failed to sync the app volumes with the device volume");
	}
}
