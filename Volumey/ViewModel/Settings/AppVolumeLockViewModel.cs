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
/// The apps whose volume is locked (the lock modes of the mixer's app rows). An app locked until it exits is kept by its name and
/// processes, so its models made later (on another device, or when a device is read again) are locked too; an app locked always is
/// saved by its name, and each time it starts it's set to its saved volume and held there (<see cref="AudioProcessModel.LockVolume"/>).
/// An app's models on all devices are locked together. It only follows the devices' apps; while no app is locked nothing else runs.
/// </summary>
public sealed class AppVolumeLockViewModel
{
	/// <summary>An app locked until it exits: matched by its name and any of its processes, so a restarted app is free</summary>
	private sealed record SessionLock(string AppName, HashSet<uint> ProcessIds, int Volume);

	private readonly IAppRouting appRouting;
	private readonly IDeviceProvider deviceProvider;
	private readonly List<AppVolumeLockEntry> entries;
	private readonly List<SessionLock> sessionLocks = new List<SessionLock>();
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
		if(app.IsSystemSounds)
			return;
		if(FindSessionLock(app) is { } sessionLock)
		{
			sessionLock.ProcessIds.UnionWith(ProcessIdsOf(app));
			app.LockVolume(sessionLock.Volume);
		}
		else if(Find(app.Name) is { } entry)
			app.LockVolume(entry.Volume, persistent: true);
	}

	private void Unwatch(AudioProcessModel app)
	{
		if(watchedApps.Remove(app))
			app.VolumeLockRequested -= OnLockRequested;
	}

	private AppVolumeLockEntry? Find(string appName)
		=> entries.FirstOrDefault(entry => SameName(entry.AppName, appName));

	private SessionLock? FindSessionLock(AudioProcessModel app)
	{
		var processIds = ProcessIdsOf(app);
		return sessionLocks.FirstOrDefault(l => SameName(l.AppName, app.Name) && l.ProcessIds.Overlaps(processIds));
	}

	private static HashSet<uint> ProcessIdsOf(AudioProcessModel app)
		=> app.SessionsSnapshot().Select(s => s.ProcessId).Append(app.ProcessId).Where(id => id != 0).ToHashSet();

	private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// A lock mode was picked for the app: free, locked until the app exits (nothing is saved) or locked also when the app starts
	/// again (the entry is saved). A new lock holds the app's volume as it is now. Its models on the other devices follow
	/// </summary>
	private void OnLockRequested(AudioProcessModel app, VolumeLockMode mode)
	{
		//the System Sounds' name follows the language, so it can't be matched later
		if(app.IsSystemSounds)
			return;

		var models = appRouting.GetModels(app);
		if(!models.Contains(app))
			models = models.Append(app).ToList();

		int volume = app.Volume;
		sessionLocks.RemoveAll(l => SameName(l.AppName, app.Name) && models.Any(m => l.ProcessIds.Overlaps(ProcessIdsOf(m))));
		bool saved = entries.RemoveAll(entry => SameName(entry.AppName, app.Name)) > 0;
		switch(mode)
		{
			case VolumeLockMode.Session:
				sessionLocks.Add(new SessionLock(app.Name, models.SelectMany(ProcessIdsOf).ToHashSet(), volume));
				foreach(var model in models)
					model.LockVolume(volume);
				break;
			case VolumeLockMode.Persistent:
				entries.Add(new AppVolumeLockEntry { AppName = app.Name, Volume = volume });
				saved = true;
				foreach(var model in models)
					model.LockVolume(volume, persistent: true);
				break;
			default:
				foreach(var model in models)
					model.UnlockVolume();
				break;
		}
		if(saved)
			Save();
	}

	private void Save() => SettingsProvider.Settings.General.LockedAppVolumes = entries.ToArray();
}
