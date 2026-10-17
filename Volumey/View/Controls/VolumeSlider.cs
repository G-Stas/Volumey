#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Volumey.Controls;

public sealed class VolumeSlider : Slider
{
	private Thumb? _thumb;

	public VolumeSlider()
		=> Loaded += OnLoaded;

	public static readonly DependencyProperty EnableMouseWheelProperty
		= DependencyProperty.Register("EnableMouseWheel",
									  typeof(bool),
									  typeof(VolumeSlider),
									  new PropertyMetadata(false, OnEnableMouseWheelChanged));

	[AttachedPropertyBrowsableForType(typeof(VolumeSlider))]
	public static void SetEnableMouseWheel(UIElement element, bool value)
		=> element.SetValue(EnableMouseWheelProperty, value);

	[AttachedPropertyBrowsableForType(typeof(VolumeSlider))]
	public static bool GetEnableMouseWheel(UIElement element)
		=> (bool)element.GetValue(EnableMouseWheelProperty);

	public static readonly DependencyProperty IsMutedProperty
		= DependencyProperty.Register("IsMuted",
									  typeof(bool),
									  typeof(VolumeSlider));

	[AttachedPropertyBrowsableForType(typeof(VolumeSlider))]
	public static void SetIsMuted(UIElement element, bool value)
		=> element.SetValue(IsMutedProperty, value);

	[AttachedPropertyBrowsableForType(typeof(VolumeSlider))]
	public static bool GetIsMuted(UIElement element)
		=> (bool)element.GetValue(IsMutedProperty);

	/// <summary>
	/// How loud the slider's app or device plays (0..1, before its volume): drawn as a bar over the fill, as that share of it,
	/// so the bar ends where the sound is at this volume
	/// </summary>
	public static readonly DependencyProperty LevelProperty
		= DependencyProperty.Register("Level",
									  typeof(double),
									  typeof(VolumeSlider),
									  new PropertyMetadata(0d));

	public double Level
	{
		get => (double)GetValue(LevelProperty);
		set => SetValue(LevelProperty, value);
	}

	/// <summary>
	/// Whether the slider shows its <see cref="Level"/> (the fill is dimmed then). Inherited: set once on the mixer for all its sliders.
	/// </summary>
	public static readonly DependencyProperty ShowsLevelProperty
		= DependencyProperty.RegisterAttached("ShowsLevel",
											  typeof(bool),
											  typeof(VolumeSlider),
											  new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

	public static void SetShowsLevel(UIElement element, bool value)
		=> element.SetValue(ShowsLevelProperty, value);

	public static bool GetShowsLevel(UIElement element)
		=> (bool)element.GetValue(ShowsLevelProperty);

	protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnPreviewMouseLeftButtonDown(e);
		if(_thumb == null)
			return;
		// It's important to check Thumb.IsMouseOver, because if it's `true` then
		// the Thumb will already have its `OnMouseLeftButtonDown` method called - there's
		// no need to manually trigger it and doing so would result in firing the event twice.
		if(_thumb.IsMouseOver)
			return;

		// When `IsMoveToPointEnabled` is true, the Slider's `OnPreviewMouseLeftButtonDown`
		// method updates the slider's value to where the user clicked. However, the Thumb
		// hasn't had its position updated yet to reflect the change. As a result, we must
		// call `UpdateLayout` on the Thumb to make sure its position is correct before we
		// trigger a `MouseLeftButtonDownEvent` on it.
		_thumb.UpdateLayout();
		_thumb.RaiseEvent(new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left)
		{
			RoutedEvent = MouseLeftButtonDownEvent,
			Source = _thumb
		});
	}

	private void OnLoaded(object sender, EventArgs e)
	{
		_thumb = (Template?.FindName("PART_Track", this) as Track)?.Thumb;
		Loaded -= OnLoaded;
	}

	private static void OnEnableMouseWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if(e.NewValue != e.OldValue && d is Slider slider)
		{
			slider.PreviewMouseWheel -= OnSliderPreviewMouseWheel;
			if(e.NewValue is true)
				slider.PreviewMouseWheel += OnSliderPreviewMouseWheel;
		}
	}

	private static void OnSliderPreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if(sender is Slider slider && slider.GetValue(EnableMouseWheelProperty) is true)
		{
			var difference = slider.SmallChange;
			var currentValue = slider.Value;
			var newValue = ConstrainToRange(slider, e.Delta > 0 ? currentValue + difference : currentValue - difference);
			slider.SetCurrentValue(ValueProperty, newValue);
			e.Handled = true;
		}
	}

	private static double ConstrainToRange(RangeBase rangeBase, double value)
	{
		var minimum = rangeBase.Minimum;
		if(value < minimum)
			return minimum;
		var maximum = rangeBase.Maximum;
		if(value > maximum)
			return maximum;
		return value;
	}
}
