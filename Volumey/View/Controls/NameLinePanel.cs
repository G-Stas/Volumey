#nullable enable
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Volumey.Controls;

/// <summary>
/// The name line of a mixer row: the first child is the name, the others are badges next to it. When the name and the badges
/// don't fit in one line, the badges go to a line of their own under the name, and the name gets the whole width (ending in "…"
/// when it's longer even alone)
/// </summary>
public sealed class NameLinePanel : Panel
{
	/// <summary>The space between the name and a badge, and between two badges</summary>
	public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(nameof(Spacing), typeof(double),
		typeof(NameLinePanel), new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

	/// <summary>The space between the name and the badges' own line</summary>
	public static readonly DependencyProperty LineSpacingProperty = DependencyProperty.Register(nameof(LineSpacing), typeof(double),
		typeof(NameLinePanel), new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

	public double Spacing
	{
		get => (double)GetValue(SpacingProperty);
		set => SetValue(SpacingProperty, value);
	}

	public double LineSpacing
	{
		get => (double)GetValue(LineSpacingProperty);
		set => SetValue(LineSpacingProperty, value);
	}

	/// <summary>The badges are on their own line</summary>
	internal bool BadgesBelow { get; private set; }

	private UIElement[] Badges() => InternalChildren.Cast<UIElement>().Skip(1).Where(c => c.Visibility != Visibility.Collapsed).ToArray();

	protected override Size MeasureOverride(Size availableSize)
	{
		if(InternalChildren.Count == 0)
			return new Size();
		var name = InternalChildren[0];
		var badges = Badges();
		foreach(var badge in badges)
			badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

		name.Measure(new Size(double.PositiveInfinity, availableSize.Height));
		double badgesWidth = badges.Sum(b => b.DesiredSize.Width + Spacing);
		double badgesHeight = badges.Length == 0 ? 0 : badges.Max(b => b.DesiredSize.Height);

		BadgesBelow = badges.Length > 0 && name.DesiredSize.Width + badgesWidth > availableSize.Width;
		if(!BadgesBelow)
		{
			if(badges.Length == 0)
				name.Measure(availableSize);
			return new Size(name.DesiredSize.Width + badgesWidth, Math.Max(name.DesiredSize.Height, badgesHeight));
		}

		name.Measure(new Size(availableSize.Width, double.PositiveInfinity));
		double lineWidth = badgesWidth - Spacing;
		return new Size(Math.Min(availableSize.Width, Math.Max(name.DesiredSize.Width, lineWidth)),
			name.DesiredSize.Height + LineSpacing + badgesHeight);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		if(InternalChildren.Count == 0)
			return finalSize;
		var name = InternalChildren[0];
		var badges = Badges();
		double nameHeight = name.DesiredSize.Height;
		double badgesHeight = badges.Length == 0 ? 0 : badges.Max(b => b.DesiredSize.Height);

		double x;
		double lineTop;
		double lineHeight;
		if(BadgesBelow)
		{
			name.Arrange(new Rect(0, 0, finalSize.Width, nameHeight));
			x = 0;
			lineTop = nameHeight + LineSpacing;
			lineHeight = badgesHeight;
		}
		else
		{
			lineHeight = Math.Max(nameHeight, badgesHeight);
			double nameWidth = Math.Min(name.DesiredSize.Width, finalSize.Width);
			name.Arrange(new Rect(0, (lineHeight - nameHeight) / 2, nameWidth, nameHeight));
			x = nameWidth + Spacing;
			lineTop = 0;
		}
		foreach(var badge in badges)
		{
			var size = badge.DesiredSize;
			badge.Arrange(new Rect(x, lineTop + (lineHeight - size.Height) / 2, size.Width, size.Height));
			x += size.Width + Spacing;
		}
		return finalSize;
	}
}
