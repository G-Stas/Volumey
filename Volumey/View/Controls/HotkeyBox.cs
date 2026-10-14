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

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Automation;
using System.Windows.Interop;
using Volumey.Localization;
using Volumey.ViewModel.Settings;

namespace Volumey.Controls;

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
		get => (HotKey)GetValue(HotKeyProperty);
		set => SetValue(HotKeyProperty, value);
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
			HotKeyBox hotKeyBox = (HotKeyBox)sender;
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
		//the template is applied again on a theme switch: the handlers are removed first so they aren't added twice
		PreviewKeyDown -= TextBoxOnPreviewKeyDown2;
		PreviewKeyUp -= OnPreviewKeyUp;
		GotKeyboardFocus -= TextBoxOnGotFocus;
		LostKeyboardFocus -= TextBoxOnLostFocus;
		IsVisibleChanged -= OnIsVisibleChanged;
		Unloaded -= OnUnloaded;
		TextChanged -= TextBoxOnTextChanged;

		base.OnApplyTemplate();

		PreviewKeyDown += TextBoxOnPreviewKeyDown2;
		PreviewKeyUp += OnPreviewKeyUp;
		//keyboard focus, not logical focus: logical focus stays on the box when the window is deactivated or hidden,
		//which would leave every hotkey ignored (and swallowed) until focus moved inside the window again
		GotKeyboardFocus += TextBoxOnGotFocus;
		LostKeyboardFocus += TextBoxOnLostFocus;
		IsVisibleChanged += OnIsVisibleChanged;
		Unloaded += OnUnloaded;
		TextChanged += TextBoxOnTextChanged;

		UpdateText();
	}

	private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if(!IsVisible)
			StopEditing();
	}

	private void OnUnloaded(object sender, RoutedEventArgs e) => StopEditing();

	private void TextBoxOnTextChanged(object sender, TextChangedEventArgs args)
	{
		this!.SelectionStart = Text.Length;
		if(Text.Length == 0)
			HotKey = null;
	}

	/// <summary>
	/// The hotkey service a box tells that it's being edited (hotkeys are ignored meanwhile) and asks who holds a combination.
	/// Inherited: set once on a page or dialog, it reaches every box in it
	/// </summary>
	public static readonly DependencyProperty HotkeyServiceProperty
		= DependencyProperty.RegisterAttached("HotkeyService", typeof(IHotkeyService), typeof(HotKeyBox),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

	public static IHotkeyService GetHotkeyService(DependencyObject element) => (IHotkeyService)element.GetValue(HotkeyServiceProperty);

	public static void SetHotkeyService(DependencyObject element, IHotkeyService value) => element.SetValue(HotkeyServiceProperty, value);

	private bool editing;

	/// <summary>The service told about the editing: a box that left its page (Unloaded) no longer inherits one</summary>
	private IHotkeyService editingService;

	private void TextBoxOnGotFocus(object sender, RoutedEventArgs routedEventArgs)
	{
		if(editing)
			return;
		editing = true;
		pressedKeys.Clear();
		editingService = GetHotkeyService(this);
		if(editingService != null)
			editingService.IsEditingHotkey = true;
		ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcherOnThreadPreprocessMessage;
	}

	private void StopEditing()
	{
		if(!editing)
			return;
		editing = false;
		if(editingService != null)
			editingService.IsEditingHotkey = false;
		editingService = null;
		ComponentDispatcher.ThreadPreprocessMessage -= ComponentDispatcherOnThreadPreprocessMessage;
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
		=> StopEditing();

	private static bool IsModifierOrTab(Key key)
		=> key is Key.Tab or Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl
			   or Key.LeftAlt or Key.RightAlt or Key.RWin or Key.LWin;

	/// <summary>Keys pressed while the box has focus: a released key that isn't here was never delivered as a key press</summary>
	private readonly HashSet<Key> pressedKeys = new HashSet<Key>();

	private void TextBoxOnPreviewKeyDown2(object sender, KeyEventArgs e)
	{
		var key = e.Key == Key.System ? e.SystemKey : e.Key;
		if(IsModifierOrTab(key))
			return;

		e.Handled = true;
		pressedKeys.Add(key);
		SetTaken(null, null);

		var currentModifierKeys = GetCurrentModifierKeys();
		if(currentModifierKeys == ModifierKeys.None && key == Key.Back)
			SetCurrentValue(HotKeyProperty, null);
		else
			SetCurrentValue(HotKeyProperty, new HotKey(key, currentModifierKeys));

		UpdateText();
	}

	/// <summary>
	/// A combination registered with RegisterHotKey (by Volumey's blocking mode or another app) never reaches the box as a key press:
	/// Windows sends it to its owner as a hotkey, and only the key's release arrives here. Then the box tells who has it
	/// (<see cref="TakenMessage"/>), or takes it in shared mode, where Volumey's hook still sees a combination another app holds.
	/// </summary>
	private void OnPreviewKeyUp(object sender, KeyEventArgs e)
	{
		var key = e.Key == Key.System ? e.SystemKey : e.Key;
		if(IsModifierOrTab(key) || pressedKeys.Remove(key))
			return;
		var combination = new HotKey(key, GetCurrentModifierKeys());
		//the box's own hotkey (typed again), or a key held down before the box got focus that nobody holds
		if(combination.Equals(HotKey))
			return;
		switch(GetHotkeyService(this)?.FindOwner(combination))
		{
			case HotkeyOwner.Volumey:
				SetTaken(combination, "Hotkeys_TakenInVolumey");
				break;
			case HotkeyOwner.AnotherApp when DataProvider.SettingsProvider.Settings.Hotkeys.BlockInSystem:
				SetTaken(combination, "Hotkeys_TakenByAnotherApp");
				break;
			case HotkeyOwner.AnotherApp:
				SetCurrentValue(HotKeyProperty, combination);
				UpdateText();
				break;
			default:
				return;
		}
		e.Handled = true;
	}

	private static readonly DependencyPropertyKey TakenMessagePropertyKey
		= DependencyProperty.RegisterReadOnly(nameof(TakenMessage), typeof(string), typeof(HotKeyBox), new PropertyMetadata(null));

	public static readonly DependencyProperty TakenMessageProperty = TakenMessagePropertyKey.DependencyProperty;

	/// <summary>
	/// "Ctrl+K: Used by another app" after a combination someone else holds was typed (the template shows it under the box);
	/// null otherwise. Cleared by the next key press.
	/// </summary>
	public string TakenMessage => (string)GetValue(TakenMessageProperty);

	private HotKey takenHotkey;
	private string takenKey;

	private void SetTaken(HotKey hotkey, string messageKey)
	{
		if(takenKey == null && messageKey == null)
			return;
		takenHotkey = hotkey;
		takenKey = messageKey;
		UpdateTakenMessage();
	}

	private void UpdateTakenMessage()
	{
		string message = takenKey == null
							 ? null
							 : string.Format(TranslationSource.Instance["Error_RejectedHotkey"], takenHotkey, TranslationSource.Instance[takenKey]);
		SetValue(TakenMessagePropertyKey, message);
		//a text box doesn't expose its template's text to UI Automation, so screen readers (and the UI tests) get it as the box's help text
		if(message == null)
			ClearValue(AutomationProperties.HelpTextProperty);
		else
			AutomationProperties.SetHelpText(this, message);
	}

	//the message follows a language switch, like the rest of the settings; only while loaded, so the app-wide
	//language source doesn't keep boxes of closed pages alive
	private void OnLanguageChanged(object sender, PropertyChangedEventArgs e) => UpdateTakenMessage();

	public HotKeyBox()
	{
		Loaded += (s, e) => TranslationSource.Instance.PropertyChanged += OnLanguageChanged;
		Unloaded += (s, e) => TranslationSource.Instance.PropertyChanged -= OnLanguageChanged;
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

	private static readonly DependencyPropertyKey KeyNamesPropertyKey
		= DependencyProperty.RegisterReadOnly(nameof(KeyNames), typeof(IReadOnlyList<string>), typeof(HotKeyBox),
											  new PropertyMetadata(Array.Empty<string>()));

	public static readonly DependencyProperty KeyNamesProperty = KeyNamesPropertyKey.DependencyProperty;

	/// <summary>
	/// The hotkey's keys, which the template shows as key caps. The text stays the whole combination ("Ctrl+Shift+Up"),
	/// so screen readers and UI Automation still read it.
	/// </summary>
	public IReadOnlyList<string> KeyNames => (IReadOnlyList<string>)GetValue(KeyNamesProperty);

	public static readonly DependencyProperty PlaceholderTextProperty
		= DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(HotKeyBox), new PropertyMetadata(null));

	/// <summary>
	/// Shown in the empty box (the settings style sets it for every box)
	/// </summary>
	public string PlaceholderText
	{
		get => (string)GetValue(PlaceholderTextProperty);
		set => SetValue(PlaceholderTextProperty, value);
	}

	private void UpdateText()
	{
		var hotkey = HotKey;
		bool none = hotkey is null || hotkey.Key == Key.None;
		Text = none ? string.Empty : hotkey.ToString();
		SetValue(KeyNamesPropertyKey, none ? Array.Empty<string>() : hotkey.GetKeyNames());
	}
}
