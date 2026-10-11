using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Volumey.Controls;
using Volumey.Localization;
using Volumey.Model;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// One media key on the Media keys page: the hotkey that presses it, changed in place
/// (<see cref="SystemHotkeysViewModel"/> checks and registers a change, and puts the old hotkey back if it can't)
/// </summary>
public sealed partial class MediaKeyItem : ObservableObject
{
	private readonly SystemHotkeysViewModel owner;

	internal SystemHotkey Model { get; }

	/// <summary>
	/// Whether the hotkey service holds the hotkey (a saved one may have failed to register on startup)
	/// </summary>
	internal bool IsRegistered { get; set; }

	public Key MediaKey => Model.SystemKey;

	/// <summary>
	/// The media key's name in the current language ("Play / pause")
	/// </summary>
	public string Name => TranslationSource.Instance[NameKey(MediaKey)];

	public HotKey Hotkey
	{
		get => Model.Replacement;
		set => owner.ChangeHotkey(this, value);
	}

	/// <summary>
	/// Why the last change was rejected or why the saved hotkey couldn't be registered, under the box
	/// </summary>
	[ObservableProperty]
	public partial string Error { get; private set; }

	private ErrorMessageType errorType = ErrorMessageType.None;
	private HotKey rejected;

	internal MediaKeyItem(SystemHotkeysViewModel owner, SystemHotkey model)
	{
		this.owner = owner;
		Model = model;
	}

	/// <summary>
	/// Takes the new hotkey, or tells the hotkey box the current one again (after a rejected change, the box still shows what was typed)
	/// </summary>
	internal void SetHotkey(HotKey hotkey)
	{
		Model.Replacement = hotkey;
		OnPropertyChanged(nameof(Hotkey));
	}

	/// <summary>
	/// Shows an error; <paramref name="rejectedHotkey"/> is a typed hotkey that the box doesn't show anymore, the message names it
	/// </summary>
	internal void SetError(ErrorMessageType type, HotKey rejectedHotkey = null)
	{
		errorType = type;
		rejected = rejectedHotkey;
		RefreshError();
	}

	/// <summary>
	/// Reads the texts again after the language changed
	/// </summary>
	internal void RefreshTexts()
	{
		RefreshError();
		OnPropertyChanged(nameof(Name));
	}

	private void RefreshError() => Error = HotkeyPairErrors.Message(errorType, rejected);

	private static string NameKey(Key key) => key switch
	{
		Key.MediaPlayPause => "Media_PlayPause",
		Key.MediaPreviousTrack => "Media_Previous",
		Key.MediaNextTrack => "Media_Next",
		_ => "Media_Stop"
	};

	[RelayCommand]
	private void Clear() => owner.ChangeHotkey(this, null);
}
