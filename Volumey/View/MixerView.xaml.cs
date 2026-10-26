#nullable enable
using System;
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

		double baseApps = SessionsList.DesiredSize.Height + ScrollViewer.Padding.Top + ScrollViewer.Padding.Bottom;
		double chrome = DesiredSize.Height - baseApps;

		// Workaround for potential client area calculation discrepancies on Windows 10.
		// It appears that borderless WindowChrome might slightly truncate the available space,
		// possibly due to invisible resizing borders not being fully accounted for.
		//
		// See related WPF behavior for Windows 10: https://github.com/dotnet/wpf/issues/3887
		// Quote: "First, the sizes for the window frame are not correct... the transparent side non-client frames are not being taken into account".
		int win10ChromeCompensation = 2;

		double finalApps = Environment.OSVersion.Version.Build < 22000 ? baseApps + win10ChromeCompensation : baseApps;

		return (chrome, finalApps);
	}
}
