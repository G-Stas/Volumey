using System.Collections.Generic;
using Volumey.Model;

namespace Volumey.DataProvider
{
	public interface IScreenInfoProvider
	{
		ScreenInfo GetPrimaryScreenInfo();
		IEnumerable<ScreenInfo> GetAllScreensInfo();
	}
}