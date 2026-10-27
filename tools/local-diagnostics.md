# Local diagnostics

The diagnostic host exercises a mock output endpoint in a separate WPF process. It does not enumerate Windows audio devices, register global hotkeys, read normal Volumey settings, or change system audio. Its profile and session descriptor live under `%LOCALAPPDATA%\Volumey\LocalDiagnostics`.

From the repository root:

```powershell
./tools/diagnostics.ps1 -Action start
./tools/diagnostics.ps1 -Action status
./tools/diagnostics.ps1 -Action state
./tools/diagnostics.ps1 -Action request -Request '{"op":"set-volume","value":42}'
./tools/diagnostics.ps1 -Action request -Request '{"op":"set-mute","value":true}'
./tools/diagnostics.ps1 -Action screenshot
./tools/diagnostics.ps1 -Action stop
```

The screenshot is written to the current session's private profile as `diagnostic.png`. Requests use one JSON object per pipe connection. Supported operations are `status`, `state`, `set-volume`, `set-mute`, `screenshot`, and `stop`. The mock begins at 58%, unmuted. Only the diagnostic window's in-memory model changes.

The isolated host currently demonstrates a semantic volume slider and mute state, rather than the production device/session mixer and real audio backend. Screenshot capture uses WPF visual rendering; it does not prove native or GPU-hosted surface capture. The production startup path remains the default unless `--local-diagnostics` is explicitly supplied.
