using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.CoreAudio;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.ViewModel.Settings;

namespace Volumey.Model
{
	/// <summary>
	/// Represents an audio output device and provides its master and application audio sessions
	/// </summary>
	public sealed class OutputDeviceModel : INotifyPropertyChanged, IDisposable
	{
		public MasterSessionModel Master { get; }
		public ObservableCollection<AudioSessionModel> Sessions { get; }

		private Action<OutputDeviceModel> _disabled;
		internal event Action<OutputDeviceModel> Disabled
		{
			add
			{
				//Prevent multiple subscribing of the same event handler
				this._disabled -= value;
				this._disabled += value;
			}
			remove => this._disabled -= value;
		}
		internal event Action<AudioSessionModel> SessionCreated;
		internal event Action<OutputDeviceModel> FormatChanged;
		public string Id => this.Master.Id;
		private readonly IDevice device;
		private readonly ISessionProvider sessionProvider;
		private readonly IDeviceStateNotificationHandler deviceStateEvents;
		private WAVEFORMATEX? currentStreamFormat;

		private static Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

		public OutputDeviceModel(IDevice device, IDeviceStateNotificationHandler deviceStateNotifications, ISessionProvider sessionProvider,
			MasterSessionModel master, ObservableCollection<AudioSessionModel> sessions)
		{
			this.device = device ?? throw new ArgumentNullException(nameof(device));
			this.Master = master ?? throw new ArgumentNullException(nameof(master));
			this.Sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
			this.currentStreamFormat = this.device.GetDeviceFormat();

			this.deviceStateEvents = deviceStateNotifications;
			this.deviceStateEvents.DeviceDisabled += OnDeviceDisabled;
			this.deviceStateEvents.NameChanged += OnDeviceNameChanged;
			this.deviceStateEvents.IconPathChanged += OnIconPathChanged;
			this.deviceStateEvents.FormatChanged += OnFormatChanged;
			this.sessionProvider = sessionProvider;
			this.sessionProvider.SessionCreated += OnSessionCreated;
			
			foreach(var session in sessions)
				session.SessionEnded += OnSessionEnded;
		}

		internal bool SetVolumeHotkeys(HotKey volUp, HotKey volDown)
			=> this.Master.SetVolumeHotkeys(volUp: volUp, volDown: volDown);

		internal void ResetVolumeHotkeys() => this.Master.ResetVolumeHotkeys();

		internal void SetStateNotificationMediator(AudioSessionStateNotificationMediator mediator)
			=> this.Master.SetStateNotificationMediator(mediator);

		internal void ResetStateNotificationMediator()
			=> this.Master.ResetStateNotificationMediator();

		internal bool SetMuteHotkeys(HotKey key)
			=> this.Master.SetMuteHotkeys(key);

		internal void ResetMuteHotkeys()
			=> this.Master.ResetMuteHotkey();

		private void OnIconPathChanged(string deviceId)
		{
			if(this.CompareId(deviceId))
			{
				dispatcher.Invoke(() =>
				{
					try { this.Master.Icon = this.device.GetIconSource(); }
					catch { }
				});
			}
		}

		private void OnDeviceNameChanged(string deviceId)
		{
			if(this.CompareId(deviceId))
			{
				dispatcher.Invoke(() =>
				{
					try
					{
						string friendlyName = this.device.GetFriendlyName();
						string deviceDesc = this.device.GetDeviceDesc();
						this.Master.DeviceDesc = deviceDesc;
						this.Master.Name = friendlyName;
					}
					catch { }
				});
			}
		}

		private async void OnFormatChanged(string deviceId)
		{
			if(this.CompareId(deviceId))
			{
				try
				{
					await dispatcher.InvokeAsync(() =>
					{
						var newFormat = this.device.GetDeviceFormat();
						if(newFormat.HasValue && !newFormat.Value.Equals(this.currentStreamFormat))
						{
							currentStreamFormat = newFormat.Value;
							this.FormatChanged?.Invoke(this);
						}
					});
				}
				catch { }
			}
		}

		private void OnDeviceDisabled(string deviceId)
		{
			if(this.CompareId(deviceId))
				this._disabled?.Invoke(this);
		}

		private void OnSessionCreated(AudioSessionModel newSession)
		{
			if(newSession != null)
			{
				dispatcher.Invoke(() =>
				{
					newSession.SessionEnded += OnSessionEnded;
					this.Sessions.Add(newSession);
					this.SessionCreated?.Invoke(newSession);
				});
			}
		}

		private void OnSessionEnded(AudioSessionModel session)
		{
			if(session != null)
			{
				dispatcher.Invoke(() =>
				{
					session.SessionEnded -= OnSessionEnded;
					this.Sessions.Remove(session);
					session.Dispose();
				});
			}
		}

		internal bool CompareId(string deviceId) =>
			string.Compare(this.Id, deviceId, StringComparison.InvariantCulture) == 0;

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public void Dispose()
		{
			this.sessionProvider.SessionCreated -= OnSessionCreated;
			this.sessionProvider.Dispose();
			this.deviceStateEvents.DeviceDisabled -= OnDeviceDisabled;
			this.deviceStateEvents.NameChanged -= OnDeviceNameChanged;
			this.deviceStateEvents.IconPathChanged -= OnIconPathChanged;
			this.deviceStateEvents.FormatChanged -= OnFormatChanged;
			this.Master.Dispose();
			if(this.Sessions != null)
			{
				foreach(var session in this.Sessions)
				{
					session.SessionEnded -= OnSessionEnded;
					session.Dispose();
				}	
			}
			this._disabled = null;
			this.SessionCreated = null;
		}
	}
}