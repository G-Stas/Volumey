<h3 align="center">
  <br>
  <img width="200" src=".github/images/icon.png" alt="Volumey">
  <br>
  Volumey
</h3>
<h4 align="center">
  A volume mixer for Windows with global hotkeys for every app and device.
  <br>
  <br>
  <a href="https://github.com/G-Stas/Volumey/releases/latest"><img src="https://img.shields.io/github/v/release/G-Stas/Volumey" alt="Latest release"></a>
  <a href="https://github.com/G-Stas/Volumey/releases"><img src="https://img.shields.io/github/downloads/G-Stas/Volumey/total" alt="GitHub downloads"></a>
  <a href="https://github.com/G-Stas/Volumey/actions/workflows/build.yml"><img src="https://img.shields.io/github/actions/workflow/status/G-Stas/Volumey/build.yml?branch=main" alt="Build status"></a>
  <br>
  <br>
  <a href="https://get.microsoft.com/installer/download/9mzcq03mx0s3?referrer=appbadge&cid=GithubBadge" target="_self" >
  	<img src="https://get.microsoft.com/images/en-us%20dark.svg" width="250"/>
  </a>
  <br>
</h4>

Windows lets you change an app's volume only with the mouse, in a mixer buried in Settings. Volumey gives every app and device its own hotkeys, so you can turn the game down and Discord up without leaving the game.

## Features
### Hotkeys
- **Any app:** turn a chosen app's volume up or down, or mute it
- **Foreground app:** control or mute whichever app is in focus
- **Output device:** change the volume of the default device or mute it
- **Default device:** switch to another output device with one key
- **Media keys:** play/pause, previous, next and stop
- **Mixer:** open the volume mixer
- **Keyboard or mouse:** use key combinations, mouse buttons or the wheel with a modifier
- **Two modes:** keep the keys to Volumey, or share them with other apps (handy in games)
- **Your step:** pick how much each press changes the volume

<details>
<summary><b>▶️ See the hotkeys in action</b></summary>
<br>
<p align="center">
  <img src=".github/images/demo-hotkeys.gif" alt="Hotkeys turn Spotify down and up, mute the output device and switch the default device, each shown by an on-screen notification">
</p>
</details>

### Volume mixer
- Opens from the tray icon or by a hotkey
- A compact pop-up next to the tray or a resizable window
- Volume and mute for every app, each of its audio sessions and the output device
- **Level meters** that show what's playing
- **Volume lock:** keep an app's volume fixed until it closes, or also when it starts again
- **Output device per app:** choose which device each app plays on

<details>
<summary><b>▶️ See the mixer in action</b></summary>
<br>
<p align="center">
  <img src=".github/images/demo-mixer.gif" alt="The mixer opens from the tray, Spotify's volume is locked and the app is moved to the speakers">
</p>
</details>

### More
- **On-screen notifications** in the corner of the screen you choose
- **Start volume:** apps that start at 100% start at your preferred volume instead
- **Volume sync:** keep app volumes in step with the device volume
- **Tray icon:** scroll over it to change the device volume, choose what a middle click does and pick its icon

<p align="center">
  <img width="420" src=".github/images/preview.png" alt="The mixer pop-up with an on-screen volume notification above it, in the dark and the light theme">
</p>

## Download
- [Microsoft Store](https://www.microsoft.com/store/apps/9mzcq03mx0s3)
- [GitHub Releases](https://github.com/G-Stas/Volumey/releases)

## Supported OS
`Windows 11` `Windows 10 version 2004 or later`

## Translations
Volumey's texts live in [`Volumey/Resources`](Volumey/Resources), one `Resources.<language>.resx` file per language. They are XML files: each `<data name="...">` holds one text in its `<value>`. Keep the `name` and any `{0}`-style placeholders as they are.

**Fix a translation:** edit the `<value>` in that language's file and open a pull request.

**Add a language:** copy `Resources.en.resx` to `Resources.<code>.resx` (e.g. `pl`, `pt-BR`), translate it, add the language to both lists in [`TranslationSource.cs`](Volumey/View/Localization/TranslationSource.cs) and to [`Package.appxmanifest`](Volumey.Package/Package.appxmanifest), and open a pull request.

## Built With
- [WPF UI](https://github.com/lepoco/wpfui)
- [NAudio](https://github.com/naudio/NAudio)
- [SharpHook](https://github.com/TolikPylypchuk/SharpHook)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon)
- [AutoUpdater.NET](https://github.com/ravibpatel/AutoUpdater.NET)

## License
Volumey is released under the [GPL-3.0 License](https://github.com/G-Stas/Volumey/blob/main/LICENSE).
