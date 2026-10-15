#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Model;
using Volumey.Services;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// Volume hotkeys for single apps, stored by process name (App hotkeys page). The hotkeys are registered while the app runs
/// on the default device. <see cref="SelectedProcess"/>, <see cref="VolumeUp"/>, <see cref="VolumeDown"/> and <see cref="AddErrors"/>
/// are the "Add app" dialog's; each saved app is an <see cref="AppHotkeysItem"/> whose hotkeys can be changed in place.
/// </summary>
public sealed partial class AppsHotkeysViewModel : HotkeyViewModel
{
	/// <summary>For the hotkey boxes of the Add app dialog</summary>
	public IHotkeyService Hotkeys => hotkeys;

	[ObservableProperty]
	public partial AudioProcessModel? SelectedProcess { get; set; }

	[ObservableProperty]
	public partial HotKey? VolumeUp { get; set; }

	[ObservableProperty]
	public partial HotKey? VolumeDown { get; set; }

	/// <summary>
	/// The "Add app" dialog's errors, under the hotkey box that has one; a box's error goes away when it changes
	/// </summary>
	public HotkeyPairErrors AddErrors { get; } = new HotkeyPairErrors();

	partial void OnVolumeUpChanged(HotKey? value) => AddErrors.ClearUp();

	partial void OnVolumeDownChanged(HotKey? value) => AddErrors.ClearDown();

	/// <summary>
	/// The running apps the "Add app" dialog offers: the default device's apps that have no hotkeys yet (see <see cref="PrepareAddApp"/>)
	/// </summary>
	public ObservableCollection<AudioProcessModel> AddableProcesses { get; } = new ObservableCollection<AudioProcessModel>();

	private int volumeStep = 1;
	public int VolumeStep
	{
		get => volumeStep;
		set
		{
			if(volumeStep != value)
			{
				volumeStep = value;
				OnPropertyChanged();

				SettingsProvider.Settings.General.VolumeStep = value;
				hotkeys.SetVolumeStep(value);
			}
		}
	}

	/// <summary>
	/// The apps with hotkeys, in the order they were added
	/// </summary>
	public ObservableCollection<AppHotkeysItem> Apps { get; }

	/// <summary>
	/// Containts registered processes that are currently launched
	/// </summary>
	private readonly List<AudioProcessModel> LaunchedProcesses = new List<AudioProcessModel>();

	[ObservableProperty]
	public partial OutputDeviceModel? DefaultDevice { get; set; }

	//shows the OSD when a hotkey changes a volume (the models' state mediator)
	private readonly INotificationService notifications;

	internal AppsHotkeysViewModel(IDeviceProvider dProvider, IHotkeyService hotkeys, INotificationService notifications) : base(hotkeys)
	{
		this.notifications = notifications;

		dProvider.DefaultDeviceChanged += OnDefaultDeviceSwitched;
		DefaultDevice = dProvider.DefaultDevice;


		VolumeStep = SettingsProvider.Settings.General.VolumeStep;
		Apps = LoadApps();
		if(Apps.Count > 0)
			RegisterWhenHotkeysActive(RegisterLoadedHotkeys);
		SettingsProvider.Settings.Notifications.PropertyChanged += OnNotificationSettingsChanged;
		ErrorDictionary.LanguageChanged += () =>
		{
			AddErrors.Refresh();
			foreach(var app in Apps)
				app.Errors.Refresh();
		};
	}

	/// <summary>
	/// Registers hotkeys that were loaded from settings on app startup
	/// </summary>
	private void RegisterLoadedHotkeys()
	{
		if(DefaultDevice == null)
			return;
		//-= first: a default device change may have subscribed already
		DefaultDevice.ProcessCreated -= OnProcessStarted;
		DefaultDevice.ProcessCreated += OnProcessStarted;
		FindAndSetupRegisteredHotkeys();
	}

	private void OnDefaultDeviceSwitched(OutputDeviceModel? newDevice)
	{
		if(DefaultDevice != null)
			DefaultDevice.ProcessCreated -= OnProcessStarted;

		//Reset hotkeys for all currently launched processes
		for(int i = LaunchedProcesses.Count - 1; i >= 0; i--)
			Release(LaunchedProcesses[i]);

		DefaultDevice = newDevice;

		if(newDevice == null)
			return;

		if(Apps.Count != 0)
		{
			newDevice.ProcessCreated -= OnProcessStarted;
			newDevice.ProcessCreated += OnProcessStarted;
			FindAndSetupRegisteredHotkeys();
		}
	}

