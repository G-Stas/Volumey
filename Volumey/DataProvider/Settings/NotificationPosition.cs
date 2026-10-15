#nullable enable
namespace Volumey.DataProvider.Settings;

/// <summary>
/// Where on the screen the OSD shows: a corner or the middle of the top or bottom edge. The values are those of the old Notification.Wpf enum,
/// which the 1.x settings stored as numbers (<see cref="LegacySettingsImporter"/>); settings.json stores the names.
/// </summary>
public enum NotificationPosition
{
	TopLeft = 0,
	TopRight = 1,
	TopCenter = 2,
	BottomLeft = 3,
	BottomRight = 4,
	BottomCenter = 5
}
