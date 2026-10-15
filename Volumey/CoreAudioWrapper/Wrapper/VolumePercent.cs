#nullable enable
using System;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// Converts between Windows' volume scalars (0..1) and the app's volume percents (0..100)
/// </summary>
internal static class VolumePercent
{
	internal static int FromScalar(float volume) => Convert.ToInt32(volume * 100);

	internal static float ToScalar(int percent) => Math.Clamp(percent, 0, 100) / 100.0f;
}
