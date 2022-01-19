using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using Volumey.Model;

namespace Volumey.View.Controls
{
	public partial class VolumeNotificationContent : UserControl, INotifyPropertyChanged
	{
		private IManagedAudioSession _session;

		public IManagedAudioSession AudioSession
		{
			get => this._session;
			set
			{
				this._session = value;
				OnPropertyChanged();
			}
		}
		
		public VolumeNotificationContent(IManagedAudioSession session)
		{
			InitializeComponent();
			this.AudioSession = session;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}