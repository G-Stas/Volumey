using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace Volumey.UITests.Infrastructure;

/// <summary>
/// The Debug build of Volumey, started like scripts/run-dev.ps1 does: settings and log in a data folder of its own, so the machine's
/// settings are never touched. One instance at a time (the Debug build's single-instance mutex): a running dev build makes the tests fail
/// with a message instead of being closed.
/// </summary>
internal sealed class VolumeyApp : IDisposable
{
	private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

	public static string RepoRoot { get; } = FindRepoRoot();

	/// <summary>The app under test: VOLUMEY_UITEST_EXE, or the Debug build in the repository</summary>
	public static string ExePath { get; } = Environment.GetEnvironmentVariable("VOLUMEY_UITEST_EXE") is { Length: > 0 } exe
		? exe
		: Path.Combine(RepoRoot, "Volumey", "bin", "x64", "Debug", "net10.0-windows10.0.19041.0", "win-x64", "Volumey.exe");

	public string DataDir { get; }
	public string SettingsFile => Path.Combine(DataDir, "settings.json");
	public string LogFile => Path.Combine(DataDir, "volumey.log");

	public UIA3Automation Automation { get; } = new();

	private Process? process;
	private Application? app;
	private readonly bool ownsDataDir;

	/// <summary>Keep the data folder after Dispose (a failed test: its log and settings explain it)</summary>
	public bool KeepDataDir { get; set; }

