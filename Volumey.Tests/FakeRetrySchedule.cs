using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Volumey.Tests;

/// <summary>
/// Stands in for the hotkey service's retry timer: keeps the scheduled retry, which a test runs with <see cref="RunPending"/>
/// </summary>
internal sealed class FakeRetrySchedule
{
	private sealed class Scheduled(TimeSpan delay, Action action) : IDisposable
	{
		public TimeSpan Delay { get; } = delay;
		public Action Action { get; } = action;
		public bool Cancelled { get; private set; }
		public void Dispose() => Cancelled = true;
	}

	private Scheduled pending;

	/// <summary>The delays of every retry scheduled so far, in order</summary>
	public List<TimeSpan> Delays { get; } = new List<TimeSpan>();

	public bool HasPending => pending is { Cancelled: false };

	public IDisposable Schedule(TimeSpan delay, Action action)
	{
		Delays.Add(delay);
		return pending = new Scheduled(delay, action);
	}

	/// <summary>Runs the scheduled retry as if its delay passed</summary>
	public void RunPending()
	{
		Assert.IsTrue(HasPending, "no retry is scheduled");
		var run = pending;
		pending = null;
		run.Action();
	}
}
