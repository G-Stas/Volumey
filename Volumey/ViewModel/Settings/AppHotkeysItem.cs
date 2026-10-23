#nullable enable
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Volumey.Controls;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// One app on the App hotkeys page: its process name and hotkeys (each optional, at least one), which can be changed in place
/// (<see cref="AppsHotkeysViewModel"/> checks and registers a change, and puts the old hotkey back if it can't)
/// </summary>
public sealed partial class AppHotkeysItem : ObservableObject
{
	private readonly AppsHotkeysViewModel owner;

	public string ProcessName { get; }

	/// <summary>
	/// The app's icon while it's running on the default device, otherwise null
	/// </summary>
	[ObservableProperty]
	public partial ImageSource? Icon { get; internal set; }

	/// <summary>
	/// Why the last change of a hotkey was rejected, under its box
	/// </summary>
	public HotkeyPairErrors Errors { get; } = new HotkeyPairErrors();

	private HotKey? volumeUp;
	public HotKey? VolumeUp
	{
		get => volumeUp;
		set => owner.ChangeHotkey(this, AppHotkeyKind.Up, value);
	}

	private HotKey? volumeDown;
	public HotKey? VolumeDown
	{
		get => volumeDown;
		set => owner.ChangeHotkey(this, AppHotkeyKind.Down, value);
	}

	private HotKey? mute;
	public HotKey? Mute
	{
		get => mute;
		set => owner.ChangeHotkey(this, AppHotkeyKind.Mute, value);
	}

	internal AppHotkeysItem(AppsHotkeysViewModel owner, string processName, HotKey? volumeUp, HotKey? volumeDown, HotKey? mute)
	{
		this.owner = owner;
		ProcessName = processName;
		this.volumeUp = volumeUp;
		this.volumeDown = volumeDown;
		this.mute = mute;
	}

	/// <summary>
	/// Takes the new hotkeys, or tells the hotkey boxes the current ones again (after a rejected change, a box still shows what was typed)
	/// </summary>
	internal void SetHotkeys(HotKey? up, HotKey? down, HotKey? newMute)
	{
		volumeUp = up;
		volumeDown = down;
		mute = newMute;
		OnPropertyChanged(nameof(VolumeUp));
		OnPropertyChanged(nameof(VolumeDown));
		OnPropertyChanged(nameof(Mute));
	}

	[RelayCommand]
	private void Remove() => owner.RemoveApp(this);
}
