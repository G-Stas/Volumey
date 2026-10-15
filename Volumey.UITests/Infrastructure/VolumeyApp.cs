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

	/// <summary>The app under test: VOLUMEY_UITEST_EXE, or the Debug build in the repository</summary>
	public static string ExePath { get; } = Environment.GetEnvironmentVariable("VOLUMEY_UITEST_EXE") is { Length: > 0 } exe
		? exe
		: Path.Combine(FindRepoRoot(), "Volumey", "bin", "x64", "Debug", "net10.0-windows10.0.19041.0", "win-x64", "Volumey.exe");

	public string DataDir { get; }
	public string SettingsFile => Path.Combine(DataDir, "settings.json");
	public string LogFile => Path.Combine(DataDir, "volumey.log");

	/// <summary>How the Debug build logs a failed WPF binding (BindingErrorListener in the app)</summary>
	private const string BindingErrorPrefix = "Binding error:";

	/// <summary>The binding errors the app logged so far (every start on this data folder)</summary>
	public IReadOnlyList<string> BindingErrors()
	{
		if(!File.Exists(LogFile))
			return [];
		//the running app keeps the log open
		using var stream = new FileStream(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using var reader = new StreamReader(stream);
		var errors = new List<string>();
		while(reader.ReadLine() is { } line)
			if(line.Contains(BindingErrorPrefix, StringComparison.Ordinal))
				errors.Add(line);
		return errors;
	}

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

	/// <summary>Extra environment variables for the app (kept for restarts), e.g. the Debug builds VOLUMEY_SHOW_UPDATE_DIALOG</summary>
	private IDictionary<string, string> environment = new Dictionary<string, string>();

	/// <param name="settings">settings.json to start with (missing values keep their defaults); null: a first start</param>
	/// <param name="environment">extra environment variables for the app</param>
	public static VolumeyApp Start(JsonObject? settings = null, string? dataDir = null, IDictionary<string, string>? environment = null)
	{
		var volumey = new VolumeyApp(dataDir);
		if(environment != null)
			volumey.environment = environment;
		//the tests find controls by their English names, and a first start takes the Windows display language
		if(dataDir == null)
		{
			settings ??= new JsonObject();
			var general = settings["General"] as JsonObject ?? new JsonObject();
			general["Language"] ??= "en";
			settings["General"] = general;
		}
		if(settings != null)
			File.WriteAllText(volumey.SettingsFile, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
		try
		{
			volumey.Launch();
		}
		catch
		{
			//the test never gets the instance: stop the app here, or it blocks every later start, and keep its log
			volumey.KeepDataDir = true;
			volumey.Dispose();
			throw;
		}
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
		foreach(var (name, value) in environment)
			start.Environment[name] = value;
		process = Process.Start(start) ?? throw new InvalidOperationException("Volumey didn't start");
		app = Attach(process);

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

	/// <summary>
	/// FlaUI's Attach reads the process's main module, which fails with a partial ReadProcessMemory while a just started process
	/// is still loading (seen on a slow VM): tried again until it works
	/// </summary>
	private static Application Attach(Process process)
	{
		var stopwatch = Stopwatch.StartNew();
		while(true)
		{
			try
			{
				return Application.Attach(process);
			}
			catch(System.ComponentModel.Win32Exception) when(stopwatch.Elapsed < TimeSpan.FromSeconds(10) && !process.HasExited)
			{
				Thread.Sleep(100);
			}
		}
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
	/// Hides the mixer (closing it only hides it, like its close button)
	/// </summary>
	public void HideMixer()
	{
		Mixer.Patterns.Window.Pattern.Close();
		Retry.WhileTrue(() => MixerIsVisible, TimeSpan.FromSeconds(5));
	}

	/// <summary>
	/// The OSD's pill for an app or device (named after it), or null while none is shown. The OSD window ("Volumey OSD") is created on
	/// first use and covers the screen's work area; only its pills are visible.
	/// </summary>
	public AutomationElement? FindOsdPill(string name)
		=> app!.GetAllTopLevelWindows(Automation).FirstOrDefault(w => w.Title == "Volumey OSD")?
			.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Custom).And(cf.ByName(name)));

	/// <summary>
	/// The OSD window: transparent, it covers the work area of the screen the OSD is on (null before the first OSD)
	/// </summary>
	public Window? OsdWindow => app!.GetAllTopLevelWindows(Automation).FirstOrDefault(w => w.Title == "Volumey OSD");

	/// <summary>
	/// Whether the mixer window is shown (Win32 IsWindowVisible: UI Automation's IsOffscreen also counts a covered window)
	/// </summary>
	public bool MixerIsVisible => IsTopLevelWindowVisible("Volumey");

	/// <summary>
	/// The titles of the app's visible top-level windows, owned ones too (UI Automation lists an owned dialog under its owner)
	/// </summary>
	public List<(IntPtr Handle, string Title, string Class)> VisibleWindows()
	{
		var windows = new List<(IntPtr, string, string)>();
		uint pid = (uint)process!.Id;
		EnumWindows((hwnd, _) =>
		{
			GetWindowThreadProcessId(hwnd, out uint owner);
			if(owner == pid && IsWindowVisible(hwnd))
			{
				var text = new System.Text.StringBuilder(256);
				GetWindowText(hwnd, text, text.Capacity);
				var cls = new System.Text.StringBuilder(256);
				GetClassName(hwnd, cls, cls.Capacity);
				windows.Add((hwnd, text.ToString(), cls.ToString()));
			}
			return true;
		}, IntPtr.Zero);
		return windows;
	}

	/// <summary>Closes a window like its X button (WM_CLOSE)</summary>
	public static void CloseWindow(IntPtr hwnd) => PostMessage(hwnd, 0x10, IntPtr.Zero, IntPtr.Zero);

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
	/// Starts the exe once more, like a person double-clicking it while it runs: the second process tells the first to show
	/// itself and exits. Returns whether it exited.
	/// </summary>
	public bool StartSecondInstance()
	{
		var start = new ProcessStartInfo(ExePath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(ExePath)! };
		start.Environment["VOLUMEY_SETTINGS_DIR"] = DataDir;
		using var second = Process.Start(start) ?? throw new InvalidOperationException("The second instance didn't start");
		return second.WaitForExit(10000);
	}

	/// <summary>
	/// Volumey's tray icon: a taskbar button named after its tooltip ("&lt;default device&gt;: 92%"). Looked up in the hidden icons
	/// flyout too, where Windows may put a new icon.
	/// Windows 11 marks tray icons with the automation ID NotifyItemIcon; Windows 10 shows them as buttons of a toolbar
	/// (ToolbarWindow32), its hidden icons button has the ID 1502 and its flyout is the NotifyIconOverflowWindow.
	/// </summary>
	public AutomationElement TrayIcon()
	{
		//without an output device (a CI runner) the tooltip says so instead
		string prefix = Audio.HasOutputDevice ? Audio.DefaultDeviceName + ":" : "No output device detected";
		AutomationElement? Find(AutomationElement? root)
			=> root?.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
			        .FirstOrDefault(e => e.Name.StartsWith(prefix) && (e.AutomationId == "NotifyItemIcon" || e.Parent?.ClassName == "ToolbarWindow32"));
		var desktop = Automation.GetDesktop();
		var taskbar = desktop.FindFirstChild(cf => cf.ByClassName("Shell_TrayWnd"));
		var icon = Retry.WhileNull(() => Find(taskbar), TimeSpan.FromSeconds(5)).Result;
		if(icon is { IsOffscreen: false })
			return icon;
		//a new icon may be among the hidden ones (always on a fresh PC like a CI runner): click it in their flyout
		AutomationElement? InFlyout()
		{
			var found = Find(desktop.FindFirstChild(cf => cf.ByClassName("NotifyIconOverflowWindow")))
			            ?? desktop.FindAllDescendants(cf => cf.ByAutomationId("NotifyItemIcon")).FirstOrDefault(e => e.Name.StartsWith(prefix));
			return found is { IsOffscreen: false } ? found : null;
		}
		//the hidden icons button toggles the flyout: clicked only while the flyout is closed
		if(InFlyout() is { } shown)
			return shown;
		HiddenIconsButton(taskbar)?.Click();
		return Retry.WhileNull(InFlyout, TimeSpan.FromSeconds(5)).Result
		       ?? throw new InvalidOperationException($"No visible tray icon whose tooltip starts with \"{prefix}\"");
	}

	private static AutomationElement? HiddenIconsButton(AutomationElement? taskbar)
		=> taskbar?.FindFirstDescendant(cf => cf.ByName("Show Hidden Icons")) ?? taskbar?.FindFirstDescendant(cf => cf.ByAutomationId("1502"));

	/// <summary>
	/// Closes Windows 10's hidden icons flyout if it's open. It stays open after a tray menu item is invoked through UI Automation
	/// (nothing takes the focus from it), also once the app's icon is gone, and a click on the hidden icons button would then close
	/// it instead of opening it.
	/// </summary>
	public static void CloseTrayOverflow()
	{
		var flyout = FindTopLevelWindow("NotifyIconOverflowWindow", null);
		if(flyout == IntPtr.Zero || !IsWindowVisible(flyout))
			return;
		using var automation = new UIA3Automation();
		HiddenIconsButton(automation.GetDesktop().FindFirstChild(cf => cf.ByClassName("Shell_TrayWnd")))?.Click();
		Retry.WhileTrue(() => IsWindowVisible(flyout), TimeSpan.FromSeconds(2));
	}

	/// <summary>
	/// Right-clicks the tray icon and returns the item of its menu (a WPF context menu: "Open mixer", "Settings", "Output device",
	/// "Sound control panel", "Sound settings", "Exit")
	/// </summary>
	public AutomationElement TrayMenuItem(string name)
	{
		TrayIcon().RightClick();
		return Retry.WhileNull(() => app!.GetAllTopLevelWindows(Automation)
		                                .Select(w => w.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.MenuItem).And(cf.ByName(name))))
		                                .FirstOrDefault(i => i != null), TimeSpan.FromSeconds(5)).Result
		       ?? throw new InvalidOperationException($"No \"{name}\" in the tray menu");
	}

	/// <summary>Exits the app with the tray menu, the way a person quits it (it saves the window's place and size then)</summary>
	public void ExitByTray()
	{
		TrayMenuItem("Exit").Patterns.Invoke.Pattern.Invoke();
		if(!process!.WaitForExit(10000))
			throw new TimeoutException("Volumey didn't exit from the tray menu");
	}

	public bool HasExited => process?.HasExited ?? true;

	public int ProcessId => process!.Id;

	/// <summary>The mixer's or settings window's handle, for Win32 checks (topmost, monitor)</summary>
	public static IntPtr Handle(Window window) => window.Properties.NativeWindowHandle.Value;

	/// <summary>
	/// Stops the app now (before the test's cleanup restores device and app state the app would react to)
	/// </summary>
	public void Kill() => Stop();

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
	private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder className, int maxCount);

	[DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)]
	private static extern IntPtr FindTopLevelWindow(string? className, string? windowName);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern uint RegisterWindowMessage(string lpString);

	[DllImport("user32.dll")]
	private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
