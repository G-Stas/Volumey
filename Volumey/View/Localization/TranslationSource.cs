using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Volumey.DataProvider;

namespace Volumey.Localization
{
	public class TranslationSource : INotifyPropertyChanged
	{
		public static readonly List<string> Languages = new List<string>
		{
			new CultureInfo("en").NativeName,
			new CultureInfo("ru").NativeName,
			new CultureInfo("de").NativeName,
			new CultureInfo("it").NativeName,
			new CultureInfo("fr").NativeName,
			new CultureInfo("es").NativeName,
			new CultureInfo("pt").NativeName,
			new CultureInfo("ja").NativeName,
			new CultureInfo("zh").NativeName,
			new CultureInfo("hi").NativeName,
			new CultureInfo("tr").NativeName,
			new CultureInfo("uk-ua").NativeName
		};

		private static readonly Dictionary<string, string> CultureDictionary = new Dictionary<string, string>
		{
			{Languages[0], "en"},
			{Languages[1], "ru"},
			{Languages[2], "de"},
			{Languages[3], "it"},
			{Languages[4], "fr"},
			{Languages[5], "es"},
            {Languages[6], "pt"},
			{Languages[7], "ja"},
			{Languages[8], "zh"},
			{Languages[9], "hi"},
			{Languages[10], "tr"},
			{Languages[11], "uk-ua"}
		};
		
		private static readonly TranslationSource instance = new TranslationSource();

		public static TranslationSource Instance => instance;

        private readonly ResourceManager resManager = Resources.Resources.ResourceManager;

        private readonly CultureInfo defaultCulture = CultureInfo.GetCultureInfo("en");
		private CultureInfo currentCulture;
		
		public CultureInfo CurrentCulture
		{
			get => this.currentCulture;
			private set
			{
				if (!Equals(this.currentCulture, value))
				{
					this.currentCulture = value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
				}
			}
		}

		public string this[string key]
		{
			get
			{
				string resource = this.resManager.GetString(key, this.currentCulture);
				//If the resource for the selected culture is not localized we will return English localized resource 
				return string.IsNullOrEmpty(resource) ? this.resManager.GetString(key, defaultCulture) : resource;
			}
		}
		
		public const string NoDeviceCaptionKey = "Error_NoDevice";
		public const string MutedCaptionKey = "Status_Muted";

		internal static void SetLanguage(string lang)
		{
            if (CultureDictionary.TryGetValue(lang, out string culture))
            {
                Instance.CurrentCulture = new CultureInfo(culture);
            }
			else 
				SetDefaultLanguage();
            
			if(SettingsProvider.Settings.AppLanguage != Instance.currentCulture.Name)
			{
				SettingsProvider.Settings.AppLanguage = Instance.currentCulture.Name;
				SettingsProvider.SaveSettings().GetAwaiter().GetResult();
			}
		}

		internal static void SetLanguage(CultureInfo ci)
		{
			if(CultureDictionary.ContainsValue(ci.Name)) 
				Instance.CurrentCulture = ci;
			else
				SetDefaultLanguage();
		}

		internal static string GetSystemLanguage()
			=> CultureInfo.CurrentUICulture.Parent.Name;
		
		internal static void SetDefaultLanguage() 
            => Instance.CurrentCulture = new CultureInfo("en");

		public event PropertyChangedEventHandler PropertyChanged;
	}
}