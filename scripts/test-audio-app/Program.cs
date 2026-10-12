using System.Globalization;
using System.Windows.Forms;
using NAudio.Wave;

//usage: testaudio <seconds> [<volume 0..1> <after seconds>] [--window] [--no-reset]
//plays silence on the default output device for <seconds>; optionally sets its own session volume after a delay,
//like a change in the Windows mixer (Volumey shows an OSD for it when "react to all volume changes" is on).
//--window also shows a small window titled "Volumey test app <pid>", so a UI test can put the app in front (foreground-app hotkeys)
bool window = args.Contains("--window");
//--no-reset keeps the session volume Windows starts it with (a test of Volumey's volume limit for new apps)
bool reset = !args.Contains("--no-reset");
args = args.Where(a => !a.StartsWith("--")).ToArray();
int seconds = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 30;
float? volume = args.Length > 1 ? float.Parse(args[1], CultureInfo.InvariantCulture) : null;
int after = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 3;

using var player = new WasapiPlayerBuilder().WithSharedMode().Build();
player.Init(new SilenceProvider(player.DeviceMixFormat));
player.Play();
//Windows remembers a session's volume per executable
if(reset)
{
	player.SessionVolume.Mute = false;
	player.SessionVolume.Volume = 1f;
}

if(window)
{
	//WinForms needs an STA thread with a message loop
	var ui = new Thread(() =>
	{
		using var form = new Form
		{
			Text = $"Volumey test app {Environment.ProcessId}",
			Width = 320,
			Height = 120,
			StartPosition = FormStartPosition.CenterScreen,
			ShowInTaskbar = true
		};
		Application.Run(form);
	});
	ui.SetApartmentState(ApartmentState.STA);
	ui.IsBackground = true;
	ui.Start();
}

if(volume is float v)
{
	Thread.Sleep(after * 1000);
	player.SessionVolume.Volume = v;
	Thread.Sleep(Math.Max(0, seconds - after) * 1000);
}
else
	Thread.Sleep(seconds * 1000);
