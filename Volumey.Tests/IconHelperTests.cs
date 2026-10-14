using System;
using System.Drawing;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Helper;

namespace Volumey.Tests;

[TestClass]
public class IconHelperTests
{
	[TestMethod]
	public void AnAppsIconIsReadOnceAndShared()
	{
		string notepad = Path.Combine(Environment.SystemDirectory, "notepad.exe");
		int reads = 0;
		Icon Extract(string path)
		{
			reads++;
			return Icon.ExtractAssociatedIcon(path);
		}

		var first = IconHelper.GetIconSource(notepad, Extract);
		//the same file in other letter case, as paths from different sources can come
		var second = IconHelper.GetIconSource(notepad.ToUpperInvariant(), Extract);

		Assert.IsNotNull(first);
		Assert.IsTrue(first.IsFrozen, "a shared image must be frozen to be used from other threads");
		Assert.AreSame(first, second);
		Assert.AreEqual(1, reads);
	}

	[TestMethod]
	public void AnIconThatCantBeReadIsTriedAgain()
	{
		const string path = @"C:\does\not\exist.exe";
		int reads = 0;
		Icon Extract(string _)
		{
			reads++;
			return null;
		}

		Assert.IsNull(IconHelper.GetIconSource(path, Extract));
		Assert.IsNull(IconHelper.GetIconSource(path, Extract));
		Assert.AreEqual(2, reads);
	}

	[TestMethod]
	public void TheGenericIconIsOneSharedFrozenImage()
	{
		var generic = IconHelper.GenericExeIconSource;
		Assert.IsNotNull(generic);
		Assert.IsTrue(generic.IsFrozen);
		Assert.AreSame(generic, IconHelper.GenericExeIconSource);
	}

	[TestMethod]
	public void IconsAreReadFromResourcePathsAndFiles()
	{
		string shell32 = Path.Combine(Environment.SystemDirectory, "shell32.dll");

		using var fromResource = IconHelper.GetFromResourcePath("%SystemRoot%\\System32\\shell32.dll,3");
		using var fromFile = IconHelper.GetFromResourcePath(Path.Combine(Environment.SystemDirectory, "notepad.exe"));
		using var fromDll = IconHelper.GetFromDll(shell32, 3);

		Assert.IsNotNull(fromResource?.GetAsImageSource());
		Assert.IsNotNull(fromFile?.GetAsImageSource());
		Assert.IsNotNull(fromDll?.GetAsImageSource());
	}

	[TestMethod]
	public void BrokenIconPathsGiveNoIcon()
	{
		Assert.IsNull(IconHelper.GetFromResourcePath(null));
		Assert.IsNull(IconHelper.GetFromResourcePath(""));
		Assert.IsNull(IconHelper.GetFromResourcePath(@"C:\does\not\exist.dll,5"));
		Assert.IsNull(IconHelper.GetFromResourcePath("not a path,abc"));
	}
}
