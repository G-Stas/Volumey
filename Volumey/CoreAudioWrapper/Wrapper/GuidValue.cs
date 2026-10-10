#nullable enable
using System;

namespace Volumey.CoreAudioWrapper.Wrapper;

//Contains external & internal guid values
public static class GuidValue
{
	/// <summary>
	/// Contains guid values of the COM interfaces that NAudio doesn't wrap
	/// </summary>
	public static class External
	{
		public const string IPolicyConfig = "F8679F50-850A-41CF-9C72-430F290290C8";
		public const string PolicyConfigCOM = "870AF99C-171D-4F9E-AF0D-E63DF40C2BC9";
	}

	/// <summary>
	/// Contains application guid values
	/// </summary>
	public static class Internal
	{
		/// <summary>
		/// Used to distinguish volume change sources between internal changes and external in volume change callbacks
		/// </summary>
		public static readonly Guid VolumeGUID = new Guid("1DD0B43C-EE90-47BE-8E12-DE3BB4CF92F0");
		public static readonly Guid Empty = Guid.Empty;
	}
}
