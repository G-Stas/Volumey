# Volumey — notes for agents

> **Rule: keep this file current.** Any change to app logic, architecture, build, packaging, dependencies, settings format or release process must update the matching section of this file in the same change. Remove anything that is no longer true, such as fixed known issues or finished plan steps.

Volumey is a Windows tray volume mixer (WPF). It sets up global hotkeys for per-app volume, device volume, mute, foreground-app volume and switching the default output device, and shows on-screen notifications. It ships through the **Microsoft Store** (package `58593StasG.Volumey`, Store ID `9mzcq03mx0s3`) and as a standalone zip on GitHub Releases. GPL-3.0. Author: Stas Geft (`G-Stas` on GitHub).

The last release is **v1.5.4.0** (May 2023), and `main` was last changed in June 2023 (Ukrainian localization).

**Git remotes (as of 2026-10-08):** this clone was originally made from Colin Findlay's fork, so `origin` → `colinfindlay-nz/Volumey` (stale: its `main` stops at Dec 2022), and **`upstream` → `G-Stas/Volumey`, the real repo**. Base work on `upstream/main`, and push to and open PRs against `G-Stas/Volumey`. The owner may rename the remotes (`origin` = G-Stas). Check with `git remote -v` and update this note. Colin's "sync app volume to master volume" feature (`AppVolumeSyncViewModel`) is already merged into the real `main`; the fork's `feature/synchroniseAppVolumes` branch is obsolete.

## Repo layout

```
Volumey.sln
Volumey/                 WPF app (net10.0-windows10.0.19041.0, WinExe, entry point Volumey.Startup.Main)
  Startup.cs             single-instance mutex, args, log4net init, Store activation check
  App.xaml(.cs)          global resources; instantiates the 4 root ViewModels as resources
  CoreAudioWrapper/
    CoreAudio/           hand-written COM interop: IMMDevice*, IAudioSession*, IAudioEndpointVolume*, IPolicyConfig, PROPVARIANT...
    Wrapper/             thin managed wrapper: DeviceEnumerator, MMDevice, IDevice(+extensions that build models),
                         AudioSessionProvider, *Notifications/*Handler (COM callbacks -> C# events), PolicyClient, GuidValue
  DataProvider/          DeviceProvider (singleton, active/default devices), SettingsProvider + AppSettings (persistence),
                         ScreenInfoProvider
  Model/                 OutputDeviceModel, MasterSessionModel, AudioProcessModel, AudioSessionModel, SystemHotkey, ScreenInfo
  ViewModel/             AppBehaviorViewModel, DeviceProviderViewModel, SettingsViewModel, ThemeViewModel
    Settings/            one VM per settings feature + HotkeysControl (static hotkey hub) + notification mediator
  View/                  MainView (window + tray), MixerView/MasterView/SessionsListView, SettingsView + SettingsPage/*,
                         Controls/ (HotkeyBox, VolumeSlider...), Converters/, Styles/, Localization/
  Helper/                NativeMethods (P/Invoke), HotkeyManager / HotkeyHookManager, NotificationManagerHelper,
                         SystemIntegrationHelper (autostart/start menu), UpdateHelper, IconHelper...
  Resources/             Resources*.resx (localization), icons, and 2 vendored DLLs (see Dependencies)
  log4net.config         Loggly appender (Release/Store only)
Volumey.Tests/           MSTest + Moq tests for models / DeviceProvider / hotkey validation
Volumey.Package/         Windows Application Packaging project (.wapproj): MSIX for the Store
LatestUpdateInfo.xml     AutoUpdater.NET feed for the non-Store build (read from raw.githubusercontent main)
global.json              pins SDK 10.0.100+ (latestFeature) and selects the Microsoft.Testing.Platform runner for `dotnet test`
```

## Build, run, test

Configurations: `Debug`, `Release`, `Store`. The only platform is `x64`, with no AnyCPU, so pass `-p:Platform=x64`.

