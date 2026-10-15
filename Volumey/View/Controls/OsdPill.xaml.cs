#nullable enable
using Volumey.Model;

namespace Volumey.View.Controls;

/// <summary>
/// One app's or device's volume in the OSD (<see cref="OsdWindow"/>) and the Notifications page's preview; its data context is the session
/// </summary>
public sealed partial class OsdPill
{
	/// <summary>
	/// The room around the pill for its shadow (the border's margin in the XAML)
	/// </summary>
	public const double ShadowMargin = 12;

	/// <summary>
	/// The pill's size in the OSD with the room for its shadow (the border's minimum width, which its content never exceeds, and height)
	/// </summary>
	public const double OuterWidth = 220 + 2 * ShadowMargin;
	public const double OuterHeight = 48 + 2 * ShadowMargin;

	public OsdPill()
	{
		InitializeComponent();
	}

	public OsdPill(IManagedMasterAudioSession session) : this()
	{
		DataContext = session;
	}
}
