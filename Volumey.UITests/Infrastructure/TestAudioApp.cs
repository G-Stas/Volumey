using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace Volumey.UITests.Infrastructure;

/// <summary>
/// A process that plays silence (scripts/test-audio-app) under its own exe name, so it gets its own audio session and mixer row.
/// It never touches other apps or the device. Built with the tests into audio-app next to them (Volumey.UITests.csproj).
/// </summary>
internal sealed class TestAudioApp : IDisposable
{
	private static readonly string BuildDir = Path.Combine(AppContext.BaseDirectory, "audio-app");

	public Process Process { get; }

	/// <summary>The exe's name (TestAudioApp copies the app under it)</summary>
	public string Name { get; }

	/// <summary>
	/// The app's name in Volumey (mixer rows, the Add app list, saved app hotkeys): the exe's file description, else its process name
	/// </summary>
	public string DisplayName => FileVersionInfo.GetVersionInfo(Process.MainModule!.FileName).FileDescription is { Length: > 0 } d ? d : Name;

	private TestAudioApp(string name, bool window, bool reset = true, bool routing = false)
	{
		Name = name;
		string dir = Path.Combine(Path.GetTempPath(), "Volumey-uitests", "apps", name);
		Directory.CreateDirectory(dir);
		if(!File.Exists(Path.Combine(BuildDir, "testaudio.exe")))
			throw new InvalidOperationException($"The test audio app isn't built: {Path.Combine(BuildDir, "testaudio.exe")} doesn't exist");
		foreach(var file in Directory.GetFiles(BuildDir))
		{
			try
			{
				File.Copy(file, Path.Combine(dir, Path.GetFileName(file) == "testaudio.exe" ? name + ".exe" : Path.GetFileName(file)), true);
			}
			catch(IOException)
			{
				//a second copy of a running app (one exe, two processes): its files are in use and already there
			}
		}
		Process = Process.Start(new ProcessStartInfo(Path.Combine(dir, name + ".exe"), "600" + (window ? " --window" : "") + (reset ? "" : " --no-reset") + (routing ? " --routing" : "")) { UseShellExecute = false, CreateNoWindow = true })
		          ?? throw new InvalidOperationException("The test audio app didn't start");
	}

	/// <summary>
	/// Starts one and waits until its session exists (at 100%: the app sets its own volume when it starts).
	/// <paramref name="window"/> also shows a small window (<see cref="BringToFront"/>).
	/// </summary>
	/// <param name="atFullVolume">false: only wait for the session (Volumey's volume limit lowers a new app right away)</param>
	/// <param name="routing">it plays through Windows' stream routing and moves at once when Windows sends it to another device</param>
	public static TestAudioApp Start(string name = "VolumeyUiTestApp", bool window = false, bool atFullVolume = true, bool routing = false)
	{
		var app = new TestAudioApp(name, window, reset: atFullVolume, routing);
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

	public bool? Muted => Audio.SessionMuted(Process.Id);

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
}
