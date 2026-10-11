using Volumey.Model;

namespace Volumey.View.Controls;

/// <summary>
/// One app's or device's volume in the OSD (<see cref="OsdWindow"/>); its data context is the session
/// </summary>
public partial class OsdPill
{
	/// <summary>
	/// The room around the pill for its shadow (the border's margin in the XAML)
	/// </summary>
	public const double ShadowMargin = 12;

	public IManagedMasterAudioSession Session { get; }

	public OsdPill(IManagedMasterAudioSession session)
	{
		InitializeComponent();
		Session = session;
		DataContext = session;
	}
}