	/// <summary>
	/// Search for registered processes amongst processes of the current default output device and set its hotkeys
	/// </summary>
	private void FindAndSetupRegisteredHotkeys()
	{
		if(DefaultDevice == null)
			return;
		foreach(AudioProcessModel process in DefaultDevice.GetImmutableProcesses())
		{
			//an app that is set up already would get its hotkeys registered twice
			if(FindApp(process.Name) is { } app && !LaunchedProcesses.Contains(process))
				SetUpSaved(process, app);
		}
	}

	private AppHotkeysItem? FindApp(string processName) => Apps.FirstOrDefault(app => app.ProcessName == processName);

	/// <summary>
	/// Registers a saved app's hotkeys for a running process. If they fail (e.g. another app took the combination),
	/// the error shows under the app's box instead of the hotkeys silently doing nothing; they stay saved.
	/// </summary>
	private void SetUpSaved(AudioProcessModel process, AppHotkeysItem app)
	{
		if(SetUp(process, app, out var failed))
			app.Errors.Clear();
		else
			app.Errors.SetFor(failed, app.VolumeUp, ErrorMessageType.VolumeReg);
	}

	/// <summary>
	/// Registers the app's hotkeys for a running process
	/// </summary>
	/// <param name="failed">The hotkey the system refused, null otherwise</param>
	private bool SetUp(AudioProcessModel process, AppHotkeysItem app, out HotKey? failed)
	{
		if(!process.SetVolumeHotkeys(app.VolumeUp, app.VolumeDown, out failed))
			return false;
		process.Exited += OnProcessExited;
		LaunchedProcesses.Add(process);
		app.Icon = process.IconSource;
		if(SettingsProvider.Settings.Notifications.Enabled && process.StateNotificationMediator == null)
			process.SetStateMediator(notifications);
		return true;
	}

	/// <summary>
	/// Releases a running process's hotkeys (it exited, its app was removed or the default device changed)
	/// </summary>
	private void Release(AudioProcessModel process)
	{
		process.ResetVolumeHotkeys();
		process.ResetStateMediator();
		process.Exited -= OnProcessExited;
		LaunchedProcesses.Remove(process);
		if(FindApp(process.Name) is { } app && !LaunchedProcesses.Any(p => p.Name == app.ProcessName))
			app.Icon = null;
	}

	/// <summary>
	/// Clears the "Add app" dialog and fills its list of apps; called when the dialog opens
	/// </summary>
	internal void PrepareAddApp()
	{
		SelectedProcess = null;
		VolumeUp = VolumeDown = null;
		AddErrors.Clear();
		AddableProcesses.Clear();
		if(DefaultDevice == null)
			return;
		foreach(var process in DefaultDevice.GetImmutableProcesses())
		{
			if(FindApp(process.Name) == null && AddableProcesses.All(p => p.Name != process.Name))
				AddableProcesses.Add(process);
		}
	}

	[RelayCommand]
	private void AddApp() => TryAddApp();

	/// <summary>
	/// Adds the selected app with the two hotkeys. False, with <see cref="AddErrors"/> set, if they can't be used.
	/// </summary>
	internal bool TryAddApp()
	{
		var process = SelectedProcess;
		if(process == null || FindApp(process.Name) != null)
			return false;

		AddErrors.Set(hotkeys.CheckHotkeysPair(VolumeUp, VolumeDown));
		//the check reports a missing hotkey
		if(AddErrors.Any || VolumeUp is not { } up || VolumeDown is not { } down)
			return false;

		var app = new AppHotkeysItem(this, process.Name, up, down);
		if(!SetUp(process, app, out var failed))
		{
			//the hotkey service logs why
			AddErrors.SetFor(failed, VolumeUp, ErrorMessageType.VolumeReg);
			return false;
		}

		//Subscribe to ProcessCreated event to search for registered processes amongst new processes
		if(Apps.Count == 0 && DefaultDevice != null)
			DefaultDevice.ProcessCreated += OnProcessStarted;
		Apps.Add(app);

		Logger.Info($"Registered app hotkeys. App: [{process.Name}] +vol: [{VolumeUp}] -vol: [{VolumeDown}], count: [{Apps.Count.ToString()}]");

		VolumeUp = VolumeDown = null;
		SelectedProcess = null;
		SaveApps();
		return true;
	}

