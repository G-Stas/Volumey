using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Localization;
using Volumey.Model;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// A device on the Devices page: a connected output device, or one that isn't connected but has a saved "make default" hotkey.
/// Its hotkey is edited while it's off and turned on with <see cref="HotkeyRegistered"/> (<see cref="DefaultDeviceHotkeysViewModel"/>).
/// </summary>
public sealed partial class DeviceEntry : ObservableObject
{
	private readonly DefaultDeviceHotkeysViewModel owner;

	/// <summary>
	/// The device's ID; a saved device is also matched by its name, and the ID is updated then
	/// </summary>
	[ObservableProperty]
	public partial string Id { get; internal set; }

	[ObservableProperty]
	public partial string Name { get; internal set; }

	/// <summary>
	/// The device's model while it's connected, otherwise null
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsConnected), nameof(IconSource), nameof(Format))]
	public partial OutputDeviceModel Model { get; internal set; }

	public bool IsConnected => Model != null;

	public ImageSource IconSource => Model?.Master.IconSource;

	/// <summary>
	/// The format the audio engine runs the device at, e.g. "24 bit, 48000 Hz"; empty if it isn't connected or the format can't be read
	/// </summary>
	public string Format => Model?.Format is { } format ? FormatName(format.Bits, format.SamplesPerSec) : string.Empty;

	internal static string FormatName(ushort bits, uint samplesPerSec)
	{
		string pattern = TranslationSource.Instance["Devices_FormatName"];
		return string.Format(string.IsNullOrEmpty(pattern) ? "{0} bit, {1} Hz" : pattern, bits, samplesPerSec);
	}

	partial void OnModelChanged(OutputDeviceModel value)
	{
		//a new model (plugged in, or rebuilt after a format change): its formats are read again
		SupportedFormats = Array.Empty<DeviceFormatOption>();
		ShowCurrentFormat();
		owner.OnModelChanged(this);
	}

	/// <summary>
	/// The formats the device can run at (read when it's selected); empty until then, or if they can't be read
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanChooseFormat))]
	public partial IReadOnlyList<DeviceFormatOption> SupportedFormats { get; internal set; } = Array.Empty<DeviceFormatOption>();

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
			if(value is { } option && !(Model?.Format is { } current && option.Matches(current)))
				owner.ChangeFormat(this, option);
		}
	}

	/// <summary>
	/// Selects the option of the device's current format in the list (without setting anything)
	/// </summary>
	internal void ShowCurrentFormat()
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
		=> Model?.Format is { } current ? formats.Where(o => o.Matches(current)).Cast<DeviceFormatOption?>().FirstOrDefault() : null;

	/// <summary>
	/// A format is being set (the list is disabled meanwhile)
	/// </summary>
	[ObservableProperty]
	public partial bool IsChangingFormat { get; internal set; }

	[ObservableProperty]
	public partial string FormatError { get; private set; }

	internal ErrorMessageType FormatErrorType { get; private set; } = ErrorMessageType.None;

	internal void SetFormatError(ErrorMessageType type)
	{
		FormatErrorType = type;
		FormatError = type == ErrorMessageType.None ? string.Empty : ErrorDictionary.Instance[type];
	}

	[ObservableProperty]
	public partial bool IsDefault { get; internal set; }

	[ObservableProperty]
	public partial HotKey Hotkey { get; set; }

	partial void OnHotkeyChanged(HotKey value) => SetError(ErrorMessageType.None);

	private bool hotkeyRegistered;
	/// <summary>
	/// The hotkey is on: registered and saved. Turning it on can fail (an error shows then and it stays off).
	/// </summary>
	public bool HotkeyRegistered
	{
		get => hotkeyRegistered;
		set
		{
			if(value == hotkeyRegistered)
				return;
			if(value)
				hotkeyRegistered = owner.TurnOn(this);
			else
			{
				owner.TurnOff(this);
				hotkeyRegistered = false;
			}
			OnPropertyChanged();
			if(hotkeyRegistered == value)
				owner.OnHotkeyStateChanged(this);
		}
	}

	/// <summary>
	/// Sets the state without registering anything (a saved hotkey being loaded)
	/// </summary>
	internal void SetRegistered(bool registered)
	{
		hotkeyRegistered = registered;
		OnPropertyChanged(nameof(HotkeyRegistered));
	}

	[ObservableProperty]
	public partial string ErrorMessage { get; private set; }

	internal ErrorMessageType ErrorType { get; private set; } = ErrorMessageType.None;

	internal void SetError(ErrorMessageType type)
	{
		ErrorType = type;
		ErrorMessage = type == ErrorMessageType.None ? string.Empty : ErrorDictionary.Instance[type];
	}

	internal DeviceEntry(DefaultDeviceHotkeysViewModel owner, string id, string name)
	{
		this.owner = owner;
		Id = id;
		Name = name;
	}
}
