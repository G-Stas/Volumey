namespace Volumey.Model
{
	public struct ScreenInfo
	{
		public readonly string Name;
		public readonly double Width;
		public readonly double Height;
		public readonly double AbsoluteLeft;
		public readonly double AbsoluteRight;
		public readonly double AbsoluteTop;
		public readonly double AbsoluteBottom;

		public ScreenInfo(string name, double width, double height, double aLeft, double aRight, double aTop, double aBottom)
		{
			Name = name;
			Width = width;
			Height = height;
			AbsoluteLeft = aLeft;
			AbsoluteRight = aRight;
			AbsoluteTop = aTop;
			AbsoluteBottom = aBottom;
		}
	
		public override string ToString()
		{
			return $"{Name} ({Width}x{Height})";
		}
	}
}