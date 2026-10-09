using System.Globalization;
using NAudio.Wave;

//usage: testaudio <seconds> [<volume 0..1> <after seconds>]
//plays silence on the default output device for <seconds>; optionally sets its own session volume after a delay,
//like a change in the Windows mixer (Volumey shows an OSD for it when "react to all volume changes" is on)
int seconds = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 30;
float? volume = args.Length > 1 ? float.Parse(args[1], CultureInfo.InvariantCulture) : null;
int after = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 3;

using var player = new WasapiPlayerBuilder().WithSharedMode().Build();
player.Init(new SilenceProvider(player.DeviceMixFormat));
player.Play();
//Windows remembers a session's volume per executable
player.SessionVolume.Mute = false;
player.SessionVolume.Volume = 1f;

if(volume is float v)
{
	Thread.Sleep(after * 1000);
	player.SessionVolume.Volume = v;
	Thread.Sleep(Math.Max(0, seconds - after) * 1000);
}
else
	Thread.Sleep(seconds * 1000);
