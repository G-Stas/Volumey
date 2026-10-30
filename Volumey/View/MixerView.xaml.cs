#nullable enable
using System.Windows;

namespace Volumey.View;

public sealed partial class MixerView
{
	public MixerView()
	{
		InitializeComponent();
	}

	/// <summary>
	/// The height the mixer needs for its fixed parts (device area, header, footer) and for the apps list,
	/// measured at the given width
	/// </summary>
	internal (double chrome, double apps) MeasureHeights(double width)
	{
		var unbounded = new Size(width, double.PositiveInfinity);
		SessionsList.Measure(unbounded);
		Measure(unbounded);
		//the scroll viewer takes the list's height when unbounded, so the rest is the fixed part
		double apps = SessionsList.DesiredSize.Height + ScrollViewer.Padding.Top + ScrollViewer.Padding.Bottom;
		return (DesiredSize.Height - apps, apps);
	}
}

