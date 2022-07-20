using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using Volumey.Model;

namespace Volumey.View.Controls
{
	public partial class VolumeNotificationContent : UserControl, INotifyPropertyChanged
	{
		private IManagedMasterAudioSession _session;

		public IManagedMasterAudioSession AudioSession
		{
			get => this._session;
			set
			{
				this._session = value;
				OnPropertyChanged();
			}
		}
		
		public VolumeNotificationContent(IManagedMasterAudioSession session)
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