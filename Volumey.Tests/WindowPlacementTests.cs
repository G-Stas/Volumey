using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Helper;

namespace Volumey.Tests;

[TestClass]
public class WindowPlacementTests
{
	//a 1920x1080 screen right of a 2560 wide one, taskbar at the bottom
	private static readonly Rectangle WorkArea = Rectangle.FromLTRB(2560, 0, 4480, 1032);

	[TestMethod]
	public void AWindowInsideStaysWhereItIs()
		=> Assert.AreEqual(new Point(3000, 300), WindowPlacement.KeepInside(new Rectangle(3000, 300, 460, 469), WorkArea));

	[TestMethod]
	public void AWindowStickingOutRightAndBottomIsMovedBackIn()
	{
		//remembered near the corner at 100%, opened at 150%: 368x375 became 552x563
		var window = new Rectangle(4000, 600, 552, 563);
		Assert.AreEqual(new Point(4480 - 552, 1032 - 563), WindowPlacement.KeepInside(window, WorkArea));
	}

	[TestMethod]
	public void AWindowStickingOutLeftAndTopIsMovedBackIn()
		=> Assert.AreEqual(new Point(2560, 0), WindowPlacement.KeepInside(new Rectangle(2500, -40, 460, 469), WorkArea));

	[TestMethod]
	public void AWindowBiggerThanTheWorkAreaIsAlignedToItsTopLeftCorner()
		=> Assert.AreEqual(new Point(2560, 0), WindowPlacement.KeepInside(new Rectangle(3000, 200, 2000, 1200), WorkArea));
}
