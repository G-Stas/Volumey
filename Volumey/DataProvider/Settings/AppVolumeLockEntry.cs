#nullable enable
namespace Volumey.DataProvider.Settings;

/// <summary>
/// An app whose volume is locked: it is held at <see cref="Volume"/> whenever it runs. The app is matched by its name,
/// the description of its executable.
/// </summary>
public sealed class AppVolumeLockEntry
{
	public string AppName { get; set; } = string.Empty;
	public int Volume { get; set; }
}
