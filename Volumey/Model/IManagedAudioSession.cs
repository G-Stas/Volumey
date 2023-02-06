using System;
using System.ComponentModel;
using System.Windows.Input;

namespace Volumey.Model
{
	public interface IManagedAudioSession : INotifyPropertyChanged, IDisposable
	{
		public string Name { get; set; }
		public bool IsMuted { get; set; }
		public int Volume { get; set; }
		public string Id { get; }
		public uint ProcessId { get; }
		public string FilePath { get; }
		public Guid GroupingParam { get; }
		public ICommand MuteCommand { get; set; }
	}
}