	/// <summary>
	/// A hotkey box of a saved app changed. The new hotkeys are checked and, while the app runs, registered; if that fails,
	/// the old ones stay (registered and saved), the box shows them again and the error shows under it.
	/// </summary>
	internal void ChangeHotkey(AppHotkeysItem app, HotKey up, HotKey down)
	{
		if(Equals(up, app.VolumeUp) && Equals(down, app.VolumeDown))
			return;
		bool upChanged = !Equals(up, app.VolumeUp);
		HotKey changed = upChanged ? up : down;
		HotKey other = upChanged ? down : up;

		ErrorMessageType error;
		if(changed == null)
			error = ErrorMessageType.NeedsBoth;
		else if(changed.Equals(other))
			error = ErrorMessageType.Diff;
		else
			error = hotkeys.HotkeyIsValid(changed);

		//every running process of the app (it can run as several with the same name) gets the change
		var processes = LaunchedProcesses.Where(p => p.Name == app.ProcessName).ToList();
		if(error == ErrorMessageType.None && processes.Count > 0)
		{
			foreach(AudioProcessModel process in processes)
				process.ResetVolumeHotkeys();
			//the hotkey service counts users: the first process registers the pair, the others only count, so it fails on the first or not at all
			if(!processes.All(p => p.SetVolumeHotkeys(up, down)))
			{
				//the hotkey service logs why; the unchanged hotkey was registered until now, so it's the changed one.
				//Reset only releases what a process holds, so the ones the new pair didn't reach are fine too
				foreach(AudioProcessModel process in processes)
				{
					process.ResetVolumeHotkeys();
					process.SetVolumeHotkeys(app.VolumeUp, app.VolumeDown);
				}
				error = ErrorMessageType.VolumeReg;
			}
		}

		if(error != ErrorMessageType.None)
		{
			//the box shows the old hotkey again, so the error names the rejected one
			app.Errors.SetRejected(upChanged, changed, error);
			app.SetHotkeys(app.VolumeUp, app.VolumeDown);
			return;
		}
		app.Errors.Clear();
		app.SetHotkeys(up, down);
		Logger.Info($"Changed app hotkeys. App: [{app.ProcessName}] +vol: [{up}] -vol: [{down}]");
		SaveApps();
	}

	internal void RemoveApp(AppHotkeysItem app)
	{
		foreach(var process in LaunchedProcesses.Where(p => p.Name == app.ProcessName).ToList())
			Release(process);

		Apps.Remove(app);

		//no need to handle this event if there are no registered hotkeys anymore
		if(Apps.Count == 0 && DefaultDevice != null)
			DefaultDevice.ProcessCreated -= OnProcessStarted;
		SaveApps();
	}

	private ObservableCollection<AppHotkeysItem> LoadApps()
	{
		var apps = new ObservableCollection<AppHotkeysItem>();
		foreach(var app in SettingsProvider.Settings.Hotkeys.Apps)
		{
			if(app.ProcessName != null && app.VolumeUp != null && app.VolumeDown != null && apps.All(a => a.ProcessName != app.ProcessName))
				apps.Add(new AppHotkeysItem(this, app.ProcessName, app.VolumeUp, app.VolumeDown));
		}
		return apps;
	}

	private void SaveApps()
	{
		SettingsProvider.Settings.Hotkeys.Apps = Apps
			.Select(app => new AppHotkeysEntry { ProcessName = app.ProcessName, VolumeUp = app.VolumeUp, VolumeDown = app.VolumeDown })
			.ToArray();
	}

	private void OnNotificationSettingsChanged(object? sender, PropertyChangedEventArgs e)
	{
		if(e.PropertyName == nameof(NotificationSettings.Enabled))
		{
			foreach(var process in LaunchedProcesses)
			{
				if(SettingsProvider.Settings.Notifications.Enabled)
					process.SetStateMediator(notifications);
				else
					process.ResetStateMediator();
			}
		}
	}

	private void OnProcessExited(AudioProcessModel process) => Release(process);

	private void OnProcessStarted(AudioProcessModel process)
	{
		if(FindApp(process.Name) is { } app)
			SetUpSaved(process, app);
	}
}
