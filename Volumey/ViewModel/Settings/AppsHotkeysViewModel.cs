using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using log4net;
using MahApps.Metro.Controls;
using Microsoft.Xaml.Behaviors.Core;
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

					//set new value in AppSettings object but save it only in App Exit event handler
					//or when other parameters will be written to the file
					SettingsProvider.Settings.VolumeStep = value;
					HotkeysControl.SetVolumeStep(value);
				}
			}
		}
		
		public ObservableConcurrentDictionary<string, Tuple<HotKey, HotKey>> RegisteredSessions { get; }

		/// <summary>
		/// Contains names of the registered sessions that are not launched
		/// </summary>
		private List<string> ClosedApps = new List<string>();

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

		private ILog _logger;
		private ILog Logger => _logger ??= LogManager.GetLogger(typeof(AppsHotkeysViewModel));
		
		public AppsHotkeysViewModel()
		{
			this.AddAppCommand = new ActionCommand(AddApp);
			this.RemoveAppCommand = new ActionCommand(RemoveApp);

			var dProvider = DeviceProvider.GetInstance();
			dProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
			this.DefaultDevice = dProvider.DefaultDevice;

			ErrorDictionary.LanguageChanged += () => this.SetErrorMessage(this.CurrentErrorType);

			this.VolumeStep = SettingsProvider.Settings.VolumeStep;
			this.RegisteredSessions = SettingsProvider.HotkeysSettings.GetRegisteredSessions();
			if(this.RegisteredSessions.Keys.Count > 0)
				HotkeysControl.Activated += this.RegisterLoadedHotkeys;
		}

		/// <summary>
		/// Registers hotkeys for the apps that were loaded from settings on app startup
		/// </summary>
		private void RegisterLoadedHotkeys()
		{
			if(this.RegisteredSessions == null)
				return;
			
			if(this.DefaultDevice == null)
			{
				this.ClosedApps = this.RegisteredSessions.Keys.ToList();
				return;
			}

			foreach(var (name, hotkeys) in this.RegisteredSessions)
			{
				try
				{
					var model = this.DefaultDevice.GetAudioSession(name);
					if(model != null)
					{
						var (up, down) = hotkeys;
						model.SetHotkeys(up, down);
						model.SessionEnded += OnSessionEnded;
						this.LaunchedSessions.Add(model);
					}
					else
					{
						this.ClosedApps.Add(name);
					}
				}
				catch { }
			}

			if(this.ClosedApps.Count > 0)
				this.DefaultDevice.SessionCreated += OnSessionCreated;
		}

		private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
		{
			if(this.DefaultDevice != null)
			{
				this.DefaultDevice.SessionCreated -= OnSessionCreated;
			}
			
			//move currently launched sessions to the closed list which will be used to search for these sessions amongst sessions of the new device
			var launchedSessionCount = this.LaunchedSessions.Count;
			for(int i = launchedSessionCount-1; i >= 0; i--)
			{
				var session = this.LaunchedSessions[i];
				session.ResetHotkeys();
				session.SessionEnded -= OnSessionEnded;
				this.ClosedApps.Add(session.Name);
				this.LaunchedSessions.Remove(session);
			}
			
			this.DefaultDevice = newDevice;
			if(this.DefaultDevice == null)
			{
				HotkeysControl.SetHotkeysState(HotkeysState.Disabled);
			}
			else if(this.ClosedApps.Count > 0)
			{
				OnDeviceChanged();
			}
		}

		private void OnDeviceChanged()
		{
			if(HotkeysControl.VolumeHotkeysState == HotkeysState.Disabled)
				HotkeysControl.SetHotkeysState(HotkeysState.Enabled);

			//search currently closed apps amongst sessions of the new device
			var closedAppsCount = this.ClosedApps.Count;
			for(int i = closedAppsCount-1; i >= 0; i--)
			{
				try
				{
					var name = this.ClosedApps[i];
					var s = this.DefaultDevice.GetAudioSession(name);
					if(s != null)
					{
						var (upHotkey, downHotkey) = this.RegisteredSessions[name];
						s.SetHotkeys(upHotkey, downHotkey);
						s.SessionEnded += OnSessionEnded;
						this.ClosedApps.Remove(name);
						this.LaunchedSessions.Add(s);
					}
				}
				catch {}
			}
			if(this.ClosedApps.Count > 0)
				this.DefaultDevice.SessionCreated += OnSessionCreated;
		}

		private async void AddApp()
		{
			var session = this.SelectedSession;
			if(this.RegisteredSessions.ContainsKey(session.Name))
				return;

			if(HotkeysControl.HotkeysAreValid(this.VolumeUp, this.VolumeDown) is var error && error != ErrorMessageType.None)
			{
				this.SetErrorMessage(error);
				return;
			}

			if(!session.SetHotkeys(this.VolumeUp, this.VolumeDown))
			{
				this.SetErrorMessage(ErrorMessageType.VolumeReg);
				return;
			}
			this.SetErrorMessage(ErrorMessageType.None);
			session.SessionEnded += OnSessionEnded;

			var hotkeys = new Tuple<HotKey, HotKey>(this.VolumeUp, this.VolumeDown);
			this.RegisteredSessions.Add(session.Name, hotkeys);
			this.LaunchedSessions.Add(session);

			Logger.Info($"Registered app hotkeys. App: [{session.Name}] +vol: [{this.VolumeUp}] -vol: [{this.VolumeDown}], count: [{this.RegisteredSessions.Keys.Count}]");
			
			this.VolumeUp = this.VolumeDown = null;
			this.SelectedSession = null;
			
			try
			{
				SettingsProvider.HotkeysSettings.AddRegisteredSession(session.Name, hotkeys);
				await SettingsProvider.SaveSettings().ConfigureAwait(false);
			}
			catch { }
		}

		private async void RemoveApp()
		{
			if(this.SelectedRegApp == null)
				return;

			var sessionName = this.SelectedRegApp.Value.Key;
			AudioSessionModel sessionToRemove = null;
			foreach(var launchedSession in this.LaunchedSessions)
			{
				if(launchedSession.Name.Equals(sessionName))
				{
					sessionToRemove = launchedSession;
					break;
				}
			}
			if(sessionToRemove == null)
			{
				this.ClosedApps.Remove(sessionName);
				if(this.ClosedApps.Count == 0 && this.DefaultDevice != null)
				{
					this.DefaultDevice.SessionCreated -= OnSessionCreated;
				}
			}
			else
			{
				sessionToRemove.ResetHotkeys();
				sessionToRemove.SessionEnded -= OnSessionEnded;
				this.LaunchedSessions.Remove(sessionToRemove);
			}
			this.RegisteredSessions.Remove(sessionName);
			try
			{
				SettingsProvider.HotkeysSettings.RemoveRegisteredSession(sessionName);
				await SettingsProvider.SaveSettings().ConfigureAwait(false);	
			}
			catch { }
		}

		private void OnSessionEnded(AudioSessionModel session)
		{
			//if the list is empty we aren't subscribed to SessionCreated event yet
			if(this.ClosedApps.Count == 0)
			{
				if(this.DefaultDevice != null)
				{
					this.DefaultDevice.SessionCreated += OnSessionCreated;
				}
			}
			session.ResetHotkeys();
			session.SessionEnded -= OnSessionEnded;
			this.LaunchedSessions.Remove(session);
			this.ClosedApps.Add(session.Name);
		}

		private void OnSessionCreated(AudioSessionModel newSession)
		{
			var sessionName = newSession.Name;
			if(this.ClosedApps.Contains(sessionName))
			{
				var (upHotkey, downHotkey) = this.RegisteredSessions[sessionName];
				newSession.SetHotkeys(volUp: upHotkey, volDown: downHotkey);
				newSession.SessionEnded += OnSessionEnded;
				this.ClosedApps.Remove(sessionName);
				this.LaunchedSessions.Add(newSession);
				if(this.ClosedApps.Count == 0)
				{
					this.DefaultDevice.SessionCreated -= OnSessionCreated;
				}
			}
		}
	}
}