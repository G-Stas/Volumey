using System;
using System.Collections.Generic;
using System.Linq;
using Volumey.Localization;

namespace Volumey.ViewModel.Settings
{
	public enum ErrorMessageType { VolumeReg, Diff, OpenReg, HotkeyExists, F12, None }

	public static class ErrorDictionary
	{
		private static readonly Dictionary<ErrorMessageType, string> instance =
			new Dictionary<ErrorMessageType, string>();

		internal static IReadOnlyDictionary<ErrorMessageType, string> Instance => instance;
		internal static event Action LanguageChanged;

		internal static void UpdateErrorDictionary()
        {
	        try
	        {
		        instance.Clear();
		        foreach(var error in Enum.GetValues(typeof(ErrorMessageType)).Cast<ErrorMessageType>())
		        {
			        if(error != ErrorMessageType.None)
				        instance.Add(error, TranslationSource.Instance[$"Error_{error.ToString()}"]);
		        }

		        LanguageChanged?.Invoke();
	        }
	        catch { }
        }

	}
}