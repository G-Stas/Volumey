using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Volumey.Controls
{
	public class VolumeSlider : Slider
	{
		private Thumb _thumb;

		public VolumeSlider()
			=> this.Loaded += OnLoaded;

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
			=> (bool) element.GetValue(EnableMouseWheelProperty);

		public static readonly DependencyProperty IsMutedProperty
			= DependencyProperty.Register("IsMuted",
			                              typeof(bool),
			                              typeof(VolumeSlider));
		
		[AttachedPropertyBrowsableForType(typeof(VolumeSlider))]
		public static void SetIsMuted(UIElement element, bool value)
			=> element.SetValue(IsMutedProperty, value);

		[AttachedPropertyBrowsableForType(typeof(VolumeSlider))]
		public static bool GetIsMuted(UIElement element)
			=> (bool) element.GetValue(IsMutedProperty);

		protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnPreviewMouseLeftButtonDown(e);
			if(this._thumb == null)
				return;
			// It's important to check Thumb.IsMouseOver, because if it's `true` then
			// the Thumb will already have its `OnMouseLeftButtonDown` method called - there's
			// no need to manually trigger it and doing so would result in firing the event twice.
			if(this._thumb.IsMouseOver)
				return;

			// When `IsMoveToPointEnabled` is true, the Slider's `OnPreviewMouseLeftButtonDown`
			// method updates the slider's value to where the user clicked. However, the Thumb
			// hasn't had its position updated yet to reflect the change. As a result, we must
			// call `UpdateLayout` on the Thumb to make sure its position is correct before we
			// trigger a `MouseLeftButtonDownEvent` on it.
			this._thumb.UpdateLayout();
			this._thumb.RaiseEvent(new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left)
			{
				RoutedEvent = MouseLeftButtonDownEvent,
				Source = this._thumb
			});
		}

		private void OnLoaded(object sender, EventArgs e)
		{
			this._thumb = (this.Template?.FindName("PART_Track", this) as Track)?.Thumb;
			this.Loaded -= OnLoaded;
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
}