```powershell
# app + tests (needs the .NET 10 SDK)
dotnet build Volumey.Tests\Volumey.Tests.csproj -c Debug -p:Platform=x64
dotnet run --project Volumey\Volumey.csproj -c Debug -p:Platform=x64
dotnet test --project Volumey.Tests\Volumey.Tests.csproj -c Debug -p:Platform=x64   # SDK 10 + MTP syntax: --project is required

# Store MSIX (needs VS MSBuild, not dotnet build). Drop the signing override once a valid cert is set up.
& "$(& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath)\MSBuild\Current\Bin\amd64\MSBuild.exe" `
  Volumey.Package\Volumey.Package.wapproj /restore /p:Configuration=Store /p:Platform=x64 /p:AppxBundle=Never `
  /p:UapAppxPackageBuildMode=StoreUpload /p:AppxPackageSigningEnabled=false
```

- **Running a dev build while your installed Volumey is running does nothing.** Both use the same single-instance mutex, so the dev build just signals the installed one and exits. Quit the installed app first.
- Tests use **MSTest 4** via `<Project Sdk="MSTest.Sdk/4.5.1">`, on the **Microsoft.Testing.Platform** runner: the test project builds to a standalone exe. They run from the CLI, Rider or VS. The suite has 54 tests, all passing.
  - The test project must use the same TFM as the app, and it needs `UseWPF`.
  - Write tests with `[TestClass]`, `[TestMethod]` and `Assert.AreEqual`/`IsTrue`/`Contains`/`ContainsSingle`. Test methods must be public, non-static instance methods, or MSTest skips them silently with a UTA007 discovery warning. Use `[STATestMethod]` for tests that need a WPF/STA thread.
  - `PlatformTarget` is always x64 and the `RuntimeIdentifier` is win-x64, because the app's `RuntimeIdentifier` is win-x64 on every platform.
- `Volumey.Package.wapproj` (MSIX) does **not** build with `dotnet build`. It needs Visual Studio MSBuild with the "Windows application packaging" (DesktopBridge) targets and the Windows SDK (`TargetPlatformVersion` 10.0.26100.0, `TargetPlatformMinVersion` 10.0.18362.0). Use the VS menu *Project → Publish → Create App Packages* or the command above.
  - The `Store` configuration of the app is **self-contained**, so the MSIX carries the .NET 10 runtime (about 73 MB). Without that, packaging fails with NETSDK1112 (runtime pack not downloaded). `Debug`/`Release` are framework-dependent: the GitHub zip build needs the **.NET 10 Desktop Runtime** on the user's machine.
  - The wapproj's `PublishTrimmed=true` has no effect: the package isn't trimmed, which is good because WPF doesn't support trimming. Remove it when you next touch the wapproj.
- Signing: the wapproj references the cert thumbprint `4C35C87C…3F96` (`Volumey.Package_TemporaryKey.pfx`, subject = the Store publisher ID). That cert **expired 2023-12-13**. For a Store upload, re-associate the project with the Store app in VS. VS then makes a fresh temporary cert, and the Store re-signs the package anyway.
- Debug builds wire up **no logging**. `Startup.PathToDebugLogConfig` is empty, and the try/catch around `App.Main` is compiled out, so exceptions crash into the debugger.

### Conditional compilation
- `STORE` (Store config): the app checks the StartupTask activation kind to start minimized, checks for updates with `Windows.Services.Store.StoreContext` and shows `UpdateDialog`, reads the version from `Package.Current`, and handles autostart through the manifest `windows.startupTask` instead of the registry.
- Non-Store: AutoUpdater.NET reads `LatestUpdateInfo.xml` on GitHub main. Autostart uses `HKCU\...\Run` plus a Start Menu shortcut (`SystemIntegrationHelper`).
- `DEBUG`: see above.

### Release checklist (from history)
Bump the version in **3 places**: `Volumey/Volumey.csproj` (`Version`, `AssemblyVersion`), `Volumey.Package/Package.appxmanifest` (`Identity Version`), and `LatestUpdateInfo.xml` (version + release zip URL). Then build the Store package for upload and the zip for GitHub Releases.

## Architecture / runtime flow

