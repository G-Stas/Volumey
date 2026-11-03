using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Helper;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Color = System.Drawing.Color;

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
	public void AnEmptyIconIsToldApartFromOneThatShowsSomething()
	{
		BitmapSource Square(byte alpha)
		{
			var pixels = new byte[4 * 4 * 4];
			for(int i = 0; i < pixels.Length; i += 4)
				pixels[i + 3] = alpha;
			return BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, pixels, 4 * 4);
		}

		Assert.IsFalse(IconHelper.HasVisiblePixels(Square(0)));
		Assert.IsTrue(IconHelper.HasVisiblePixels(Square(1)));
	}

	[TestMethod]
	public void AnAppsIconComesFromItsExecutable()
	{
		var icon = IconHelper.GetAppIconSource(Path.Combine(Environment.SystemDirectory, "notepad.exe"));

		Assert.IsNotNull(icon);
		Assert.IsTrue(IconHelper.HasVisiblePixels(icon));
		Assert.IsTrue(icon.IsFrozen);
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

	private const int GlyphSize = 16;
	private const int GlyphCenter = GlyphSize / 2;

	/// <summary>A round glyph of one color on a transparent background</summary>
	internal static Icon CreateGlyph(Color color)
	{
		using var bitmap = new Bitmap(GlyphSize, GlyphSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		using(var graphics = Graphics.FromImage(bitmap))
		using(var brush = new SolidBrush(color))
		{
			graphics.SmoothingMode = SmoothingMode.None;
			graphics.FillEllipse(brush, 3, 3, GlyphSize - 6, GlyphSize - 6);
		}
		var handle = new HICON(bitmap.GetHicon());
		using var borrowed = Icon.FromHandle(handle);
		var owned = (Icon)borrowed.Clone();
		PInvoke.DestroyIcon(handle);
		return owned;
	}

	[TestMethod]
	public void OnlyAnIconOfWhiteIsAWhiteGlyph()
	{
		using var white = CreateGlyph(Color.White);
		using var black = CreateGlyph(Color.Black);
		using var colored = CreateGlyph(Color.OrangeRed);
		using var empty = CreateGlyph(Color.Transparent);

		Assert.IsTrue(IconHelper.IsWhiteGlyph(white));
		Assert.IsFalse(IconHelper.IsWhiteGlyph(black));
		Assert.IsFalse(IconHelper.IsWhiteGlyph(colored));
		Assert.IsFalse(IconHelper.IsWhiteGlyph(empty), "nothing is visible");
	}

	[TestMethod]
	public void AnInvertedGlyphIsDarkAndKeepsItsShape()
	{
		using var white = CreateGlyph(Color.White);

		using var inverted = IconHelper.Inverted(white);

		Assert.AreEqual(white.Width, inverted.Width);
		Assert.AreEqual(white.Height, inverted.Height);
		using var bitmap = inverted.ToBitmap();
		var center = bitmap.GetPixel(GlyphCenter, GlyphCenter);
		Assert.AreEqual(0, center.R);
		Assert.AreEqual(0, center.G);
		Assert.AreEqual(0, center.B);
		Assert.AreEqual(byte.MaxValue, center.A);
		Assert.AreEqual(0, bitmap.GetPixel(0, 0).A, "the transparent part stays transparent");
		Assert.IsFalse(IconHelper.IsWhiteGlyph(inverted));
	}

	[TestMethod]
	public void AnIconOfTheSizeAskedIsReadFromAFile()
	{
		string imageres = Path.Combine(Environment.SystemDirectory, "imageres.dll");
		if(!File.Exists(imageres))
			Assert.Inconclusive("This Windows has no imageres.dll");

		foreach(int size in new[] { 16, 20, 24, 32, 48 })
		{
			using var icon = IconHelper.GetFromDllOfSize(imageres, 11, size);
			Assert.IsNotNull(icon, size.ToString());
			Assert.AreEqual(size, icon.Width, size.ToString());
			Assert.AreEqual(size, icon.Height, size.ToString());
		}
	}

	[TestMethod]
	public void NoIconOfASizeIsReadFromAMissingFileOrIcon()
	{
		Assert.IsNull(IconHelper.GetFromDllOfSize(null, 11, 16));
		Assert.IsNull(IconHelper.GetFromDllOfSize(@"C:\does\not\exist.dll", 11, 16));
		Assert.IsNull(IconHelper.GetFromDllOfSize(Path.Combine(Environment.SystemDirectory, "imageres.dll"), int.MaxValue, 16));
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
