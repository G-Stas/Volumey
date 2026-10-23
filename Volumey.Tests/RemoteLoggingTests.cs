using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Helper;

namespace Volumey.Tests;

/// <summary>
/// The Loggly appender against a local HTTP endpoint instead of Loggly: it checks that log4net-loggly works with the log4net
/// version the app uses (it was built against log4net 2.0.8) and that the key goes where Loggly expects it.
/// </summary>
[TestClass]
public class RemoteLoggingTests
{
	private const string TestKey = "00000000-1111-2222-3333-444444444444";

	[TestMethod]
	public async Task AMessageIsPostedWithTheInputKey()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		try
		{
			int port = ((IPEndPoint)listener.LocalEndpoint).Port;
			var request = ReceiveOneRequest(listener);

			var appender = RemoteLogging.CreateAppender(TestKey, $"http://127.0.0.1:{port}/");
			var repository = LogManager.CreateRepository("remote-logging-test-" + Guid.NewGuid().ToString("N"));
			BasicConfigurator.Configure(repository, RemoteLogging.CreatePipeline(appender));
			LogManager.GetLogger(repository.Name, "test").Error("remote logging test message");
			appender.Flush(5000);

			Assert.IsTrue(await Task.WhenAny(request, Task.Delay(10_000)) == request, "nothing was posted");
			string text = await request;
			StringAssert.StartsWith(text, "POST ");
			StringAssert.Contains(text, TestKey);
			StringAssert.Contains(text, "remote logging test message");
			repository.Shutdown();
		}
		finally
		{
			listener.Stop();
		}
	}

	[TestMethod]
	public void ARepeatedMessageIsSentOnce()
	{
		var filter = new DuplicateLogMessageFilter();
		var first = new log4net.Core.LoggingEvent(typeof(RemoteLoggingTests), null, "test", log4net.Core.Level.Error, "same", null);
		var second = new log4net.Core.LoggingEvent(typeof(RemoteLoggingTests), null, "test", log4net.Core.Level.Error, "same", null);

		Assert.AreEqual(log4net.Filter.FilterDecision.Accept, filter.Decide(first));
		Assert.AreEqual(log4net.Filter.FilterDecision.Deny, filter.Decide(second));
	}

	private static (log4net.Repository.ILoggerRepository Repository, log4net.Appender.MemoryAppender Sent) CreatePipeline()
	{
		var sent = new log4net.Appender.MemoryAppender();
		var repository = LogManager.CreateRepository("remote-pipeline-test-" + Guid.NewGuid().ToString("N"));
		BasicConfigurator.Configure(repository, RemoteLogging.CreatePipeline(sent));
		((log4net.Repository.Hierarchy.Hierarchy)repository).Root.Level = log4net.Core.Level.Info;
		return (repository, sent);
	}

	[TestMethod]
	public void WarningsAndErrorsAreSentInfoIsNot()
	{
		var (repository, sent) = CreatePipeline();
		var log = LogManager.GetLogger(repository.Name, "test");

		log.Info("device changed");
		log.Warn("a warning");
		log.Error("an error");

		CollectionAssert.AreEqual(new[] { "a warning", "an error" }, Array.ConvertAll(sent.GetEvents(), e => e.RenderedMessage));
		repository.Shutdown();
	}

	[TestMethod]
	public void ACrashSendsTheLastInfoMessagesOnce()
	{
		var (repository, sent) = CreatePipeline();
		var log = LogManager.GetLogger(repository.Name, "test");

		for(int i = 0; i < RemoteLogging.CrashContextSize + 10; i++)
			log.Info($"step {i} of the device");
		log.Fatal("crash");

		var events = sent.GetEvents();
		Assert.AreEqual(1, Array.FindAll(events, e => e.Level == log4net.Core.Level.Fatal).Length, "the crash is sent once");
		var info = Array.FindAll(events, e => e.Level == log4net.Core.Level.Info);
		//the rate limit lets the same message (numbers aside) through a few times
		Assert.AreEqual(MessageRateFilter.MaxPerWindow, info.Length);
		Assert.AreEqual("step 10 of the device", info[0].RenderedMessage, "the oldest of the last messages comes first");
		repository.Shutdown();
	}

	[TestMethod]
	public void ACrashSendsDifferentInfoMessagesUpToTheContextSize()
	{
		var (repository, sent) = CreatePipeline();
		var log = LogManager.GetLogger(repository.Name, "test");
		string[] words = ["sleep", "wake", "plug", "unplug", "switch", "format", "start", "exit"];

		for(int i = 0; i < RemoteLogging.CrashContextSize + 5; i++)
			log.Info($"{words[i % words.Length]} {new string('x', i)}");
		log.Fatal("crash");

		Assert.AreEqual(RemoteLogging.CrashContextSize, Array.FindAll(sent.GetEvents(), e => e.Level == log4net.Core.Level.Info).Length);
		repository.Shutdown();
	}

	[TestMethod]
	public void InfoMessagesArentSentAtExit()
	{
		var (repository, sent) = CreatePipeline();
		LogManager.GetLogger(repository.Name, "test").Info("device changed");

		repository.Shutdown();

		Assert.AreEqual(0, sent.GetEvents().Length);
	}

	[TestMethod]
	public void AMessageIsSentAFewTimesAnHourThenItsDropsAreCounted()
	{
		var time = new DateTime(2026, 10, 23, 12, 0, 0);
		var filter = new MessageRateFilter(() => time);
		log4net.Core.LoggingEvent Event(string message)
			=> new(typeof(RemoteLoggingTests), null, "test", log4net.Core.Level.Error, message, null);

		for(int i = 0; i < MessageRateFilter.MaxPerWindow; i++)
			Assert.AreEqual(log4net.Filter.FilterDecision.Neutral, filter.Decide(Event($"retry {i}")));
		Assert.AreEqual(log4net.Filter.FilterDecision.Deny, filter.Decide(Event("retry 99")), "numbers don't make a message new");
		Assert.AreEqual(log4net.Filter.FilterDecision.Deny, filter.Decide(Event("retry 100")));
		Assert.AreEqual(log4net.Filter.FilterDecision.Neutral, filter.Decide(Event("another message")));

		time += MessageRateFilter.Window;
		var next = Event("retry 101");
		Assert.AreEqual(log4net.Filter.FilterDecision.Neutral, filter.Decide(next));
		Assert.AreEqual(2, next.LookupProperty(MessageRateFilter.DroppedProperty));

		var writer = new StringWriter();
		new RemoteLogging.LogglyLayout().Format(writer, next);
		StringAssert.EndsWith(writer.ToString(), "[2 more like it dropped]");
	}

	/// <summary>
	/// Reads one HTTP request (headers and the body of its Content-Length) and answers 200
	/// </summary>
	private static async Task<string> ReceiveOneRequest(TcpListener listener)
	{
		using var client = await listener.AcceptTcpClientAsync();
		using var stream = client.GetStream();
		var received = new MemoryStream();
		var buffer = new byte[8192];
		while(true)
		{
			int read = await stream.ReadAsync(buffer);
			if(read == 0)
				break;
			received.Write(buffer, 0, read);
			string sofar = Encoding.UTF8.GetString(received.ToArray());
			int headerEnd = sofar.IndexOf("\r\n\r\n", StringComparison.Ordinal);
			if(headerEnd < 0)
				continue;
			int length = 0;
			foreach(var line in sofar[..headerEnd].Split("\r\n"))
				if(line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
					length = int.Parse(line["Content-Length:".Length..].Trim());
			if(received.Length >= headerEnd + 4 + length)
				break;
		}
		byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
		await stream.WriteAsync(response);
		return Encoding.UTF8.GetString(received.ToArray());
	}
}
