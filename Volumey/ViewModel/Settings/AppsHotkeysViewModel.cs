using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using log4net;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;
using Volumey.Helper;
using Volumey.Model;
using Volumey.Services;

namespace Volumey.ViewModel.Settings;

public sealed class AppsHotkeysViewModel : HotkeyViewModel
{
	private AudioProcessModel selectedProcess;
	public AudioProcessModel SelectedProcess
	{
		get => selectedProcess;
		set
		{
			selectedProcess = value;
			OnPropertyChanged();
		}
	}

	private HotKey volumeUp;
	public HotKey VolumeUp
	{
		get => volumeUp;
		set
		{
			volumeUp = value;
			OnPropertyChanged();
		}
	}

	private HotKey volumeDown;
	public HotKey VolumeDown
	{
		get => volumeDown;
		set
		{
			volumeDown = value;
			OnPropertyChanged();
		}
	}

	private KeyValuePair<string, VolumeHotkeysPair>? selectedRegApp;
	public KeyValuePair<string, VolumeHotkeysPair>? SelectedRegApp
	{
		get => selectedRegApp;
		set
		{
			selectedRegApp = value;
			OnPropertyChanged();
		}
	}

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

				//set new value in AppSettings but save it in App.Exit event handler
				//or when other parameters will be saved
				SettingsProvider.Settings.General.VolumeStep = value;
				hotkeys.SetVolumeStep(value);
			}
		}
	}

	public ObservableConcurrentDictionary<string, VolumeHotkeysPair> RegisteredProcesses { get; }

	/// <summary>
	/// Containts registered processes that are currently launched
	/// </summary>
	private readonly List<AudioProcessModel> LaunchedProcesses = new List<AudioProcessModel>();

	public ICommand AddAppCommand { get; }
	public ICommand RemoveAppCommand { get; }

	private OutputDeviceModel defaultDevice;
	public OutputDeviceModel DefaultDevice
	{
		get => defaultDevice;
		set
		{
			defaultDevice = value;
			OnPropertyChanged();
		}
	}

	private AudioProcessStateNotificationMediator _sMediator;
	private AudioProcessStateNotificationMediator ProcessStateMediator
	{
		get
		{
			_sMediator ??= new AudioProcessStateNotificationMediator(notifications);
			return _sMediator;
		}
	}

	private ILog _logger;
	private ILog Logger => _logger ??= LogManager.GetLogger(typeof(AppsHotkeysViewModel));

	private readonly IHotkeyService hotkeys;
	private readonly INotificationService notifications;

	internal AppsHotkeysViewModel(IDeviceProvider dProvider, IHotkeyService hotkeys, INotificationService notifications)
	{
		this.hotkeys = hotkeys;
		this.notifications = notifications;
		AddAppCommand = new ActionCommand(AddApp);
		RemoveAppCommand = new ActionCommand(RemoveApp);

		dProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
		DefaultDevice = dProvider.DefaultDevice;


		VolumeStep = SettingsProvider.Settings.General.VolumeStep;
		RegisteredProcesses = LoadRegisteredProcesses();
		if(RegisteredProcesses.Keys.Count > 0)
		{
			if(this.hotkeys.IsActive)
				RegisterLoadedHotkeys();
			else
				this.hotkeys.Activated += RegisterLoadedHotkeys;
		}
		SettingsProvider.Settings.Notifications.PropertyChanged += OnNotificationSettingsChanged;
	}

	/// <summary>
	/// Registers hotkeys that were loaded from settings on app startup
	/// </summary>
	private void RegisterLoadedHotkeys()
	{
		if(RegisteredProcesses == null || DefaultDevice == null)
			return;
		//-= first: a default device change may have subscribed already
		DefaultDevice.ProcessCreated -= OnProcessStarted;
		DefaultDevice.ProcessCreated += OnProcessStarted;
		FindAndSetupRegisteredHotkeys();
	}

	private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
	{
		if(DefaultDevice != null)
			DefaultDevice.ProcessCreated -= OnProcessStarted;

		//Reset hotkeys for all currently launched processes
		var launchedProcessesCount = LaunchedProcesses.Count;
		for(int i = launchedProcessesCount - 1; i >= 0; i--)
		{
			var process = LaunchedProcesses[i];
			process.Exited -= OnProcessExited;
			process.ResetVolumeHotkeys();
			process.ResetStateMediator();
			LaunchedProcesses.Remove(process);
		}

		DefaultDevice = newDevice;

		if(newDevice == null)
			return;

		if(RegisteredProcesses.Keys.Count != 0)
		{
			DefaultDevice.ProcessCreated -= OnProcessStarted;
			DefaultDevice.ProcessCreated += OnProcessStarted;
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
			if(RegisteredProcesses.TryGetValue(process.Name, out var hotkeys) && !LaunchedProcesses.Contains(process))
			{
				process.SetVolumeHotkeys(hotkeys.VolumeUp, hotkeys.VolumeDown);
				process.Exited += OnProcessExited;
				LaunchedProcesses.Add(process);
				if(SettingsProvider.Settings.Notifications.Enabled && process.StateNotificationMediator == null)
					process.SetStateMediator(ProcessStateMediator);
			}
		}
	}

	private void AddApp()
	{
		var process = SelectedProcess;
		if(RegisteredProcesses.ContainsKey(process.Name))
			return;

		if(this.hotkeys.HotkeysAreValid(VolumeUp, VolumeDown) is var error && error != ErrorMessageType.None)
		{
			SetErrorMessage(error);
			return;
		}

		if(!process.SetVolumeHotkeys(VolumeUp, VolumeDown))
		{
			SetErrorMessage(ErrorMessageType.VolumeReg);
			return;
		}

		process.Exited += OnProcessExited;

		if(SettingsProvider.Settings.Notifications.Enabled && process.StateNotificationMediator == null)
			process.SetStateMediator(ProcessStateMediator);
		SetErrorMessage(ErrorMessageType.None);

		var hotkeys = new VolumeHotkeysPair(VolumeUp, VolumeDown);

		//Subscribe to ProcessCreated event to search for registered processes amongst new processes
		if(RegisteredProcesses.Keys.Count == 0 && DefaultDevice != null)
			DefaultDevice.ProcessCreated += OnProcessStarted;
		RegisteredProcesses.Add(process.Name, hotkeys);
		LaunchedProcesses.Add(process);

		Logger.Info($"Registered app hotkeys. App: [{process.Name}] +vol: [{VolumeUp}] -vol: [{VolumeDown}], count: [{RegisteredProcesses.Keys.Count.ToString()}]");

		VolumeUp = VolumeDown = null;
		SelectedProcess = null;

		try
		{
			SaveRegisteredProcesses();
		}
		catch { }
	}

	private void RemoveApp()
	{
		if(SelectedRegApp == null)
			return;

		var processName = SelectedRegApp.Value.Key;

		AudioProcessModel process = LaunchedProcesses.FirstOrDefault(p => p.Name.Equals(processName));

		if(process != null)
		{
			process.ResetVolumeHotkeys();
			process.ResetStateMediator();
			process.Exited -= OnProcessExited;
			LaunchedProcesses.Remove(process);
		}

		RegisteredProcesses.Remove(processName);

		//no need to handle this event if there are no registered hotkeys anymore
		if(RegisteredProcesses.Keys.Count == 0 && DefaultDevice != null)
			DefaultDevice.ProcessCreated -= OnProcessStarted;
		try
		{
			SaveRegisteredProcesses();
		}
		catch { }
	}

	private static ObservableConcurrentDictionary<string, VolumeHotkeysPair> LoadRegisteredProcesses()
	{
		var processes = new ObservableConcurrentDictionary<string, VolumeHotkeysPair>();
		foreach(var app in SettingsProvider.Settings.Hotkeys.Apps)
		{
			if(app.ProcessName != null && app.VolumeUp != null && app.VolumeDown != null)
				processes.Add(app.ProcessName, new VolumeHotkeysPair(app.VolumeUp, app.VolumeDown));
		}
		return processes;
	}

	private void SaveRegisteredProcesses()
	{
		var apps = RegisteredProcesses
					   .Select(p => new AppHotkeysEntry { ProcessName = p.Key, VolumeUp = p.Value.VolumeUp, VolumeDown = p.Value.VolumeDown })
					   .ToArray();
		SettingsProvider.Settings.Hotkeys.Apps = apps;
	}

	private void OnNotificationSettingsChanged(object sender, PropertyChangedEventArgs e)
	{
		if(e.PropertyName == nameof(NotificationSettings.Enabled))
		{
			if(SettingsProvider.Settings.Notifications.Enabled)
				SetVolumeStateMediatorForActiveProcesses();
			else
				ResetVolumeStateMediatorForActiveProcesses();
		}
	}

	private void SetVolumeStateMediatorForActiveProcesses()
	{
		foreach(var process in LaunchedProcesses)
			process.SetStateMediator(ProcessStateMediator);
	}

	private void ResetVolumeStateMediatorForActiveProcesses()
	{
		foreach(var process in LaunchedProcesses)
			process.ResetStateMediator();
	}

	private void OnProcessExited(AudioProcessModel process)
	{
		process.ResetVolumeHotkeys();
		process.ResetStateMediator();
		process.Exited -= OnProcessExited;
		LaunchedProcesses.Remove(process);
	}

	private void OnProcessStarted(AudioProcessModel process)
	{
		if(RegisteredProcesses.TryGetValue(process.Name, out var hotkeys))
		{
			process.SetVolumeHotkeys(volUp: hotkeys.VolumeUp, volDown: hotkeys.VolumeDown);
			process.Exited += OnProcessExited;
			LaunchedProcesses.Add(process);
			if(SettingsProvider.Settings.Notifications.Enabled)
				process.SetStateMediator(ProcessStateMediator);
		}
	}
}
