#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Model;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// The apps whose volume is locked (the lock button of the mixer's app rows), saved by app name: each app that starts is set to its
/// saved volume and held there until it exits (<see cref="AudioProcessModel.LockVolume"/>). An app's models on all devices are
/// locked together. It only follows the devices' apps; while no app is locked nothing else runs.
/// </summary>
public sealed class AppVolumeLockViewModel
{
	private readonly IAppRouting appRouting;
	private readonly IDeviceProvider deviceProvider;
	private readonly List<AppVolumeLockEntry> entries;
	private readonly HashSet<OutputDeviceModel> watchedDevices = new HashSet<OutputDeviceModel>();
	private readonly HashSet<AudioProcessModel> watchedApps = new HashSet<AudioProcessModel>();

	internal AppVolumeLockViewModel(IDeviceProvider deviceProvider, IAppRouting appRouting)
	{
		this.deviceProvider = deviceProvider;
		this.appRouting = appRouting;
		entries = SettingsProvider.Settings.General.LockedAppVolumes.ToList();

		deviceProvider.ActiveDevices.CollectionChanged += OnDevicesChanged;
		foreach(var device in deviceProvider.ActiveDevices.ToList())
			Watch(device);
	}

	/// <summary>
	/// Volumey is closing: every locked app gets back the volume it had before it was locked. The saved locks are kept, so they are
	/// applied again the next time the app starts
	/// </summary>
	public void ReleaseAll()
	{
		foreach(var app in watchedApps.ToList())
			app.RestoreVolumeBeforeLock();
	}

	//UI thread
	private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if(e.Action == NotifyCollectionChangedAction.Reset)
		{
			foreach(var device in watchedDevices.Where(d => !deviceProvider.ActiveDevices.Contains(d)).ToList())
				Unwatch(device);
			foreach(var device in deviceProvider.ActiveDevices.ToList())
				Watch(device);
			return;
		}
		foreach(var device in e.OldItems?.OfType<OutputDeviceModel>() ?? [])
			Unwatch(device);
		foreach(var device in e.NewItems?.OfType<OutputDeviceModel>() ?? [])
			Watch(device);
	}

	private void Watch(OutputDeviceModel device)
	{
		if(!watchedDevices.Add(device))
			return;
		device.Processes.CollectionChanged += OnProcessesChanged;
		foreach(var app in device.Processes.ToList())
			Watch(app);
	}

	private void Unwatch(OutputDeviceModel device)
	{
		if(!watchedDevices.Remove(device))
			return;
		device.Processes.CollectionChanged -= OnProcessesChanged;
		foreach(var app in device.Processes.ToList())
			Unwatch(app);
	}

	//UI thread: an app started or exited on a device
	private void OnProcessesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		foreach(var app in e.OldItems?.OfType<AudioProcessModel>() ?? [])
			Unwatch(app);
		foreach(var app in e.NewItems?.OfType<AudioProcessModel>() ?? [])
			Watch(app);
	}

	private void Watch(AudioProcessModel app)
	{
		if(!watchedApps.Add(app))
			return;
		app.VolumeLockRequested += OnLockRequested;
		if(!app.IsSystemSounds && Find(app.Name) is { } entry)
			app.LockVolume(entry.Volume, persistent: true);
	}

	private void Unwatch(AudioProcessModel app)
	{
		if(watchedApps.Remove(app))
			app.VolumeLockRequested -= OnLockRequested;
	}

	private AppVolumeLockEntry? Find(string appName)
		=> entries.FirstOrDefault(entry => string.Equals(entry.AppName, appName, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// The lock button goes through three states: free, locked until the app exits (nothing is saved) and locked also when the app
	/// starts again (the entry is saved). Its models on the other devices follow
	/// </summary>
	private void OnLockRequested(AudioProcessModel app)
	{
		//the System Sounds' name follows the language, so it can't be matched later
		if(app.IsSystemSounds)
			return;

		var models = appRouting.GetModels(app);
		if(!models.Contains(app))
			models = models.Append(app).ToList();

		switch(app.LockMode)
		{
			case VolumeLockMode.None:
			{
				int volume = app.Volume;
				foreach(var model in models)
					model.LockVolume(volume);
				break;
			}
			case VolumeLockMode.Session:
			{
				int volume = app.Volume;
				entries.RemoveAll(entry => string.Equals(entry.AppName, app.Name, StringComparison.OrdinalIgnoreCase));
				entries.Add(new AppVolumeLockEntry { AppName = app.Name, Volume = volume });
				foreach(var model in models)
					model.LockVolume(volume, persistent: true);
				Save();
				break;
			}
			default:
			{
				entries.RemoveAll(entry => string.Equals(entry.AppName, app.Name, StringComparison.OrdinalIgnoreCase));
				foreach(var model in models)
					model.UnlockVolume();
				Save();
				break;
			}
		}
	}

	private void Save() => SettingsProvider.Settings.General.LockedAppVolumes = entries.ToArray();
}
