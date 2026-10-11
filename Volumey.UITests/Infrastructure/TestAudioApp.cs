using System.Diagnostics;

namespace Volumey.UITests.Infrastructure;

/// <summary>
/// A process that plays silence (scripts/test-audio-app) under its own exe name, so it gets its own audio session and mixer row.
/// It never touches other apps or the device. Built once per test run into %TEMP%\Volumey-uitests\audio-app.
/// </summary>
internal sealed class TestAudioApp : IDisposable
{
	private static readonly Lazy<string> Build = new(BuildApp);

	public Process Process { get; }

	/// <summary>The process name Volumey saves app hotkeys under</summary>
	public string Name { get; }

	private TestAudioApp(string name)
	{
		Name = name;
		string dir = Path.Combine(Path.GetTempPath(), "Volumey-uitests", "apps", name);
		Directory.CreateDirectory(dir);
		foreach(var file in Directory.GetFiles(Build.Value))
			File.Copy(file, Path.Combine(dir, Path.GetFileName(file) == "testaudio.exe" ? name + ".exe" : Path.GetFileName(file)), true);
		Process = Process.Start(new ProcessStartInfo(Path.Combine(dir, name + ".exe"), "600") { UseShellExecute = false, CreateNoWindow = true })
		          ?? throw new InvalidOperationException("The test audio app didn't start");
	}

	/// <summary>
	/// Starts one and waits until its session exists (at 100%: the app sets its own volume when it starts)
	/// </summary>
	public static TestAudioApp Start(string name = "VolumeyUiTestApp")
	{
		var app = new TestAudioApp(name);
		if(Audio.WaitFor(() => Audio.SessionVolume(app.Process.Id), 100, 10000) != 100)
		{
			app.Dispose();
			throw new InvalidOperationException("The test audio app has no session at 100% on the default device");
		}
		return app;
	}

	public int? Volume => Audio.SessionVolume(Process.Id);

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
