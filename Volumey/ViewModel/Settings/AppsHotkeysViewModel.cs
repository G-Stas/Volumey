using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
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
		private AudioSessionModel selectedSession;
		public AudioSessionModel SelectedSession
		{
			get => selectedSession;
			set
			{
				this.selectedSession = value;
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
		
		private KeyValuePair<string, Tuple<HotKey, HotKey>>? selectedRegApp;
		public KeyValuePair<string, Tuple<HotKey, HotKey>>? SelectedRegApp
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
		
		public ObservableConcurrentDictionary<string, Tuple<HotKey, HotKey>> RegisteredSessions { get; }

		/// <summary>
		/// Containts registered sessions that are currently launched
		/// </summary>
		private readonly List<AudioSessionModel> LaunchedSessions = new List<AudioSessionModel>();

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

		private AudioSessionStateNotificationMediator _sMediator;
		private AudioSessionStateNotificationMediator SessionStateMediator
		{
			get
			{
				this._sMediator ??= new AudioSessionStateNotificationMediator();
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
			this.RegisteredSessions = SettingsProvider.HotkeysSettings.GetRegisteredSessions();
			if(this.RegisteredSessions.Keys.Count > 0)
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
			if(this.RegisteredSessions == null || this.DefaultDevice == null)
				return;
			this.DefaultDevice.SessionCreated += OnSessionCreated;
			this.FindAndSetupRegisteredHotkeys();
		}

		private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
		{
			if(this.DefaultDevice != null)
				this.DefaultDevice.SessionCreated -= OnSessionCreated;
			
			//Reset hotkeys for all currently launched sessions
			var launchedSessionCount = this.LaunchedSessions.Count;
			for(int i = launchedSessionCount-1; i >= 0; i--)
			{
				var session = this.LaunchedSessions[i];
				session.ResetVolumeHotkeys();
				session.ResetStateNotificationMediator();
				session.SessionEnded -= OnSessionEnded;
				this.LaunchedSessions.Remove(session);
			}
			
			this.DefaultDevice = newDevice;
			
			if(newDevice == null)
				return;
			
			if(this.RegisteredSessions.Keys.Count != 0)
			{
				this.DefaultDevice.SessionCreated += OnSessionCreated;
				this.FindAndSetupRegisteredHotkeys();
			}
		}

		/// <summary>
		/// Search for registered sessions amongst sessions of the current default output device and set its hotkeys
		/// </summary>
		private void FindAndSetupRegisteredHotkeys()
		{
			if(this.DefaultDevice == null)
				return;
			for(int i = 0; i < this.DefaultDevice.Sessions.Count; i++)
			{
				var session = this.DefaultDevice.Sessions[i];
				if(this.RegisteredSessions.TryGetValue(session.Name, out var hotkeys))
				{
					session.SetVolumeHotkeys(hotkeys.Item1, hotkeys.Item2);
					session.SessionEnded += OnSessionEnded;
					this.LaunchedSessions.Add(session);
					if(SettingsProvider.NotificationsSettings.Enabled)
						session.SetStateNotificationMediator(this.SessionStateMediator);
				}
			}
		}

		private async Task AddApp()
		{
			var session = this.SelectedSession;
			if(this.RegisteredSessions.ContainsKey(session.Name))
				return;

			if(HotkeysControl.HotkeysAreValid(this.VolumeUp, this.VolumeDown) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				return;
			}

			if(!session.SetVolumeHotkeys(this.VolumeUp, this.VolumeDown))
			{
				this.SetErrorMessage(ErrorMessageType.VolumeReg);
				return;
			}
			session.SessionEnded += OnSessionEnded;
			if(SettingsProvider.NotificationsSettings.Enabled)
				session.SetStateNotificationMediator(this.SessionStateMediator);
			this.SetErrorMessage(ErrorMessageType.None);

			try
			{
				//Find other sessions with the same name (i.e. sessions of the same process) to set its hotkeys as well
				for(int i = 0; i < this.DefaultDevice.Sessions.Count; i++)
				{
					var otherSession = this.DefaultDevice.Sessions[i];
					if(otherSession.Name.Equals(session.Name) && otherSession != session)
					{
						otherSession.SetVolumeHotkeys(this.VolumeUp, this.VolumeDown);
						otherSession.SessionEnded += OnSessionEnded;
						this.LaunchedSessions.Add(otherSession);
						if(SettingsProvider.NotificationsSettings.Enabled)
							otherSession.SetStateNotificationMediator(this.SessionStateMediator);
					}
				}
			}
			catch { }

			var hotkeys = new Tuple<HotKey, HotKey>(this.VolumeUp, this.VolumeDown);

			//Subscribe to SessionCreated event to search for registered sessions amongst new sessions
			if (this.RegisteredSessions.Keys.Count == 0 && this.DefaultDevice != null)
				this.DefaultDevice.SessionCreated += OnSessionCreated;
			this.RegisteredSessions.Add(session.Name, hotkeys);
			this.LaunchedSessions.Add(session);

			Logger.Info($"Registered app hotkeys. App: [{session.Name}] +vol: [{this.VolumeUp}] -vol: [{this.VolumeDown}], count: [{this.RegisteredSessions.Keys.Count.ToString()}]");
			
			this.VolumeUp = this.VolumeDown = null;
			this.SelectedSession = null;
			
			try
			{
				SettingsProvider.HotkeysSettings.AddRegisteredSession(session.Name, hotkeys);
				await SettingsProvider.SaveSettings().ConfigureAwait(false);
			}
			catch { }
		}

		private async Task RemoveApp()
		{
			if(this.SelectedRegApp == null)
				return;

			var sessionName = this.SelectedRegApp.Value.Key;
			
			//Find sessions with the selected name (i.e. all sessions of this process) and reset its hotkeys
			var launchedSessionCount = this.LaunchedSessions.Count;
			for(int i = launchedSessionCount-1; i >= 0; i--)
			{
				var session = this.LaunchedSessions[i];
				if(session.Name.Equals(sessionName))
				{
					session.ResetVolumeHotkeys();
					session.ResetStateNotificationMediator();
					session.SessionEnded -= OnSessionEnded;
					this.LaunchedSessions.Remove(session);
				}
			}
			
			this.RegisteredSessions.Remove(sessionName);

			//No need to handle this event if there are no registered hotkeys anymore
			if(this.RegisteredSessions.Keys.Count == 0 && this.DefaultDevice != null)
				this.DefaultDevice.SessionCreated -= OnSessionCreated;
			try
			{
				SettingsProvider.HotkeysSettings.RemoveRegisteredSession(sessionName);
				await SettingsProvider.SaveSettings().ConfigureAwait(false);
			}
			catch { }
		}
		
		private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if(e.PropertyName.Equals(nameof(SettingsProvider.NotificationsSettings.Enabled)))
			{
				if(SettingsProvider.NotificationsSettings.Enabled)
					SetVolumeStateMediatorForActiveSessions();
				else
					ResetVolumeStateMediatorForActiveSessions();
			}
		}

		private void SetVolumeStateMediatorForActiveSessions()
		{
			foreach(var session in this.LaunchedSessions)
				session.SetStateNotificationMediator(this.SessionStateMediator);
		}

		private void ResetVolumeStateMediatorForActiveSessions()
		{
			foreach(var session in this.LaunchedSessions)
				session.ResetStateNotificationMediator();
		}

		private void OnSessionEnded(AudioSessionModel session)
		{
			session.ResetVolumeHotkeys();
			session.ResetStateNotificationMediator();
			session.SessionEnded -= OnSessionEnded;
			this.LaunchedSessions.Remove(session);
		}

		private void OnSessionCreated(AudioSessionModel newSession)
		{
			if(this.RegisteredSessions.TryGetValue(newSession.Name, out var hotkeys))
			{
				newSession.SetVolumeHotkeys(volUp: hotkeys.Item1, volDown: hotkeys.Item2);
				newSession.SessionEnded += OnSessionEnded;
				this.LaunchedSessions.Add(newSession);
				if(SettingsProvider.NotificationsSettings.Enabled)
					newSession.SetStateNotificationMediator(this.SessionStateMediator);
			}
		}
	}
}