#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Volumey.Helper;

/// <summary>
/// Carries the folder overrides of a dev or test run through the command line as <c>"-env:NAME=value"</c> arguments.
/// Windows restarts an app, and starts the autostart entry and the Start menu shortcut, with the user's environment,
/// not the one the app was started with, so without them a dev build started that way would use the real settings.
/// </summary>
internal static partial class EnvironmentArgs
{
	/// <summary>Overrides the settings folder, e.g. to run a dev build without touching the real settings</summary>
	internal const string SettingsDirVariable = "VOLUMEY_SETTINGS_DIR";
	/// <summary>Path of a plain-text log file for debug builds, e.g. set by scripts/run-dev.ps1</summary>
	internal const string LogFileVariable = "VOLUMEY_LOG_FILE";

	private const string Prefix = "-env:";
	//only these can be set from the command line
	private static readonly string[] Variables = [SettingsDirVariable, LogFileVariable];

	/// <summary>
	/// The arguments for the overrides set in this process, each with a leading space; empty when none is set
	/// </summary>
	internal static string FromEnvironment() => Build(Environment.GetEnvironmentVariable);

	internal static string Build(Func<string, string?> getVariable)
	{
		var args = new StringBuilder();
		foreach(var name in Variables)
		{
			if(getVariable(name) is string value && value.Length > 0)
				args.Append(' ').Append(Quote($"{Prefix}{name}={value}"));
		}
		return args.ToString();
	}

	/// <summary>
	/// The override arguments of a command line written by <see cref="Build"/>, each with a leading space
	/// </summary>
	internal static string FindIn(string commandLine)
		=> string.Concat(QuotedArg().Matches(commandLine).Select(match => " " + match.Value));

	[GeneratedRegex("\"" + Prefix + "[^\"]*\"")]
	private static partial Regex QuotedArg();

	/// <summary>
	/// Sets the overrides found in the arguments, before anything reads them
	/// </summary>
	internal static void Apply(IEnumerable<string> args) => Apply(args, Environment.SetEnvironmentVariable);

	internal static void Apply(IEnumerable<string> args, Action<string, string> setVariable)
	{
		foreach(var arg in args)
		{
			if(!arg.StartsWith(Prefix, StringComparison.Ordinal))
				continue;
			int separator = arg.IndexOf('=', Prefix.Length);
			if(separator < 0)
				continue;
			var name = arg.Substring(Prefix.Length, separator - Prefix.Length);
			var value = arg.Substring(separator + 1);
			if(value.Length > 0 && Array.IndexOf(Variables, name) >= 0)
				setVariable(name, value);
		}
	}

	/// <summary>
	/// Quotes an argument that contains no quotes: backslashes before the closing quote are doubled,
	/// otherwise Windows' command line parsing reads the last one as an escaped quote (e.g. "C:\")
	/// </summary>
	private static string Quote(string arg)
	{
		int trailingBackslashes = arg.Length - arg.TrimEnd('\\').Length;
		return $"\"{arg}{new string('\\', trailingBackslashes)}\"";
	}
}
