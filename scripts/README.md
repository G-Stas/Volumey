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

On GitHub, the *UI tests* workflow (Actions → UI tests → Run workflow) runs them with `-NoAudio`, since the runners have no audio device.
