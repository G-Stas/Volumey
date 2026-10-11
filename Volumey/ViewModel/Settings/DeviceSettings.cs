using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using log4net;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Localization;
using Volumey.Model;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// The audio features of a connected device on the Devices page: format, exclusive mode and balance.
/// One per device model: a change that reconfigures the device (format) rebuilds the model, and the new model
/// gets new settings that read the new state. Read when the device is selected (<see cref="Load"/>).
/// </summary>
public sealed partial class DeviceSettings : ObservableObject
{
	private static readonly ILog Logger = LogManager.GetLogger(typeof(DeviceSettings));

	private readonly OutputDeviceModel model;
	private readonly IDeviceProvider provider;
	private bool loading, loaded, detached;

	public DeviceSettings(OutputDeviceModel model, IDeviceProvider provider)
	{
		this.model = model;
		this.provider = provider;
	}

	/// <summary>
	/// A change is being applied: the controls are disabled meanwhile
	/// </summary>
	[ObservableProperty]
	public partial bool IsBusy { get; private set; }

	[ObservableProperty]
	public partial string Error { get; private set; }

	private ErrorMessageType errorType = ErrorMessageType.None;

	internal void SetError(ErrorMessageType type)
	{
		errorType = type;
		Error = type == ErrorMessageType.None ? string.Empty : ErrorDictionary.Instance[type];
	}

	internal void RefreshError() => SetError(errorType);

	/// <summary>
	/// Reads the device's state off the UI thread, once
	/// </summary>
	internal void Load()
	{
		if(loading || loaded || detached)
			return;
		loading = true;
		LoadAsync().Forget(Logger, "Failed to read the device's settings");
	}

	private async Task LoadAsync()
	{
		var formats = Task.Run(model.GetSupportedFormats);
		var features = provider.GetDeviceFeaturesAsync(model.Id);
		ShowFeatures(await features ?? DeviceFeatureState.Unknown);
		SetSupportedFormats(await formats ?? Array.Empty<DeviceFormatOption>());
		if(!detached)
		{
			model.ChannelVolumesChanged += ReadChannelVolumes;
			ReadChannelVolumes();
		}
		loading = false;
		loaded = true;
	}

	/// <summary>
	/// The model was replaced or the device went away: stop listening
	/// </summary>
	internal void Detach()
	{
		detached = true;
		model.ChannelVolumesChanged -= ReadChannelVolumes;
	}

	/// <summary>
	/// Runs a change with the controls disabled; on failure the error shows and <paramref name="restore"/> puts the real state back
	/// </summary>
	private void Change(Func<Task<bool>> change, ErrorMessageType failure, Action restore)
		=> ChangeAsync(change, failure, restore).Forget(Logger, "Failed to change a device setting");

	private async Task ChangeAsync(Func<Task<bool>> change, ErrorMessageType failure, Action restore)
	{
		IsBusy = true;
		SetError(ErrorMessageType.None);
		bool changed = await change();
		IsBusy = false;
		if(!changed)
		{
			SetError(failure);
			restore();
		}
	}

	#region Format

	/// <summary>
	/// The format the audio engine runs the device at, e.g. "24 bit, 48000 Hz"; empty if it can't be read
	/// </summary>
	public string Format => model.Format is { } format ? FormatName(format.Bits, format.SamplesPerSec) : string.Empty;

	internal static string FormatName(ushort bits, uint samplesPerSec)
	{
		string pattern = TranslationSource.Instance["Devices_FormatName"];
		return string.Format(string.IsNullOrEmpty(pattern) ? "{0} bit, {1} Hz" : pattern, bits, samplesPerSec);
	}

