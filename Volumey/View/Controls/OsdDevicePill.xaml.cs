#nullable enable
using Volumey.Model;

namespace Volumey.View.Controls;

/// <summary>
/// The new default output device in the OSD (<see cref="OsdWindow"/>); its data context is the device's master session
/// </summary>
public sealed partial class OsdDevicePill
{
	public OsdDevicePill()
	{
		InitializeComponent();
	}

	public OsdDevicePill(IManagedMasterAudioSession device) : this()
	{
		DataContext = device;
	}
}
