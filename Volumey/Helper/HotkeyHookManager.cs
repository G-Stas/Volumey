using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Volumey.Controls;

namespace Volumey.Helper
{
	/// <summary>
	/// Provides global hotkeys functionality by installing a low-level hook to the user keyboard.
	/// Doesn't block registered hotkeys combinations input.
	/// </summary>
	public class HotkeyHookManager : IHotkeyManager
	{
		public event Action<HotKey> HotkeyPressed;

		/// <summary>
		/// Currently pressed modifier keys sequence
		/// </summary>
		private ModifierKeys modifiersSequence = ModifierKeys.None;

		/// <summary>
		/// Currently pressed keys (virtual key codes) sequence
		/// </summary>
		private readonly List<int> keysSequence = new List<int>();

		private readonly HashSet<HotKey> registeredHotkeys = new HashSet<HotKey>();
		public int RegisteredHotkeysCount => this.registeredHotkeys.Count;

		/// <summary>
		/// Handle to the hook procedure.
		/// </summary>
		private readonly IntPtr hookHandle;

		private readonly WinHookCallback hookCallback;

		public delegate IntPtr WinHookCallback(int msg, IntPtr wParam, ref int lParam);

		private const int WH_KEYBOARD_LL = 13;
		private const int WM_KEYDOWN = 0x0100;
		private const int WM_KEYUP = 0x0101;
		private const int WM_SYSKEYDOWN = 0x0104;
		private const int WM_SYSKEYUP = 0x0105;

		private const int vkShift = 0xA0;
		private const int vkCtrl = 0xA2;
		private const int vkAlt = 0xA4;
		private const int vkRightShift = 0xA1;
		private const int vkRightCtrl = 0xA3;
		private const int vkRightAlt = 0xA5;

		internal HotkeyHookManager()
		{
			this.hookCallback = LowLevelKeyboardProc;
			this.hookHandle = NativeMethods.SetWindowsHookExA(WH_KEYBOARD_LL, this.hookCallback, IntPtr.Zero, 0);
		}

		public IReadOnlyList<HotKey> GetRegisteredHotkeys() => this.registeredHotkeys.ToArray();

		public void RegisterHotkey(HotKey hotkey)
		{
			if(this.HotkeyIsRegistered(hotkey))
				return;
			this.registeredHotkeys.Add(hotkey);
		}

		public void UnregisterHotkey(HotKey hotkey)
		{
			this.registeredHotkeys.Remove(hotkey);
		}

		/// <summary>
		/// Adds a key to the currently pressed keys sequence
		/// </summary>
		/// <param name="vk">Virtual key code</param>
		private void AddKeyCode(int vk)
		{
			switch(vk)
			{
				case vkShift:
				case vkRightShift:
				{
					this.modifiersSequence |= ModifierKeys.Shift;
					break;
				}
				case vkCtrl:
				case vkRightCtrl:
				{
					this.modifiersSequence |= ModifierKeys.Control;
					break;
				}
				case vkAlt:
				case vkRightAlt:
				{
					this.modifiersSequence |= ModifierKeys.Alt;
					break;
				}
				default:
				{
					if(!this.keysSequence.Contains(vk))
						this.keysSequence.Add(vk);
					break;
				}
			}

			this.CheckCurrentSequence();
		}

		/// <summary>
		/// Removes a key from the currently pressed key sequence
		/// </summary>
		/// <param name="vk">Virtual key code</param>
		private void RemoveKeyCode(int vk)
		{
			switch(vk)
			{
				case vkShift:
				case vkRightShift:
				{
					this.modifiersSequence &= ~ModifierKeys.Shift;
					break;
				}
				case vkCtrl:
				case vkRightCtrl:
				{
					this.modifiersSequence &= ~ModifierKeys.Control;
					break;
				}
				case vkAlt:
				case vkRightAlt:
				{
					this.modifiersSequence &= ~ModifierKeys.Alt;
					break;
				}
				default:
				{
					this.keysSequence.Remove(vk);
					break;
				}
			}

			this.CheckCurrentSequence();
		}

		/// <summary>
		/// Invokes HotkeyPressed event if current keys sequence is equal to any of the registered hotkeys
		/// </summary>
		private void CheckCurrentSequence()
		{
			if(this.registeredHotkeys.Count == 0)
				return;
			//Combinations with more than one key pressed aren't supported (e.g. CTRL + A + Z)
			if(this.keysSequence.Count != 1)
				return;
			var hotkey = new HotKey(KeyInterop.KeyFromVirtualKey(this.keysSequence[0]), this.modifiersSequence);
			if(HotkeyIsRegistered(hotkey))
				this.HotkeyPressed?.Invoke(hotkey);
		}

		private bool HotkeyIsRegistered(HotKey hotkey)
			=> this.registeredHotkeys.Contains(hotkey);

		/// <summary>
		/// An application-defined or library-defined callback function used with the SetWindowsHookEx function.
		/// The system calls this function every time a new keyboard input event is about to be posted into a thread input queue.
		/// </summary>
		/// <param name="nCode">A code the hook procedure uses to determine how to process the message.</param>
		/// <param name="wParam">The identifier of the keyboard message.</param>
		/// <param name="virtualKeyCode">Pressed key virtual code.</param>
		/// <returns></returns>
		private IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, ref int virtualKeyCode)
		{
			//If nCode is less than zero, the hook procedure must pass the message to the CallNextHookEx function without further processing and should return the value returned by CallNextHookEx.
			if(nCode < 0)
			{
				var ptr = new IntPtr(virtualKeyCode);
				return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, ref ptr);
			}

			int vkCode = virtualKeyCode;
			switch(wParam.ToInt32())
			{
				case WM_KEYDOWN:
				case WM_SYSKEYDOWN:
				{
					AddKeyCode(vkCode);
					break;
				}
				case WM_KEYUP:
				case WM_SYSKEYUP:
				{
					RemoveKeyCode(vkCode);
					break;
				}
			}

			return IntPtr.Zero;
		}

		public void Dispose()
		{
			NativeMethods.UnhookWindowsHookEx(this.hookHandle);
			this.registeredHotkeys.Clear();
			this.keysSequence.Clear();
			this.HotkeyPressed = null;
		}
	}
}