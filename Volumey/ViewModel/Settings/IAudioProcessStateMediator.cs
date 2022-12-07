using Volumey.Model;

namespace Volumey.ViewModel.Settings
{
	public interface IAudioProcessStateMediator
	{
		void NotifyAudioStateChange(IManagedMasterAudioSession sender);
		void NotifyOfDisposing(IManagedMasterAudioSession sender);
	}
}