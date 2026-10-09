<#
.SYNOPSIS
  Logs whether a process is alive and its UI thread answers, every 2 s, to a file. For hang tests (e.g. sleep and wake, #57).

.DESCRIPTION
  Sends WM_NULL to the process's first top-level window with a 1 s timeout, which also works while Volumey is hidden in the tray
  (Process.Responding only checks a visible main window). Writes every change plus a heartbeat once a minute; a gap in the
  heartbeats shows the time the PC was asleep. Stops when the process exits.

.EXAMPLE
  Start-Process pwsh -WindowStyle Hidden -ArgumentList '-NoProfile','-File','scripts/watch-responsiveness.ps1','-ProcessId',(Get-Process Volumey).Id,'-Out',"$env:TEMP\watchdog.log"
#>
param([int]$ProcessId, [string]$Out)
Add-Type @'
using System; using System.Runtime.InteropServices; using System.Collections.Generic;
public static class W {
  delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
  public static List<IntPtr> Windows(uint pid) {
    var list = new List<IntPtr>();
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if(p == pid) list.Add(h); return true; }, IntPtr.Zero);
    return list;
  }
  // SMTO_ABORTIFHUNG = 2
  public static bool Answers(IntPtr h) { IntPtr r; return SendMessageTimeout(h, 0, IntPtr.Zero, IntPtr.Zero, 2, 1000, out r) != IntPtr.Zero; }
}
'@
$last = $null
while($true) {
  $p = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
  if(-not $p) { $state = 'EXITED' }
  else {
    $wins = [W]::Windows([uint32]$ProcessId)
    if($wins.Count -eq 0) { $state = 'no windows' }
    elseif([W]::Answers($wins[0])) { $state = 'responding' }
    else { $state = 'NOT RESPONDING' }
  }
  # log every change, plus a heartbeat once a minute (gaps show the sleep)
  $now = Get-Date
  if($state -ne $last -or $now.Second -lt 2) { Add-Content $Out "$($now.ToString('HH:mm:ss')) $state"; $last = $state }
  if($state -eq 'EXITED') { break }
  Start-Sleep -Seconds 2
}