	private VolumeyApp(string? dataDir)
	{
		ownsDataDir = dataDir == null;
		DataDir = dataDir ?? Path.Combine(Path.GetTempPath(), "Volumey-uitests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(DataDir);
	}

	/// <param name="settings">settings.json to start with (missing values keep their defaults); null: a first start</param>
	public static VolumeyApp Start(JsonObject? settings = null, string? dataDir = null)
	{
		var volumey = new VolumeyApp(dataDir);
		if(settings != null)
			File.WriteAllText(volumey.SettingsFile, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
		volumey.Launch();
		return volumey;
	}

	private void Launch()
	{
		if(!File.Exists(ExePath))
			throw new InvalidOperationException($"Build the app first (Debug, x64): {ExePath} doesn't exist");
		var running = Process.GetProcessesByName("Volumey").Where(IsUnderTest).ToList();
		if(running.Count > 0)
			throw new InvalidOperationException($"A dev build of Volumey is running (pid {string.Join(", ", running.Select(p => p.Id))}). " +
			                                    "Close it first: it shares the single-instance lock and the hotkeys with the app under test.");

		var start = new ProcessStartInfo(ExePath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(ExePath)! };
		start.Environment["VOLUMEY_SETTINGS_DIR"] = DataDir;
		start.Environment["VOLUMEY_LOG_FILE"] = LogFile;
		process = Process.Start(start) ?? throw new InvalidOperationException("Volumey didn't start");
		app = Application.Attach(process);

		//the window starts hidden; a second start's message shows it (the hotkeys are registered by then, MainView's constructor)
		var mixer = Retry.WhileNull(() =>
		{
			if(process.HasExited)
				throw new InvalidOperationException($"Volumey exited with code {process.ExitCode}; see {LogFile}");
			ShowMixer();
			return FindWindow("Volumey");
		}, StartTimeout, TimeSpan.FromMilliseconds(500)).Result;
		if(mixer == null)
			throw new TimeoutException($"Volumey's window didn't show within {StartTimeout.TotalSeconds} s; see {LogFile}");
	}

	private static bool IsUnderTest(Process p)
	{
		try
		{
			return string.Equals(p.MainModule?.FileName, ExePath, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Shows the mixer the way a second start does (the Debug build's message has the "-dev" suffix, Startup.InstanceSuffix)
	/// </summary>
	public void ShowMixer() => PostMessage((IntPtr)0xffff, RegisterWindowMessage("WM_SHOWME-dev"), IntPtr.Zero, IntPtr.Zero);

	/// <summary>A visible top-level window of the app with this title, or null</summary>
	public Window? FindWindow(string title)
		=> app!.GetAllTopLevelWindows(Automation).FirstOrDefault(w => w.Title == title && !w.IsOffscreen);

	public Window Mixer => FindWindow("Volumey") ?? throw new InvalidOperationException("The mixer isn't visible");

	/// <summary>
	/// Whether the mixer window is shown (Win32 IsWindowVisible: UI Automation's IsOffscreen also counts a covered window)
	/// </summary>
	public bool MixerIsVisible => IsTopLevelWindowVisible("Volumey");

	private bool IsTopLevelWindowVisible(string title)
	{
		bool visible = false;
		uint pid = (uint)process!.Id;
		EnumWindows((hwnd, _) =>
		{
			GetWindowThreadProcessId(hwnd, out uint owner);
			if(owner == pid && IsWindowVisible(hwnd))
			{
				var text = new System.Text.StringBuilder(64);
				GetWindowText(hwnd, text, text.Capacity);
				if(text.ToString() == title)
					visible = true;
			}
			return true;
		}, IntPtr.Zero);
		return visible;
	}

	/// <summary>
	/// Opens the settings window with the mixer's settings button
	/// </summary>
	public SettingsWindow OpenSettings()
	{
		var existing = FindWindow("Volumey settings");
		if(existing == null)
		{
			if(!MixerIsVisible)
				ShowMixer();
			var button = Retry.WhileNull(() => FindWindow("Volumey")?.FindFirstDescendant(cf => cf.ByAutomationId("SettingsButton")),
				TimeSpan.FromSeconds(10)).Result ?? throw new InvalidOperationException("No settings button in the mixer");
			button.AsButton().Invoke();
			existing = Retry.WhileNull(() => FindWindow("Volumey settings"), TimeSpan.FromSeconds(10)).Result
			           ?? throw new TimeoutException("The settings window didn't open");
		}
		return new SettingsWindow(this, existing);
	}

	/// <summary>
	/// The saved settings; settings.json is written 300 ms after a change (VolumeySettings.SaveDelay)
	/// </summary>
	public JsonNode ReadSettings()
	{
		for(int attempt = 0; ; attempt++)
		{
			try
			{
				using var stream = new FileStream(SettingsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
				return JsonNode.Parse(stream) ?? throw new InvalidDataException("settings.json is empty");
			}
			catch(Exception e) when(attempt < 20 && e is IOException or JsonException)
			{
				Thread.Sleep(100);
			}
		}
	}

	/// <summary>
	/// Waits until the saved settings satisfy <paramref name="condition"/>; fails with the file's content otherwise
	/// </summary>
	public void WaitForSettings(Func<JsonNode, bool> condition, string what, int timeoutMs = 5000)
	{
		var watch = Stopwatch.StartNew();
		JsonNode? last = null;
		while(watch.ElapsedMilliseconds < timeoutMs)
		{
			if(File.Exists(SettingsFile))
			{
				last = ReadSettings();
				if(condition(last))
					return;
			}
			Thread.Sleep(150);
		}
		Assert.Fail($"settings.json: {what} didn't happen within {timeoutMs} ms. Content:\n{last}");
	}

	/// <summary>
	/// Stops the app and starts it again on the same data folder (saved settings and hotkeys come back)
	/// </summary>
	public void Restart()
	{
		Stop();
		Launch();
	}

	private void Stop()
	{
		if(process is { HasExited: false })
		{
			//give a pending settings write its 300 ms; the app has no exit message, and the tray icon disappears when it's hovered
			Thread.Sleep(600);
			process.Kill();
			process.WaitForExit(10000);
		}
		app?.Dispose();
		app = null;
		process = null;
	}

	public void Dispose()
	{
		Stop();
		Automation.Dispose();
		if(ownsDataDir && !KeepDataDir)
		{
			try
			{
				Directory.Delete(DataDir, true);
			}
			catch
			{
				//the log can still be locked for a moment; the folder is in %TEMP%
			}
		}
	}

	private static string FindRepoRoot()
	{
		for(var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
			if(File.Exists(Path.Combine(dir.FullName, "Volumey.sln")))
				return dir.FullName;
		throw new InvalidOperationException("Volumey.sln not found above " + AppContext.BaseDirectory);
	}

	private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr hwnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int maxCount);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern uint RegisterWindowMessage(string lpString);

	[DllImport("user32.dll")]
	private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
