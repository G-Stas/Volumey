using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Volumey.DataProvider.Settings
{
	/// <summary>
	/// Base class of a settings section. Mark derived classes with [NotifyChanges] so their auto-properties raise
	/// <see cref="PropertyChanged"/>; <see cref="VolumeySettings"/> autosaves when any section raises it.
	/// </summary>
	public abstract class SettingsSection : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Values in the file that no property reads: removed or renamed settings, which migration steps read with <see cref="TakeOtherValue{T}"/>,
		/// and settings of a newer app version, which are written back so a downgrade doesn't lose them.
		/// </summary>
		[JsonExtensionData]
		private IDictionary<string, JToken> otherValues;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		/// <summary>
		/// Reads and removes a value that no property reads anymore. Returns the default if it isn't there.
		/// </summary>
		internal T TakeOtherValue<T>(string name)
		{
			if(this.otherValues != null && this.otherValues.Remove(name, out JToken value))
				return value.ToObject<T>();
			return default;
		}

		/// <summary>
		/// Stores a value that only a migration step reads, e.g. a 1.x setting in its old form.
		/// </summary>
		internal void SetOtherValue(string name, object value)
			=> (this.otherValues ??= new Dictionary<string, JToken>())[name] = JToken.FromObject(value);
	}
}
