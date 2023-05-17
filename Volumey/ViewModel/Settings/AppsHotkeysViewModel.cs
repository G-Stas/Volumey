using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using log4net;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public sealed class AppsHotkeysViewModel : HotkeyViewModel
	{
		private AudioProcessModel selectedProcess;
		public AudioProcessModel SelectedProcess
		{
			get => selectedProcess;
			set
			{
				this.selectedProcess = value;
				OnPropertyChanged();
			}
		}

		private HotKey volumeUp;
		public HotKey VolumeUp
		{
			get => volumeUp;
			set
			{
				this.volumeUp = value;
				OnPropertyChanged();
			}
		}

		private HotKey volumeDown;
		public HotKey VolumeDown
		{
			get => this.volumeDown;
			set
			{
				this.volumeDown = value;
				OnPropertyChanged();
			}
		}
		
		private KeyValuePair<string, VolumeHotkeysPair>? selectedRegApp;
		public KeyValuePair<string, VolumeHotkeysPair>? SelectedRegApp
		{
			get => selectedRegApp;
			set
			{
				this.selectedRegApp = value;
				OnPropertyChanged();
			}
		}

		private int volumeStep = 1;
		public int VolumeStep
		{
			get => volumeStep;
			set
			{
				if(this.volumeStep != value)
				{
					this.volumeStep = value;
					OnPropertyChanged();

					//set new value in AppSettings but save it in App.Exit event handler
					//or when other parameters will be saved
					SettingsProvider.Settings.VolumeStep = value;
					HotkeysControl.SetVolumeStep(value);
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
				this.defaultDevice = value;
				OnPropertyChanged();
			}
		}

		private AudioProcessStateNotificationMediator _sMediator;
		private AudioProcessStateNotificationMediator ProcessStateMediator
		{
			get
			{
				this._sMediator ??= new AudioProcessStateNotificationMediator();
				return this._sMediator;
			}
		}

		private ILog _logger;
		private ILog Logger => _logger ??= LogManager.GetLogger(typeof(AppsHotkeysViewModel));
		
		public AppsHotkeysViewModel()
		{
			this.AddAppCommand = new ActionCommand(async () => await AddApp());
			this.RemoveAppCommand = new ActionCommand(async () => await RemoveApp());

			var dProvider = DeviceProvider.GetInstance();
			dProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
			this.DefaultDevice = dProvider.DefaultDevice;

			ErrorDictionary.LanguageChanged += () => this.SetErrorMessage(this.CurrentErrorType);

			this.VolumeStep = SettingsProvider.Settings.VolumeStep;
			this.RegisteredProcesses = SettingsProvider.HotkeysSettings.GetRegisteredProcesses();
			if(this.RegisteredProcesses.Keys.Count > 0)
			{
				if(HotkeysControl.IsActive)
					this.RegisterLoadedHotkeys();
				else
					HotkeysControl.Activated += this.RegisterLoadedHotkeys;
			}
			SettingsProvider.NotificationsSettings.PropertyChanged += OnSettingsPropertyChanged;
		}

		/// <summary>
		/// Registers hotkeys that were loaded from settings on app startup
		/// </summary>
		private void RegisterLoadedHotkeys()
		{
			if(this.RegisteredProcesses == null || this.DefaultDevice == null)
				return;
			this.DefaultDevice.ProcessCreated += OnProcessStarted;
			this.FindAndSetupRegisteredHotkeys();
		}

		private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
		{
			if(this.DefaultDevice != null)
				this.DefaultDevice.ProcessCreated -= OnProcessStarted;
			
			//Reset hotkeys for all currently launched processes
			var launchedProcessesCount = this.LaunchedProcesses.Count;
			for(int i = launchedProcessesCount-1; i >= 0; i--)
			{
				var process = this.LaunchedProcesses[i];
				process.ResetVolumeHotkeys();
				process.ResetStateMediator();
				this.LaunchedProcesses.Remove(process);
			}
			
			this.DefaultDevice = newDevice;
			
			if(newDevice == null)
				return;
			
			if(this.RegisteredProcesses.Keys.Count != 0)
			{
				this.DefaultDevice.ProcessCreated += OnProcessStarted;
				this.FindAndSetupRegisteredHotkeys();
			}
		}

		/// <summary>
		/// Search for registered processes amongst processes of the current default output device and set its hotkeys
		/// </summary>
		private void FindAndSetupRegisteredHotkeys()
		{
			if(this.DefaultDevice == null)
				return;
			foreach(AudioProcessModel process in this.DefaultDevice.GetImmutableProcesses())
			{
				if(this.RegisteredProcesses.TryGetValue(process.Name, out var hotkeys))
				{
					process.SetVolumeHotkeys(hotkeys.VolumeUp, hotkeys.VolumeDown);
					process.Exited += OnProcessExited;
					this.LaunchedProcesses.Add(process);
					if(SettingsProvider.NotificationsSettings.Enabled && process.StateNotificationMediator == null)
						process.SetStateMediator(this.ProcessStateMediator);
				}
			}
		}

		private async Task AddApp()
		{
			var process = this.SelectedProcess;
			if(this.RegisteredProcesses.ContainsKey(process.Name))
				return;

			if(HotkeysControl.HotkeysAreValid(this.VolumeUp, this.VolumeDown) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				return;
			}

			if(!process.SetVolumeHotkeys(this.VolumeUp, this.VolumeDown))
			{
				this.SetErrorMessage(ErrorMessageType.VolumeReg);
				return;
			}
			
			process.Exited += OnProcessExited;
			
			if(SettingsProvider.NotificationsSettings.Enabled && process.StateNotificationMediator == null)
				process.SetStateMediator(this.ProcessStateMediator);
			this.SetErrorMessage(ErrorMessageType.None);

			var hotkeys = new VolumeHotkeysPair(this.VolumeUp, this.VolumeDown);

			//Subscribe to ProcessCreated event to search for registered processes amongst new processes
			if (this.RegisteredProcesses.Keys.Count == 0 && this.DefaultDevice != null)
				this.DefaultDevice.ProcessCreated += OnProcessStarted;
			this.RegisteredProcesses.Add(process.Name, hotkeys);
			this.LaunchedProcesses.Add(process);

			Logger.Info($"Registered app hotkeys. App: [{process.Name}] +vol: [{this.VolumeUp}] -vol: [{this.VolumeDown}], count: [{this.RegisteredProcesses.Keys.Count.ToString()}]");
			
			this.VolumeUp = this.VolumeDown = null;
			this.SelectedProcess = null;
			
			try
			{
				SettingsProvider.HotkeysSettings.AddRegisteredProcess(process.Name, hotkeys);
				await SettingsProvider.SaveSettings().ConfigureAwait(false);
			}
			catch { }
		}

		private async Task RemoveApp()
		{
			if(this.SelectedRegApp == null)
				return;

			var processName = this.SelectedRegApp.Value.Key;

			AudioProcessModel process = this.LaunchedProcesses.FirstOrDefault(p => p.Name.Equals(processName));

			if(process != null)
			{
				process.ResetVolumeHotkeys();
				process.ResetStateMediator();
				process.Exited -= OnProcessExited;
				this.LaunchedProcesses.Remove(process);
			}
			
			this.RegisteredProcesses.Remove(processName);

			//no need to handle this event if there are no registered hotkeys anymore
			if(this.RegisteredProcesses.Keys.Count == 0 && this.DefaultDevice != null)
				this.DefaultDevice.ProcessCreated -= OnProcessStarted;
			try
			{
				SettingsProvider.HotkeysSettings.RemoveRegisteredSession(processName);
				await SettingsProvider.SaveSettings().ConfigureAwait(false);
			}
			catch { }
		}
		
		private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if(e.PropertyName.Equals(nameof(SettingsProvider.NotificationsSettings.Enabled)))
			{
				if(SettingsProvider.NotificationsSettings.Enabled)
					SetVolumeStateMediatorForActiveProcesses();
				else
					ResetVolumeStateMediatorForActiveProcesses();
			}
		}

		private void SetVolumeStateMediatorForActiveProcesses()
		{
			foreach(var process in this.LaunchedProcesses)
				process.SetStateMediator(this.ProcessStateMediator);
		}

		private void ResetVolumeStateMediatorForActiveProcesses()
		{
			foreach(var process in this.LaunchedProcesses)
				process.ResetStateMediator();
		}

		private void OnProcessExited(AudioProcessModel process)
		{
			process.ResetVolumeHotkeys();
			process.ResetStateMediator();
			process.Exited -= OnProcessExited;
			this.LaunchedProcesses.Remove(process);
		}

		private void OnProcessStarted(AudioProcessModel process)
		{
			if(this.RegisteredProcesses.TryGetValue(process.Name, out var hotkeys))
			{
				process.SetVolumeHotkeys(volUp: hotkeys.VolumeUp, volDown: hotkeys.VolumeDown);
				process.Exited += OnProcessExited;
				this.LaunchedProcesses.Add(process);
				if(SettingsProvider.NotificationsSettings.Enabled)
					process.SetStateMediator(this.ProcessStateMediator);
			}
		}
	}
}