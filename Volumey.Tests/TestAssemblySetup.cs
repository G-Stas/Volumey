using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;

namespace Volumey.Tests;

[TestClass]
public static class TestAssemblySetup
{
	/// <summary>
	/// Keeps tests away from the real settings file of the machine.
	/// </summary>
	[AssemblyInitialize]
	public static void Initialize(TestContext context)
		=> SettingsProvider.UseSettings(new VolumeySettings());
}
