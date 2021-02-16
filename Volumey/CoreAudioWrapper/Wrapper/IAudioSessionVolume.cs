using System;

namespace Volumey.CoreAudioWrapper.Wrapper
{
	public interface IAudioSessionVolume
	{
		float GetVolume();

		void SetVolume(int newVolume, ref Guid context);

		bool GetMute();

		void SetMute(bool newState);
	}
}