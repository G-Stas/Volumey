<h3 align="center">
  <br>
  <img width="300" src="https://i.imgur.com/yBMXIUz.png" alt="volumey">
  <br>
  Volumey
</h3>
<h4 align="center">
  Volume mixer that allows you to set up global hotkeys to control your Windows audio system. 
  <br>
  <br>
  <a href='//www.microsoft.com/store/apps/9mzcq03mx0s3?cid=storebadge&ocid=badge'>
      <img width="160" src='https://developer.microsoft.com/store/badges/images/English_get-it-from-MS.png' alt='English badge'/>
  </a>
  <br>
  <a href='https://boosty.to/stasg'>
      <img width="160" src='https://i.imgur.com/fS20w8T.png'/>
  </a>
</h4>

## Features
### Hotkeys
- Global hotkeys to change the volume of any application
- Global hotkeys to change the volume of the foreground application
- Global hotkeys to change the volume of the default output device and to mute it
- Global hotkeys to make another output device the default one
- Global hotkeys that act as media keys (play/pause, previous, next, stop)
- Global hotkey to open the volume mixer
- Two hotkey modes: hotkeys that take the keys for themselves, or shared hotkeys that let the keys through to other apps (useful in games)
- Change the volume by hotkeys with your preferred step
- The settings tell you when a key combination is already used by Volumey or by another app

### Volume mixer
- A Windows 11 style volume mixer, opened from the tray icon or by a hotkey
- Opens as a compact pop-up next to the tray or as a resizable window; can remember where you left it
- Volume and mute for every app, its separate audio sessions and the output device; muted sliders are greyed out
- Pick the output device or make it the default from the mixer or the tray icon's menu

### Devices
- Change a device's output format (bit depth and sample rate), exclusive mode and left/right balance
- Hotkeys for devices that aren't connected are kept until the device comes back

### More
- On-screen volume notifications in the style of Windows 11, in a corner of the screen you choose
- Limit the volume of apps that start at 100% to your preferred value
- Keep the volume of apps in step with the device volume
- Light and dark theme that follows Windows, with your Windows accent color
- Available in English, Chinese, French, German, Hindi, Italian, Japanese, Portuguese, Russian, Spanish, Turkish and Ukrainian

## Preview
<h6> 
  <img height="750" src="https://i.imgur.com/yyA8Q0d.gif">
</h6>
  Pressed hotkeys are displayed via <a href="https://github.com/Code52/carnac">Carnac</a>

## Download
- [Microsoft Store](https://www.microsoft.com/store/apps/9mzcq03mx0s3): updates automatically.
- [GitHub Releases](https://github.com/G-Stas/Volumey/releases): two zips.
  - `Volumey_<version>_x64.zip` runs as is (it includes .NET).
  - `Volumey_<version>_x64_framework-dependent.zip` is smaller and needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64).
  
  The zip builds check for updates when they start and offer to install them. Settings from Volumey 1.x are imported on the first start.

## Supported OS
`Windows 11` `Windows 10 18362.0+`

## Built With
- [.NET 10](https://dotnet.microsoft.com/) and WPF
- [WPF UI](https://github.com/lepoco/wpfui)
- [NAudio](https://github.com/naudio/NAudio)
- [SharpHook](https://github.com/TolikPylypchuk/SharpHook)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon)
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [AutoUpdater.NET](https://github.com/ravibpatel/AutoUpdater.NET)

## Copyright
Volumey is provided under the [GPL-3.0 License](https://github.com/G-Stas/Volumey/blob/main/LICENSE)