	/// <summary>
	/// The formats the device can run at; empty until they're read, or if they can't be
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanChooseFormat))]
	public partial IReadOnlyList<DeviceFormatOption> SupportedFormats { get; private set; } = Array.Empty<DeviceFormatOption>();

	/// <summary>
	/// The format list is shown (otherwise the current format as text)
	/// </summary>
	public bool CanChooseFormat => SupportedFormats.Count > 0;

	private DeviceFormatOption? selectedFormat;
	/// <summary>
	/// The list's choice: the device's current format, or the one being set. Picking another one sets it (Windows reconfigures the device).
	/// </summary>
	public DeviceFormatOption? SelectedFormat
	{
		get => selectedFormat;
		set
		{
			if(Equals(value, selectedFormat))
				return;
			selectedFormat = value;
			OnPropertyChanged();
			if(value is { } option && !(model.Format is { } current && option.Matches(current)))
				Change(() => provider.SetDeviceFormatAsync(model.Id, option), ErrorMessageType.FormatRefused, ShowCurrentFormat);
		}
	}

	private void ShowCurrentFormat()
	{
		selectedFormat = CurrentOption(SupportedFormats);
		OnPropertyChanged(nameof(SelectedFormat));
	}

	/// <summary>
	/// The list read for the device, with its current format selected already when the list shows
	/// </summary>
	internal void SetSupportedFormats(IReadOnlyList<DeviceFormatOption> formats)
	{
		selectedFormat = CurrentOption(formats);
		SupportedFormats = formats;
		OnPropertyChanged(nameof(SelectedFormat));
	}

	private DeviceFormatOption? CurrentOption(IReadOnlyList<DeviceFormatOption> formats)
		=> model.Format is { } current ? formats.Where(o => o.Matches(current)).Cast<DeviceFormatOption?>().FirstOrDefault() : null;

	#endregion

	#region Exclusive mode

	private DeviceFeatureState features = DeviceFeatureState.Unknown;

	private void ShowFeatures(DeviceFeatureState state)
	{
		features = state;
		allowExclusive = state.AllowExclusive ?? false;
		exclusivePriority = state.ExclusivePriority ?? false;
		OnPropertyChanged(nameof(HasExclusiveMode));
		OnPropertyChanged(nameof(AllowExclusive));
		OnPropertyChanged(nameof(ExclusivePriority));
		OnPropertyChanged(nameof(CanSetExclusivePriority));
	}

	public bool HasExclusiveMode => features.AllowExclusive != null && features.ExclusivePriority != null;

	private bool allowExclusive;
	public bool AllowExclusive
	{
		get => allowExclusive;
		set
		{
			SetSwitch(ref allowExclusive, value, DeviceSwitch.AllowExclusive, nameof(AllowExclusive));
			OnPropertyChanged(nameof(CanSetExclusivePriority));
		}
	}

	private bool exclusivePriority;
	public bool ExclusivePriority
	{
		get => exclusivePriority;
		set => SetSwitch(ref exclusivePriority, value, DeviceSwitch.ExclusivePriority, nameof(ExclusivePriority));
	}

	/// <summary>
	/// Priority only matters while apps may take exclusive control (as in Windows' sound settings)
	/// </summary>
	public bool CanSetExclusivePriority => AllowExclusive;

	private void SetSwitch(ref bool field, bool value, DeviceSwitch setting, string propertyName)
	{
		if(field == value)
			return;
		bool previous = field;
		field = value;
		OnPropertyChanged(propertyName);
		Change(async () =>
		{
			bool changed = await provider.SetDeviceSwitchAsync(model.Id, setting, value);
			if(changed)
				features = setting switch
				{
					DeviceSwitch.AllowExclusive => features with { AllowExclusive = value },
					_ => features with { ExclusivePriority = value }
				};
			return changed;
		}, ErrorMessageType.SettingFailed, () => RestoreSwitch(setting, previous, propertyName));
	}

	private void RestoreSwitch(DeviceSwitch setting, bool value, string propertyName)
	{
		switch(setting)
		{
			case DeviceSwitch.AllowExclusive: allowExclusive = value; break;
			default: exclusivePriority = value; break;
		}
		OnPropertyChanged(propertyName);
		OnPropertyChanged(nameof(CanSetExclusivePriority));
	}

	#endregion

	#region Balance

	/// <summary>
	/// The device has a left and a right channel (the first two channels; Windows' sound settings show the same)
	/// </summary>
	[ObservableProperty]
	public partial bool HasBalance { get; private set; }

	private double left, right;

	/// <summary>
	/// The left channel's level, 0–100 (a channel can't be louder than the device volume: Windows keeps the device volume at the louder one)
	/// </summary>
	public double Left
	{
		get => left;
		set => SetChannel(ref left, value, 0, nameof(Left));
	}

	public double Right
	{
		get => right;
		set => SetChannel(ref right, value, 1, nameof(Right));
	}

	private void SetChannel(ref double field, double value, int channel, string propertyName)
	{
		value = Math.Round(Math.Clamp(value, 0, 100));
		if(field == value)
			return;
		field = value;
		OnPropertyChanged(propertyName);
		try
		{
			model.SetChannelVolume(channel, (float)(value / 100));
		}
		catch(Exception e)
		{
			Logger.Error("Failed to change the balance", e);
			SetError(ErrorMessageType.SettingFailed);
			ReadChannelVolumes();
		}
	}

	/// <summary>
	/// Shows the levels Windows has now (after a change by Volumey, Windows or the device volume)
	/// </summary>
	private void ReadChannelVolumes()
	{
		var levels = model.GetChannelVolumes();
		HasBalance = levels.Count >= 2;
		if(!HasBalance)
			return;
		left = Math.Round(levels[0] * 100);
		right = Math.Round(levels[1] * 100);
		OnPropertyChanged(nameof(Left));
		OnPropertyChanged(nameof(Right));
	}

	#endregion
}
