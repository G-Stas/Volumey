#nullable enable
namespace Volumey.ViewModel.Settings;

/// <summary>
/// The error of a hotkey box while the system refuses its hotkey (<see cref="IHotkeyService.IsRefused"/>): it shows unless
/// another error does, and goes once the hotkey is registered. <c>IsRefusal</c> tells this error from the others.
/// </summary>
internal static class RefusalError
{
	/// <returns>The error the box shows next</returns>
	internal static (ErrorMessageType type, bool isRefusal) Next(ErrorMessageType current, bool isRefusal, bool refused, ErrorMessageType error)
	{
		if(refused && (current == ErrorMessageType.None || isRefusal))
			return (error, true);
		if(!refused && isRefusal)
			return (ErrorMessageType.None, false);
		return (current, isRefusal);
	}
}
