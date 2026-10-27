# Scripts

## UI tests

`run-ui-tests.ps1` builds the Debug app and runs the UI tests (`Volumey.UITests`) against it.

```powershell
./scripts/run-ui-tests.ps1                          # all tests (about 5 minutes)
./scripts/run-ui-tests.ps1 -NoAudio                 # skip the tests that need an output device
./scripts/run-ui-tests.ps1 -Filter MediaKeysTests   # one test class or test name
./scripts/run-ui-tests.ps1 -NoBuild                 # run without building first
```

Before you run them:

- Close any running dev build of Volumey. The tests use the same exe, and the script stops if one is running.
- Don't use the keyboard or mouse while the tests run: they click and press keys.

What the tests change:

- They use their own settings folder, so your settings stay as they are.
- They press Ctrl+Alt+Shift with F13–F24, keys no keyboard has.
- They change the volume and mute of a silent test app and of the default output device, then put them back. They never change the default device.
- One test shows a small test app window and puts it in front (foreground-app hotkeys).
- The media key tests make Volumey send real media keys (play/pause, previous, next, stop). The test catches and swallows them with a keyboard hook, so your music player doesn't react.

If a test fails, it keeps its data folder (`settings.json` and `volumey.log` in `%TEMP%\Volumey-uitests`) and prints the path.

### In a VMware VM

`run-ui-tests-vm.ps1` builds here and runs the tests inside a VMware Workstation VM, for example on another Windows version. It copies only the builds into the guest (no sources, no `.git`), runs them on the guest's desktop with `ui-tests-guest.ps1` and copies the output back to `%TEMP%\Volumey-uitests-vm`. Your own keyboard and mouse stay free.

```powershell
./scripts/run-ui-tests-vm.ps1                              # everything
./scripts/run-ui-tests-vm.ps1 -Filter TrayTests -NoBuild   # one class, without building first
```

It reads the VM and the guest's sign-in from `ui-tests-vm.settings` in the repository's root (git ignores it; another file with `-Config`):

```
vmx=D:\VMs\Windows 10\Windows 10.vmx
user=<the guest's Windows user>
password=<its password>
```

The guest needs:

- VMware Tools and the .NET 10 Desktop Runtime (x64)
- a password on its Windows user: `vmrun` can't sign in without one
- that user signed in, with the desktop unlocked and the VM window open (turn off the screen lock and sleep)
- a screen of at least 1920×1080; a second monitor at another scale also runs the tests that need one

On GitHub, the *UI tests* workflow (Actions → UI tests → Run workflow) runs them with `-NoAudio`, since the runners have no audio device.

## Release zips

`build-release.ps1` builds the two GitHub release zips of the version in `Volumey/Volumey.csproj` into `artifacts\<version>`, with `SHA256SUMS.txt`:

- `Volumey_<version>_x64.zip` is self-contained: it carries the .NET 10 runtime, trimmed (about 62 MB), and starts on any PC. `LatestUpdateInfo.xml` points the in-app updater to this one.
- `Volumey_<version>_x64_framework-dependent.zip` is small (about 12 MB) but needs the .NET 10 Desktop Runtime. It's only offered on the release page.

Both are Release builds and send logs to Loggly only with an input key (`loggly.key` or `VOLUMEY_LOGGLY_INPUT_KEY`); the script stops without one unless `-NoLoggly` is given.
