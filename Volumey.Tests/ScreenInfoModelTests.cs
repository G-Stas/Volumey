using Volumey.Model;
using Xunit;

namespace Volumey.Tests
{
	public class ScreenInfoModelTests
	{
		private ScreenInfo model;

		public ScreenInfoModelTests()
		{
			model = new ScreenInfo(".//DISPLAY1", true, 1920, 1018, 1920, 1080, 0, 1920, 0, 1018, 1);
		}

		[Fact]
		public void ToStringShouldDisplayCorrectNativeResolutionValues()
		{
			//assert
			string expectedValue = $"{model.Name} ({model.NativeWidth}x{model.NativeHeight})";
			Assert.Equal(expectedValue, model.ToString());
		}
		
		[Fact]
		public void EqualsWithOtherModelShouldReturnFalse()
		{
			//arrange
			ScreenInfo other = new ScreenInfo(".//DISPLAY2", false, 2880, 1776, 2880, 1800, 1920, 4800, 0, 1776, 1.5);
			
			//assert
			Assert.False(model.Equals(other));
		}

		[Fact]
		public void EqualsWithNullShouldReturnFalse()
		{
			Assert.False(model.Equals(null));
		}
		
		[Fact]
		public void EqualsShouldReturnTrue()
		{
			//arrange
			ScreenInfo other = new ScreenInfo(".//DISPLAY1", true, 1920, 1018, 1920, 1080, 0, 1920, 0, 1018, 1);
			
			//assert
			Assert.True(model.Equals(other));
		}
	}
}