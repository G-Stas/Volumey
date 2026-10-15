#nullable enable
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Volumey.DataProvider.Settings;

/// <summary>
/// Base class of a settings section. Mark derived classes with [NotifyChanges] so their auto-properties raise
/// <see cref="PropertyChanged"/>; <see cref="VolumeySettings"/> autosaves when any section raises it.
/// </summary>
public abstract class SettingsSection : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
