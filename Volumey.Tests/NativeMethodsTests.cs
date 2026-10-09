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
}
