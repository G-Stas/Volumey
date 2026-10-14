using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.Model;

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
			_disabled -= value;
			_disabled += value;
		}
		remove => _disabled -= value;
	}
	internal event Action<AudioProcessModel> ProcessCreated;

	/// <summary>
	/// Windows reconfigured the device's audio engine: its format changed, or spatial sound was turned on or off.
	/// It disconnects all sessions then, and the device's objects stop working (AUDCLNT_E_DEVICE_INVALIDATED),
	/// so the model has to be replaced by a new one. Raised once, on any thread.
	/// </summary>
	internal event Action<OutputDeviceModel> Invalidated;
	private int invalidated;
	private volatile bool disposed;
	public string Id => Master.Id;
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
		Master = master ?? throw new ArgumentNullException(nameof(master));
		if(processes == null)
			throw new ArgumentNullException(nameof(processes));

		Processes = new ObservableCollection<AudioProcessModel>(processes);

		foreach(AudioProcessModel process in processes)
		{
			process.Exited += OnProcessExited;
			process.DeviceReconfigured += OnProcessDeviceReconfigured;
		}

		currentStreamFormat = this.device.GetDeviceFormat();

		deviceStateEvents = deviceStateNotifications;
		deviceStateEvents.DeviceDisabled += OnDeviceDisabled;
		deviceStateEvents.NameChanged += OnDeviceNameChanged;
		deviceStateEvents.IconPathChanged += OnIconPathChanged;
		deviceStateEvents.FormatChanged += OnFormatChanged;
		this.sessionProvider = sessionProvider;
		this.sessionProvider.SessionCreated += OnSessionCreated;
		this.sessionProvider.DeviceInvalidated += RaiseInvalidated;

		Processes.CollectionChanged += OnProcessesCollectionChanged;
	}

	internal bool SetVolumeHotkeys(HotKey volUp, HotKey volDown)
		=> Master.SetVolumeHotkeys(volUp: volUp, volDown: volDown);

	/// <param name="failed">The hotkey the system refused, null otherwise</param>
	internal bool SetVolumeHotkeys(HotKey volUp, HotKey volDown, out HotKey failed)
		=> Master.SetVolumeHotkeys(volUp, volDown, out failed);

	/// <summary>
	/// The device's format when the model was made (a format change makes a new model, see <see cref="Invalidated"/>); null if it couldn't be read
	/// </summary>
	internal DeviceFormat? Format => currentStreamFormat;

	/// <summary>
	/// The formats the device can run at (makes COM calls: call it off the UI thread); empty if they can't be read
	/// </summary>
	internal IReadOnlyList<DeviceFormatOption> GetSupportedFormats()
		=> disposed ? Array.Empty<DeviceFormatOption>() : device.GetSupportedFormats();

	/// <summary>
	/// The level of each channel (0..1): the device's balance; empty if they can't be read
	/// </summary>
	internal IReadOnlyList<float> GetChannelVolumes() => disposed ? Array.Empty<float>() : device.GetChannelVolumes();

	internal void SetChannelVolume(int channel, float level)
	{
		if(!disposed)
			device.SetChannelVolume(channel, level);
	}

	private Action channelVolumesChanged;
	/// <summary>
	/// A channel level or the device volume changed; raised on the UI thread
	/// </summary>
	internal event Action ChannelVolumesChanged
	{
		add
		{
			if(channelVolumesChanged == null && !disposed)
				device.ChannelVolumesChanged += OnChannelVolumesChanged;
			channelVolumesChanged += value;
		}
		remove
		{
			channelVolumesChanged -= value;
			if(channelVolumesChanged == null && !disposed)
				device.ChannelVolumesChanged -= OnChannelVolumesChanged;
		}
	}

	private int channelVolumesQueued;

	//a COM thread: passed on without waiting (see DeviceStateNotificationsHandler).
	//The listeners read the levels when it runs, so the changes that arrive meanwhile don't queue more
	private void OnChannelVolumesChanged()
	{
		if(Interlocked.Exchange(ref channelVolumesQueued, 1) != 0)
			return;
		dispatcher.InvokeAsync(() =>
		{
			Volatile.Write(ref channelVolumesQueued, 0);
			channelVolumesChanged?.Invoke();
		});
	}

	internal void ResetVolumeHotkeys() => Master.ResetVolumeHotkeys();

	internal void SetStateNotificationMediator(IAudioProcessStateMediator mediator)
		=> Master.SetStateMediator(mediator);

	internal void ResetStateNotificationMediator(bool force = false)
		=> Master.ResetStateMediator(force);

	internal bool SetMuteHotkey(HotKey key)
		=> Master.SetMuteHotkey(key);

	internal void ResetMuteHotkeys()
		=> Master.ResetMuteHotkey();

	/// <remarks>
	/// Empty once the model is disposed: listeners still get the old default device after it was disabled and disposed
	/// (e.g. a headset that drops out on sleep and wake), and must not see its disposed processes.
	/// </remarks>
	internal async Task<IImmutableList<AudioProcessModel>> GetImmutableProcessesAsync()
	{
		if(disposed)
			return ImmutableList<AudioProcessModel>.Empty;
		if(_cachedImmutableProcesses != null)
			return _cachedImmutableProcesses;

		await _processesSemaphore.WaitAsync();
		try
		{
			return _cachedImmutableProcesses = Processes.ToImmutableList();
		}
		finally
		{
			_processesSemaphore.Release();
		}
	}

	/// <summary>
	/// For the UI thread, the only one that changes <see cref="Processes"/>, so no lock is needed. Taking the lock
	/// would deadlock with <see cref="OnSessionCreated"/>: that holds the lock while it waits for the UI thread.
	/// </summary>
	internal IImmutableList<AudioProcessModel> GetImmutableProcesses()
	{
		if(disposed)
			return ImmutableList<AudioProcessModel>.Empty;
		return _cachedImmutableProcesses ??= Processes.ToImmutableList();
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
		if(CompareId(deviceId))
		{
			dispatcher.Invoke(() =>
			{
				try
				{
					//switch to the new icon before releasing the old one, which the tray may still show
					var oldIcon = Master.Icon;
					Master.Icon = device.GetIcon();
					Master.IconSource = Master.Icon?.GetAsImageSource();
					oldIcon?.Dispose();
				}
				catch(Exception e)
				{
					Logger.Error("Failed to update the device icon", e);
				}
			});
		}
	}

	private void OnDeviceNameChanged(string deviceId)
	{
		if(CompareId(deviceId))
		{
			dispatcher.Invoke(() =>
			{
				try
				{
					string friendlyName = device.GetFriendlyName();
					string deviceDesc = device.GetDeviceDesc();
					Master.DeviceDesc = deviceDesc;
					Master.Name = friendlyName;
				}
				catch { }
			});
		}
	}

	//raised on the UI thread
	private void OnFormatChanged(string deviceId)
	{
		if(!CompareId(deviceId))
			return;
		try
		{
			var newFormat = device.GetDeviceFormat();
			Logger.Info($"Device format notification: [{Master.DeviceFriendlyName}], {currentStreamFormat} -> {newFormat}");
			if(newFormat.HasValue && newFormat != currentStreamFormat)
			{
				currentStreamFormat = newFormat;
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
		if(Interlocked.Exchange(ref invalidated, 1) == 0)
		{
			Logger.Info($"Device reconfigured, its model will be replaced: [{Master.DeviceFriendlyName}]");
			Invalidated?.Invoke(this);
		}
	}

	private void OnDeviceDisabled(string deviceId)
	{
		if(CompareId(deviceId))
			_disabled?.Invoke(this);
	}

	private void OnProcessExited(AudioProcessModel exitedProcess)
		=> OnProcessExitedAsync(exitedProcess).Forget(Logger, $"Failed to remove the exited app [{exitedProcess.Name}]");

	private async Task OnProcessExitedAsync(AudioProcessModel exitedProcess)
	{
		exitedProcess.Exited -= OnProcessExited;
		await _processesSemaphore.WaitAsync();
		try
		{
			await dispatcher.InvokeAsync(() => Processes.Remove(exitedProcess));
		}
		finally
		{
			_processesSemaphore.Release();
		}
		exitedProcess.Dispose();
	}

	//raised on a COM thread
	private void OnSessionCreated(object sender, SessionCreatedEventArgs e)
		=> OnSessionCreatedAsync(e).Forget(Logger, "Failed to add a new audio session");

	private async Task OnSessionCreatedAsync(SessionCreatedEventArgs e)
	{
		var session = e.SessionModel;
		bool added = false;
		await _processesSemaphore.WaitAsync();
		try
		{
			if(disposed)
				return;

			if(Processes.FirstOrDefault(p => p.GroupingParam.Equals(session.GroupingParam) || p.FilePath.Equals(session.FilePath)) is AudioProcessModel process)
			{
				await dispatcher.InvokeAsync(() =>
				{
					session.Name = process.Name;
					process.AddSession(session);
				});
				added = true;
			}
			else if(e.CreateProcessModel() is AudioProcessModel newProcess)
			{
				newProcess.AddSession(session);
				newProcess.Exited += OnProcessExited;
				newProcess.DeviceReconfigured += OnProcessDeviceReconfigured;
				//listeners get the new process on the UI thread (after an await this method runs on the thread pool)
				await dispatcher.InvokeAsync(() =>
				{
					Processes.Add(newProcess);
					ProcessCreated?.Invoke(newProcess);
				});
				added = true;
			}
		}
		finally
		{
			_processesSemaphore.Release();
			//a session nobody took (the device was disposed, or its app couldn't be read) would never be released
			if(!added)
				session.Dispose();
		}
	}

	internal bool CompareId(string deviceId) =>
		string.Compare(Id, deviceId, StringComparison.InvariantCulture) == 0;

	//never raised: the bound properties don't change, but without INotifyPropertyChanged WPF tracks bindings to the model
	//through PropertyDescriptor, which keeps the model alive
	public event PropertyChangedEventHandler PropertyChanged { add { } remove { } }

	/// <summary>
	/// Stops all notifications right away; the apps and the device's COM objects are released afterwards
	/// (the COM release on the thread pool: it waits for running audio callbacks, and those may wait for the UI thread).
	/// </summary>
	public void Dispose()
	{
		if(disposed)
			return;
		disposed = true;
		sessionProvider.SessionCreated -= OnSessionCreated;
		sessionProvider.DeviceInvalidated -= RaiseInvalidated;
		sessionProvider.Dispose();
		deviceStateEvents.DeviceDisabled -= OnDeviceDisabled;
		deviceStateEvents.NameChanged -= OnDeviceNameChanged;
		deviceStateEvents.IconPathChanged -= OnIconPathChanged;
		deviceStateEvents.FormatChanged -= OnFormatChanged;
		Master.Dispose();
		_disabled = null;
		ProcessCreated = null;
		Invalidated = null;
		//the device unsubscribes from Windows when it's released
		channelVolumesChanged = null;
		ReleaseAsync().Forget(Logger, $"Failed to release the device [{Master.DeviceFriendlyName}]");
	}

	private async Task ReleaseAsync()
	{
		//waits for a session that is being added; the semaphore isn't disposed: it holds no OS handle here,
		//and late callers (listeners, session events in flight) would throw
		await _processesSemaphore.WaitAsync();
		try
		{
			foreach(var process in Processes)
				process.Dispose();
		}
		finally
		{
			_processesSemaphore.Release();
		}
		var disposedDevice = device;
		await Task.Run(() => disposedDevice.Dispose());
	}
}
