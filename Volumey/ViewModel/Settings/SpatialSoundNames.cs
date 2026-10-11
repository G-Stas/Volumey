using System;
using System.Collections.Generic;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Localization;
using Windows.Media.Audio;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// The names of the spatial sound formats, as Windows' sound settings call them ("Off", "Windows Sonic for Headphones"...)
/// </summary>
public static class SpatialSoundNames
{
	private static readonly Dictionary<Guid, string> Keys = new()
	{
		[Guid.Parse(SpatialAudioFormatSubtype.WindowsSonic)] = "Devices_Spatial_WindowsSonic",
		[Guid.Parse(SpatialAudioFormatSubtype.DolbyAtmosForHeadphones)] = "Devices_Spatial_DolbyAtmosHeadphones",
		[Guid.Parse(SpatialAudioFormatSubtype.DolbyAtmosForHomeTheater)] = "Devices_Spatial_DolbyAtmosHomeTheater",
		[Guid.Parse(SpatialAudioFormatSubtype.DolbyAtmosForSpeakers)] = "Devices_Spatial_DolbyAtmosSpeakers",
		[Guid.Parse(SpatialAudioFormatSubtype.DTSHeadphoneX)] = "Devices_Spatial_DtsHeadphoneX",
		[Guid.Parse(SpatialAudioFormatSubtype.DTSXUltra)] = "Devices_Spatial_DtsXUltra"
	};

	public static string Of(SpatialSoundFormat format)
	{
		if(format.IsOff)
			return TranslationSource.Instance["Devices_SpatialOff"];
		return Keys.TryGetValue(format.Id, out var key) ? TranslationSource.Instance[key] : format.Id.ToString("B");
	}
}