1. `Startup.Main`: a named mutex enforces a single instance. A second instance broadcasts the registered `WM_SHOWME` message and exits. The `-restart` arg waits up to 5s for the mutex, and `-minimized` starts the app in the tray. Then it runs `App.Main()`.
2. `App.xaml` creates the root VMs as **XAML resources**: `AppBehaviorViewModel`, `SettingsViewModel`, `DeviceProviderViewModel`, `ThemeViewModel`. There is no DI container. Singletons and statics are used throughout: `DeviceProvider.GetInstance()`, the static `SettingsProvider`, the static `HotkeysControl`, `TranslationSource.Instance`.
3. The `MainView` ctor force-creates the HWND, because the window starts hidden. It hooks `WndProc` (WM_HOTKEY, WM_SHOWME, …), calls `RegisterApplicationRestart`, and gives the HWND to `SettingsViewModel` → `HotkeysControl.SetHotkeyManager(new HotkeyManager(hwnd))`. That fires `HotkeysControl.Activated`, and each settings VM then registers its saved hotkeys.
4. The tray icon is `Hardcodet.NotifyIcon.Wpf` (`MainView.Tray`). Win11 uses a plain text tooltip because custom tooltips break there.

### Audio layer
- `DeviceEnumerator` wraps `IMMDeviceEnumerator`. It builds an `OutputDeviceModel` for every active render endpoint through `IDeviceExtensions.GetOutputDeviceModel`, which activates `IAudioSessionManager2` and enumerates sessions into `AudioProcessModel`s.
- `DeviceStateNotificationsHandler` (an `IMMNotificationClient`) raises DefaultDeviceChanged, ActiveDeviceAdded, DeviceDisabled, NameChanged, IconPathChanged and FormatChanged. These arrive on **COM/MTA threads**, and models marshal to the UI with `App.Current.Dispatcher.Invoke`.
- `MasterSessionModel` is the endpoint volume (`IAudioEndpointVolume` + callback). `AudioProcessModel` groups one or more `AudioSessionModel`s (`ISimpleAudioVolume` + `IAudioSessionEvents`) that share a PID. `AudioSessionProvider` (`IAudioSessionNotification`) adds sessions as they are created (`SessionCreatedEventArgs`). Sessions are grouped into processes by PID and also by the session's grouping parameter. Outside `OutputDeviceModel`, read the processes through `GetImmutableProcessesAsync()`, not the live collection.
- **Echo suppression:** volume and mute writes from the app pass `GuidValue.Internal.VolumeGUID` as the COM event context. Callbacks compare the context and skip work for self-originated changes. `GuidValue.Internal.Empty` marks hotkey-originated changes so that notifications still show. Keep this mechanism in any rewrite.
- `PolicyClient` sets the default device through the **undocumented** `IPolicyConfig` COM interface, for all three roles (Console, Multimedia, Communications).
- Wrapper interfaces (`IDevice`, `IAudioSessionVolume`, `IAudioSessionStateNotifications`, `ISessionProvider`, `IDeviceStateNotificationHandler`, `IMasterVolumeNotificationHandler`) exist so the tests can mock COM with Moq.

### Hotkeys
- `HotkeysControl` (static) holds the one `IHotkeyManager`. It ref-counts hotkeys (`HotkeysUserCount`) so that several features can share a combo, raises `HotkeyPressed`, and validates hotkeys (no bare F12, no duplicates via `AppSettings.HotkeyExists`).
- `HotkeyManager` (**active**) uses `RegisterHotKey`/WM_HOTKEY, which blocks the key combo system-wide. `HotkeyHookManager` uses a low-level keyboard hook (`WH_KEYBOARD_LL`) that does not block keys. It is implemented but **unused**: the switching code in `SettingsViewModel` is commented out, along with the `BlockHotkeys` / `AllowDuplicates` settings.
- Models subscribe to `HotkeysControl.HotkeyPressed` and handle their own up/down/mute hotkeys (`SetVolumeHotkeys`, `SetMuteHotkey`). App hotkeys are stored by **process name** (`VolumeHotkeysPair`) and get applied when a matching process shows up. Default-device hotkeys are stored as a `DeviceInfo` (ID + name), and an active device matches if its ID **or** its name matches, after which the stored info is refreshed.

