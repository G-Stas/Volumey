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
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Volumey.Controls;

[Serializable]
public class HotKey : IEquatable<HotKey>
{
	private const int VK_MENU = 18;
	private const int VK_CONTROL = 17;
	private const int VK_SHIFT = 16;

	public HotKey(Key key, ModifierKeys modifierKeys = ModifierKeys.None)
	{
		Key = key;
		ModifierKeys = modifierKeys;
	}

	public Key Key { get; }

	public ModifierKeys ModifierKeys { get; }

	public override bool Equals(object obj)
	{
		return obj is HotKey key && Equals(key);
	}

	public override int GetHashCode()
	{
		unchecked
		{
			return ((int)Key * 397) ^ (int)ModifierKeys;
		}
	}

	public bool Equals(HotKey other)
	{
		if(other is null)
			return false;
		return Key == other.Key && ModifierKeys == other.ModifierKeys;
	}

	public override string ToString() => string.Join("+", GetKeyNames());

	/// <summary>
	/// The keys' names, modifiers first, as the keyboard layout names them (the hotkey box shows each as a key cap)
	/// </summary>
	public IReadOnlyList<string> GetKeyNames()
	{
		var names = new List<string>();
		if((ModifierKeys & ModifierKeys.Alt) == ModifierKeys.Alt)
			names.Add(GetLocalizedKeyStringUnsafe(VK_MENU));
		if((ModifierKeys & ModifierKeys.Control) == ModifierKeys.Control)
			names.Add(GetLocalizedKeyStringUnsafe(VK_CONTROL));
		if((ModifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift)
			names.Add(GetLocalizedKeyStringUnsafe(VK_SHIFT));
		if((ModifierKeys & ModifierKeys.Windows) == ModifierKeys.Windows)
			names.Add("Windows");
		names.Add(GetLocalizedKeyString(Key));
		return names;
	}

	private static string GetLocalizedKeyString(Key key)
	{
		if(key >= Key.BrowserBack && key <= Key.LaunchApplication2)
		{
			return key.ToString();
		}

		var vkey = KeyInterop.VirtualKeyFromKey(key);
		return GetLocalizedKeyStringUnsafe(vkey) ?? key.ToString();
	}

	private static string GetLocalizedKeyStringUnsafe(int key)
	{
		// strip any modifier keys
		long keyCode = key & 0xffff;

		long scanCode = PInvoke.MapVirtualKey((uint)keyCode, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC);

		// shift the scancode to the high word
		scanCode = (scanCode << 16);
		if(keyCode == 45 ||
			keyCode == 46 ||
			keyCode == 144 ||
			(33 <= keyCode && keyCode <= 40))
		{
			// add the extended key flag
			scanCode |= 0x1000000;
		}

		Span<char> name = stackalloc char[256];
		int length = PInvoke.GetKeyNameText((int)scanCode, name);
		return length > 0 ? name[..length].ToString() : null;
	}
}
