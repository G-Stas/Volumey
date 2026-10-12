using System.Drawing;
using System.Xml.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Microsoft.Win32;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The General page: language and theme switched live, start with Windows and the Start menu entry (the dev build's own "Volumey-dev"
/// entries, put back as they were), the volume step box, and the hotkey mode note's link to this page
/// </summary>
[TestClass]
public class GeneralPageTests : UiTest
{
	private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string DevEntry = "Volumey-dev";

	private static readonly string StartMenuLink =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", DevEntry + ".lnk");

	private void OpenPage()
	{
		StartApp();
		Settings.Open("NavGeneral", "ThemeBox");
	}

	/// <summary>A translated text, read from the app's resx file of that language</summary>
	private static string Translation(string language, string key)
		=> XDocument.Load(Path.Combine(VolumeyApp.RepoRoot, "Volumey", "Resources", $"Resources.{language}.resx"))
		            .Root!.Elements("data").First(d => (string?)d.Attribute("name") == key).Element("value")!.Value;

	[TestMethod]
	public void TheLanguageChangesTheWindowsTextsRightAway()
	{
		OpenPage();
		string russianTitle = Translation("ru", "Nav_General");

		//the list's order is TranslationSource.Languages: English, then Russian
		Settings.SelectItem("LanguageBox", 1);
		App.WaitForSettings(s => s["General"]?["Language"]?.GetValue<string>() == "ru", "Russian saved");
		Assert.IsTrue(Settings.ShowsText(russianTitle), $"the page didn't switch to Russian ({russianTitle})");

		Settings.SelectItem("LanguageBox", 0);
		App.WaitForSettings(s => s["General"]?["Language"]?.GetValue<string>() == "en", "English saved");
		Assert.IsTrue(Settings.ShowsText("Appearance"), "the page didn't switch back to English");
		Assert.IsFalse(Settings.ShowsText(russianTitle, 500), "a Russian text stayed");
	}

	/// <summary>The settings window's average brightness, 0–255</summary>
	private int Brightness()
	{
		Thread.Sleep(700);
		using var image = Settings.Window.Capture();
		long sum = 0;
		int count = 0;
		for(int x = 0; x < image.Width; x += 7)
			for(int y = 0; y < image.Height; y += 7)
			{
				var c = image.GetPixel(x, y);
				sum += (c.R + c.G + c.B) / 3;
				count++;
			}
		return (int)(sum / count);
	}

	[TestMethod]
	public void TheThemeRepaintsTheWindowRightAway()
	{
		OpenPage();
		Settings.Window.SetForeground();

		Settings.SelectItem("ThemeBox", ThemeIndex("Light"));
		App.WaitForSettings(s => s["General"]?["Theme"]?.GetValue<string>() == "Light", "light theme saved");
		int light = Brightness();
		Settings.SelectItem("ThemeBox", ThemeIndex("Dark"));
		App.WaitForSettings(s => s["General"]?["Theme"]?.GetValue<string>() == "Dark", "dark theme saved");
		int dark = Brightness();

		Assert.IsTrue(light - dark > 60, $"the window didn't repaint (brightness light {light}, dark {dark})");
	}

	private int ThemeIndex(string name)
	{
		var combo = Settings.Find("ThemeBox").AsComboBox();
		combo.Expand();
		int index = Array.FindIndex(combo.Items, i => i.Name == name);
		combo.Collapse();
		if(index < 0)
			throw new InvalidOperationException($"No theme {name}: {string.Join(", ", combo.Items.Select(i => i.Name))}");
		return index;
	}

	/// <summary>Puts the dev build's autostart entry back as it was</summary>
	private sealed class KeepRunEntry : IDisposable
	{
		private readonly object? value;

		public KeepRunEntry()
		{
			using var key = Registry.CurrentUser.OpenSubKey(RunKey);
			value = key?.GetValue(DevEntry);
		}

		public void Dispose()
		{
			using var key = Registry.CurrentUser.CreateSubKey(RunKey);
			if(value == null)
				key.DeleteValue(DevEntry, false);
			else
				key.SetValue(DevEntry, value);
		}
	}

	/// <summary>Puts the dev build's Start menu shortcut back as it was</summary>
	private sealed class KeepStartMenuLink : IDisposable
	{
		private readonly byte[]? content = File.Exists(StartMenuLink) ? File.ReadAllBytes(StartMenuLink) : null;

		public void Dispose()
		{
			if(content == null)
				File.Delete(StartMenuLink);
			else
				File.WriteAllBytes(StartMenuLink, content);
		}
	}

	private static string? RunEntry()
	{
		using var key = Registry.CurrentUser.OpenSubKey(RunKey);
		return key?.GetValue(DevEntry) as string;
	}

	[TestMethod]
	public void StartWithWindowsAddsAndRemovesTheAutostartEntry()
	{
		Later(new KeepRunEntry());
		OpenPage();

		Settings.SetSwitch("StartWithWindowsToggle", true);
		Assert.IsTrue(Eventually(() => RunEntry()?.Contains(VolumeyApp.ExePath, StringComparison.OrdinalIgnoreCase) == true),
			$"no autostart entry for the app: {RunEntry()}");
		Assert.IsTrue(RunEntry()!.Contains("-minimized"), "the autostart entry doesn't start the app minimized");

		Settings.SetSwitch("StartWithWindowsToggle", false);
		Assert.IsTrue(Eventually(() => RunEntry() == null), "the autostart entry is still there");
	}

	[TestMethod]
	public void TheStartMenuSwitchAddsAndRemovesTheShortcut()
	{
		Later(new KeepStartMenuLink());
		OpenPage();

		Settings.SetSwitch("StartMenuToggle", true);
		Assert.IsTrue(Eventually(() => File.Exists(StartMenuLink)), "no Start menu shortcut");

		Settings.SetSwitch("StartMenuToggle", false);
		Assert.IsTrue(Eventually(() => !File.Exists(StartMenuLink)), "the Start menu shortcut is still there");
	}

	private void TypeIntoStepBox(string text)
	{
		Settings.Window.SetForeground();
		Settings.Find("VolumeStepBox").Focus();
		Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
		Keyboard.Type(text);
		Keyboard.Type(VirtualKeyShort.RETURN);
		Wait.UntilInputIsProcessed();
		Settings.LeaveBox();
	}

	[TestMethod]
	public void TheVolumeStepBoxTakesOnlyDigitsAndStaysWithin1To100()
	{
		OpenPage();
		Settings.TypeNumber("VolumeStepBox", 5);
		App.WaitForSettings(s => s["General"]?["VolumeStep"]?.GetValue<int>() == 5, "5 saved");

		TypeIntoStepBox("-x");
		Assert.AreEqual("5", Settings.Number("VolumeStepBox"), "letters or a minus were typed into the box");

		TypeIntoStepBox("150");
		App.WaitForSettings(s => s["General"]?["VolumeStep"]?.GetValue<int>() == 100, "150 clamped to 100");
		Assert.AreEqual("100", Settings.Number("VolumeStepBox"), "the box doesn't show the clamped value");

		TypeIntoStepBox("0");
		App.WaitForSettings(s => s["General"]?["VolumeStep"]?.GetValue<int>() == 1, "0 clamped to 1");
		Assert.AreEqual("1", Settings.Number("VolumeStepBox"), "the box doesn't show the clamped value");
	}

	[TestMethod]
	public void TheHotkeyModeNoteOpensTheGeneralPage()
	{
		StartApp();
		Settings.Open("NavMediaKeys", "MediaKeyList");

		Settings.Find("OpenGeneralLink").AsButton().Invoke();

		Assert.IsNotNull(Settings.TryFind("BlockHotkeysRadio", TimeSpan.FromSeconds(5)), "the General page didn't open");
	}
}