### Settings persistence
- `SettingsProvider` reads and writes `%LOCALAPPDATA%\Volumey\appconfig` with **`BinaryFormatter`** (`[Serializable] AppSettings` plus nested `HotkeysAppSettings` and `NotificationsAppSettings`). New fields use `[OptionalField]` to keep old files compatible, and `[OnDeserialized]` migrates old formats. The Store build writes the same path, because MSIX redirects LocalAppData to the package folder.
- Saving uses `FileMode.OpenOrCreate` without truncating the file. If a newer payload is shorter, old bytes are left at the end. Use `FileMode.Create` instead.
- On .NET 10, `BinaryFormatter` only works because of three things. **All of them are temporary, until settings move to JSON (item B):**
  - the `System.Runtime.Serialization.Formatters` compat package (Microsoft's unsupported out-of-band copy);
  - `<EnableUnsafeBinaryFormatterSerialization>true` in both the app and test projects;
  - `NoWarn SYSLIB0011` in the app project.
- The settings file stores type identities, so the **types inside it must stay loadable**. A real v1.5.4 file references the assemblies `mscorlib`, `WindowsBase` (`Key`/`ModifierKeys`), `Volumey` (`AppSettings` and its nested types, `SerializableHotkey`, …) and **`Notification.Wpf`** (the vendored popup library's position/enum types in `NotificationsAppSettings`). Renaming or moving any `[Serializable]` settings type, or removing or replacing `Notification.Wpf` before item B, breaks loading for existing users, and the app then silently resets to defaults. `ParallelExtensionsExtras` is *not* in the file.
- Verified on 2026-10-08: a real v1.5.4 settings file (with app and device hotkeys) loads on the .NET 10 build, and it serializes and reloads unchanged.

### UI
- ModernWpfUI 0.9.4 (Fluent-ish controls, NavigationView, ContentDialog). The theme comes from `ThemeViewModel` plus the `Colors.Light/Dark.xaml` + `Brushes.xaml` theme dictionaries, with the Windows accent color from `SystemColorHelper`.
- On-screen volume popups use the `Notification.Wpf` fork via `NotificationManagerHelper`. `AudioProcessStateNotificationMediator` lets models request a popup without referencing the UI.
- Multi-monitor placement uses `WpfScreenHelper` (`ScreenInfoProvider`).
- Localization: `Resources/Resources.<lang>.resx` + `TranslationSource` (live culture switching) + the `{loc:Localization Key}` markup extension. Languages: en, ru, de, it, fr, es, pt, ja, zh, hi, tr, uk-ua. `Resources.resx` (neutral) generates `Resources.Designer.cs`. When you add a string, add it to `Resources.resx` and to every `.<lang>.resx`. To add a language, add the resx file and add the culture to both lists in `TranslationSource`. Note: `tr` and `uk-ua` are missing from `Package.appxmanifest` `<Resources>`.

## Dependencies
| Package | Purpose | Notes |
|---|---|---|
| ModernWpfUI 0.9.4 | UI controls/theme | Frozen 0.9.x line; to be replaced by iNKORE.UI.WPF.Modern (item C) |
| Hardcodet.NotifyIcon.Wpf (**vendored DLL**, author's fork `G-Stas/wpf-notifyicon`) | tray icon | `Resources/` binary, not NuGet |
| Notification.Wpf (**vendored DLL**, fork `G-Stas/Notification.Wpf`) | OSD popups | `Resources/` binary, not NuGet |
| Autoupdater.NET.Official 1.8.2 | non-Store updates | 1.9.x is available; works on .NET 10 (update dialog verified) |
| log4net-loggly 9.0.0 | remote logging to Loggly | **Loggly input key is committed in `log4net.config`** |
| log4net 2.0.17, Newtonsoft.Json 13.0.4 (direct refs) | override the vulnerable transitive versions from log4net-loggly (2.0.8 / 10.0.3) | log4net 2.0.17 still has one moderate advisory (NU1902, GHSA-4f7c-pmjv-c25w). It's fixed in log4net 3.x, but log4net-loggly was built against 2.x, so test logging before bumping |
| MSFT.ParallelExtensionsExtras 1.2.0 | `ObservableConcurrentDictionary` (built at runtime from settings) | .NET Framework-only package (NU1701 suppressed); worth replacing with a small in-repo class |
| System.Runtime.Serialization.Formatters 10.0.x | BinaryFormatter on .NET 9+ | temporary, see Settings persistence |
| Microsoft.Xaml.Behaviors.Wpf, WpfScreenHelper | misc | |

WinRT APIs (`StoreContext`, `AppInstance`, `Package`) come from the `-windows10.0.19041.0` TFM; the `Microsoft.Windows.SDK.Contracts` package is gone. `System.Drawing` comes from the Windows Desktop framework. Code analyzers come with the SDK, so there's no NetAnalyzers package.

## Known issues / gotchas found during analysis
- Tests aren't isolated from the machine. Anything that touches `SettingsProvider` (e.g. `HotkeysValidationTests`) loads the **real** `%LOCALAPPDATA%\Volumey\appconfig`, and creates it if it's missing. The tests only change settings in memory, but their results can depend on the developer's own settings. Fix this with the `ISettingsStore` from item B.
- The settings file is not truncated on save (see above).
- `AppVolumeSyncViewModel` subscribes to `defaultDevice.Master.PropertyChanged` when it's switched on, but never unsubscribes: not when it's switched off, and not when the default device changes. `OnDefaultDeviceChanged` also never subscribes to the new device's master. After a device switch, changing the *old* device's volume syncs the *new* device's apps, and the new device's master volume is ignored. Turning it off and on again adds a duplicate handler.
- `SystemHotkey` overrides `Equals` without `GetHashCode` (CS0659). It is used in hash collections, so check this.
- Several fire-and-forget `async` calls (CS4014) and `async void` handlers.
- The appxmanifest `MaxVersionTested` is 10.0.14393.0, an old value. Bump it when you modernize.

## Modernization plan (owner's intent)
The owner is coming back to the project to work through the GitHub issues and modernize the app. Planned work items are below. **Constraints for all of them:** the Store package identity (`58593StasG.Volumey`), publisher, and StartupTask `TaskId` must stay the same, and existing users' settings must survive the upgrade.

### Suggested order
Each step should leave a working, shippable build.
1. ~~Universal tests + green baseline (item E).~~ Done: MSTest, 54/54 passing.
2. ~~Retarget to **.NET 10**~~. Done (2026-10-08): the app and tests are on `net10.0-windows10.0.19041.0`, and the Store MSIX builds. .NET 10 is LTS, supported until Nov 2028. Don't target .NET 11 until it ships as stable, and only move to it if the owner asks. BinaryFormatter is kept alive temporarily, see Settings persistence.
   - **Still to verify by hand:** launch the app and the installed MSIX and smoke-test them (tray, mixer, hotkeys, OSD, settings save, theme, autostart). The vendored DLLs and ModernWpfUI 0.9.4 were built for older .NET, so check them at runtime.
3. New settings format (item B). This also removes the BinaryFormatter compat package, and it must land **before** `Notification.Wpf` is replaced (item C), because that library's types are stored in the binary settings file.
4. Audio layer → NAudio (item A).
5. Non-blocking hook hotkeys (item D).
6. UI framework (item C). This is the biggest and riskiest change, so do it last, on top of a cleaner architecture.

### A. Replace the hand-written CoreAudio wrapper with NAudio
Use `NAudio.Wasapi` / `NAudio.CoreAudioApi`.
- `MMDeviceEnumerator`, `MMDevice`, `AudioEndpointVolume` (+`OnVolumeNotification`), `AudioSessionManager` (`OnSessionCreated`, `Sessions`), `AudioSessionControl` (`RegisterEventClient` / `IAudioSessionEventsHandler`, `SimpleAudioVolume`, `GetProcessID`, `IconPath`, `DisplayName`), and `IMMNotificationClient` via `RegisterEndpointNotificationCallback` all exist in NAudio.
- **Not in NAudio:** `IPolicyConfig` (set the default device). Keep `PolicyClient` + `IPolicyConfig.cs`.
- **Event-context GUID (checked against NAudio master, Oct 2026):**
  - Endpoint (master) volume is fine. `AudioEndpointVolume.NotificationGuid` sets the context used by the `MasterVolumeLevelScalar`/`Mute` setters, and `AudioVolumeNotificationData.EventContext` exposes it in callbacks.
  - Per-app sessions are **not** covered. `SimpleAudioVolume.Volume`/`Mute` hard-code `Guid.Empty`, and `IAudioSessionEventsHandler.OnVolumeChanged(float, bool)` drops the context. Keep a small interop path for session volume writes and session events (`ISimpleAudioVolume`, `IAudioSessionEvents`), or replace the echo suppression for sessions with something else (e.g. compare against the last value the app wrote).
- **Device format:** NAudio has no device-format property. Read `PKEY_AudioEngine_DeviceFormat` from `MMDevice.Properties` and parse the `WAVEFORMATEX` blob, as the current code does.
- NAudio's `AudioSessionManager.OnSessionCreated` passes a raw `IAudioSessionControl` (wrap it yourself), on a COM thread. NAudio objects have finalizers that make COM calls, so dispose them explicitly.
- Keep the wrapper interfaces, or replacements for them, so the tests can keep mocking the audio layer. Models must not depend on NAudio types directly.

### B. New settings format (simpler versioning)
Replace `BinaryFormatter`, and its `[OptionalField]` / `[OnDeserialized]` versioning, with a readable format that has explicit versions.
- Direction: **JSON via `System.Text.Json`** with a top-level `"version": N` field. An ordered list of migration steps (`vN → vN+1`) works on the `JsonNode` DOM before the file is bound to the settings classes. Missing properties fall back to defaults and unknown ones are ignored, so most additions need no migration.
- Store hotkeys as readable `Key` / `ModifierKeys` names, not enum ints.
- **One-time import of the old file.** When `%LOCALAPPDATA%\Volumey\appconfig` (binary) exists and the new file doesn't, read the old file once, write the new one, then rename the old file to `appconfig.bak`.
  - The target is .NET 10, where `BinaryFormatter` throws `PlatformNotSupportedException`. Do the import with the unsupported compat package `System.Runtime.Serialization.Formatters` (used only for this one-time read), or with a custom reader for the old NRBF payload (`System.Formats.Nrbf`).
- Write the file atomically: temp file, then `File.Replace` or `File.Move` with overwrite. This also fixes the "not truncated on save" bug.
- Move persistence out of the static `SettingsProvider` into an injectable `ISettingsStore`, so tests can use an in-memory store.

### C. Explore UI frameworks that look closer to native Windows 11
The app uses ModernWpfUI 0.9.4. That version line is frozen, unsupported, and imitates the Windows 10 Fluent style.

**Decision (2026-10-08): the owner chose iNKORE.UI.WPF.Modern** because it already has stable releases. Check the latest stable version and its supported TFMs on NuGet before migrating. The other candidates are kept below for reference.

Candidates (state as of Oct 2026):
- **ModernWPF 1.x** (same NuGet id `ModernWpfUI`, `1.0.0-rc.1`, revived upstream at Kinnara/ModernWpf): the smallest step. It targets net462, net8 and net10. On `net10.0-windows` the stock controls use the platform WPF Fluent theme (Win11 look), and ModernWPF adds NavigationView, ContentDialog, `WindowBackdrop` (Mica/Acrylic on Win11 22H2+) and a WinUI-style `TitleBar`. There is an official 0.9→1.x migration guide; the API is not drop-in compatible.
- **WPF + the .NET 9+ built-in Fluent theme alone** (`Application.ThemeMode`): no third-party dependency, but there is no NavigationView or ContentDialog, and the API was still marked experimental.
- **iNKORE.UI.WPF.Modern** (a ModernWpf fork, v0.10.x, LGPL-2.1): Win11-styled, with `SettingsCard`/`SettingsExpander` (Win11 Settings-app look) and backdrops. Its migration from ModernWpf is close, but the namespaces differ.
- **WPF-UI (lepoco/wpfui)** (4.x, .NET 10, MIT, the most active WPF option): Win11 controls, Mica/Acrylic, NavigationView, a TitleBar, and a built-in `NotifyIcon` that could replace the vendored Hardcodet fork. Moderate migration, because its controls and theme dictionaries replace ModernWpf's, but the XAML and VM structure is kept.
- **WinUI 3 / Windows App SDK**: the real Win11 controls and Mica, and packaged MSIX fits the Store.
  - The views have to be rewritten.
  - There is no built-in tray icon (use H.NotifyIcon.WinUI).
  - Windowing for the flyout and OSD works differently, and there is no `DynamicResource`.
- **Avalonia + FluentAvalonia**: cross-platform, which this app doesn't need. Lowest priority.

For any choice:
- Re-evaluate the OSD (the `Notification.Wpf` fork) and the tray icon (the `Hardcodet` fork). Ideally the vendored DLLs go away.
- Prototype the main mixer flyout plus one settings page before migrating.
- Keep ViewModels framework-agnostic.

### D. Hotkeys without blocking them in Windows (keyboard hook)
`HotkeyManager` uses `RegisterHotKey`, which takes the key combo away from every other app. The planned option listens with a low-level hook instead, so keys still reach other apps.

Most of the code already exists:
- `Helper/HotkeyHookManager.cs` (`WH_KEYBOARD_LL`) implements `IHotkeyManager`.
- The toggle code (`BlockHotkeysInSystem`, `AllowDuplicateHotkeys`) is commented out in `ViewModel/SettingsViewModel.cs`.
- `ConvertHotkeysToNewManager` is commented out in `HotkeysControl`.

Things to get right:
- **Callback speed:** return from the hook callback fast. Windows silently removes hooks that exceed `LowLevelHooksTimeout` (about 1 s). Dispatch work asynchronously and never make COM audio calls inside the callback.
- **Message loop:** the thread that installs the hook must pump messages.
- **Key handling:** handle auto-repeat and left/right modifier variants.
- **Elevated windows:** the hook can't see input sent to elevated windows. Fixing that needs `uiAccess=true`, which Store apps can't use. Tell users about this limitation.
- **Duplicates:** combos used by more than one feature become possible, so define what `AllowDuplicates` means.
- **Switching managers at runtime:** move every registered hotkey, keeping the ref counts in `HotkeysControl`.
- **Security software:** some anti-cheat and security tools flag global keyboard hooks.

### E. Tests that don't depend on Visual Studio
Tests must run the same way from the CLI, CI, Rider and VS.
- **Done (2026-10-08):** the owner chose **MSTest**. The tests are on MSTest.Sdk 4.5.1 with the Microsoft.Testing.Platform runner (selected in `global.json`) on .NET 10, with all 54 passing. See *Build, run, test*.
- Put interfaces in front of the statics and singletons (`SettingsProvider`, `DeviceProvider.GetInstance()`, `HotkeysControl`, `App.Current.Dispatcher`) so the models become testable.
- Add a GitHub Actions workflow (`windows-latest`) running `dotnet build` + `dotnet test`.

## Machine setup
The owner installs tooling **manually**. Don't run installers (winget, the VS installer) yourself; report what's missing instead.

To build every project in the solution you need:
- **.NET 10 SDK.** It comes with current Visual Studio, or install it standalone.
- **Visual Studio** (Community is fine), current release. Workloads: **.NET desktop development** and **Windows application development**. Components: **MSIX Packaging Tools** and **Windows 11 SDK 10.0.26100**. These are only needed for `Volumey.Package.wapproj`. If you change the SDK version, update the wapproj's `TargetPlatformVersion` to match.
- Optional: the GitHub CLI `gh`.

State on 2026-10-08:
- Present: .NET SDK 10.0.401 (plus older SDKs back to 3.1), and VS Community 2026 (18.10.3) with the workloads and components above. The Windows SDK 10.0.26100 is installed under `D:\Windows Kits\10`. Everything builds: app, tests and MSIX.
- Leftover: the old VS 2022 17.4 Preview is still installed and can be removed.
- Missing: `gh` (optional).
- The signing cert has expired, so re-associate with the Store app before the next upload.
