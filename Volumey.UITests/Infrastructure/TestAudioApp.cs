using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace Volumey.UITests.Infrastructure;

/// <summary>
/// A process that plays silence (scripts/test-audio-app) under its own exe name, so it gets its own audio session and mixer row.
/// It never touches other apps or the device. Built once per test run into %TEMP%\Volumey-uitests\audio-app.
/// </summary>
internal sealed class TestAudioApp : IDisposable
{
	private static readonly Lazy<string> Build = new(BuildApp);

	public Process Process { get; }

	/// <summary>The exe's name (TestAudioApp copies the app under it)</summary>
	public string Name { get; }

	/// <summary>
	/// The app's name in Volumey (mixer rows, the Add app list, saved app hotkeys): the exe's file description, else its process name
	/// </summary>
	public string DisplayName => FileVersionInfo.GetVersionInfo(Process.MainModule!.FileName).FileDescription is { Length: > 0 } d ? d : Name;

	private TestAudioApp(string name, bool window, bool reset = true)
	{
		Name = name;
		string dir = Path.Combine(Path.GetTempPath(), "Volumey-uitests", "apps", name);
		Directory.CreateDirectory(dir);
		foreach(var file in Directory.GetFiles(Build.Value))
			File.Copy(file, Path.Combine(dir, Path.GetFileName(file) == "testaudio.exe" ? name + ".exe" : Path.GetFileName(file)), true);
		Process = Process.Start(new ProcessStartInfo(Path.Combine(dir, name + ".exe"), "600" + (window ? " --window" : "") + (reset ? "" : " --no-reset")) { UseShellExecute = false, CreateNoWindow = true })
		          ?? throw new InvalidOperationException("The test audio app didn't start");
	}

	/// <summary>
	/// Starts one and waits until its session exists (at 100%: the app sets its own volume when it starts).
	/// <paramref name="window"/> also shows a small window (<see cref="BringToFront"/>).
	/// </summary>
	/// <param name="atFullVolume">false: only wait for the session (Volumey's volume limit lowers a new app right away)</param>
	public static TestAudioApp Start(string name = "VolumeyUiTestApp", bool window = false, bool atFullVolume = true)
	{
		var app = new TestAudioApp(name, window, reset: atFullVolume);
		if(!atFullVolume)
		{
			if(!FlaUI.Core.Tools.Retry.WhileFalse(() => Audio.SessionVolume(app.Process.Id) != null, TimeSpan.FromSeconds(10)).Result)
			{
				app.Dispose();
				throw new InvalidOperationException("The test audio app has no session on the default device");
			}
			return app;
		}
		if(Audio.WaitFor(() => Audio.SessionVolume(app.Process.Id), 100, 10000) != 100)
		{
			app.Dispose();
			throw new InvalidOperationException("The test audio app has no session at 100% on the default device");
		}
		return app;
	}

	public int? Volume => Audio.SessionVolume(Process.Id);

	/// <summary>
	/// Puts the app's window (started with <c>window: true</c>) in front, so it's the foreground app for Volumey
	/// </summary>
	public void BringToFront(UIA3Automation automation)
	{
		var window = Retry.WhileNull(() => automation.GetDesktop().FindFirstChild(cf => cf.ByProcessId(Process.Id).And(cf.ByControlType(ControlType.Window))),
			TimeSpan.FromSeconds(10)).Result?.AsWindow() ?? throw new InvalidOperationException("The test app has no window");
		window.SetForeground();
		if(!Retry.WhileFalse(() => GetForegroundWindow() == window.Properties.NativeWindowHandle.Value, TimeSpan.FromSeconds(3)).Result)
			throw new InvalidOperationException("The test app's window didn't come to the front");
	}

	[System.Runtime.InteropServices.DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	public void Dispose()
	{
		try
		{
			if(!Process.HasExited)
				Process.Kill();
		}
		catch(InvalidOperationException)
		{
		}
		Process.Dispose();
	}

	private static string BuildApp()
	{
		string output = Path.Combine(Path.GetTempPath(), "Volumey-uitests", "audio-app");
		string project = Path.Combine(VolumeyApp.RepoRoot, "scripts", "test-audio-app", "TestAudioApp.csproj");
		var publish = Process.Start(new ProcessStartInfo("dotnet", $"publish \"{project}\" -c Release -o \"{output}\"")
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		})!;
		string log = publish.StandardOutput.ReadToEnd() + publish.StandardError.ReadToEnd();
		publish.WaitForExit();
		if(publish.ExitCode != 0)
			throw new InvalidOperationException("Building the test audio app failed:\n" + log);
		return output;
	}
}
