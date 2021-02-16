using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Volumey.DataProvider;
using Volumey.Localization;

namespace Volumey.ViewModel.Settings
{
	public class LangSettings : INotifyPropertyChanged
	{
		public static List<string> Languages => TranslationSource.Languages;

		private string selectedLanguage;
		public string SelectedLanguage
		{
			get => selectedLanguage;
			set
			{
				if(this.selectedLanguage != value)
				{
					this.selectedLanguage = value;
					TranslationSource.SetLanguage(value);
					ErrorDictionary.UpdateErrorDictionary();
					OnPropertyChanged();
				}
			}
		}

		internal LangSettings() => LoadLocalization();
		
		private void LoadLocalization()
		{
			try { TranslationSource.SetLanguage(new CultureInfo(SettingsProvider.Settings.AppLanguage)); }
			catch { TranslationSource.SetDefaultLanguage(); }
			finally { this.SelectedLanguage = TranslationSource.Instance.CurrentCulture.NativeName; }
		}
		
		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}