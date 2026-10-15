using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.DataProvider;
using Volumey.Localization;

namespace Volumey.Tests;

[TestClass]
public class LocalizationTests
{
	[TestCleanup]
	public void Cleanup() => TranslationSource.SetDefaultLanguage();

	[TestMethod]
	public void LanguageIsSelectedByNativeNameAndSaved()
	{
		TranslationSource.SetLanguage(new CultureInfo("de").NativeName);

		Assert.AreEqual("de", TranslationSource.Instance.CurrentCulture.Name);
		Assert.AreEqual("de", SettingsProvider.Settings.General.Language);
	}

	[TestMethod]
	public void UnknownLanguageFallsBackToEnglish()
	{
		TranslationSource.SetLanguage("Klingon");
		Assert.AreEqual("en", TranslationSource.Instance.CurrentCulture.Name);

		TranslationSource.SetLanguage(new CultureInfo("ko"));
		Assert.AreEqual("en", TranslationSource.Instance.CurrentCulture.Name);
	}

	[TestMethod]
	public void EverySupportedLanguageSurvivesARestart()
	{
		//the saved culture name is used to select the language on the next start
		foreach(string language in TranslationSource.Languages)
		{
			TranslationSource.SetLanguage(language);
			string saved = SettingsProvider.Settings.General.Language;
			TranslationSource.SetDefaultLanguage();

			TranslationSource.SetLanguage(new CultureInfo(saved));

			Assert.AreEqual(saved, TranslationSource.Instance.CurrentCulture.Name, $"language: {language}");
		}
	}

	[TestMethod]
	public void TheLanguageListShowsLanguagesWithoutCountries()
	{
		foreach(string language in TranslationSource.Languages)
		{
			Assert.IsFalse(language.Contains('('), $"{language} names a country");
			//the language picker selects the current language by this name
			TranslationSource.SetLanguage(language);
			Assert.AreEqual(language, TranslationSource.CurrentLanguageName);
		}
	}

	[TestMethod]
	public void SystemLanguageSelectsTheMatchingTranslation()
	{
		//the system language is the parent culture of the UI culture, e.g. "uk" or "zh-Hans"
		TranslationSource.SetLanguage(new CultureInfo("uk"));
		Assert.AreEqual("uk-UA", TranslationSource.Instance.CurrentCulture.Name);

		TranslationSource.SetLanguage(new CultureInfo("zh-Hans"));
		Assert.AreEqual("zh", TranslationSource.Instance.CurrentCulture.Name);
	}

	[TestMethod]
	public void MissingTranslationFallsBackToEnglish()
	{
		TranslationSource.SetDefaultLanguage();
		string english = TranslationSource.Instance[TranslationSource.MutedCaptionKey];
		Assert.IsFalse(string.IsNullOrEmpty(english));

		TranslationSource.SetLanguage(new CultureInfo("ru"));
		Assert.IsFalse(string.IsNullOrEmpty(TranslationSource.Instance[TranslationSource.MutedCaptionKey]));
		Assert.IsNull(TranslationSource.Instance["No_Such_Key"]);
	}

	[TestMethod]
	public void EveryLanguageTranslatesEveryString()
	{
		var manager = Resources.Resources.ResourceManager;
		var english = manager.GetResourceSet(new CultureInfo("en"), true, false).Cast<DictionaryEntry>()
			.ToDictionary(e => (string)e.Key, e => (string)e.Value);
		foreach(string language in TranslationSource.Languages)
		{
			TranslationSource.SetLanguage(language);
			var culture = TranslationSource.Instance.CurrentCulture;
			if(culture.Name == "en")
				continue;
			//only this language's own strings, no fallback
			var set = manager.GetResourceSet(culture, true, false);
			Assert.IsNotNull(set, $"language: {culture.Name}");
			foreach(var (key, text) in english)
			{
				string translated = set.GetString(key);
				Assert.IsFalse(string.IsNullOrWhiteSpace(translated), $"{culture.Name}: {key} isn't translated");
				CollectionAssert.AreEquivalent(Placeholders(text), Placeholders(translated), $"{culture.Name}: {key} has other placeholders");
			}
		}

		static string[] Placeholders(string text) => Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).Distinct().ToArray();
	}

	[TestMethod]
	public void LanguageChangeNotifiesBindings()
	{
		int notifications = 0;
		TranslationSource.Instance.PropertyChanged += Count;
		try
		{
			TranslationSource.SetLanguage(new CultureInfo("fr"));
			TranslationSource.SetLanguage(new CultureInfo("fr"));
		}
		finally
		{
			TranslationSource.Instance.PropertyChanged -= Count;
		}
		Assert.AreEqual(1, notifications);

		void Count(object s, System.ComponentModel.PropertyChangedEventArgs e) => notifications++;
	}
}
