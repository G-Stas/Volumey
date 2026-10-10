using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Volumey.Controls;
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
	public string Format => Model?.Format is { } format ? $"{format.BitsPerSample} bit, {format.SamplesPerSec} Hz" : string.Empty;

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
