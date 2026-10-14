#nullable enable
using System.Text;
using System.Windows.Input;
using Volumey.Controls;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.Model;

public sealed class SystemHotkey
{
	public Key SystemKey { get; set; }

	public HotKey? Replacement { get; set; }

	public SystemHotkey(Key sysKey)
	{
		SystemKey = sysKey;
	}

	public SystemHotkey(Key sysKey, HotKey replacement)
	{
		SystemKey = sysKey;
		Replacement = replacement;
	}

	public void RegisterHotkeyHandler(IHotkeyService hotkeys)
	{
		hotkeys.HotkeyPressed += OnHotkeyPressed;
	}

	public void UnregisterHotkeyHandler(IHotkeyService hotkeys)
	{
		hotkeys.HotkeyPressed -= OnHotkeyPressed;
	}

	private void OnHotkeyPressed(HotKey pressed)
	{
		if(pressed.Equals(Replacement))
		{
			SystemIntegrationHelper.SimulateKeyPress(SystemKey);
		}
	}

	public override string ToString()
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(SystemKey.ToString());
		if(Replacement != null)
		{
			sb.Append($"	{Replacement.ToString()}");
		}
		return sb.ToString();
	}

	public override bool Equals(object? obj)
	{
		if(obj is SystemHotkey other)
			return other.SystemKey == SystemKey;
		return this == obj;
	}

	//equality is defined by the system key only, see Equals
	public override int GetHashCode() => SystemKey.GetHashCode();
}
