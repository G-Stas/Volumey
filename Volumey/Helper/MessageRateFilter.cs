#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using log4net.Core;
using log4net.Filter;

namespace Volumey.Helper;

/// <summary>
/// Lets the same message through at most <see cref="MaxPerWindow"/> times per <see cref="Window"/>, so one PC with a failing or
/// constantly changing device can't flood the remote log. Messages that differ only in numbers count as the same. The first
/// message let through after drops carries the number dropped in the <see cref="DroppedProperty"/> event property.
/// Returns Neutral for a message it lets through, so later filters still run.
/// </summary>
public sealed partial class MessageRateFilter : FilterSkeleton
{
	public const string DroppedProperty = "Dropped";
	public const int MaxPerWindow = 5;
	public static readonly TimeSpan Window = TimeSpan.FromHours(1);

	//a bound on the remembered messages; past it, the expired ones are forgotten
	private const int PruneAbove = 500;

	private sealed class Entry
	{
		public DateTime WindowStart;
		public int Sent;
		public int Dropped;
	}

	private readonly Func<DateTime> now;
	private readonly Dictionary<string, Entry> entries = new();

	public MessageRateFilter() : this(() => DateTime.UtcNow) { }

	public MessageRateFilter(Func<DateTime> now) => this.now = now;

	[GeneratedRegex(@"\d+")]
	private static partial Regex Digits();

	public override FilterDecision Decide(LoggingEvent loggingEvent)
	{
		string key = $"{loggingEvent.Level?.Name}|{loggingEvent.LoggerName}|{Digits().Replace(loggingEvent.RenderedMessage ?? "", "#")}";
		var time = now();
		lock(entries)
		{
			if(!entries.TryGetValue(key, out var entry))
			{
				if(entries.Count >= PruneAbove)
					Prune(time);
				entries[key] = entry = new Entry { WindowStart = time };
			}
			else if(time - entry.WindowStart >= Window)
			{
				entry.WindowStart = time;
				entry.Sent = 0;
			}
			if(entry.Sent >= MaxPerWindow)
			{
				entry.Dropped++;
				return FilterDecision.Deny;
			}
			entry.Sent++;
			if(entry.Dropped > 0)
			{
				loggingEvent.Properties[DroppedProperty] = entry.Dropped;
				entry.Dropped = 0;
			}
			return FilterDecision.Neutral;
		}
	}

	private void Prune(DateTime time)
	{
		var expired = new List<string>();
		foreach(var (key, entry) in entries)
			if(time - entry.WindowStart >= Window && entry.Dropped == 0)
				expired.Add(key);
		foreach(var key in expired)
			entries.Remove(key);
	}
}
