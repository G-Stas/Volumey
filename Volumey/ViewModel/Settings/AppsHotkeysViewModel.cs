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
/// Volume and mute hotkeys for single apps, stored by process name (App hotkeys page); each is optional, an app has at least one.
/// The hotkeys are registered while the app runs, on its home model (where it is set to play); a press changes it on every device. <see cref="SelectedProcess"/>, <see cref="VolumeUp"/>,
/// <see cref="VolumeDown"/>, <see cref="Mute"/> and <see cref="AddErrors"/> are the "Add app" dialog's; each saved app is an <see cref="AppHotkeysItem"/> whose hotkeys can be changed in place.
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

	[ObservableProperty]
	public partial HotKey? Mute { get; set; }

	/// <summary>
	/// The "Add app" dialog's Add button: an app and at least one hotkey are set
	/// </summary>
	public bool CanAdd => SelectedProcess != null && (VolumeUp != null || VolumeDown != null || Mute != null);

	/// <summary>
	/// The "Add app" dialog's errors, under the hotkey box that has one; a box's error goes away when it changes
	/// </summary>
	public HotkeyPairErrors AddErrors { get; } = new HotkeyPairErrors();

	partial void OnSelectedProcessChanged(AudioProcessModel? value) => OnPropertyChanged(nameof(CanAdd));

	partial void OnVolumeUpChanged(HotKey? value)
	{
		AddErrors.ClearUp();
		OnPropertyChanged(nameof(CanAdd));
	}

	partial void OnVolumeDownChanged(HotKey? value)
	{
		AddErrors.ClearDown();
		OnPropertyChanged(nameof(CanAdd));
	}

	partial void OnMuteChanged(HotKey? value)
	{
		AddErrors.ClearMute();
		OnPropertyChanged(nameof(CanAdd));
	}

	/// <summary>
	/// The running apps the "Add app" dialog offers: the apps on every device that have no hotkeys yet (see <see cref="PrepareAddApp"/>)
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
	/// The apps' models whose hotkeys are registered: each running app's home model (<see cref="IAppRouting.GetApps"/>)
	/// </summary>
	private readonly List<AudioProcessModel> LaunchedProcesses = new List<AudioProcessModel>();

	private readonly IAppRouting appRouting;
	private bool hotkeysActive;

	//shows the OSD when a hotkey changes a volume (the models' state mediator)
	private readonly INotificationService notifications;

	internal AppsHotkeysViewModel(IHotkeyService hotkeys, INotificationService notifications, IAppRouting appRouting) : base(hotkeys)
	{
		this.notifications = notifications;
		this.appRouting = appRouting;

		VolumeStep = SettingsProvider.Settings.General.VolumeStep;
		Apps = LoadApps();
		RegisterWhenHotkeysActive(() =>
		{
			hotkeysActive = true;
			Reconcile();
		});
		appRouting.AppsChanged += Reconcile;
		SettingsProvider.Settings.Notifications.PropertyChanged += OnNotificationSettingsChanged;
		ErrorDictionary.LanguageChanged += () =>
		{
			AddErrors.Refresh();
			foreach(var app in Apps)
				app.Errors.Refresh();
		};
	}

	/// <summary>
	/// Keeps the saved apps' hotkeys on the running apps' home models: released from models that are no longer home (the app exited,
	/// moved to another device, the default device changed), then set up on the saved apps' home models. Windows refuses a combination
	/// twice, so an app's hotkeys sit on one model; a press changes all of them
	/// </summary>
	private void Reconcile()
	{
		if(!hotkeysActive)
			return;
		var homes = appRouting.GetApps();
		foreach(var process in LaunchedProcesses.Where(p => !homes.Contains(p)).ToList())
			Release(process);
		foreach(AudioProcessModel process in homes)
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
		SetUp(process, app, out var failed);
		app.Errors.SetFailed(failed, app.VolumeUp, app.VolumeDown, app.Mute, ErrorMessageType.VolumeReg);
	}

	/// <summary>
	/// Registers the app's hotkeys for a running process, each on its own: the ones the system refuses don't keep the others from working
	/// </summary>
	/// <param name="failed">The hotkeys the system refused</param>
	/// <returns>False if any was refused</returns>
	private bool SetUp(AudioProcessModel process, AppHotkeysItem app, out IReadOnlyList<HotKey> failed)
	{
		bool all = Register(process, app.VolumeUp, app.VolumeDown, app.Mute, out failed);
		LaunchedProcesses.Add(process);
		app.Icon = process.IconSource;
		if(SettingsProvider.Settings.Notifications.Enabled && process.StateNotificationMediator == null)
			process.SetStateMediator(notifications);
		return all;
	}

	/// <summary>A press changes every model of the app on all devices</summary>
	private bool Register(AudioProcessModel process, HotKey? up, HotKey? down, HotKey? mute, out IReadOnlyList<HotKey> failed)
		=> process.SetHotkeys(up, down, mute, out failed, () => appRouting.GetModels(process));

	/// <summary>
	/// Releases a model's hotkeys (it's no longer its app's home model, or its app was removed)
	/// </summary>
	private void Release(AudioProcessModel process)
	{
		process.ResetVolumeHotkeys();
		process.ResetStateMediator();
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
		VolumeUp = VolumeDown = Mute = null;
		AddErrors.Clear();
		AddableProcesses.Clear();
		foreach(var process in appRouting.GetApps())
		{
			if(FindApp(process.Name) == null && AddableProcesses.All(p => p.Name != process.Name))
				AddableProcesses.Add(process);
		}
	}

	[RelayCommand]
	private void AddApp() => TryAddApp();

	/// <summary>
	/// Adds the selected app with the hotkeys that are set (at least one). False, with <see cref="AddErrors"/> set, if they can't be used.
	/// </summary>
	internal bool TryAddApp()
	{
		var process = SelectedProcess;
		if(process == null || FindApp(process.Name) != null)
			return false;

		//the check reports no hotkey at all
		AddErrors.Set(hotkeys.CheckAppHotkeys(VolumeUp, VolumeDown, Mute));
		if(AddErrors.Any)
			return false;

		var app = new AppHotkeysItem(this, process.Name, VolumeUp, VolumeDown, Mute);
		if(!SetUp(process, app, out var failed))
		{
			//the dialog stays open with the error; the hotkeys that did register are released again (the hotkey service logs why)
			Release(process);
			AddErrors.SetFailed(failed, VolumeUp, VolumeDown, Mute, ErrorMessageType.VolumeReg);
			return false;
		}

		Apps.Add(app);

		Logger.Info($"Registered app hotkeys. App: [{process.Name}] +vol: [{VolumeUp}] -vol: [{VolumeDown}] mute: [{Mute}], count: [{Apps.Count.ToString()}]");

		VolumeUp = VolumeDown = Mute = null;
		SelectedProcess = null;
		SaveApps();
		return true;
	}

	/// <summary>
	/// A hotkey box of a saved app changed. Each hotkey is optional; clearing the app's last one removes the app. The new hotkeys are
	/// checked and, while the app runs, registered; if that fails, the old ones stay (registered and saved), the box shows them again
	/// and the error shows under it.
	/// </summary>
	internal void ChangeHotkey(AppHotkeysItem app, AppHotkeyKind kind, HotKey? changed)
	{
		var up = kind == AppHotkeyKind.Up ? changed : app.VolumeUp;
		var down = kind == AppHotkeyKind.Down ? changed : app.VolumeDown;
		var mute = kind == AppHotkeyKind.Mute ? changed : app.Mute;
		if(Equals(up, app.VolumeUp) && Equals(down, app.VolumeDown) && Equals(mute, app.Mute))
			return;
		if(up == null && down == null && mute == null)
		{
			RemoveApp(app);
			return;
		}

		ErrorMessageType error = ErrorMessageType.None;
		if(changed != null)
		{
			var others = kind switch
			{
				AppHotkeyKind.Up => new[] { down, mute },
				AppHotkeyKind.Down => new[] { up, mute },
				_ => new[] { up, down }
			};
			error = others.Any(other => changed.Equals(other)) ? ErrorMessageType.Diff : hotkeys.HotkeyIsValid(changed);
		}

		//every running process of the app (it can run as several with the same name) gets the change
		var processes = LaunchedProcesses.Where(p => p.Name == app.ProcessName).ToList();
		var refused = new HashSet<HotKey>();
		if(error == ErrorMessageType.None && processes.Count > 0)
		{
			//the hotkey service counts users: the first process registers a hotkey, the others only count, so one fails on the first or not at all
			foreach(AudioProcessModel process in processes)
			{
				process.ResetVolumeHotkeys();
				Register(process, up, down, mute, out var failed);
				refused.UnionWith(failed);
			}
			if(changed != null && refused.Contains(changed))
			{
				//the hotkey service logs why. Reset only releases what a process holds, so the old hotkeys go back as they were
				foreach(AudioProcessModel process in processes)
				{
					process.ResetVolumeHotkeys();
					Register(process, app.VolumeUp, app.VolumeDown, app.Mute, out _);
				}
				error = ErrorMessageType.VolumeReg;
			}
		}

		if(error != ErrorMessageType.None)
		{
			//the box shows the old hotkey again, so the error names the rejected one
			app.Errors.SetRejected(kind, changed, error);
			app.SetHotkeys(app.VolumeUp, app.VolumeDown, app.Mute);
			return;
		}
		//an unchanged hotkey that another app holds keeps its error
		app.Errors.SetFailed(refused, up, down, mute, ErrorMessageType.VolumeReg);
		app.SetHotkeys(up, down, mute);
		Logger.Info($"Changed app hotkeys. App: [{app.ProcessName}] +vol: [{up}] -vol: [{down}] mute: [{mute}]");
		SaveApps();
	}

	internal void RemoveApp(AppHotkeysItem app)
	{
		foreach(var process in LaunchedProcesses.Where(p => p.Name == app.ProcessName).ToList())
			Release(process);

		Apps.Remove(app);
		SaveApps();
	}

	private ObservableCollection<AppHotkeysItem> LoadApps()
	{
		var apps = new ObservableCollection<AppHotkeysItem>();
		foreach(var app in SettingsProvider.Settings.Hotkeys.Apps)
		{
			bool anyHotkey = app.VolumeUp != null || app.VolumeDown != null || app.Mute != null;
			if(app.ProcessName != null && anyHotkey && apps.All(a => a.ProcessName != app.ProcessName))
				apps.Add(new AppHotkeysItem(this, app.ProcessName, app.VolumeUp, app.VolumeDown, app.Mute));
		}
		return apps;
	}

	private void SaveApps()
	{
		SettingsProvider.Settings.Hotkeys.Apps = Apps
			.Select(app => new AppHotkeysEntry { ProcessName = app.ProcessName, VolumeUp = app.VolumeUp, VolumeDown = app.VolumeDown, Mute = app.Mute })
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
}
