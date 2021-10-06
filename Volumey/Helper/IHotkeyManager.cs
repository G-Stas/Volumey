using System;
using System.Collections.Generic;
using Volumey.Controls;

namespace Volumey.Helper
{
	public interface IHotkeyManager : IDisposable
	{
		event Action<HotKey> HotkeyPressed;
		int RegisteredHotkeysCount { get; }
		IReadOnlyList<HotKey> GetRegisteredHotkeys();
		void RegisterHotkey(HotKey key);
		void UnregisterHotkey(HotKey key);
	}
}