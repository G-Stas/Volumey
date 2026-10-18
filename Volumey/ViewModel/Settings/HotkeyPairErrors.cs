#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using Volumey.Controls;
using Volumey.Localization;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// The errors of a volume up/down hotkey pair and, for an app, its mute hotkey, one per hotkey, so the settings show each under its own box
/// (App hotkeys page: the foreground app, each saved app and the "Add app" dialog)
/// </summary>
public sealed partial class HotkeyPairErrors : ObservableObject
{
	private ErrorMessageType upType = ErrorMessageType.None;
	private ErrorMessageType downType = ErrorMessageType.None;
	private ErrorMessageType muteType = ErrorMessageType.None;
	private HotKey? rejectedUp;
	private HotKey? rejectedDown;
	private HotKey? rejectedMute;

	[ObservableProperty]
	public partial string VolumeUp { get; private set; }

	[ObservableProperty]
	public partial string VolumeDown { get; private set; }

	[ObservableProperty]
	public partial string Mute { get; private set; }

	public bool Any => upType != ErrorMessageType.None || downType != ErrorMessageType.None || muteType != ErrorMessageType.None;

	public void Set(ErrorMessageType up, ErrorMessageType down, ErrorMessageType mute = ErrorMessageType.None)
	{
		upType = up;
		downType = down;
		muteType = mute;
		rejectedUp = rejectedDown = rejectedMute = null;
		Refresh();
	}

	public void Set((ErrorMessageType up, ErrorMessageType down) errors) => Set(errors.up, errors.down);

	public void Set((ErrorMessageType up, ErrorMessageType down, ErrorMessageType mute) errors) => Set(errors.up, errors.down, errors.mute);

	/// <summary>
	/// An error for a hotkey that was typed into a box but rejected, while the box shows the old hotkey again (a saved app):
	/// the message names the rejected hotkey ("Ctrl+M: ..."), so it doesn't read as if the shown hotkey were wrong
	/// </summary>
	public void SetRejected(AppHotkeyKind kind, HotKey? rejected, ErrorMessageType error)
	{
		Set(kind == AppHotkeyKind.Up ? error : ErrorMessageType.None, kind == AppHotkeyKind.Down ? error : ErrorMessageType.None,
			kind == AppHotkeyKind.Mute ? error : ErrorMessageType.None);
		switch(kind)
		{
			case AppHotkeyKind.Up: rejectedUp = rejected; break;
			case AppHotkeyKind.Down: rejectedDown = rejected; break;
			default: rejectedMute = rejected; break;
		}
		Refresh();
	}

	/// <inheritdoc cref="SetRejected(AppHotkeyKind, HotKey?, ErrorMessageType)"/>
	public void SetRejected(bool up, HotKey? rejected, ErrorMessageType error) => SetRejected(up ? AppHotkeyKind.Up : AppHotkeyKind.Down, rejected, error);

	/// <summary>
	/// Shows <paramref name="error"/> under the box of <paramref name="failed"/> (the hotkey of the pair the system refused)
	/// </summary>
	public void SetFor(HotKey? failed, HotKey? up, ErrorMessageType error)
		=> Set(Equals(failed, up) ? error : ErrorMessageType.None, Equals(failed, up) ? ErrorMessageType.None : error);

	/// <summary>
	/// Shows <paramref name="error"/> under the box of <paramref name="failed"/> (the app hotkey the system refused)
	/// </summary>
	public void SetFor(HotKey? failed, HotKey? up, HotKey? down, ErrorMessageType error)
	{
		var kind = Equals(failed, up) ? AppHotkeyKind.Up : Equals(failed, down) ? AppHotkeyKind.Down : AppHotkeyKind.Mute;
		Set(kind == AppHotkeyKind.Up ? error : ErrorMessageType.None, kind == AppHotkeyKind.Down ? error : ErrorMessageType.None,
			kind == AppHotkeyKind.Mute ? error : ErrorMessageType.None);
	}

	public void Clear() => Set(ErrorMessageType.None, ErrorMessageType.None);

	public void ClearUp() => Set(ErrorMessageType.None, downType, muteType);

	public void ClearDown() => Set(upType, ErrorMessageType.None, muteType);

	public void ClearMute() => Set(upType, downType);

	/// <summary>
	/// Reads the messages again, e.g. after the language changed
	/// </summary>
	public void Refresh()
	{
		VolumeUp = Message(upType, rejectedUp);
		VolumeDown = Message(downType, rejectedDown);
		Mute = Message(muteType, rejectedMute);
	}

	internal static string Message(ErrorMessageType type, HotKey? rejected)
	{
		if(type == ErrorMessageType.None)
			return string.Empty;
		string message = ErrorDictionary.Instance[type];
		if(rejected == null)
			return message;
		string? format = TranslationSource.Instance["Error_RejectedHotkey"];
		return string.IsNullOrEmpty(format) ? message : string.Format(format, rejected, message);
	}
}

/// <summary>Which of an app's hotkeys (App hotkeys page)</summary>
public enum AppHotkeyKind { Up, Down, Mute }
