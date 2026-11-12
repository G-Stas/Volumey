#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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

	//the box's error shows that the system refuses its hotkey (see ShowRefused)
	private bool refusedUp;
	private bool refusedDown;
	private bool refusedMute;

	public void Set(ErrorMessageType up, ErrorMessageType down, ErrorMessageType mute = ErrorMessageType.None)
	{
		upType = up;
		downType = down;
		muteType = mute;
		rejectedUp = rejectedDown = rejectedMute = null;
		refusedUp = refusedDown = refusedMute = false;
		Refresh();
	}

	/// <summary>
	/// Shows <paramref name="error"/> under each box whose hotkey the system refuses for now, and removes it from the boxes
	/// whose hotkey was registered since (see <see cref="RefusalError"/>)
	/// </summary>
	public void ShowRefused(Func<HotKey?, bool> isRefused, HotKey? up, HotKey? down, HotKey? mute, ErrorMessageType error)
	{
		var (newUp, newRefusedUp) = RefusalError.Next(upType, refusedUp, isRefused(up), error);
		var (newDown, newRefusedDown) = RefusalError.Next(downType, refusedDown, isRefused(down), error);
		var (newMute, newRefusedMute) = RefusalError.Next(muteType, refusedMute, isRefused(mute), error);
		if(newRefusedUp != refusedUp)
			rejectedUp = null;
		if(newRefusedDown != refusedDown)
			rejectedDown = null;
		if(newRefusedMute != refusedMute)
			rejectedMute = null;
		(upType, refusedUp, downType, refusedDown, muteType, refusedMute) = (newUp, newRefusedUp, newDown, newRefusedDown, newMute, newRefusedMute);
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

	/// <summary>
	/// Shows <paramref name="error"/> under the box of each of <paramref name="failed"/> (an app's hotkeys the system refused), clears the others
	/// </summary>
	public void SetFailed(IReadOnlyCollection<HotKey> failed, HotKey? up, HotKey? down, HotKey? mute, ErrorMessageType error)
	{
		ErrorMessageType For(HotKey? hotkey) => hotkey != null && failed.Contains(hotkey) ? error : ErrorMessageType.None;
		Set(For(up), For(down), For(mute));
	}

	public void Clear() => Set(ErrorMessageType.None, ErrorMessageType.None);

	public void ClearUp()
	{
		upType = ErrorMessageType.None;
		rejectedUp = null;
		refusedUp = false;
		Refresh();
	}

	public void ClearDown()
	{
		downType = ErrorMessageType.None;
		rejectedDown = null;
		refusedDown = false;
		Refresh();
	}

	public void ClearMute()
	{
		muteType = ErrorMessageType.None;
		rejectedMute = null;
		refusedMute = false;
		Refresh();
	}

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
