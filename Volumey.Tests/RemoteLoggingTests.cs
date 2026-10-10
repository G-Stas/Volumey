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
			BasicConfigurator.Configure(repository, appender);
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
