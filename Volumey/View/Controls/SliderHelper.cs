//  MIT License
//
//  Copyright (c) .NET Foundation and Contributors. All rights reserved.
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//  
//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.
//  
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Volumey.Controls
{
	public class SliderHelper
	{
		public static readonly DependencyProperty ChangeValueByProperty
			= DependencyProperty.RegisterAttached(
			                                      "ChangeValueBy",
			                                      typeof(MouseWheelChange),
			                                      typeof(SliderHelper),
			                                      new PropertyMetadata(MouseWheelChange.SmallChange));

		public static readonly DependencyProperty EnableMouseWheelProperty
			= DependencyProperty.RegisterAttached(
			                                      "EnableMouseWheel",
			                                      typeof(MouseWheelState),
			                                      typeof(SliderHelper),
			                                      new PropertyMetadata(MouseWheelState.None,
			                                                           OnEnableMouseWheelChanged));
		
		[AttachedPropertyBrowsableForType(typeof(Slider))]
		public static void SetEnableMouseWheel(UIElement element, MouseWheelState value)
		{
			element.SetValue(EnableMouseWheelProperty, value);
		}

		

		[AttachedPropertyBrowsableForType(typeof(Slider))]
		public static MouseWheelState GetEnableMouseWheel(UIElement element)
		{
			return(MouseWheelState) element.GetValue(EnableMouseWheelProperty);
		}

		private static void OnEnableMouseWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if(e.NewValue != e.OldValue)
			{
				switch(d)
				{
					case Slider slider:
					{
						slider.PreviewMouseWheel -= OnSliderPreviewMouseWheel;
						if((MouseWheelState) e.NewValue != MouseWheelState.None)
						{
							slider.PreviewMouseWheel += OnSliderPreviewMouseWheel;
						}

						break;
					}
				}
			}
		}

		private static void OnSliderPreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			if(sender is Slider slider && (slider.IsFocused ||
			                               MouseWheelState.MouseHover
			                                              .Equals(slider.GetValue(EnableMouseWheelProperty))))
			{
				var changeType = (MouseWheelChange) slider.GetValue(ChangeValueByProperty);
				var difference = changeType == MouseWheelChange.LargeChange ? slider.LargeChange : slider.SmallChange;

				var currentValue = slider.Value;
				var newValue =
					ConstrainToRange(slider, e.Delta > 0 ? currentValue + difference : currentValue - difference);

				slider.SetCurrentValue(RangeBase.ValueProperty, newValue);

				e.Handled = true;
			}
		}

		private static object ConstrainToRange(RangeBase rangeBase, double value)
		{
			var minimum = rangeBase.Minimum;
			if(value < minimum)
			{
				return minimum;
			}

			var maximum = rangeBase.Maximum;
			if(value > maximum)
			{
				return maximum;
			}

			return value;
		}

		public enum MouseWheelState
		{
			/// <summary>
			/// Do not change the value of the slider if the user rotates the mouse wheel.
			/// </summary>
			None,

			/// <summary>
			/// Change the value of the slider only if the control is focused.
			/// </summary>
			ControlFocused,

			/// <summary>
			/// Changes the value of the slider if the mouse pointer is over this element.
			/// </summary>
			MouseHover
		}

		public enum MouseWheelChange
		{
			/// <summary>
			/// Change the value of the slider if the user rotates the mouse wheel by the value defined for <see cref="RangeBase.SmallChange"/>
			/// </summary>
			SmallChange,

			/// <summary>
			/// Change the value of the slider if the user rotates the mouse wheel by the value defined for <see cref="RangeBase.LargeChange"/>
			/// </summary>
			LargeChange
		}
	}
}