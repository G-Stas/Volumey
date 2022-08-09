using System;
using System.Text;
using System.Windows.Input;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.Model
{
	[Serializable]
	public class SystemHotkey
	{
		private Key _systemKey;
		public Key SystemKey
		{
			get => _systemKey;
			set
			{
				_systemKey = value;
			} 
		}

		[NonSerialized]
		public HotKey _replacement;
		public HotKey Replacement
		{
			get
			{
				if(_replacement == null)
				{
					var key = _serializableReplacement.ToHotKey();
					if(key.Key != Key.None || key.ModifierKeys != ModifierKeys.None)
					{
						_replacement = key;
						return _replacement;
					}
				}
				return _replacement;
			}
			set
			{
				_replacement = value;
				_serializableReplacement = _replacement.ToSerializableHotkey();
			} 
		}
		
		private AppSettings.SerializableHotkey _serializableReplacement;

		public SystemHotkey(Key sysKey)
		{
			SystemKey = sysKey;
		}

		public SystemHotkey(Key sysKey, HotKey replacement)
		{
			SystemKey = sysKey;
			Replacement = replacement;
		}

		public void RegisterHotkeyHandler()
		{
			HotkeysControl.HotkeyPressed += OnHotkeyPressed;
		}

		public void UnregisterHotkeyHandler()
		{
			HotkeysControl.HotkeyPressed -= OnHotkeyPressed;
		}

		private void OnHotkeyPressed(HotKey pressed)
		{
			if(pressed.Equals(this.Replacement))
			{
				SystemIntegrationHelper.SimulateKeyPress(this.SystemKey);
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
				return other.SystemKey == this.SystemKey;
			return this == obj;
		}
	}
}