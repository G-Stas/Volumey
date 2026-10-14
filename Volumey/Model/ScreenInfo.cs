#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Volumey.Model;

public struct ScreenInfo
{
	public readonly string Name;
	public readonly double NativeWidth;
	public readonly double NativeHeight;
	public readonly double Width;
	public readonly double Height;
	public readonly double WorkingAreaLeft;
	public readonly double WorkingAreaRight;
	public readonly double WorkingAreaTop;
	public readonly double ScaleFactor;
	public readonly double WorkingAreaBottom;
	public readonly bool IsPrimary;

	public ScreenInfo(string name, bool isPrimary, double width, double height, double nativeWidth, double nativeHeight,
						double wkLeft, double wkRight, double wkTop, double wkBottom, double scaleFactor)
	{
		Name = name;
		IsPrimary = isPrimary;
		Width = width;
		Height = height;
		NativeWidth = nativeWidth;
		NativeHeight = nativeHeight;
		WorkingAreaLeft = wkLeft;
		WorkingAreaRight = wkRight;
		WorkingAreaTop = wkTop;
		ScaleFactor = scaleFactor;
		WorkingAreaBottom = wkBottom;
	}

	public override bool Equals(object? obj)
	{
		if(!(obj is ScreenInfo other))
			return false;

		return Equals(other);
	}

	public bool Equals(ScreenInfo other)
	{
		return Name == other.Name && Width.Equals(other.Width) && Height.Equals(other.Height) && ScaleFactor.Equals(other.ScaleFactor) && WorkingAreaBottom.Equals(other.WorkingAreaBottom)
			   && WorkingAreaLeft.Equals(other.WorkingAreaLeft) && WorkingAreaRight.Equals(other.WorkingAreaRight) && WorkingAreaTop.Equals(other.WorkingAreaTop)
			   && NativeWidth.Equals(other.NativeWidth) && NativeHeight.Equals(other.NativeHeight);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Name, Width, Height, WorkingAreaLeft, WorkingAreaRight, WorkingAreaTop, WorkingAreaBottom, ScaleFactor);
	}

	public override string ToString()
	{
		return $"{Name} ({NativeWidth}x{NativeHeight})";
	}

	/// <summary>
	/// The screen with the remembered device name, otherwise the primary screen (e.g. while the remembered one isn't connected yet),
	/// otherwise the first one. Returns the default value if there are no screens.
	/// </summary>
	internal static ScreenInfo Find(IReadOnlyCollection<ScreenInfo> screens, string name)
	{
		if(name != null)
		{
			foreach(var screen in screens)
				if(screen.Name == name)
					return screen;
		}
		foreach(var screen in screens)
			if(screen.IsPrimary)
				return screen;
		return screens.FirstOrDefault();
	}
}
