using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Volumey.DataProvider;
using Volumey.Localization;

namespace Volumey.ViewModel.Settings;

public sealed class LangSettings : ObservableObject
{
	public static List<string> Languages => TranslationSource.Languages;

	private string selectedLanguage;
	public string SelectedLanguage
	{
		get => selectedLanguage;
		set
		{
			if(selectedLanguage != value)
			{
				selectedLanguage = value;
				TranslationSource.SetLanguage(value);
				ErrorDictionary.UpdateErrorDictionary();
				OnPropertyChanged();
			}
		}
	}

	internal LangSettings() => LoadLocalization();

	private void LoadLocalization()
	{
		try { TranslationSource.SetLanguage(new CultureInfo(SettingsProvider.Settings.General.Language ?? TranslationSource.GetSystemLanguage())); }
		catch { TranslationSource.SetDefaultLanguage(); }
		finally { SelectedLanguage = TranslationSource.CurrentLanguageName; }
	}
}
