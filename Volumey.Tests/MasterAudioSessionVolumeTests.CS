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

	[TestMethod]
	public void SynchronousModeMustWriteOnTheCallerThread()
	{
		int writerThread = -1;
		var volume = new MasterAudioSessionVolume((v, c) => writerThread = Environment.CurrentManagedThreadId, (m, c) => { }, background: false);

		volume.SetVolume(30, Guid.Empty);

		Assert.AreEqual(Environment.CurrentManagedThreadId, writerThread);
		Assert.IsFalse(volume.HasPendingWrites);
	}

	[TestMethod]
	public void SynchronousModeMustThrowWriteExceptionsToTheCaller()
	{
		var volume = new MasterAudioSessionVolume((v, c) => throw new COMException(), (m, c) => { }, background: false);

		Assert.Throws<COMException>(() => volume.SetVolume(30, Guid.Empty));
	}

	[TestMethod]
	public void QueuedVolumesMustBeCoalescedIntoTheLatest()
	{
		var written = new List<int>();
		using var started = new ManualResetEventSlim();
		using var gate = new ManualResetEventSlim();
		var volume = new MasterAudioSessionVolume((v, c) =>
		{
			started.Set();
			gate.Wait(TestContext.CancellationToken);
			lock(written)
				written.Add(v);
		}, (m, c) => { }, background: true);

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
		var volume = new MasterAudioSessionVolume((v, c) =>
		{
			started.Set();
			gate.Wait(TestContext.CancellationToken);
		}, (m, c) =>
		{
			lock(muted)
				muted.Add(m);
		}, background: true);

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
		var volume = new MasterAudioSessionVolume((v, c) => throw error, (m, c) => { }, background: true);
		volume.WritesCompleted += (e, state) =>
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
		var volume = new MasterAudioSessionVolume((v, c) => { }, (m, c) => { }, background: true);
		volume.WritesCompleted += (e, state) =>
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
		var volume = new MasterAudioSessionVolume((v, c) =>
		{
			started.Set();
			gate.Wait(TestContext.CancellationToken);
			lock(written)
				written.Add(v);
		}, (m, c) => { }, background: true);

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
