using System.Collections.Generic;
using Volumey.Model;

namespace Volumey.DataProvider;

public interface IScreenInfoProvider
{
	IEnumerable<ScreenInfo> GetAllScreensInfo();
}
