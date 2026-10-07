using Volumey.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Volumey.Tests
{
	[TestClass]
	public class ScreenInfoModelTests
	{
		private ScreenInfo model;

		public ScreenInfoModelTests()
		{
			model = new ScreenInfo(".//DISPLAY1", true, 1920, 1018, 1920, 1080, 0, 1920, 0, 1018, 1);
		}

		[TestMethod]
		public void ToStringShouldDisplayCorrectNativeResolutionValues()
		{
			//assert
			string expectedValue = $"{model.Name} ({model.NativeWidth}x{model.NativeHeight})";
			Assert.AreEqual(expectedValue, model.ToString());
		}
		
		[TestMethod]
		public void EqualsWithOtherModelShouldReturnFalse()
		{
			//arrange
			ScreenInfo other = new ScreenInfo(".//DISPLAY2", false, 2880, 1776, 2880, 1800, 1920, 4800, 0, 1776, 1.5);
			
			//assert
			Assert.IsFalse(model.Equals(other));
		}

		[TestMethod]
		public void EqualsWithNullShouldReturnFalse()
		{
			Assert.IsFalse(model.Equals(null));
		}
		
		[TestMethod]
		public void EqualsShouldReturnTrue()
		{
			//arrange
			ScreenInfo other = new ScreenInfo(".//DISPLAY1", true, 1920, 1018, 1920, 1080, 0, 1920, 0, 1018, 1);
			
			//assert
			Assert.IsTrue(model.Equals(other));
		}
	}
}