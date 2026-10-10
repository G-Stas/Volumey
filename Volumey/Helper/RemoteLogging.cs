using System.Linq;
using System.Reflection;
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

	/// <summary>
	/// The input key this build was made with, null for a build without remote logging
	/// </summary>
	internal static string BuildInputKey
		=> typeof(RemoteLogging).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
								  .FirstOrDefault(a => a.Key == "LogglyInputKey")?.Value is { Length: > 0 } key ? key : null;

	internal static LogglyAppender CreateAppender(string inputKey, string rootUrl = LogglyUrl)
	{
		var appender = new LogglyAppender
		{
			RootUrl = rootUrl,
			CustomerToken = inputKey,
			Tag = "log4net",
			Layout = new PatternLayout("%message %property{AppVer}")
		};
		//repeated messages (e.g. one failure per audio callback) are sent once
		appender.AddFilter(new DuplicateLogMessageFilter());
		appender.ActivateOptions();
		return appender;
	}
}
