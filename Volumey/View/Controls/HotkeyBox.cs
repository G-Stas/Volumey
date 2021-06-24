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
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls;

namespace Volumey.Controls
{
	public class HotKeyBox : TextBox
	{
		private static int WM_Hotkey = 786;

		/// <summary>Identifies the <see cref="HotKey"/> dependency property.</summary>
		public static readonly DependencyProperty HotKeyProperty
			= DependencyProperty.Register(nameof(HotKey),
			                              typeof(HotKey),
			                              typeof(HotKeyBox),
			                              new FrameworkPropertyMetadata(default(HotKey),
			                                                            FrameworkPropertyMetadataOptions
				                                                           .BindsTwoWayByDefault,
			                                                            OnHotKeyPropertyChanged));

		private static void OnHotKeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as HotKeyBox)?.UpdateText();
		}

		/// <summary>
		/// Gets or sets the <see cref="HotKey"/> for this <see cref="HotKeyBox"/>.
		/// </summary>
		public HotKey HotKey
		{
			get => (HotKey) this.GetValue(HotKeyProperty);
			set => this.SetValue(HotKeyProperty, value);
		}

		static HotKeyBox()
		{
			EventManager.RegisterClassHandler(typeof(HotKeyBox), GotFocusEvent, new RoutedEventHandler(OnGotFocus));
		}

		private static void OnGotFocus(object sender, RoutedEventArgs e)
		{
			// If we're an editable HotKeyBox, forward focus to the TextBox or previous element
			if(!e.Handled)
			{
				HotKeyBox hotKeyBox = (HotKeyBox) sender;
				if(hotKeyBox.Focusable && e.OriginalSource == hotKeyBox)
				{
					// MoveFocus takes a TraversalRequest as its argument.
					var request = new TraversalRequest((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
						                                   ? FocusNavigationDirection.Previous
						                                   : FocusNavigationDirection.Next);
					// Gets the element with keyboard focus.
					// Change keyboard focus.
					if(Keyboard.FocusedElement == hotKeyBox)
						hotKeyBox.Focus();
					else
						(Keyboard.FocusedElement as UIElement)?.MoveFocus(request);
					e.Handled = true;
				}
			}
		}

		public override void OnApplyTemplate()
		{
			this.PreviewKeyDown -= this.TextBoxOnPreviewKeyDown2;
			this.GotFocus -= this.TextBoxOnGotFocus;
			this.LostFocus -= this.TextBoxOnLostFocus;
			this.TextChanged -= this.TextBoxOnTextChanged;

			base.OnApplyTemplate();

			this.PreviewKeyDown += this.TextBoxOnPreviewKeyDown2;
			this.GotFocus += this.TextBoxOnGotFocus;
			this.LostFocus += this.TextBoxOnLostFocus;
			this.TextChanged += this.TextBoxOnTextChanged;

			this.UpdateText();
		}

		private void TextBoxOnTextChanged(object sender, TextChangedEventArgs args)
		{
			this!.SelectionStart = this.Text.Length;
			if(this.Text.Length == 0)
				this.HotKey = null;
		}

		private void TextBoxOnGotFocus(object sender, RoutedEventArgs routedEventArgs)
		{
			ComponentDispatcher.ThreadPreprocessMessage += this.ComponentDispatcherOnThreadPreprocessMessage;
		}

		private void ComponentDispatcherOnThreadPreprocessMessage(ref MSG msg, ref bool handled)
		{
			if(msg.message == WM_Hotkey)
			{
				// swallow all hotkeys, so our control can catch the key strokes
				handled = true;
			}
		}

		private void TextBoxOnLostFocus(object sender, RoutedEventArgs routedEventArgs)
		{
			ComponentDispatcher.ThreadPreprocessMessage -= this.ComponentDispatcherOnThreadPreprocessMessage;
		}

		private void TextBoxOnPreviewKeyDown2(object sender, KeyEventArgs e)
		{
			var key = e.Key == Key.System ? e.SystemKey : e.Key;
			switch(key)
			{
				case Key.Tab:
				case Key.LeftShift:
				case Key.RightShift:
				case Key.LeftCtrl:
				case Key.RightCtrl:
				case Key.LeftAlt:
				case Key.RightAlt:
				case Key.RWin:
				case Key.LWin:
					return;
			}

			e.Handled = true;

			var currentModifierKeys = GetCurrentModifierKeys();
			if(currentModifierKeys == ModifierKeys.None && key == Key.Back)
				this.SetCurrentValue(HotKeyProperty, null);
			else
				this.SetCurrentValue(HotKeyProperty, new HotKey(key, currentModifierKeys));

			this.UpdateText();
		}

		private static ModifierKeys GetCurrentModifierKeys()
		{
			var modifier = ModifierKeys.None;
			if(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
			{
				modifier |= ModifierKeys.Control;
			}

			if(Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
			{
				modifier |= ModifierKeys.Alt;
			}

			if(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
			{
				modifier |= ModifierKeys.Shift;
			}

			if(Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
			{
				modifier |= ModifierKeys.Windows;
			}

			return modifier;
		}

		private void UpdateText()
		{
			var hotkey = this.HotKey;
			this.Text = hotkey is null || hotkey.Key == Key.None ? string.Empty : hotkey.ToString();
		}
	}
}