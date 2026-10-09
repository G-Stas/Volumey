using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Volumey.Controls;
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
		
		/// <summary>
		/// Provides collection of audio processes of the output device.
		/// Should be used directly only in the UI, other classes must call <see cref="GetImmutableProcesses"/> methods to access processes.
		/// </summary>
		public ObservableCollection<AudioProcessModel> Processes { get; }
		
		private IImmutableList<AudioProcessModel> _cachedImmutableProcesses;
		
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

		/// <summary>
		/// Windows reconfigured the device's audio engine: its format changed, or spatial sound was turned on or off (#82, #85).
		/// It disconnects all sessions then, and the device's objects stop working (AUDCLNT_E_DEVICE_INVALIDATED),
		/// so the model has to be replaced by a new one. Raised once, on any thread.
		/// </summary>
		internal event Action<OutputDeviceModel> Invalidated;
		private int invalidated;
		public string Id => this.Master.Id;
		private readonly IDevice device;
		private readonly ISessionProvider sessionProvider;
		private readonly IDeviceStateNotificationHandler deviceStateEvents;
		private DeviceFormat? currentStreamFormat;


		private readonly SemaphoreSlim _processesSemaphore = new SemaphoreSlim(1, 1);

		private static Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

		private static log4net.ILog logger;
		private static log4net.ILog Logger => logger ??= log4net.LogManager.GetLogger(typeof(OutputDeviceModel));

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
				process.DeviceReconfigured += OnProcessDeviceReconfigured;
			}

			this.currentStreamFormat = this.device.GetDeviceFormat();

			this.deviceStateEvents = deviceStateNotifications;
			this.deviceStateEvents.DeviceDisabled += OnDeviceDisabled;
			this.deviceStateEvents.NameChanged += OnDeviceNameChanged;
			this.deviceStateEvents.IconPathChanged += OnIconPathChanged;
			this.deviceStateEvents.FormatChanged += OnFormatChanged;
			this.sessionProvider = sessionProvider;
			this.sessionProvider.SessionCreated += OnSessionCreated;
			this.sessionProvider.DeviceInvalidated += RaiseInvalidated;
			
			this.Processes.CollectionChanged += OnProcessesCollectionChanged;
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

		internal async Task<IImmutableList<AudioProcessModel>> GetImmutableProcessesAsync()
		{
			if(_cachedImmutableProcesses != null)
				return _cachedImmutableProcesses;
			
			await _processesSemaphore.WaitAsync();
			try
			{
				return _cachedImmutableProcesses = this.Processes.ToImmutableList();
			}
			finally
			{
				_processesSemaphore.Release();
			}
		}

		internal IImmutableList<AudioProcessModel> GetImmutableProcesses()
		{
			if(_cachedImmutableProcesses != null)
				return _cachedImmutableProcesses;
			
			_processesSemaphore.Wait();
			try
			{
				return _cachedImmutableProcesses = this.Processes.ToImmutableList();
			}
			finally
			{
				_processesSemaphore.Release();
			}
		}

		/// <summary>
		/// Invalidates cached immutable processes collection if the original collection was changed
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnProcessesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			_cachedImmutableProcesses = null;
		}

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

		//raised on the UI thread
		private void OnFormatChanged(string deviceId)
		{
			if(!this.CompareId(deviceId))
				return;
			try
			{
				var newFormat = this.device.GetDeviceFormat();
				Logger.Info($"Device format notification: [{this.Master.DeviceFriendlyName}], {this.currentStreamFormat} -> {newFormat}");
				if(newFormat.HasValue && newFormat != this.currentStreamFormat)
				{
					this.currentStreamFormat = newFormat;
					RaiseInvalidated();
				}
			}
			catch(Exception e)
			{
				Logger.Error("Failed to handle a device format notification", e);
			}
		}

		private void OnProcessDeviceReconfigured(AudioProcessModel process) => RaiseInvalidated();

		private void RaiseInvalidated()
		{
			if(Interlocked.Exchange(ref this.invalidated, 1) == 0)
			{
				Logger.Info($"Device reconfigured, its model will be replaced: [{this.Master.DeviceFriendlyName}]");
				this.Invalidated?.Invoke(this);
			}
		}

		private void OnDeviceDisabled(string deviceId)
		{
			if(this.CompareId(deviceId))
				this._disabled?.Invoke(this);
		}

		private async void OnProcessExited(AudioProcessModel exitedProcess)
		{
			await OnProcessExitedAsync(exitedProcess);
		}

		private async Task OnProcessExitedAsync(AudioProcessModel exitedProcess)
		{
			await _processesSemaphore.WaitAsync();
			try
			{
				await dispatcher.InvokeAsync(() =>
				{
					this.Processes.Remove(exitedProcess);
				});
			}
			catch { }
			finally
			{
				_processesSemaphore.Release();
			}
			exitedProcess.Exited -= OnProcessExited;
			exitedProcess.Dispose();
		}

		private async void OnSessionCreated(object sender, SessionCreatedEventArgs e)
		{
			await _processesSemaphore.WaitAsync();
			try
			{
				if(Processes.FirstOrDefault(p => p.GroupingParam.Equals(e.SessionModel.GroupingParam) ||
											     p.FilePath.Equals(e.SessionModel.FilePath)) is AudioProcessModel
				   process)
				{
					await dispatcher.InvokeAsync(() =>
					{
						e.SessionModel.Name = process.Name;
						process.AddSession(e.SessionModel);
					});
				}
				else
				{
					AudioProcessModel newProcess = e.CreateProcessModel();
					newProcess.AddSession(e.SessionModel);
					
					await dispatcher.InvokeAsync(() =>
					{
						this.Processes.Add(newProcess);
					});
					
					newProcess.Exited += OnProcessExited;
					newProcess.DeviceReconfigured += OnProcessDeviceReconfigured;
					this.ProcessCreated?.Invoke(newProcess);
				}
			}
			catch { }
			finally
			{
				_processesSemaphore.Release();
			}
		}

		internal bool CompareId(string deviceId) =>
			string.Compare(this.Id, deviceId, StringComparison.InvariantCulture) == 0;

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public async void Dispose()
		{
			this.sessionProvider.SessionCreated -= OnSessionCreated;
			this.sessionProvider.DeviceInvalidated -= RaiseInvalidated;
			this.sessionProvider.Dispose();
			this.deviceStateEvents.DeviceDisabled -= OnDeviceDisabled;
			this.deviceStateEvents.NameChanged -= OnDeviceNameChanged;
			this.deviceStateEvents.IconPathChanged -= OnIconPathChanged;
			this.deviceStateEvents.FormatChanged -= OnFormatChanged;
			this.Master.Dispose();

			await _processesSemaphore.WaitAsync();
			try
			{
				foreach(var process in this.Processes)
					process.Dispose();
			}
			catch { }
			finally
			{
				_processesSemaphore.Release();
			}
			
			this._processesSemaphore.Dispose();
			//Release the device's COM objects on the thread pool: a release waits for running audio callbacks,
			//and those may wait for the UI thread
			var disposedDevice = this.device;
			_ = Task.Run(() => disposedDevice.Dispose());
			this._disabled = null;
			this.ProcessCreated = null;
			this.Invalidated = null;
		}
	}
}