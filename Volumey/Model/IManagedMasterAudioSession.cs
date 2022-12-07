using System;
using System.Drawing;
using System.Windows.Media;
using Volumey.Controls;
using Volumey.ViewModel.Settings;

namespace Volumey.Model
{
	public interface IManagedMasterAudioSession : IManagedAudioSession
	{
		public ImageSource IconSource { get; set; }
		public Icon Icon { get; set; }
		public bool AnyHotkeyRegistered { get; }
		public void SetVolume(int newValue, bool notify, ref Guid guid);
		public void SetMute(bool newState, bool notify, ref Guid context);
		public void SetStateMediator(IAudioProcessStateMediator mediator);
		public void ResetStateMediator(bool force);
		public bool SetVolumeHotkeys(HotKey volUp, HotKey volDown);
		public void ResetVolumeHotkeys();
		public bool SetMuteHotkey(HotKey hotkey);
		public void ResetMuteHotkey();
	}
}