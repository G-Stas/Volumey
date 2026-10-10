using CommunityToolkit.Mvvm.ComponentModel;
using Volumey.Controls;
using Volumey.Localization;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// The errors of a volume up/down hotkey pair, one per hotkey, so the settings show each under its own box
/// (App hotkeys page: the foreground app, each saved app and the "Add app" dialog)
/// </summary>
public sealed partial class HotkeyPairErrors : ObservableObject
{
	private ErrorMessageType upType = ErrorMessageType.None;
	private ErrorMessageType downType = ErrorMessageType.None;
	private HotKey rejectedUp;
	private HotKey rejectedDown;

	[ObservableProperty]
	public partial string VolumeUp { get; private set; }

	[ObservableProperty]
	public partial string VolumeDown { get; private set; }

	public bool Any => upType != ErrorMessageType.None || downType != ErrorMessageType.None;

	public void Set(ErrorMessageType up, ErrorMessageType down)
	{
		upType = up;
		downType = down;
		rejectedUp = rejectedDown = null;
		Refresh();
	}

	public void Set((ErrorMessageType up, ErrorMessageType down) errors) => Set(errors.up, errors.down);

	/// <summary>
	/// An error for a hotkey that was typed into a box but rejected, while the box shows the old hotkey again (a saved app):
	/// the message names the rejected hotkey ("Ctrl+M: ..."), so it doesn't read as if the shown hotkey were wrong
	/// </summary>
	public void SetRejected(bool up, HotKey rejected, ErrorMessageType error)
	{
		Set(up ? error : ErrorMessageType.None, up ? ErrorMessageType.None : error);
		if(up)
			rejectedUp = rejected;
		else
			rejectedDown = rejected;
		Refresh();
	}

	/// <summary>
	/// Shows <paramref name="error"/> under the box of <paramref name="failed"/> (the hotkey of the pair the system refused)
	/// </summary>
	public void SetFor(HotKey failed, HotKey up, ErrorMessageType error)
		=> Set(Equals(failed, up) ? error : ErrorMessageType.None, Equals(failed, up) ? ErrorMessageType.None : error);

	public void Clear() => Set(ErrorMessageType.None, ErrorMessageType.None);

	public void ClearUp() => Set(ErrorMessageType.None, downType);

	public void ClearDown() => Set(upType, ErrorMessageType.None);

	/// <summary>
	/// Reads the messages again, e.g. after the language changed
	/// </summary>
	public void Refresh()
	{
		VolumeUp = Message(upType, rejectedUp);
		VolumeDown = Message(downType, rejectedDown);
	}

	private static string Message(ErrorMessageType type, HotKey rejected)
	{
		if(type == ErrorMessageType.None)
			return string.Empty;
		string message = ErrorDictionary.Instance[type];
		if(rejected == null)
			return message;
		string format = TranslationSource.Instance["Error_RejectedHotkey"];
		return string.IsNullOrEmpty(format) ? message : string.Format(format, rejected, message);
	}
}
