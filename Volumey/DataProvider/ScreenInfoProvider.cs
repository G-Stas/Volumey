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
			return new ScreenInfo(screen.DeviceName, screen.WpfBounds.Width, screen.WpfBounds.Height,
			                       screen.WpfWorkingArea.Left, screen.WpfWorkingArea.Right,
			                       screen.WpfWorkingArea.Top, screen.WpfWorkingArea.Bottom);
		}

		public IEnumerable<ScreenInfo> GetAllScreensInfo()
		{
			List<ScreenInfo> screens = new List<ScreenInfo>();
			
			foreach(var screen in Screen.AllScreens)
			{
				screens.Add(new ScreenInfo(screen.DeviceName, screen.WpfBounds.Width, screen.WpfBounds.Height, 
				                             screen.WpfWorkingArea.Left, screen.WpfWorkingArea.Right, 
				                             screen.WpfWorkingArea.Top, screen.WpfWorkingArea.Bottom));
			}
			return screens;
		}
	}
}