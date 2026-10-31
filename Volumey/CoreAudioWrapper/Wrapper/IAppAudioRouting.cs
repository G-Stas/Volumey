#nullable enable
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// The output device Windows plays an app on (per-app device preference, what Windows' Volume mixer settings use)
/// </summary>
public interface IAppAudioRouting
{
	/// <summary>
	/// False when Windows doesn't offer the interface or offers an unknown version of it; <see cref="GetRoute"/> and
	/// <see cref="SetRoute"/> must not be called then
	/// </summary>
	bool IsAvailable { get; }

	/// <summary>
	/// The app's route for the console role; null when Windows didn't answer (the process is gone, or Windows refused)
	/// </summary>
	AppRoute? GetRoute(uint processId);

	/// <summary>
	/// Sets the app's route for the console, multimedia and communications roles, in that order; stops at the first role Windows refuses
	/// </summary>
	RouteWrite SetRoute(uint processId, AppRoute route);
}
