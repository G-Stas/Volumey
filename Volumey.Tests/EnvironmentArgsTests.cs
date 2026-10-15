using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Helper;

namespace Volumey.Tests;

/// <summary>
/// The folder overrides passed through the restart command line, parsed back the way Windows splits a command line
/// </summary>
[TestClass]
public class EnvironmentArgsTests
{
	[DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);

	[DllImport("kernel32.dll")]
	private static extern IntPtr LocalFree(IntPtr memory);

	private static string[] SplitLikeWindows(string args)
	{
		//the first token is read as the program path with other rules, so a dummy one goes first
		var argv = CommandLineToArgvW("Volumey.exe" + args, out int count);
		try
		{
			var result = new string[count - 1];
			for(int i = 1; i < count; i++)
				result[i - 1] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size))!;
			return result;
		}
		finally
		{
			LocalFree(argv);
		}
	}

	private static Dictionary<string, string> RoundTrip(Dictionary<string, string> variables)
	{
		var args = EnvironmentArgs.Build(name => variables.GetValueOrDefault(name));
		var applied = new Dictionary<string, string>();
		EnvironmentArgs.Apply(SplitLikeWindows(args), (name, value) => applied[name] = value);
		return applied;
	}

	[TestMethod]
	public void NoOverridesGiveNoArguments()
	{
		Assert.AreEqual("", EnvironmentArgs.Build(_ => null));
		Assert.AreEqual("", EnvironmentArgs.Build(_ => ""));
	}

	[TestMethod]
	[DataRow(@"C:\Users\Some User\AppData\Local\Temp\Volumey-dev")]
	[DataRow(@"C:\Temp\Volumey dev\")]
	[DataRow(@"C:\")]
	[DataRow(@"\\server\share\Volumey\\")]
	public void TheSettingsFolderComesBackUnchanged(string folder)
	{
		var applied = RoundTrip(new Dictionary<string, string> { [EnvironmentArgs.SettingsDirVariable] = folder });

		Assert.HasCount(1, applied);
		Assert.AreEqual(folder, applied[EnvironmentArgs.SettingsDirVariable]);
	}

	[TestMethod]
	public void BothOverridesComeBackAfterOtherArguments()
	{
		var variables = new Dictionary<string, string>
		{
			[EnvironmentArgs.SettingsDirVariable] = @"C:\Temp\Volumey dev",
			[EnvironmentArgs.LogFileVariable] = @"C:\Temp\Volumey dev\volumey.log"
		};
		var args = Startup.MinimizedArg + EnvironmentArgs.Build(name => variables.GetValueOrDefault(name));
		var applied = new Dictionary<string, string>();

		var split = SplitLikeWindows(" " + args);
		EnvironmentArgs.Apply(split, (name, value) => applied[name] = value);

		Assert.AreEqual(Startup.MinimizedArg, split[0]);
		CollectionAssert.AreEquivalent(variables, applied);
	}

	[TestMethod]
	public void OnlyTheOverridesCanBeSet()
	{
		var applied = new Dictionary<string, string>();

		EnvironmentArgs.Apply(["-env:PATH=C:\\evil", "-env:VOLUMEY_SETTINGS_DIR", "-env:VOLUMEY_LOG_FILE=", "-minimized"],
			(name, value) => applied[name] = value);

		Assert.IsEmpty(applied);
	}
}
