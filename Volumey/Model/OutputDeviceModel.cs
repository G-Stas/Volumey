using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.CoreAudio;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.Model
{
	/// <summary>
	/// Represents an audio output device and provides its master and application audio sessions
	/// </summary>
	public sealed class OutputDeviceModel : INotifyPropertyChanged, IDisposable
	{
		private string name;
		public string Name
		{
			get => name;
			private set
			{
				name = value;
				OnPropertyChanged();
			}
		}
		
		public MasterSessionModel Master { get; }
		public ObservableCollection<AudioSessionModel> Sessions { get; }
		internal event Action<OutputDeviceModel> Disabled;
		internal event Action<AudioSessionModel> SessionCreated;
		internal event Action<OutputDeviceModel> FormatChanged;
		public readonly string Id;
		private readonly IDevice device;
		private readonly ISessionProvider sessionProvider;
		private readonly IDeviceStateNotificationHandler deviceStateEvents;
		private WAVEFORMATEX? currentStreamFormat;

		private static Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

		public OutputDeviceModel(IDevice device, IDeviceStateNotificationHandler deviceStateNotifications, ISessionProvider sessionProvider,
			MasterSessionModel master, ObservableCollection<AudioSessionModel> sessions)
		{
			this.device = device ?? throw new ArgumentNullException(nameof(device));
			this.Id = device.GetId();
			this.Master = master ?? throw new ArgumentNullException(nameof(master));
			this.Sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
			this.Name = master.DeviceDesc;
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

		internal bool SetHotkeys(HotKey volUp, HotKey volDown) =>
			this.Master.SetHotkeys(volUp: volUp, volDown: volDown);

		internal void ResetHotkeys() => this.Master.ResetHotkeys();

		private void OnIconPathChanged(string deviceId)
		{
			if(this.CompareId(deviceId))
			{
				dispatcher.Invoke(() =>
				{
					try { this.Master.DeviceIcon = this.device.GetIconSource(); }
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
						this.Master.DeviceFriendlyName = friendlyName;
						this.Name = this.Master.DeviceDesc = deviceDesc;
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

		/// <summary>
		/// Search for audio session by its name in the collection of audio sessions of the device
		/// </summary>
		/// <param name="appName">The name of audio session</param>
		public AudioSessionModel GetAudioSession(string appName)
		{
			AudioSessionModel musicSession = null;
			Monitor.Enter(this.Sessions);
			try
			{
				foreach(var session in this.Sessions)
				{
					if(session.Name.Equals(appName, StringComparison.CurrentCulture))
					{
						musicSession = session;
						break;
					}
				}
			}
			finally
			{
				Monitor.Exit(this.Sessions);
			}
			return musicSession;
		}

		private void OnDeviceDisabled(string deviceId)
		{
			if(this.CompareId(deviceId))
				this.Disabled?.Invoke(this);
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
			this.Disabled = null;
			this.SessionCreated = null;
		}
	}
}