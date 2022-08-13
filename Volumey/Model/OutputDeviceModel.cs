using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.CoreAudio;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.Model
{
	/// <summary>
	/// Represents an audio output device and provides its master and application audio sessions
	/// </summary>
	public sealed class OutputDeviceModel : INotifyPropertyChanged, IDisposable
	{
		public MasterSessionModel Master { get; }
		public ObservableCollection<AudioProcessModel> Processes { get; }

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
		internal event Action<AudioProcessModel> ProcessCreated;
		internal event Action<OutputDeviceModel> FormatChanged;
		public string Id => this.Master.Id;
		private readonly IDevice device;
		private readonly ISessionProvider sessionProvider;
		private readonly IDeviceStateNotificationHandler deviceStateEvents;
		private WAVEFORMATEX? currentStreamFormat;

		private object _processesLock = new object();

		private static Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

		public OutputDeviceModel(IDevice device, IDeviceStateNotificationHandler deviceStateNotifications, ISessionProvider sessionProvider,
			MasterSessionModel master, List<AudioProcessModel> processes)
		{
			this.device = device ?? throw new ArgumentNullException(nameof(device));
			this.Master = master ?? throw new ArgumentNullException(nameof(master));
			if(processes == null)
				throw new ArgumentNullException(nameof(processes));

			this.Processes = new ObservableCollection<AudioProcessModel>(processes);

			foreach(AudioProcessModel process in processes)
			{
				process.Exited += OnProcessExited;
			}

			this.currentStreamFormat = this.device.GetDeviceFormat();

			this.deviceStateEvents = deviceStateNotifications;
			this.deviceStateEvents.DeviceDisabled += OnDeviceDisabled;
			this.deviceStateEvents.NameChanged += OnDeviceNameChanged;
			this.deviceStateEvents.IconPathChanged += OnIconPathChanged;
			this.deviceStateEvents.FormatChanged += OnFormatChanged;
			this.sessionProvider = sessionProvider;
			this.sessionProvider.SessionCreated += OnSessionCreated;
		}

		internal bool SetVolumeHotkeys(HotKey volUp, HotKey volDown)
			=> this.Master.SetVolumeHotkeys(volUp: volUp, volDown: volDown);

		internal void ResetVolumeHotkeys() => this.Master.ResetVolumeHotkeys();

		internal void SetStateNotificationMediator(AudioProcessStateNotificationMediator mediator)
			=> this.Master.SetStateMediator(mediator);

		internal void ResetStateNotificationMediator(bool force = false)
			=> this.Master.ResetStateMediator(force);

		internal bool SetMuteHotkey(HotKey key)
			=> this.Master.SetMuteHotkey(key);

		internal void ResetMuteHotkeys()
			=> this.Master.ResetMuteHotkey();

		private void OnIconPathChanged(string deviceId)
		{
			if(this.CompareId(deviceId))
			{
				dispatcher.Invoke(() =>
				{
					try
					{
						if(this.Master.Icon != null)
						{
							NativeMethods.DestroyIcon(this.Master.Icon.Handle);
							this.Master.Icon.Dispose();
						}
						this.Master.Icon = this.device.GetIcon();
						this.Master.IconSource = this.Master.Icon.GetAsImageSource();
					}
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

		private void OnProcessExited(AudioProcessModel exitedProcess)
		{
			lock(_processesLock)
			{
				dispatcher.Invoke(() =>
				{
					this.Processes.Remove(exitedProcess);
				});
			}
			exitedProcess.Exited -= OnProcessExited;
			exitedProcess.Dispose();
		}

		private void OnSessionCreated((AudioSessionModel model, IAudioSessionControl sessionControl) newSession)
		{
			try
			{
				AudioProcessModel process = Processes.FirstOrDefault(p => p.FilePath.Equals(newSession.model.FilePath));
				if(process != null)
				{
					dispatcher.Invoke(() =>
					{
						newSession.model.Name = process.Name;
						process.AddSession(newSession.model);
					});
				}
				else
				{
					dispatcher.Invoke(() =>
					{
						AudioProcessModel newProcess = newSession.model.GetProcessModelFromSessionModel(newSession.sessionControl);
						newProcess.AddSession(newSession.model);
						lock(_processesLock)
						{
							this.Processes.Add(newProcess);
						}
						newProcess.Exited += OnProcessExited;
						this.ProcessCreated?.Invoke(newProcess);
					});

				}
			}
			catch { }
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
			foreach(var process in this.Processes)
				process.Dispose();
			this._disabled = null;
			this.ProcessCreated = null;
		}
	}
}