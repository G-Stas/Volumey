using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Helper;

namespace Volumey.Tests;

[TestClass]
public class NativeMethodsTests
{
	[TestMethod]
	public void ProcessImagePathOfOwnProcessIsItsExePath()
	{
		string path = NativeMethods.GetProcessImagePath(Environment.ProcessId, out int error);

		Assert.AreEqual(0, error);
		Assert.AreEqual(Environment.ProcessPath, path, ignoreCase: true);
	}

	[TestMethod]
	public void ProcessImagePathOfMissingProcessIsNull()
	{
		//process IDs are multiples of 4, so this one never exists
		string path = NativeMethods.GetProcessImagePath(3, out int error);

		Assert.IsNull(path);
		Assert.AreNotEqual(0, error);
	}

	[TestMethod]
	public void TheSystemsProcessListHasTheExePathWithItsDriveLetter()
	{
		Assert.AreEqual(Environment.ProcessPath, NativeMethods.GetProcessImagePathFromSystem(Environment.ProcessId), ignoreCase: true);
		Assert.IsNull(NativeMethods.GetProcessImagePathFromSystem(3));
	}

	[TestMethod]
	public void AKernelPathGetsTheDriveLetterOfItsDevice()
	{
		string DeviceOf(string drive) => drive switch
		{
			"C:" => @"\Device\HarddiskVolume3",
			"D:" => @"\Device\HarddiskVolume30",
			_ => null
		};

		Assert.AreEqual(@"C:\Games\Warcraft III\war3.exe", NativeMethods.ToDosPath(@"\Device\HarddiskVolume3\Games\Warcraft III\war3.exe", DeviceOf));
		//a device whose name starts like another's
		Assert.AreEqual(@"D:\app.exe", NativeMethods.ToDosPath(@"\Device\HarddiskVolume30\app.exe", DeviceOf));
		Assert.IsNull(NativeMethods.ToDosPath(@"\Device\HarddiskVolume7\app.exe", DeviceOf));
	}

	[TestMethod]
	public void ProcessNamesFromPathsMatchProcessProcessName()
	{
		Assert.AreEqual("war3", NativeMethods.ProcessNameFromPath(@"C:\Games\Warcraft III\war3.exe"));
		Assert.AreEqual("App", NativeMethods.ProcessNameFromPath(@"C:\App.EXE"));
		//only ".exe" is dropped, like Process.ProcessName does
		Assert.AreEqual("tool.bin", NativeMethods.ProcessNameFromPath(@"C:\tool.bin"));
		Assert.AreEqual(System.Diagnostics.Process.GetCurrentProcess().ProcessName, NativeMethods.ProcessNameFromPath(Environment.ProcessPath!));
	}
}
