using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Helper;

namespace Volumey.Tests;

/// <summary>
/// The autostart entry's command line and how a start points an existing entry at the running exe
/// </summary>
[TestClass]
public class AutostartEntryTests
{
	private const string Exe = @"C:\Program Files\Volumey\Volumey.exe";
	private const string DevFolderArg = "\"-env:VOLUMEY_SETTINGS_DIR=C:\\Temp\\Volumey dev\"";

	[TestMethod]
	public void TheEntryStartsTheQuotedExeMinimizedWithTheOverrides()
	{
		Assert.AreEqual($"\"{Exe}\" -minimized", SystemIntegrationHelper.StartupCommandLine(Exe, ""));
		Assert.AreEqual($"\"{Exe}\" -minimized {DevFolderArg}", SystemIntegrationHelper.StartupCommandLine(Exe, " " + DevFolderArg));
	}

	[TestMethod]
	[DataRow($"\"{Exe}\" -minimized")]
	[DataRow($"\"{Exe}\" -minimized {DevFolderArg}")]
	[DataRow("\"c:\\program files\\volumey\\VOLUMEY.EXE\" -minimized")]
	public void AnEntryForTheRunningExeIsKept(string entry)
	{
		Assert.IsNull(SystemIntegrationHelper.RepointStartupCommandLine(entry, Exe));
	}

	[TestMethod]
	public void AnEntryForAnotherExeIsPointedAtTheRunningOneWithItsOwnOverrides()
	{
		var entry = $"\"D:\\Old place\\Volumey.exe\" -minimized {DevFolderArg}";

		Assert.AreEqual($"\"{Exe}\" -minimized {DevFolderArg}", SystemIntegrationHelper.RepointStartupCommandLine(entry, Exe));
	}

	[TestMethod]
	[DataRow(@"C:\Program Files\Volumey\Volumey.exe -minimized")]
	[DataRow($"\"{Exe}\"")]
	[DataRow($"\"{Exe}\" -minimizedX")]
	[DataRow($"\"{Exe}.old\" -minimized")]
	public void AnUnquotedOrNotMinimizedEntryIsRewritten(string entry)
	{
		Assert.AreEqual($"\"{Exe}\" -minimized", SystemIntegrationHelper.RepointStartupCommandLine(entry, Exe));
	}
}
