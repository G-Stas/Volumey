using System.Collections.Generic;
using Volumey.Model;
using WpfScreenHelper;

namespace Volumey.DataProvider
{
	public class ScreenInfoProvider : IScreenInfoProvider
	{
		public ScreenInfo GetPrimaryScreenInfo()
		{
			var screen = Screen.PrimaryScreen;
			return new ScreenInfo(screen.DeviceName, screen.Primary, screen.WorkingArea.Width, screen.WorkingArea.Height,
			                      screen.Bounds.Width, screen.Bounds.Height,
			                      screen.WorkingArea.Left, screen.WorkingArea.Right, 
			                      screen.WorkingArea.Top, screen.WorkingArea.Bottom, screen.ScaleFactor); 
		}

		public IEnumerable<ScreenInfo> GetAllScreensInfo()
		{
			List<ScreenInfo> screens = new List<ScreenInfo>();

			foreach(var screen in Screen.AllScreens)
			{
				var current = new ScreenInfo(screen.DeviceName, screen.Primary, screen.WorkingArea.Width, screen.WorkingArea.Height,
												screen.Bounds.Width, screen.Bounds.Height,
			                                    screen.WorkingArea.Left, screen.WorkingArea.Right,
			                                    screen.WorkingArea.Top, screen.WorkingArea.Bottom, screen.ScaleFactor); 
				screens.Add(current);
			}
			return screens;
		}
	}
}