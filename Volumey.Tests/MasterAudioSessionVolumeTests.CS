using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.Tests;

[TestClass]
public class MasterAudioSessionVolumeTests
{
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

	private static MasterAudioSessionVolume Create(Action<int, Guid> volumeWriter, Action<bool, Guid> muteWriter = null)
		=> new(volumeWriter, muteWriter ?? ((m, c) => { }), () => 0f, () => false);

	[TestMethod]
	public void QueuedVolumesMustBeCoalescedIntoTheLatest()
	{
		var written = new List<int>();
		using var started = new ManualResetEventSlim();
		using var gate = new ManualResetEventSlim();
		var volume = Create((v, c) =>
		{
			started.Set();
			gate.Wait(TestContext.CancellationToken);
			lock(written)
				written.Add(v);
		});

		volume.SetVolume(10, Guid.Empty);
		Assert.IsTrue(started.Wait(Timeout, TestContext.CancellationToken));
		volume.SetVolume(20, Guid.Empty);
		volume.SetVolume(30, Guid.Empty);
		volume.SetVolume(40, Guid.Empty);
		Assert.IsTrue(volume.HasPendingWrites);

		gate.Set();
		Assert.IsTrue(SpinWait.SpinUntil(() => !volume.HasPendingWrites, Timeout));
		Assert.AreSequenceEqual([10, 40], written);
	}

	[TestMethod]
	public void MuteMustGoThroughTheSameQueueAndNotBlockTheCaller()
	{
		var muted = new List<bool>();
		using var started = new ManualResetEventSlim();
		using var gate = new ManualResetEventSlim();
		var volume = Create((v, c) =>
		{
			started.Set();
			gate.Wait(TestContext.CancellationToken);
		}, (m, c) =>
		{
			lock(muted)
				muted.Add(m);
		});

		volume.SetVolume(10, Guid.Empty);
		Assert.IsTrue(started.Wait(Timeout, TestContext.CancellationToken));

		//returns at once although the device is still busy with the volume write
		volume.SetMute(true, Guid.Empty);
		Assert.IsEmpty(muted);

		gate.Set();
		Assert.IsTrue(SpinWait.SpinUntil(() => !volume.HasPendingWrites, Timeout));
		Assert.AreSequenceEqual([true], muted);
	}

	[TestMethod]
	public void WritesCompletedMustReportTheErrorOfAFailedWrite()
	{
		var error = new COMException();
		using var reported = new ManualResetEventSlim();
		Exception received = null;
		var volume = Create((v, c) => throw error);
		volume.WritesCompleted += e =>
		{
			received = e;
			reported.Set();
		};

		volume.SetVolume(10, Guid.Empty);

		Assert.IsTrue(reported.Wait(Timeout, TestContext.CancellationToken));
		Assert.AreSame(error, received);
	}

	[TestMethod]
	public void WritesCompletedMustBeRaisedAfterSuccessfulWritesToo()
	{
		using var reported = new ManualResetEventSlim();
		Exception received = new Exception("not raised");
		var volume = Create((v, c) => { });
		volume.WritesCompleted += e =>
		{
			received = e;
			reported.Set();
		};

		volume.SetVolume(10, Guid.Empty);

		Assert.IsTrue(reported.Wait(Timeout, TestContext.CancellationToken));
		Assert.IsNull(received);
		Assert.IsFalse(volume.HasPendingWrites);
	}

	[TestMethod]
	public void DisposeMustNotBlockAndMustDropPendingWrites()
	{
		var written = new List<int>();
		using var started = new ManualResetEventSlim();
		using var gate = new ManualResetEventSlim();
		var volume = Create((v, c) =>
		{
			started.Set();
			gate.Wait(TestContext.CancellationToken);
			lock(written)
				written.Add(v);
		});

		volume.SetVolume(10, Guid.Empty);
		Assert.IsTrue(started.Wait(Timeout, TestContext.CancellationToken));
		volume.SetVolume(20, Guid.Empty);

		//returns although a write is in progress
		Assert.IsTrue(Task.Run(volume.Dispose, TestContext.CancellationToken).Wait(Timeout, TestContext.CancellationToken));

		//the write in progress can be awaited before releasing the device
		var waiting = volume.WaitForWritesAsync();
		Assert.IsFalse(waiting.IsCompleted);

		gate.Set();
		Assert.IsTrue(waiting.Wait(Timeout, TestContext.CancellationToken));
		//the queued 20 was dropped, and nothing is written after the dispose
		volume.SetVolume(30, Guid.Empty);
		Assert.IsFalse(volume.HasPendingWrites);
		Assert.AreSequenceEqual([10], written);
	}

	public TestContext TestContext { get; set; }
}
