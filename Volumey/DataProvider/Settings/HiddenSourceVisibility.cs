#nullable enable
using System;
using System.Linq;

namespace Volumey.DataProvider.Settings;

/// <summary>Stores mixer visibility by stable output device and Windows audio session identity.</summary>
internal static class HiddenSourceVisibility
{
	internal static string CreateId(string deviceId, string sessionId)
		=> $"{deviceId.Length}:{deviceId}{sessionId}";

	internal static bool IsHidden(GeneralSettings settings, string id)
		=> settings.HiddenSourceIds.Contains(id, StringComparer.Ordinal);

	internal static void SetHidden(GeneralSettings settings, string id, bool hidden)
	{
		var ids = settings.HiddenSourceIds.Where(existing => !StringComparer.Ordinal.Equals(existing, id)).ToList();
		if(hidden)
			ids.Add(id);
		settings.HiddenSourceIds = ids.ToArray();
	}
}
