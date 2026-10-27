#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Volumey.DataProvider.Settings;

/// <summary>Applies the saved row order while leaving sources without a saved position in their original order.</summary>
internal static class MixerProcessOrder
{
	internal static void Apply<T>(ObservableCollection<T> processes, System.Func<T, string> getKey, IReadOnlyList<string> savedOrder)
	{
		var rank = savedOrder.Select((key, index) => (key, index))
			.GroupBy(x => x.key, System.StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First().index, System.StringComparer.OrdinalIgnoreCase);
		var ordered = processes
			.Select((process, index) => (process, index, rank: rank.TryGetValue(getKey(process), out int value) ? value : int.MaxValue))
			.OrderBy(x => x.rank)
			.ThenBy(x => x.index)
			.Select(x => x.process)
			.ToArray();

		for(int i = 0; i < ordered.Length; i++)
		{
			int current = processes.IndexOf(ordered[i]);
			if(current != i)
				processes.Move(current, i);
		}
	}

	internal static string[] SaveOrder<T>(IEnumerable<T> processes, System.Func<T, string> getKey, IReadOnlyList<string> previousOrder)
	{
		var current = processes.Select(getKey).ToArray();
		var keys = new HashSet<string>(current, System.StringComparer.OrdinalIgnoreCase);
		return current.Concat(previousOrder.Where(key => keys.Add(key))).ToArray();
	}
}
