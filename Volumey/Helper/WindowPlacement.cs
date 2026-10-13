using System.Drawing;

namespace Volumey.Helper;

/// <summary>
/// Window placement math, in physical pixels
/// </summary>
internal static class WindowPlacement
{
	/// <summary>
	/// Where a window has to move to lie inside the work area: shifted back in by the part that sticks out
	/// (e.g. remembered near an edge, then opened bigger because the screen's scale went up). A window bigger than the work area
	/// is aligned to its top left corner, so its title bar and header stay reachable.
	/// </summary>
	public static Point KeepInside(Rectangle window, Rectangle workArea)
	{
		int left = window.Left;
		int top = window.Top;
		if(window.Right > workArea.Right)
			left = workArea.Right - window.Width;
		if(window.Bottom > workArea.Bottom)
			top = workArea.Bottom - window.Height;
		if(left < workArea.Left)
			left = workArea.Left;
		if(top < workArea.Top)
			top = workArea.Top;
		return new Point(left, top);
	}
}
