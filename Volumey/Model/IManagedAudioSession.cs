using System;
using System.Windows.Input;
using System.Windows.Media;

namespace Volumey.Model
{
	public interface IManagedAudioSession
	{
		public ImageSource Icon { get; set; }
		public string Name { get; }
		public bool IsMuted { get; set; }
		public int Volume { get; set; }
		public string Id { get; }
		public ICommand MuteCommand { get; set; }
	}
}