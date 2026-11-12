#nullable enable
using System;

namespace Volumey.Helper;

/// <summary>
/// The delays of something that is tried again until it works: 1 s, doubling each time, then every <see cref="MaxDelay"/>
/// </summary>
internal static class RetryBackoff
{
	internal static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(1);
	internal static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

	/// <summary>Retries that reach <see cref="MaxDelay"/> once an hour: how often a lasting failure is logged again</summary>
	internal const int RetriesPerHour = 12;

	/// <summary>The delay before retry number <paramref name="retry"/> (0-based)</summary>
	internal static TimeSpan Delay(int retry)
		=> retry >= 20 ? MaxDelay : TimeSpan.FromTicks(Math.Min(FirstDelay.Ticks << retry, MaxDelay.Ticks));
}
