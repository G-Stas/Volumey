#nullable enable
using Volumey.DataProvider.Settings;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.Services;

/// <summary>
/// The on-screen volume popups (OSD). As the models' <see cref="IAudioProcessStateMediator"/> it shows and closes them on the UI thread.
/// </summary>
public interface INotificationService : IAudioProcessStateMediator
{
	void ShowNotification(IManagedMasterAudioSession session);

	void CloseNotification(IManagedMasterAudioSession session);

	/// <summary>
	/// A sample app for the preview (not a real session: changing it changes nothing)
	/// </summary>
	IManagedMasterAudioSession Example { get; }

	/// <summary>
	/// Shows the OSD of <see cref="Example"/> for the display time
	/// </summary>
	void ShowNotificationExample();

	void CloseNotificationExample();

	/// <summary>
	/// The work area of the OSD's screen, in physical pixels
	/// </summary>
	void SetWindowWorkArea(int left, int top, int width, int height);

	void ChangePosition(NotificationPosition newPosition);

	/// <summary>
	/// The distances from the screen edges, in pixels at 100% scaling
	/// </summary>
	void SetIndents(int horizontal, int vertical);

	void SetNotificationDisplayTime(int timeInSeconds);
}
