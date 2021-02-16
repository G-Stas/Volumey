using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces
{
	[Guid(GuidValue.External.IMMEndpoint), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IMMEndpoint
	{
		/// <summary>
		/// Gets the dataflow of the Endpoint device
		/// </summary>
		/// <param name="flow"></param>
		/// <returns></returns>
		int GetDataFlow([Out] out EDataFlow flow);
	}
}