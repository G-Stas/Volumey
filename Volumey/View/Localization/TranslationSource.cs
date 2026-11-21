#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using Volumey.DataProvider;

namespace Volumey.Localization;

public sealed class TranslationSource : INotifyPropertyChanged
{
	public static readonly List<string> Languages = new List<string>
	{
		new CultureInfo("en").NativeName,
		new CultureInfo("pl").NativeName,
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
		//the language alone, like the others ("українська", not "українська (Україна)")
		new CultureInfo("uk").NativeName
	};

	private static readonly Dictionary<string, string> CultureDictionary = new Dictionary<string, string>
	{
		{Languages[0], "en"},
		{Language[1], "pl"}
		{Languages[2], "ru"},
		{Languages[3], "de"},
		{Languages[4], "it"},
		{Languages[5], "fr"},
		{Languages[6], "es"},
		{Languages[7], "pt"},
		{Languages[8], "ja"},
		{Languages[9], "zh"},
		{Languages[10], "hi"},
		{Languages[11], "tr"},
		{Languages[12], "uk-ua"}
	};

	private static readonly TranslationSource instance = new TranslationSource();

	public static TranslationSource Instance => instance;

	private readonly ResourceManager resManager = Resources.Resources.ResourceManager;

	private readonly CultureInfo defaultCulture = CultureInfo.GetCultureInfo("en");
	private CultureInfo? currentCulture;

	public CultureInfo? CurrentCulture
	{
		get => currentCulture;
		private set
		{
			if(!Equals(currentCulture, value))
			{
				currentCulture = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
			}
		}
	}

	/// <summary>The string in the current language, else in English; null for an unknown key</summary>
	public string? this[string key]
	{
		get
		{
			string? resource = resManager.GetString(key, currentCulture);
			//If the resource for the selected culture is not localized we will return English localized resource 
			return string.IsNullOrEmpty(resource) ? resManager.GetString(key, defaultCulture) : resource;
		}
	}

	public const string NoDeviceCaptionKey = "Error_NoDevice";
	public const string MutedCaptionKey = "Status_Muted";

	internal static void SetLanguage(string lang)
	{
		if(CultureDictionary.TryGetValue(lang, out string? culture))
		{
			Instance.CurrentCulture = new CultureInfo(culture);
		}
		else
			SetDefaultLanguage();

		SettingsProvider.Settings.General.Language = Instance.currentCulture?.Name;
	}

	internal static void SetLanguage(CultureInfo ci)
	{
		//the saved name differs in case from the supported one ("uk-UA" / "uk-ua"),
		//and a system language may only match by language ("uk", "zh-Hans")
		string? culture = CultureDictionary.Values.FirstOrDefault(c => string.Equals(c, ci.Name, StringComparison.OrdinalIgnoreCase))
						 ?? CultureDictionary.Values.FirstOrDefault(c => new CultureInfo(c).TwoLetterISOLanguageName == ci.TwoLetterISOLanguageName);
		if(culture != null)
			Instance.CurrentCulture = new CultureInfo(culture);
		else
			SetDefaultLanguage();
	}

	/// <summary>
	/// The current language's name as the language list shows it
	/// </summary>
	internal static string CurrentLanguageName => new CultureInfo((Instance.CurrentCulture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName).NativeName;

	internal static string GetSystemLanguage()
		=> CultureInfo.CurrentUICulture.Parent.Name;

	internal static void SetDefaultLanguage()
			=> Instance.CurrentCulture = new CultureInfo("en");

	public event PropertyChangedEventHandler? PropertyChanged;
}
