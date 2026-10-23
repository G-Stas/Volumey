#nullable enable
using System.IO;
using System.Linq;
using System.Reflection;
using log4net.Appender;
using log4net.Core;
using log4net.Filter;
using log4net.Layout;
using log4net.loggly;

namespace Volumey.Helper;

/// <summary>
/// Logging to Loggly (Release and Store builds). The input key isn't in the repository: the build takes it from the
/// VOLUMEY_LOGGLY_INPUT_KEY environment variable or the git-ignored loggly.key file and stores it in the assembly.
/// </summary>
internal static class RemoteLogging
{
	internal const string LogglyUrl = "https://logs-01.loggly.com/";

	/// <summary>How many info messages before a crash are sent with it</summary>
	internal const int CrashContextSize = 30;

	/// <summary>
	/// The input key this build was made with, null for a build without remote logging
	/// </summary>
	internal static string? BuildInputKey
		=> typeof(RemoteLogging).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
								  .FirstOrDefault(a => a.Key == "LogglyInputKey")?.Value is { Length: > 0 } key ? key : null;

	internal static LogglyAppender CreateAppender(string inputKey, string rootUrl = LogglyUrl)
	{
		var appender = new LogglyAppender
		{
			RootUrl = rootUrl,
			CustomerToken = inputKey,
			Tag = "log4net",
			Layout = new LogglyLayout()
		};
		appender.ActivateOptions();
		return appender;
	}

	/// <summary>
	/// The appenders for the root logger, all ending in <paramref name="target"/>: warnings and errors are sent as they happen;
	/// info messages only as the context of a crash (the last <see cref="CrashContextSize"/> before a fatal error), since devices and
	/// sessions change all the time on some PCs. Each message is limited by <see cref="MessageRateFilter"/>, and a message repeated
	/// right away is sent once.
	/// </summary>
	internal static IAppender[] CreatePipeline(IAppender target)
	{
		var gate = new ForwardingAppender { Name = "RemoteGate" };
		gate.AddFilter(new MessageRateFilter());
		//repeated messages (e.g. one failure per audio callback) are sent once
		gate.AddFilter(new DuplicateLogMessageFilter());
		gate.AddAppender(target);
		gate.ActivateOptions();

		var direct = new ForwardingAppender { Name = "RemoteWarnings", Threshold = Level.Warn };
		direct.AddAppender(gate);
		direct.ActivateOptions();

		//the buffer gets info and fatal messages; a fatal one sends the buffer, and only its info messages pass on
		//(the fatal message itself goes the direct way)
		var infoOnly = new ForwardingAppender { Name = "RemoteCrashContextInfo" };
		infoOnly.AddFilter(new LevelRangeFilter { LevelMin = Level.Info, LevelMax = Level.Info, AcceptOnMatch = true });
		infoOnly.AddFilter(new DenyAllFilter());
		infoOnly.AddAppender(gate);
		infoOnly.ActivateOptions();

		var context = new CrashContextAppender
		{
			Name = "RemoteCrashContext",
			//the fatal message takes a place in the buffer too
			BufferSize = CrashContextSize + 1,
			Lossy = true,
			//no location info: it walks the stack for every buffered message
			Fix = FixFlags.Partial,
			Evaluator = new LevelEvaluator(Level.Fatal)
		};
		context.AddFilter(new LevelMatchFilter { LevelToMatch = Level.Info, AcceptOnMatch = true });
		context.AddFilter(new LevelMatchFilter { LevelToMatch = Level.Fatal, AcceptOnMatch = true });
		context.AddFilter(new DenyAllFilter());
		context.AddAppender(infoOnly);
		context.ActivateOptions();

		return [direct, context];
	}

	/// <summary>
	/// A lossy buffer that is only ever sent by a fatal error: log4net's buffering appenders also send a lossy buffer when they close,
	/// which would send the last info messages at every exit
	/// </summary>
	private sealed class CrashContextAppender : BufferingForwardingAppender
	{
		protected override void OnClose()
		{
			RemoveAllAppenders();
		}
	}

	/// <summary>
	/// The message and the app version, and how many messages like it <see cref="MessageRateFilter"/> dropped before it
	/// </summary>
	internal sealed class LogglyLayout : PatternLayout
	{
		public LogglyLayout() : base("%message %property{AppVer}") { }

		public override void Format(TextWriter writer, LoggingEvent loggingEvent)
		{
			base.Format(writer, loggingEvent);
			if(loggingEvent.LookupProperty(MessageRateFilter.DroppedProperty) is int dropped)
				writer.Write($" [{dropped} more like it dropped]");
		}
	}
}
