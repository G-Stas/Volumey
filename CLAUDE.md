# Volumey — notes for agents

> **Rule: keep this file current.** Any change to app logic, architecture, build, packaging, dependencies, settings format or release process must update the matching section of this file in the same change. Remove anything that is no longer true, such as fixed known issues or finished plan steps.

Volumey is a Windows tray volume mixer (WPF). It sets up global hotkeys for per-app volume, device volume, mute, foreground-app volume and switching the default output device, and shows on-screen notifications. It ships through the **Microsoft Store** (package `58593StasG.Volumey`, Store ID `9mzcq03mx0s3`) and as a standalone zip on GitHub Releases. GPL-3.0. Author: Stas Geft (`G-Stas` on GitHub).

The last release is **v1.5.4.0** (May 2023), and `main` was last changed in June 2023 (Ukrainian localization).

**Git remotes:** `origin` → `G-Stas/Volumey` (the real repo). `colin` → `colinfindlay-nz/Volumey`, the fork this clone was first made from, which is stale and only kept for reference. Colin's "sync app volume to master volume" feature (`AppVolumeSyncViewModel`) is already merged into `main`.

## Current state (2026-10-09)
- **All 2.0 work is local and unpushed** (owner's decision). Don't push and don't suggest pushing, PRs or CI runs unless the owner asks; validate everything locally (see *Validating a step*).
- Local branches: `main` (= `origin/main`, released 1.5.4 + Ukrainian) and `release/2.0`. No work branch is open.
- `release/2.0` = `main` plus, in this order:
  1. MSTest instead of xUnit, and .NET 10 (app, tests, Store package).
  2. `feature/json-settings`: settings.json via Nucs.JsonSettings with a one-time 1.x import, plus the isolated dev run and UI inspection scripts.
  3. `chore/code-cleanup`: a warning-free build, the in-repo `ObservableConcurrentDictionary`, Store package build fixes, manifest languages, and a CI workflow file (inactive, since nothing is pushed).
  4. `feature/naudio`: the audio layer on NAudio.Wasapi 3.1.0 (item A), including a fix for a hang when a device is disabled.
- Validated on `release/2.0`: Release build, 144/144 unit tests and 8/8 audio integration tests, the Store MSIX build (contains `NAudio.*.dll`), and dev runs: mixer, settings pages, settings saved through the UI, app and device hotkey lists; app and device volume and mute in both directions (app ↔ Windows) with fast slider sweeps (no jump back); disabling and re-enabling a device; device rename; switching the default device from the app and from Windows.
- **Not yet verified at runtime on 2.0:** hotkeys, OSD popups, the tray menu, autostart, and an installed MSIX.
- **Open work branch `feature/sharphook-hotkeys`** (item D, not merged yet): non-blocking hotkeys through SharpHook plus the mode toggle. Done: Release build, 152/152 unit tests, 8/8 integration tests, the Store MSIX build (contains `SharpHook.dll` and `uiohook.dll`), and a dev run (toggle saves `Hotkeys.BlockInSystem`, the hook DLL loads, repeated switching and a start in hook mode work, clean log). **Still to check before merging:** pressing real hotkeys in both modes (keys reach other apps only in non-blocking mode), and switching modes with hotkeys registered. These need real key presses, so the owner does them.
- Item G (more tests) layers 1–3 are done; layer 4 (UI tests) comes before item C, later (owner's decision).

## Branching (decided 2026-10-08)
- **`main`**: the released 1.5.x line. Only hotfixes for the current Store/GitHub release go here.
- **`release/2.0`**: integration branch for the next major update (2.0). It collects all modernization work: NAudio, hook hotkeys, the new UI, and GitHub issue fixes.
- **Work branches**: branch off `release/2.0` with a descriptive name (e.g. `feature/naudio`, `fix/app-volume-sync-device-switch`). Merge them back locally with `git merge --no-ff`. Never commit feature work directly to `main`.
- **Before starting a new step, validate the branch state (owner's rule):**
  1. Check that the previous work branch is finished and merged into `release/2.0`; if not, validate it (build + `dotnet test`) and merge it first (`git merge --no-ff`).
  2. Create the new work branch from the current `release/2.0`, never stack it on an unmerged work branch.
  3. Delete merged work branches.
- Keep `release/2.0` buildable and green (`dotnet test`). If a hotfix lands on `main`, merge `main` into `release/2.0`.
- When 2.0 ships: bump the version (see *Release checklist*), merge `release/2.0` into `main`, and tag `v2.0.0.0`.

### Validating a step (before merging into `release/2.0`)
1. `dotnet build Volumey.Tests\Volumey.Tests.csproj -c Release -p:Platform=x64 --no-incremental`: no errors, and no warnings except the known log4net NU1902.
2. `dotnet test --project Volumey.Tests\Volumey.Tests.csproj -c Release -p:Platform=x64`: all tests pass. When the audio layer or models change, also run the audio integration tests: `dotnet test --project Volumey.IntegrationTests\Volumey.IntegrationTests.csproj -c Release -p:Platform=x64` (needs an output device; they only change their own session's volume).
3. **Build the Store MSIX** with VS MSBuild (command under *Build, run, test*) whenever project files, packages or the manifest change. A plain `dotnet build` doesn't cover the packaging project, and that's how a broken package build once went unnoticed.
4. Smoke-test the changed area in a dev run (`scripts/run-dev.ps1`, `scripts/ui-inspect.ps1`).
5. Update this file, commit, merge with `--no-ff`, and delete the work branch.

## Repo layout

```
Volumey.sln
Volumey/                 WPF app (net10.0-windows10.0.19041.0, WinExe, entry point Volumey.Startup.Main)
  Startup.cs             single-instance mutex, args, log4net init, Store activation check
  App.xaml(.cs)          global resources; instantiates the 4 root ViewModels as resources
  CoreAudioWrapper/
    CoreAudio/           IPolicyConfig + ERole: the only hand-written COM interop left (set the default device)
    Wrapper/             wrapper over NAudio: DeviceEnumerator, AudioDevice (IDevice), AudioModelFactory (builds models),
                         AudioSessionProvider, *Notifications/*Handler (NAudio events -> C# events), SessionEchoFilter,
                         DeviceFormat, PolicyClient, GuidValue
  DataProvider/          DeviceProvider (singleton, active/default devices), SettingsProvider (settings store access),
                         Settings/ (settings.json via Nucs.JsonSettings: VolumeySettings + sections, legacy 1.x importer),
                         ScreenInfoProvider
  Model/                 OutputDeviceModel, MasterSessionModel, AudioProcessModel, AudioSessionModel, SystemHotkey, ScreenInfo
  ViewModel/             AppBehaviorViewModel, DeviceProviderViewModel, SettingsViewModel, ThemeViewModel
    Settings/            one VM per settings feature + HotkeysControl (static hotkey hub) + notification mediator
  View/                  MainView (window + tray), MixerView/MasterView/SessionsListView, SettingsView + SettingsPage/*,
                         Controls/ (HotkeyBox, VolumeSlider...), Converters/, Styles/, Localization/
  Helper/                NativeMethods (P/Invoke), HotkeyManager / SharpHookHotkeyManager, NotificationManagerHelper,
                         SystemIntegrationHelper (autostart/start menu), UpdateHelper, IconHelper...
  Resources/             Resources*.resx (localization), icons, and 2 vendored DLLs (see Dependencies)
  log4net.config         Loggly appender (Release/Store only)
Volumey.Tests/           MSTest + Moq unit tests: models, ViewModels (ViewModels/ViewModelTestBase), converters, settings, localization
Volumey.IntegrationTests/ MSTest tests against real Windows audio (own session playing silence through NAudio)
Volumey.Package/         Windows Application Packaging project (.wapproj): MSIX for the Store
LatestUpdateInfo.xml     AutoUpdater.NET feed for the non-Store build (read from raw.githubusercontent main)
scripts/                 run-dev.ps1 (isolated debug run), ui-inspect.ps1 (UI Automation: screenshot, tree, click)
global.json              pins SDK 10.0.100+ (latestFeature) and selects the Microsoft.Testing.Platform runner for `dotnet test`
```

## Build, run, test

Configurations: `Debug`, `Release`, `Store`. The only platform is `x64`, with no AnyCPU, so pass `-p:Platform=x64`.

```powershell
# app + tests (needs the .NET 10 SDK)
dotnet build Volumey.Tests\Volumey.Tests.csproj -c Debug -p:Platform=x64
dotnet run --project Volumey\Volumey.csproj -c Debug -p:Platform=x64
dotnet test --project Volumey.Tests\Volumey.Tests.csproj -c Debug -p:Platform=x64   # SDK 10 + MTP syntax: --project is required
dotnet test --project Volumey.IntegrationTests\Volumey.IntegrationTests.csproj -c Debug -p:Platform=x64   # needs an output device

# Store MSIX (needs VS MSBuild, not dotnet build). Drop the signing override once a valid cert is set up.
& "$(& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath)\MSBuild\Current\Bin\amd64\MSBuild.exe" `
  Volumey.Package\Volumey.Package.wapproj /restore /p:Configuration=Store /p:Platform=x64 /p:AppxBundle=Never `
  /p:UapAppxPackageBuildMode=StoreUpload /p:AppxPackageSigningEnabled=false
```

- **Release/Store builds share the single-instance mutex with an installed Volumey:** while it runs, a second release build just signals it and exits. Debug builds have their own mutex (see below).
- Tests use **MSTest 4** via `<Project Sdk="MSTest.Sdk/4.5.1">`, on the **Microsoft.Testing.Platform** runner: the test project builds to a standalone exe. They run from the CLI, Rider or VS. The unit suite has 152 tests, all passing. Line coverage of the app is about 42% from unit tests and about 49% together with the integration tests (2026-10-09; the vendored tray DLL is excluded). `.github/workflows/build.yml` (`windows-latest`, build + test) is prepared, but it doesn't run because nothing is pushed; the Store package couldn't be built there anyway, because it needs Visual Studio MSBuild. The app has `InternalsVisibleTo Volumey.Tests`, and the tests reference the vendored `Notification.Wpf.dll` directly.
  - **Audio integration tests** (`Volumey.IntegrationTests`, category `Integration`, 8 tests): a separate project because they run a real WPF `Application` on its own UI thread (`AudioTestEnvironment`), as the app does, so model callbacks marshal to a real dispatcher; that would change how the unit tests behave. The test process plays silence (`TestAudioSession`, NAudio `WasapiPlayer`) to get its own session and uses that session's volume as "the Windows mixer". Device volumes are only read. Without an output device the tests are inconclusive; CI runs only `Volumey.Tests`. Windows remembers the test executable's session volume and mute between runs, so `TestAudioSession` resets them.
  - The test project must use the same TFM as the app, and it needs `UseWPF`.
  - Write tests with `[TestClass]`, `[TestMethod]` and `Assert.AreEqual`/`IsTrue`/`Contains`/`ContainsSingle`. Test methods must be public, non-static instance methods, or MSTest skips them silently with a UTA007 discovery warning. Use `[STATestMethod]` for tests that need a WPF/STA thread.
  - **Coverage** comes with MSTest.Sdk, no tool needed: `dotnet test --project Volumey.Tests\Volumey.Tests.csproj -c Debug -p:Platform=x64 --coverage --coverage-output-format cobertura --coverage-output <file>`. Read the `Volumey` package's `line-rate`; the `Hardcodet.NotifyIcon.Wpf` package is the vendored tray DLL.
  - Tests that drive the UI with the keyboard or mouse (layer 4 of item G, not written yet) must stay out of the normal run, e.g. in a separate category or project, and only run on demand.
  - `PlatformTarget` is always x64 and the `RuntimeIdentifier` is win-x64, because the app's `RuntimeIdentifier` is win-x64 on every platform.
- `Volumey.Package.wapproj` (MSIX) does **not** build with `dotnet build`. It needs Visual Studio MSBuild with the "Windows application packaging" (DesktopBridge) targets and the Windows SDK (`TargetPlatformVersion` 10.0.26100.0, `TargetPlatformMinVersion` 10.0.18362.0). Use the VS menu *Project → Publish → Create App Packages* or the command above.
  - The `Store` configuration of the app is **self-contained**, so the MSIX carries the .NET 10 runtime (about 73 MB). Without that, packaging fails with NETSDK1112 (runtime pack not downloaded). `Debug`/`Release` are framework-dependent: the GitHub zip build needs the **.NET 10 Desktop Runtime** on the user's machine.
  - The source `Package.appxmanifest` lists 10.0.18362 / 10.0.26100 as min/tested versions, but the build overwrites them anyway with the wapproj's `TargetPlatformMinVersion`/`TargetPlatformVersion`. The package isn't trimmed (WPF doesn't support trimming).
- Signing: the wapproj references the cert thumbprint `4C35C87C…3F96` (`Volumey.Package_TemporaryKey.pfx`, subject = the Store publisher ID). That cert **expired 2023-12-13**. For a Store upload, re-associate the project with the Store app in VS. VS then makes a fresh temporary cert, and the Store re-signs the package anyway.
- Debug builds wire up **no logging** by default (`Startup.PathToDebugLogConfig` is empty), and the try/catch around `App.Main` is compiled out, so exceptions crash into the debugger. Set `VOLUMEY_LOG_FILE` (done by `scripts/run-dev.ps1`) to log to a file.

### Running and inspecting the app (for agents too)
Use the scripts in `scripts/` to run and look at the app without the owner's help:
- **`./scripts/run-dev.ps1 [-Reset] [-ImportRealSettings] [-NoBuild] [-Minimized]`** builds and starts the Debug build. It prints the PID and the paths of the settings and log files.
  - **Isolated from the installed app:** Debug builds use their own single-instance mutex and `WM_SHOWME` message (suffix `-dev`, see `Startup.InstanceSuffix`), so they run next to an installed release build.
  - **Hotkey clash:** a hotkey can be registered by only one of the two apps at a time.
  - **Settings:** they go to `-DataDir` (default `%TEMP%\Volumey-dev`) through `VOLUMEY_SETTINGS_DIR`, never the real `%LOCALAPPDATA%\Volumey`.
  - **Importing:** `-ImportRealSettings` copies the real 1.x `appconfig` into the data folder (read-only) so the importer runs on real data.
  - **Logging:** goes to `<DataDir>\volumey.log` through `VOLUMEY_LOG_FILE`, including unhandled exceptions (Debug builds otherwise have no logging and crash on errors).
  - **Restarting:** a running dev instance is stopped first, because it locks the exe.
- **`./scripts/ui-inspect.ps1`** drives the running dev build through Windows UI Automation:
  - `-Show` brings the window up.
  - `-Screenshot <png>` captures the app's windows; read the PNG to look at the UI.
  - `-Tree [-Depth n]` dumps controls with their name, automation ID and state (toggle, value, selected).
  - `-Click <name|automationId>` invokes, toggles or selects a control.
  - **Add `-Mouse`** where the app reacts to real clicks, not to selection. That includes the ModernWpf NavigationView items, e.g. `-Click SettingsItem -Mouse`, and the settings page links. `-Mouse` moves the owner's cursor.
  - `-At x,y` clicks with the real mouse at a position relative to the main window (for unnamed controls such as combo boxes; find the position on a `-Screenshot` first). It refuses to click when no window is visible. Combo box drop-downs are separate popup windows; `-Screenshot` includes them. **Never click at raw screen coordinates**: if the window has hidden itself, the click lands on another app. The Escape key and losing focus can hide the window; bring it back with `-Show`.
- **Checking a change:** change it through the UI, then read `settings.json` in the data folder. Look for errors in `volumey.log`, and in the Windows Application event log (`.NET Runtime`) after a crash.
- **Don't simulate volume hotkeys** unless the owner agrees: they change real app and device volumes.

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
Built on **NAudio.Wasapi 3.x** (`NAudio.CoreAudioApi`). NAudio 3 uses source-generated COM (`ComWrappers`); its raw COM interfaces are internal, so the app only uses NAudio's wrapper classes. Only `Volumey/CoreAudioWrapper/Wrapper/` touches NAudio types: models and VMs use the wrapper interfaces and the app's own types (`DeviceFormat`, `AudioSessionState`, `AudioSessionDisconnectReason`).
- `DeviceEnumerator` wraps NAudio's `MMDeviceEnumerator`. It builds an `OutputDeviceModel` for every active render endpoint through `AudioModelFactory.CreateOutputDeviceModel`, which reads the device's `AudioSessionManager.Sessions` into `AudioProcessModel`s.
- `AudioDevice` (`IDevice`) wraps NAudio's `MMDevice` and **owns** it: `OutputDeviceModel.Dispose` disposes it **on the thread pool**, which also releases its endpoint volume and session manager. A COM release waits for running audio callbacks, and those may wait for the UI thread, so never release device objects on the UI thread (it hung the app when a device was disabled). NAudio keeps a device's property store open, and an open store doesn't show later changes, so `AudioDevice` reopens the store for each property read (name, description, icon, format).
- `DeviceStateNotificationsHandler` uses NAudio's `MMDeviceNotificationClient` (`CreateNotificationClient`, without a synchronization context) and raises DefaultDeviceChanged, ActiveDeviceAdded, DeviceDisabled, NameChanged, IconPathChanged and FormatChanged. NAudio raises them on **COM threads**; the handler passes every event to the UI thread with `Dispatcher.InvokeAsync` and **never waits** on the COM thread (a waiting callback deadlocks with a device release on the UI thread). Handlers downstream therefore run on the UI thread.
- `MasterSessionModel` is the endpoint volume (`AudioEndpointVolume` + `OnVolumeNotification`). NAudio raises that event on the `SynchronizationContext` that was current when the endpoint volume was created (the UI thread in practice), otherwise on a COM thread.
- `AudioProcessModel` groups one or more `AudioSessionModel`s that share a PID. A session is NAudio's `AudioSessionControl` + `SimpleAudioVolume` + an `IAudioSessionEventsHandler` (`AudioSessionStateNotifications`, which owns and disposes the session control). `AudioSessionProvider` (`AudioSessionManager.OnSessionCreated`, on a COM thread) adds sessions as they are created (`SessionCreatedEventArgs.CreateProcessModel`). Sessions are grouped into processes by PID and also by the session's grouping parameter. Outside `OutputDeviceModel`, read the processes through `GetImmutableProcessesAsync()`, not the live collection.
- **Echo suppression** (skipping callbacks caused by the app's own writes). Keep it in any rewrite.
  - **Master:** writes pass an event context through `AudioEndpointVolume.NotificationGuid`: `GuidValue.Internal.VolumeGUID` for UI changes (the callback skips them), `GuidValue.Internal.Empty` for hotkey changes so that notifications still show.
  - **Sessions:** NAudio writes session volume with an empty context and drops the context from session events. `SessionEchoFilter` remembers the state (volume + mute) each own write leads to, for 1 s, and the session callback skips a matching report. A matched state stays an echo for the whole second, because **Windows can deliver the same session event several times** to one handler (found by the integration tests: removing a match after the first copy let a repeated volume echo un-mute the app). The `ref Guid context` of `AudioSessionVolume.SetVolume` is ignored.
  - Both callbacks also skip reports that don't match the current system state (outdated callbacks).
- `PolicyClient` sets the default device through the **undocumented** `IPolicyConfig` COM interface (classic `[ComImport]` interop in `CoreAudioWrapper/CoreAudio/`, not in NAudio), for all three roles (Console, Multimedia, Communications).
- Wrapper interfaces (`IDevice`, `IAudioSessionVolume`, `IAudioSessionStateNotifications`, `ISessionProvider`, `IDeviceStateNotificationHandler`, `IMasterVolumeNotificationHandler`, `IDeviceEnumerator`) exist so the tests can mock the audio layer with Moq.

### Hotkeys
- `HotkeysControl` (static) holds the one `IHotkeyManager`. It ref-counts hotkeys (`HotkeysUserCount`) so that several features can share a combo, raises `HotkeyPressed`, and validates hotkeys (no bare F12, no duplicates via `SettingsProvider.HotkeyExists`).
- Two hotkey managers behind `IHotkeyManager`; the setting `Hotkeys.BlockInSystem` (default `true`, toggle "Block registered hotkeys in the OS" on the Misc settings page, `SettingsViewModel.BlockHotkeysInSystem`) picks one:
  - **Blocking** (`HotkeyManager`, the app's own code): `RegisterHotKey`/WM_HOTKEY. Other apps don't get the combo; registering fails if another app owns it. Works with elevated windows in front.
  - **Non-blocking** (`SharpHookHotkeyManager`, SharpHook `EventLoopGlobalHook`, keyboard only): only listens, so other apps still get the keys. Registering never fails (a hook can't detect conflicts). It **doesn't see keys while an elevated (administrator) window is in focus** (the settings page says so). Key events arrive on the hook's event-loop thread; the manager only matches them there (`ToHotkey`: raw code = virtual-key code, `EventMask` → `ModifierKeys`, left/right modifiers alike, modifier keys alone ignored) and raises `HotkeyPressed` on the UI thread. Auto-repeat fires repeatedly, like `RegisterHotKey`.
  - Switching at runtime: `HotkeysControl.SetHotkeyManager` moves the registered hotkeys to the new manager (a combo that fails to register there is logged and skipped), disposes the old one, keeps the user counts, and doesn't raise `Activated` again.
  - `HotkeysControl.IsEditingHotkey` (set by `HotKeyBox` while it has focus) ignores hotkeys, so typing a registered combo into a hotkey box doesn't trigger it in non-blocking mode.
  - Duplicates are rejected in both modes, as before. SharpHook brings the native `uiohook.dll` (copied next to the exe and into the MSIX).
- Models subscribe to `HotkeysControl.HotkeyPressed` and handle their own up/down/mute hotkeys (`SetVolumeHotkeys`, `SetMuteHotkey`). App hotkeys are stored by **process name** (`VolumeHotkeysPair`) and get applied when a matching process shows up. Default-device hotkeys are stored as a `DeviceInfo` (ID + name), and an active device matches if its ID **or** its name matches, after which the stored info is refreshed.

### Settings persistence
Settings are a JSON file managed by **Nucs.JsonSettings** (`DataProvider/Settings/`). They're composed of sections.
- **File:** `%LOCALAPPDATA%\Volumey\settings.json`. The Store build uses the same path, because MSIX redirects LocalAppData to the package folder.
  - The environment variable **`VOLUMEY_SETTINGS_DIR`** overrides the folder. Use it to run a dev build without touching the real settings.
- **Classes:**
  - `VolumeySettings` (root, `NotifiyingJsonSettings`, `[Autosave]`) has the sections `General`, `Window`, `Hotkeys` and `Notifications`. Each section is a `SettingsSection` (`INotifyPropertyChanged`) marked `[NotifyChanges]`.
  - Access them through `SettingsProvider.Settings`, e.g. `SettingsProvider.Settings.General.VolumeStep = 5`. There is no Save call.
- **How saving works:**
  - The `[Autosave]`/`[NotifyChanges]` attributes are woven into the setters **at build time** (AspectInjector, via the `Nucs.JsonSettings.Autosave`/`.NotifyChanges` packages).
    - **The weaving build step is inherited by every project that references the app** (the packages use `buildTransitive`). `Volumey.Tests`, `Volumey.IntegrationTests` and `Volumey.Package` therefore set `<AspectInjector_Enabled>false</AspectInjector_Enabled>`. Without it, the Release test build fails while reading PDBs, and the MSIX build fails trying to weave the package project. Do the same in any new project that references `Volumey`. The packaged `Volumey.dll` must still be woven; it contains the marker `__a$_instance`.
  - The library's `NotificationBinder` listens to each section's `PropertyChanged` and saves the whole file.
  - `[NotifyChanges]` only raises `PropertyChanged` when the value actually changes, and it is **not inherited**, so put it on every new section class.
  - **Items inside lists are not watched:** always assign a new list (`Hotkeys.Apps = newList`). Never mutate the list you got.
  - VMs react to setting changes by subscribing to the section's `PropertyChanged`, e.g. `Settings.Notifications.PropertyChanged`.
- **JSON format:**
  - Readable and indented, with enums stored by name (`StringEnumConverter`).
  - `HotKey` is stored as `{ "Key": "F1", "ModifierKeys": "Control, Shift" }`; Json.NET deserializes it through its constructor.
  - `TypeNameHandling.None` means no .NET type names are written, so renaming or moving C# types is safe; renaming *properties* is not.
- **Threading:** settings are also written from background threads. `VolumeySettings.Save` takes a lock so saves can't overlap. `PropertyChanged` fires on the writing thread.
- **Versioning:** `VolumeySettings.Version` / `CurrentVersion`.
  - Adding a property needs nothing, because missing values keep their defaults.
  - Renaming, moving or retyping a property needs a bumped `CurrentVersion` and a step in `SettingsProvider.Migrate`. To read a removed or renamed value there, add a `[JsonExtensionData]` dictionary to the affected class.
  - The library's own versioning module only resets or renames the file on a mismatch, so it isn't used.
- **Corrupt file:** `RecoveryAction.RenameAndLoadDefault` renames it to `settings.<n>.json` and loads defaults. If loading fails for another reason, the app logs it and runs on unsaved defaults.
- **Window position:** `Window.Left`/`Top` are kept in `AppBehaviorViewModel` and only written in `OnExit`, because they change on every window move.
- **Import from 1.x (`LegacySettingsImporter`):** on the first start (no `settings.json` yet), the importer reads the old BinaryFormatter file `appconfig` once.
  - It parses the file as plain data with **`System.Formats.Nrbf`**, which is part of .NET 10, so no old types and no BinaryFormatter are needed.
  - The member names in the importer are the old `AppSettings` field names (e.g. `currentAppTheme`, `serializableRegisteredSessions`, `<MusicAppName>k__BackingField`) and must stay as they are.
  - It also converts the oldest single-app hotkey format (`MusicAppName`).
  - **`appconfig` is never modified or renamed**, so a downgrade to 1.x keeps its settings.
  - Not imported, because nothing read them: `userHasRated`, `firstLaunchDate`, `launchCount`, `rememberLastPosition`.
- **Tests:**
  - `TestAssemblySetup` uses a plain `new VolumeySettings()`, which has no file and no autosave, so tests never touch the machine's settings.
  - `VolumeySettingsTests` cover autosave of nested sections, the JSON format, notifications and recovery.
  - `LegacySettingsImporterTests` use synthetic 1.x files in `Volumey.Tests/TestData/`, generated with the real 1.5.4 `AppSettings` class and BinaryFormatter. Don't commit a real user's `appconfig`: it contains device names and IDs.

### UI
- ModernWpfUI 0.9.4 (Fluent-ish controls, NavigationView, ContentDialog). The theme comes from `ThemeViewModel` plus the `Colors.Light/Dark.xaml` + `Brushes.xaml` theme dictionaries, with the Windows accent color from `SystemColorHelper`.
- On-screen volume popups use the `Notification.Wpf` fork via `NotificationManagerHelper`. `AudioProcessStateNotificationMediator` lets models request a popup without referencing the UI.
- Multi-monitor placement uses `WpfScreenHelper` (`ScreenInfoProvider`).
- Localization: `Resources/Resources.<lang>.resx` + `TranslationSource` (live culture switching) + the `{loc:Localization Key}` markup extension. Languages: en, ru, de, it, fr, es, pt, ja, zh, hi, tr, uk-ua. `Resources.resx` (neutral) generates `Resources.Designer.cs`. When you add a string, add it to `Resources.resx` and to every `.<lang>.resx`. To add a language, add the resx file and add the culture to both lists in `TranslationSource`. Also add the language to `Package.appxmanifest` `<Resources>` (`uk-ua` is declared there as `uk`).

## Dependencies
| Package | Purpose | Notes |
|---|---|---|
| ModernWpfUI 0.9.4 | UI controls/theme | Frozen 0.9.x line; to be replaced by iNKORE.UI.WPF.Modern (item C) |
| Hardcodet.NotifyIcon.Wpf (**vendored DLL**, author's fork `G-Stas/wpf-notifyicon`) | tray icon | `Resources/` binary, not NuGet. To be replaced by `H.NotifyIcon.Wpf` (item F) |
| Notification.Wpf (**vendored DLL**, fork `G-Stas/Notification.Wpf`) | OSD popups | `Resources/` binary, not NuGet |
| Autoupdater.NET.Official 1.8.2 | non-Store updates | 1.9.x is available; works on .NET 10 (update dialog verified) |
| log4net-loggly 9.0.0 | remote logging to Loggly | **Loggly input key is committed in `log4net.config`** |
| log4net 2.0.17, Newtonsoft.Json 13.0.4 (direct refs) | override the vulnerable transitive versions from log4net-loggly (2.0.8 / 10.0.3) | log4net 2.0.17 still has one moderate advisory (NU1902, GHSA-4f7c-pmjv-c25w). It's fixed in log4net 3.x, but log4net-loggly was built against 2.x, so test logging before bumping |
| — | `ObservableConcurrentDictionary` | In-repo `Helper/ObservableConcurrentDictionary.cs` (WPF-bindable; raises a reset on the creating thread's context). Replaced the .NET Framework-only `MSFT.ParallelExtensionsExtras` package |
| NAudio.Wasapi 3.1.0 (+ NAudio.Core) | audio devices, endpoint volume, sessions | MIT, active (3.1.0 Sep 2026). Targets `net9.0`, AOT-compatible, source-generated COM. 3.x made the raw COM interfaces internal |
| Nucs.JsonSettings 2.3.2 (+ .Autosave, .NotifyChanges) | settings.json, autosave, change notifications | MIT, active (Aug 2026). Build-time weaving via AspectInjector; built on Newtonsoft.Json (already referenced) |
| SharpHook 8.0.0 | non-blocking hotkeys (keyboard hook) | MIT, active (Aug 2026). Ships native `uiohook.dll` (win-x64), included in the MSIX. Mouse hooks are available for later (issues #31, #70, #93) |
| Microsoft.Xaml.Behaviors.Wpf, WpfScreenHelper | misc | |

WinRT APIs (`StoreContext`, `AppInstance`, `Package`) come from the `-windows10.0.19041.0` TFM; the `Microsoft.Windows.SDK.Contracts` package is gone. `System.Drawing` comes from the Windows Desktop framework. Code analyzers come with the SDK, so there's no NetAnalyzers package.

## GitHub issues (checked 2026-10-08)
42 issues are open. The owner wants to finish the modernization before working through them. Only #18, #47 and #75 were read in full; for the rest, only the titles are known, so **read the issue before assuming its cause**. Titles can be misleading:
- **#18** ("device hotkeys not visible after restart"): the hotkeys are saved and work. The *Change default device by hotkey* page just hides them in a collapsed combo box with no selection, while the hotkey box shows the default F4. This is a UI problem, reproduced in a dev run; fix it with the new UI or with a quick pre-selection.
- **#47 / #75** ("save settings across reboots" / "settings suddenly resets"): these are about **app volumes** that apps or Windows reset (e.g. Discord back to 100%), not about Volumey's settings. Volumey never stored app volumes. This is a possible new feature: remember and restore app volumes, together with the presets request #84.
- Grouped by title:
  - **Crashes** when an audio device is removed (#71, #73), after sleep (#57), in games (#58) or with many apps (#66); the app stops randomly (#88); a memory leak (#87).
  - **OSD position and multi-monitor:** #65, #76, #91, #92.
  - **Hotkeys in games:** #46, #63; these may benefit from item D.
  - **Feature requests:** per-app mute hotkeys (#59, #64, #86), hiding apps or System Sounds (#67, #89), non-default devices (#17, #77), per-app output device (#14), mouse wheel and tray (#70, #93), and more.

## Known issues / gotchas found during analysis
- **Dev builds use the real settings** unless `VOLUMEY_SETTINGS_DIR` is set. After the first 2.0 start, later changes made in 1.x aren't imported again; delete `settings.json` to re-import.
- Several `async void` handlers and `Task.Run` fire-and-forget calls (the build is warning-free, but exceptions there are lost or crash the app).

## Modernization plan (owner's intent)
The owner is coming back to the project to work through the GitHub issues and modernize the app. Planned work items are below. **Constraints for all of them:** the Store package identity (`58593StasG.Volumey`), publisher, and StartupTask `TaskId` must stay the same, and existing users' settings must survive the upgrade.

### Suggested order
Each step should leave a working, shippable build.
1. ~~Universal tests + green baseline (item E).~~ Done: MSTest.
2. ~~Retarget to **.NET 10**~~. Done (2026-10-08): the app and tests are on `net10.0-windows10.0.19041.0`, and the Store MSIX builds. .NET 10 is LTS, supported until Nov 2028. Don't target .NET 11 until it ships as stable, and only move to it if the owner asks.
   - **Verified (2026-10-08, Debug build):** startup, mixer, settings pages, theme, update check, and settings save through the UI. **Still to verify:** hotkeys, OSD popups, autostart, the tray menu, and the installed MSIX. The vendored DLLs and ModernWpfUI 0.9.4 were built for older .NET.
   - ~~Code cleanup~~ Done (2026-10-08): no compiler warnings, the .NET Framework-only package replaced, package build fixes, manifest languages.
3. ~~New settings format (item B).~~ Done (2026-10-08): settings.json via Nucs.JsonSettings, see Settings persistence. The new settings don't depend on `Notification.Wpf` types anymore (the position is stored by name).
4. ~~Audio layer → NAudio (item A).~~ Done (2026-10-09).
5. Hotkeys through a library, with a non-blocking mode (item D). Implemented on `feature/sharphook-hotkeys`, runtime check by the owner pending.
6. Tray icon on the current NotifyIcon library (item F).
7. UI framework (item C). This is the biggest and riskiest change, so do it last, on top of a cleaner architecture.

**Inserted 2026-10-09:** more tests (item G), layers 1–3 before item D, layer 4 (UI tests) before item C.

### A. Replace the hand-written CoreAudio wrapper with NAudio
**Done (2026-10-09)** with NAudio.Wasapi 3.1.0; see *Audio layer* for how it works. Decisions:
- **NAudio 3.x, not 2.x:** current line, targets `net9.0`. In 3.x the raw COM interfaces are internal, so a separate interop path for session volume (to keep the event context) wasn't possible. Session echo suppression now compares states instead (`SessionEchoFilter`).
- Kept from the old code: `PolicyClient` + `IPolicyConfig.cs` (not in NAudio), and the wrapper interfaces (without their COM base interfaces), so the tests still mock the audio layer and models don't see NAudio types.
- The device format is read from the `PKEY_AudioEngine_DeviceFormat` blob (NAudio returns `byte[]`) into `DeviceFormat`.
- The app's own `AudioSessionState` enum had Active and Inactive swapped; the new enum matches Core Audio (only stored, never compared).

### B. New settings format
**Done (2026-10-08).** The owner chose a plain JSON file with a ready-made library (Nucs.JsonSettings) and a composed settings object (root + sections). A LiteDB key-value store was tried first and dropped. See *Settings persistence*.

Possible follow-up: inject `VolumeySettings` (or its sections) into the ViewModels and models instead of the static `SettingsProvider.Settings`, as part of the general "fewer statics" work in item E.

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
- Re-evaluate the OSD (the `Notification.Wpf` fork); ideally that vendored DLL goes away too. The tray icon is handled separately in item F.
- Prototype the main mixer flyout plus one settings page before migrating.
- Keep ViewModels framework-agnostic.

### D. Hotkeys through a library, including non-blocking ones
`HotkeyManager` uses `RegisterHotKey`, which takes the key combo away from every other app. The owner wants a **non-blocking mode** (a low-level hook that only listens, so keys still reach other apps), and wants **hotkeys handled by a library instead of the app's own code**, at least for the non-blocking mode. Decided 2026-10-08.

Candidates (state as of Oct 2026):
- **SharpHook** (`SharpHook` 8.0.0, Aug 2026, MIT, actively maintained): the preferred option for non-blocking hotkeys.
  - Global keyboard **and mouse** hooks, built on the native libuiohook. Mouse buttons and the wheel are covered too, which fits issues #31 (mouse buttons as hotkeys) and #70/#93 (mouse wheel).
  - `EventLoopGlobalHook` runs the handlers on its own thread and queues events, so slow handlers don't get the hook removed by Windows. Run it with `RunAsync()`. Only one global hook should exist per process.
  - It can also suppress events on Windows (`SuppressEvent`, only synchronously with `SimpleGlobalHook`). That's not the same as `RegisterHotKey`, so evaluate it before using it for blocking mode.
  - It ships a native `uiohook.dll`, which has to end up in the MSIX and the zip.
- **NHotkey.Wpf** (4.0.0, Jan 2026): a small wrapper around `RegisterHotKey`. It's a candidate for the **blocking** mode if the own `HotkeyManager` should go too; otherwise keep `HotkeyManager` for blocking mode.
- Not recommended: H.Hooks (last release Dec 2024), MouseKeyHook (Apr 2023), GlobalHotKeys (2017).

**Implemented on `feature/sharphook-hotkeys` (2026-10-09)**, see *Hotkeys*. Owner's decision: keep the own `HotkeyManager` for blocking mode (SharpHook can only swallow keys from its hook, which isn't the same as `RegisterHotKey`: no conflict check, blind to elevated windows), SharpHook for non-blocking mode. The old `HotkeyHookManager` and its P/Invokes are deleted. `AllowDuplicates` was dropped: duplicates stay rejected in both modes.

Things to get right:
- **Callback speed:** Windows silently removes low-level hooks that respond too slowly (`LowLevelHooksTimeout`, about 1 s). Never make COM audio calls on the hook thread; dispatch the work.
- **Elevated windows:** a hook can't see input sent to elevated windows (UIPI). Fixing that needs `uiAccess=true`, which Store apps can't use. `RegisterHotKey` doesn't have this problem, which is one more reason to keep blocking mode. Tell users about this limitation.
- **Duplicates:** combos used by more than one feature become possible in non-blocking mode, so define what `AllowDuplicates` means.
- **Switching managers at runtime:** move every registered hotkey, keeping the ref counts in `HotkeysControl`.
- **Security software:** some anti-cheat and security tools flag global hooks. This may matter for the game-related issues #46, #58 and #63.

### E. Tests that don't depend on Visual Studio
Tests must run the same way from the CLI, CI, Rider and VS.
- **Done (2026-10-08):** the owner chose **MSTest**. The tests are on MSTest.Sdk 4.5.1 with the Microsoft.Testing.Platform runner (selected in `global.json`) on .NET 10. See *Build, run, test*.
- Put interfaces in front of the statics and singletons (`SettingsProvider`, `DeviceProvider.GetInstance()`, `HotkeysControl`, `App.Current.Dispatcher`) so the models become testable.
- GitHub Actions workflow: `.github/workflows/build.yml` exists but is inactive, because the work isn't pushed.

### F. Tray icon: switch to the current NotifyIcon library
The tray icon uses a **vendored DLL** built from the owner's fork of Hardcodet (`G-Stas/wpf-notifyicon`, `Volumey/Resources/Hardcodet.NotifyIcon.Wpf.dll`). The owner wants the current library from NuGet instead. Decided 2026-10-08.
- **Preferred: `H.NotifyIcon.Wpf`** (2.4.1, Dec 2025, MIT). It's the maintained continuation of Hardcodet.NotifyIcon.Wpf and keeps its API: `TaskbarIcon`, `TrayToolTip`, `TrayPopup`, `ContextMenu`, commands. On top of that it has native context menus with dark/light theming, Windows 11 Efficiency Mode, icons generated from text, and NativeAOT/trimming support. There's also a WinUI package, should the UI ever move there.
- Alternative: the official `Hardcodet.NotifyIcon.Wpf` 2.0.1 (Oct 2024), an almost drop-in replacement but much less active.
- **Before switching**, diff the owner's fork against its upstream to find what the fork changed, and keep that behavior. Known areas: the Win11 tooltip workaround in `MainView.SetTrayIconTooltip` (plain text tooltip on Windows 11) and the tray context menu.
- H.NotifyIcon by default derives the tray icon's ID from the exe path. For the Store build the path changes with every version, so Windows could forget "always show this icon" after updates. Check how it behaves with MSIX; the library supports a fixed ID.
- This step is independent of the UI framework (iNKORE stays on WPF), so it can be done before item C. Afterwards, remove the vendored DLL and its `<Reference>`/`<EmbeddedResource>` entries from `Volumey.csproj`.

### G. More tests (owner's request, 2026-10-09)
Coverage before: about 20% of the app's lines. The owner wants many more tests. Plan, one work branch per layer:
1. **Plain unit tests** (no code changes). **Done** (`test/unit-basics`): converters, `HotKey`/`HotKeyBox`, `SystemHotkey`, localization, settings loading (`SettingsProvider.LoadSettings(dir)`: first launch, legacy import, reload). Found and fixed: a saved Ukrainian language ("uk-UA" vs "uk-ua") and system languages such as "uk" or "zh-Hans" fell back to English on start.
   - Not tested here, because they need the app's resources (`App.Current.FindResource`): the tray icon providers and `ButtonIconThemeConverter`.
2. **ViewModel tests.** **Done** (`test/viewmodels`). The settings VMs that use devices got `internal` constructors taking `IDeviceProvider` (the public ones stay for XAML). Tests derive from `Volumey.Tests/ViewModels/ViewModelTestBase`, which gives each test fresh settings, a reset `HotkeysControl` (`HotkeysControl.Reset()`, tests only), a fake `IHotkeyManager` and a fake `IDeviceProvider`. Covered: app volume sync, volume limit, device/app/default-device/system/foreground/open-mixer hotkeys (register, save, reject invalid, move to the new default device, load from settings).
   - Found and fixed: app volume sync stayed on the old device after a default device switch (the old known issue); turning volume limit or sync on twice subscribed twice; toggling notifications without an output device threw `NullReferenceException` in `DeviceVolumeHotkeysViewModel`; some VMs wrote settings from `Task.Run` through `SettingsProvider.Settings` read late (now taken before the task starts).
   - Not covered yet (need more seams): `NotificationViewModel` (static `NotificationManagerHelper`, real screens), `AppBehaviorViewModel` and `ThemeViewModel` (`App.Current`), `LangSettings`, `SettingsViewModel`.
3. **Audio integration tests.** **Done** (`test/audio-integration`, project `Volumey.IntegrationTests`, see *Build, run, test*): existing and new sessions are listed, volume and mute go both ways, fast slider changes end on the last value, two model sets of the same session don't fight, the device is described (name, ID, format, master volume; read only). Found and fixed: repeated session events defeated the session echo filter (see *Audio layer*).
4. **UI tests (later, before item C)** with FlaUI against the dev build in an isolated data folder. They use the keyboard and mouse, so they stay out of the normal test run (owner's decision) and run on demand only.
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
