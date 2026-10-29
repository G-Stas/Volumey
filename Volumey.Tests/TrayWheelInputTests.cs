using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Helper;

namespace Volumey.Tests;

[TestClass]
public class TrayWheelInputTests
{
	[TestMethod]
	public void ANotchIsOneStep()
	{
		int remainder = 0;

		Assert.AreEqual(1, TrayWheelInput.AddDelta(ref remainder, 120));
		Assert.AreEqual(-1, TrayWheelInput.AddDelta(ref remainder, -120));
		Assert.AreEqual(0, remainder);
	}

	[TestMethod]
	public void SmallDeltasAddUpToOneStep()
	{
		int remainder = 0;

		Assert.AreEqual(0, TrayWheelInput.AddDelta(ref remainder, 30));
		Assert.AreEqual(0, TrayWheelInput.AddDelta(ref remainder, 30));
		Assert.AreEqual(0, TrayWheelInput.AddDelta(ref remainder, 30));
		Assert.AreEqual(1, TrayWheelInput.AddDelta(ref remainder, 30));
		Assert.AreEqual(0, remainder);
	}

	[TestMethod]
	public void AFastSpinIsSeveralSteps()
	{
		int remainder = 0;

		Assert.AreEqual(-2, TrayWheelInput.AddDelta(ref remainder, -270));
		Assert.AreEqual(-30, remainder);
	}

	[TestMethod]
	public void AChangeOfDirectionDropsThePartLeftFromTheOtherDirection()
	{
		int remainder = 0;
		TrayWheelInput.AddDelta(ref remainder, 60);

		Assert.AreEqual(-1, TrayWheelInput.AddDelta(ref remainder, -120));
		Assert.AreEqual(0, remainder);
	}
}
