using System;
using Volumey.Controls;

namespace Volumey.Helper
{
	public interface IHotkeyManager : IDisposable
	{
		event Action<HotKey> HotkeyPressed;
		int RegisteredHotkeysCount { get; }
		void RegisterHotkey(HotKey key);
		void UnregisterHotkey(HotKey key);
	}
}