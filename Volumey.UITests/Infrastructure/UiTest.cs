using System.Text.Json.Nodes;

namespace Volumey.UITests.Infrastructure;

/// <summary>
/// Base of the UI test classes: a fresh app per test (its own data folder) and the English error texts the pages show
/// </summary>
public abstract class UiTest
{
	protected const string ErrorFailedToRegister = "Failed to register hotkey";
	protected const string ErrorExists = "Hotkey has already been added";
	//under a hotkey box after a combination someone else holds was typed (blocking mode)
	protected const string TakenInVolumey = "Already used in Volumey";
	protected const string TakenByAnotherApp = "Used by another app";
	protected const string ErrorF12 = "F12 is reserved by the OS";
	protected const string ErrorDiffer = "Set different hotkeys";

	private readonly List<IDisposable> cleanup = new();

	public TestContext TestContext { get; set; } = null!;

	internal VolumeyApp App { get; private set; } = null!;

	internal SettingsWindow Settings { get; private set; } = null!;

	/// <summary>
	/// Starts the app (with <paramref name="settings"/> as its settings.json, if given) and opens its settings window
	/// </summary>
	internal void StartApp(JsonObject? settings = null)
	{
		App = VolumeyApp.Start(settings);
		Settings = App.OpenSettings();
	}

	/// <summary>
	/// Starts the app on a data folder the test prepared (files next to settings.json, e.g. links.json) and opens its settings window
	/// </summary>
	internal void StartAppIn(string dataDir)
	{
		App = VolumeyApp.Start(dataDir: dataDir);
		Settings = App.OpenSettings();
	}

	/// <summary>
	/// Starts the app with only its mixer shown (no settings window)
	/// </summary>
	internal void StartMixer(JsonObject? settings = null, IDictionary<string, string>? environment = null)
		=> App = VolumeyApp.Start(settings, environment: environment);

	/// <summary>Disposed after the test, in reverse order</summary>
	internal T Later<T>(T disposable) where T : IDisposable
	{
		cleanup.Add(disposable);
		return disposable;
	}

	internal void Restart()
	{
		App.Restart();
		Settings = App.OpenSettings();
	}

	[TestCleanup]
	public void StopApp()
	{
		for(int i = cleanup.Count - 1; i >= 0; i--)
			cleanup[i].Dispose();
		bool failed = TestContext.CurrentTestOutcome != UnitTestOutcome.Passed;
		if(failed && App != null)
		{
			App.KeepDataDir = true;
			TestContext.WriteLine("Data folder (settings, log): " + App.DataDir);
		}
		App?.Dispose();
	}

	/// <summary>
	/// Whether <paramref name="condition"/> becomes true within <paramref name="timeoutMs"/> (FlaUI's Retry.WhileTrue/WhileFalse report
	/// success, not the last value, which is easy to misread)
	/// </summary>
	internal static bool Eventually(Func<bool> condition, int timeoutMs = 3000)
		=> FlaUI.Core.Tools.Retry.WhileFalse(condition, TimeSpan.FromMilliseconds(timeoutMs)).Result;

	/// <summary>A hotkey as settings.json stores it</summary>
	internal static bool IsHotkey(JsonNode? node, Combo combo)
		=> node?["Key"]?.GetValue<string>() == combo.JsonKey && node?["ModifierKeys"]?.GetValue<string>() == combo.JsonModifiers;

	internal static JsonObject HotkeyJson(Combo combo) => new() { ["Key"] = combo.JsonKey, ["ModifierKeys"] = combo.JsonModifiers };
}
