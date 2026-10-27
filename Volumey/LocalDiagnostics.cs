#nullable enable
using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Volumey;

/// <summary>A deliberately isolated, non-audio diagnostic window for the volume-control workflow.</summary>
internal static class LocalDiagnostics
{
	private static string sessionPath = string.Empty;
	private static string profilePath = string.Empty;
	private static string pipeName = string.Empty;
	private static int volume = 58;
	private static bool muted;
	private static Window? window;
	private static TextBlock? stateText;
	private static CancellationTokenSource stop = new();

	internal static void Start(string[] args)
	{
		string? GetValue(string name)
		{
			int index = Array.IndexOf(args, name);
			return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
		}

		sessionPath = Path.GetFullPath(GetValue("--session") ?? throw new ArgumentException("--session is required"));
		profilePath = Path.GetFullPath(GetValue("--profile") ?? throw new ArgumentException("--profile is required"));
		pipeName = GetValue("--pipe") ?? throw new ArgumentException("--pipe is required");
		Directory.CreateDirectory(profilePath);
		File.WriteAllText(sessionPath, JsonSerializer.Serialize(new
		{
			protocol = 1,
			pid = Environment.ProcessId,
			executable = Environment.ProcessPath,
			startedUtc = DateTime.UtcNow,
			profile = profilePath,
			pipe = pipeName,
			mode = "mock-audio-endpoint"
		}, new JsonSerializerOptions { WriteIndented = true }));

		var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
		app.Startup += (_, _) =>
		{
			window = BuildWindow();
			app.MainWindow = window;
			window.Show();
			_ = ServeAsync(stop.Token);
		};
		app.Exit += (_, _) =>
		{
			stop.Cancel();
			if(File.Exists(sessionPath))
				File.Delete(sessionPath);
		};
		app.Run();
	}

	private static Window BuildWindow()
	{
		var panel = new StackPanel { Margin = new Thickness(20), Width = 360 };
		panel.Children.Add(new TextBlock { Text = "VOLUMEY · LOCAL DIAGNOSTICS", FontSize = 12, Margin = new Thickness(0, 0, 0, 14) });
		panel.Children.Add(new TextBlock { Text = "Mock output device", FontSize = 20, FontWeight = FontWeights.SemiBold });
		panel.Children.Add(new TextBlock { Text = "In-memory endpoint · does not change Windows audio", Opacity = .72, Margin = new Thickness(0, 2, 0, 12) });
		var slider = new Slider { Minimum = 0, Maximum = 100, Value = volume, IsSnapToTickEnabled = false };
		AutomationProperties.SetAutomationId(slider, "MockVolumeSlider");
		var row = new DockPanel();
		var mute = new Button { Content = "Mute", Padding = new Thickness(12, 6, 12, 6) };
		AutomationProperties.SetAutomationId(mute, "MockMuteButton");
		DockPanel.SetDock(mute, Dock.Left);
		row.Children.Add(mute);
		stateText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
		row.Children.Add(stateText);
		panel.Children.Add(slider);
		panel.Children.Add(row);
		panel.Children.Add(new TextBlock { Text = $"Private profile: {profilePath}", FontSize = 10, Opacity = .65, Margin = new Thickness(0, 16, 0, 0), TextWrapping = TextWrapping.Wrap });
		slider.ValueChanged += (_, e) => { volume = (int)e.NewValue; UpdateText(); };
		mute.Click += (_, _) => { muted = !muted; UpdateText(); };
		UpdateText();
		return new Window
		{
			Title = "Volumey — Local Diagnostics",
			Width = 440,
			Height = 250,
			WindowStartupLocation = WindowStartupLocation.CenterScreen,
			Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
			Foreground = Brushes.White,
			Content = panel
		};
	}

	private static void UpdateText()
	{
		if(stateText != null)
			stateText.Text = $"{volume}% · {(muted ? "Muted" : "Unmuted")}";
	}

	private static async Task ServeAsync(CancellationToken cancellationToken)
	{
		while(!cancellationToken.IsCancellationRequested)
		{
			await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
			try
			{
				await pipe.WaitForConnectionAsync(cancellationToken);
				using var reader = new StreamReader(pipe);
				await using var writer = new StreamWriter(pipe) { AutoFlush = true };
				string? line = await reader.ReadLineAsync(cancellationToken);
				if(line == null)
					continue;
				using JsonDocument request = JsonDocument.Parse(line);
				string op = request.RootElement.GetProperty("op").GetString() ?? "";
				object response = await Application.Current.Dispatcher.InvokeAsync(() => HandleRequest(op, request.RootElement));
				await writer.WriteLineAsync(JsonSerializer.Serialize(response));
			}
			catch(OperationCanceledException)
			{
				return;
			}
			catch(Exception e)
			{
				await pipe.WriteAsync(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = e.Message }) + "\n"), cancellationToken);
			}
		}
	}

	private static object HandleRequest(string op, JsonElement request)
	{
		switch(op)
		{
			case "status":
			case "state":
				return new { ready = window?.IsLoaded == true, pid = Environment.ProcessId, volume, muted, device = "Mock output device", profile = profilePath };
			case "set-volume":
				volume = Math.Clamp(request.GetProperty("value").GetInt32(), 0, 100);
				UpdateText();
				return new { completed = true, volume };
			case "set-mute":
				muted = request.GetProperty("value").GetBoolean();
				UpdateText();
				return new { completed = true, muted };
			case "screenshot":
				string path = Path.Combine(profilePath, "diagnostic.png");
				var bounds = VisualTreeHelper.GetDescendantBounds(window!);
				var bitmap = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32);
				bitmap.Render(window!);
				using(var output = File.Create(path))
				{
					var encoder = new PngBitmapEncoder();
					encoder.Frames.Add(BitmapFrame.Create(bitmap));
					encoder.Save(output);
				}
				return new { completed = true, screenshot = path };
			case "stop":
				Application.Current.Dispatcher.BeginInvoke(() => Application.Current.Shutdown());
				return new { completed = true };
			default:
				return new { error = $"Unsupported operation: {op}" };
		}
	}
}
