#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// The apps whose volume is locked (the lock modes of the mixer's app rows). An app held now is kept with its lock, so its models made
/// later (on another device, or when a device is read again) are held the same way; an app locked always is also saved by its name, and
/// each time it starts it's held at its saved volume (<see cref="AppVolumeLock.Hold"/>). An app's models on all devices are locked
/// together. It only follows the devices' apps; while no app is locked nothing else runs.
/// </summary>
public sealed class AppVolumeLockViewModel
{
	/// <summary>
	/// An app held now: matched by its name, its executable and any of its processes, so a restarted app is a new one. A model of the app
	/// made later adds its processes
	/// </summary>
	private sealed class HeldApp(AudioProcessModel app, IEnumerable<uint> processIds, VolumeLock volumeLock)
	{
		public string AppName { get; } = app.Name;

		/// <summary>The executable's full path, null when it isn't known</summary>
		public string? FilePath { get; } = Path.IsPathFullyQualified(app.FilePath) ? app.FilePath : null;

		public HashSet<uint> ProcessIds { get; } = processIds.ToHashSet();
		public VolumeLock Lock { get; } = volumeLock;

		public bool Matches(AudioProcessModel model, uint[] processIds)
			=> SameName(AppName, model.Name)
			   && string.Equals(FilePath, Path.IsPathFullyQualified(model.FilePath) ? model.FilePath : null, StringComparison.OrdinalIgnoreCase)
			   && ProcessIds.Overlaps(processIds);
	}

	private readonly IAppRouting appRouting;
	private readonly IDeviceProvider deviceProvider;
	private readonly Func<uint, string?, bool> isProcessRunning;
	private readonly List<AppVolumeLockEntry> entries;
	private readonly List<HeldApp> heldApps = new List<HeldApp>();
	private readonly HashSet<OutputDeviceModel> watchedDevices = new HashSet<OutputDeviceModel>();
	private readonly HashSet<AudioProcessModel> watchedApps = new HashSet<AudioProcessModel>();

	/// <param name="isProcessRunning">Whether a process runs, by its ID and its executable's path (null when it isn't known): an app's
	/// lock is dropped once none of its processes does</param>
	internal AppVolumeLockViewModel(IDeviceProvider deviceProvider, IAppRouting appRouting, Func<uint, string?, bool>? isProcessRunning = null)
	{
		this.deviceProvider = deviceProvider;
		this.appRouting = appRouting;
		this.isProcessRunning = isProcessRunning ?? ((id, path) => NativeMethods.IsProcessRunning((int)id, path));
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
			app.Lock.Release(restore: true);
		heldApps.Clear();
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
		app.Lock.Requested += OnLockRequested;
		if(app.IsSystemSounds)
			return;
		var processIds = app.ProcessIds();
		if(heldApps.FirstOrDefault(held => held.Matches(app, processIds)) is { } heldApp)
		{
			heldApp.ProcessIds.UnionWith(processIds);
			app.Lock.Hold(heldApp.Lock);
		}
		else if(entries.FirstOrDefault(entry => SameName(entry.AppName, app.Name)) is { } entry)
		{
			var volumeLock = new VolumeLock(VolumeLockMode.Always, entry.Volume, app.Volume);
			heldApps.Add(new HeldApp(app, processIds, volumeLock));
			app.Lock.Hold(volumeLock);
		}
	}

	private void Unwatch(AudioProcessModel app)
	{
		if(!watchedApps.Remove(app))
			return;
		app.Lock.Requested -= OnLockRequested;
		//the app exited, or its model is made again (then its processes still run)
		heldApps.RemoveAll(held => !held.ProcessIds.Any(id => isProcessRunning(id, held.FilePath)));
	}

	private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// A lock mode was picked for the app: free, locked until the app exits (nothing is saved) or locked also when the app starts
	/// again (the entry is saved). A new lock holds the app's volume as it is now; an app held already keeps the volume it gets back on
	/// exit. Its models on the other devices follow
	/// </summary>
	private void OnLockRequested(AudioProcessModel app, VolumeLockMode mode)
	{
		//the System Sounds' name follows the language, so it can't be matched later
		if(app.IsSystemSounds)
			return;

		var models = appRouting.GetModels(app);
		if(!models.Contains(app))
			models = models.Append(app).ToList();
		var processIds = models.SelectMany(m => m.ProcessIds()).Distinct().ToArray();

		int volumeBefore = heldApps.FirstOrDefault(held => held.Matches(app, processIds))?.Lock.VolumeBefore ?? app.Volume;
		heldApps.RemoveAll(held => held.Matches(app, processIds));
		bool saved = entries.RemoveAll(entry => SameName(entry.AppName, app.Name)) > 0;
		if(mode == VolumeLockMode.Off)
		{
			foreach(var model in models)
				model.Lock.Release();
		}
		else
		{
			var volumeLock = new VolumeLock(mode, app.Volume, volumeBefore);
			heldApps.Add(new HeldApp(app, processIds, volumeLock));
			if(mode == VolumeLockMode.Always)
			{
				entries.Add(new AppVolumeLockEntry { AppName = app.Name, Volume = volumeLock.Volume });
				saved = true;
			}
			foreach(var model in models)
				model.Lock.Hold(volumeLock);
		}
		if(saved)
			Save();
	}

	private void Save() => SettingsProvider.Settings.General.LockedAppVolumes = entries.ToArray();
}
