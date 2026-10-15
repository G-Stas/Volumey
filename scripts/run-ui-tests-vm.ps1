<#
.SYNOPSIS
  Builds the Debug app and the UI tests here and runs the tests inside a VMware Workstation VM.

.DESCRIPTION
  Copies only the builds (no sources, no .git) into the guest with vmrun, runs them there on the signed-in desktop and copies
  the result back to %TEMP%\Volumey-uitests-vm: result.log, and data.zip with the data folders of failed tests.
  The guest needs the .NET 10 Desktop Runtime (x64), VMware Tools, its user signed in with the desktop unlocked and shown, and a
  password on that user (vmrun can't sign in to a Windows guest without one). Its screen should be at least 1920x1080.
  The tests click and press keys inside the guest only: the host's keyboard and mouse stay free.

.EXAMPLE
  ./scripts/run-ui-tests-vm.ps1 -Vmx 'D:\VMs\Windows 10\Windows 10.vmx' -GuestUser user
  ./scripts/run-ui-tests-vm.ps1 -Filter MediaKeysTests -NoBuild   # with VOLUMEY_UITEST_VMX and VOLUMEY_UITEST_VM_USER set
#>
param(
    [string]$Vmx = $env:VOLUMEY_UITEST_VMX,
    [string]$GuestUser = $env:VOLUMEY_UITEST_VM_USER,
    [string]$Filter = '',
    [switch]$NoAudio,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$appDir = Join-Path $repo 'Volumey\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64'
$testsDir = Join-Path $repo 'Volumey.UITests\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64'
$vmrun = Join-Path ${env:ProgramFiles(x86)} 'VMware\VMware Workstation\vmrun.exe'
if (-not (Test-Path $vmrun)) { $vmrun = Join-Path $env:ProgramFiles 'VMware\VMware Workstation\vmrun.exe' }
if (-not (Test-Path $vmrun)) { throw 'vmrun.exe not found: install VMware Workstation' }
if (-not $Vmx -or -not (Test-Path $Vmx)) { throw 'Pass the VM''s .vmx file with -Vmx (or set VOLUMEY_UITEST_VMX)' }
if (-not $GuestUser) { throw 'Pass the guest''s Windows user with -GuestUser (or set VOLUMEY_UITEST_VM_USER)' }

#the guest password: VOLUMEY_UITEST_VM_PASSWORD, else asked for
$password = $env:VOLUMEY_UITEST_VM_PASSWORD
if (-not $password) {
    $password = (Get-Credential -UserName $GuestUser -Message 'The VM''s Windows password').GetNetworkCredential().Password
}

function Invoke-Vmrun {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    $output = & $vmrun -T ws -gu $GuestUser -gp $password @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "vmrun $($Arguments[0]) failed: $output" }
    $output
}

if (-not $NoBuild) {
    dotnet build (Join-Path $repo 'Volumey\Volumey.csproj') -c Debug -p:Platform=x64 --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Building the app failed' }
    dotnet build (Join-Path $repo 'Volumey.UITests\Volumey.UITests.csproj') -c Debug -p:Platform=x64 --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Building the UI tests failed' }
}

$work = Join-Path $env:TEMP 'Volumey-uitests-vm'
$stage = Join-Path $work 'stage'
$zip = Join-Path $work 'stage.zip'
New-Item -ItemType Directory -Force $work | Out-Null
Remove-Item $stage, $zip, (Join-Path $work 'result.log'), (Join-Path $work 'exitcode.txt'), (Join-Path $work 'data.zip') -Recurse -Force -ErrorAction SilentlyContinue
#robocopy's exit codes below 8 mean success
robocopy $appDir (Join-Path $stage 'app') /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Copying the app failed' }
robocopy $testsDir (Join-Path $stage 'tests') /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Copying the tests failed' }
Copy-Item (Join-Path $PSScriptRoot 'ui-tests-guest.ps1') $stage
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Fastest

if ((& $vmrun -T ws checkToolsState $Vmx) -ne 'running') { throw 'The VM isn''t running or VMware Tools isn''t running in it' }
$guest = 'C:\Volumey-uitests'
$powershell = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
Write-Host 'Copying the builds into the VM'
Invoke-Vmrun runProgramInGuest $Vmx $powershell -NoProfile -Command "Remove-Item '$guest' -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory '$guest' | Out-Null" | Out-Null
Invoke-Vmrun CopyFileFromHostToGuest $Vmx $zip "$guest\stage.zip" | Out-Null
Invoke-Vmrun runProgramInGuest $Vmx $powershell -NoProfile -Command "Expand-Archive '$guest\stage.zip' '$guest\run'" | Out-Null

$guestArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "$guest\run\ui-tests-guest.ps1")
if ($NoAudio) { $guestArguments += '-NoAudio' }
if ($Filter) { $guestArguments += @('-Filter', $Filter) }
Write-Host 'UI tests running in the VM: leave its keyboard and mouse alone (the host''s are free).' -ForegroundColor Yellow
#on the signed-in desktop (-interactive), in front (-activeWindow), so the tests may bring their windows to the foreground.
#A failing test run makes the guest program exit with a non-zero code, which vmrun reports as an error: the exit code is read below.
& $vmrun -T ws -gu $GuestUser -gp $password runProgramInGuest $Vmx -activeWindow -interactive $powershell @guestArguments 2>&1 | Out-Null

$log = Join-Path $work 'result.log'
Invoke-Vmrun CopyFileFromGuestToHost $Vmx "$guest\result.log" $log | Out-Null
Invoke-Vmrun CopyFileFromGuestToHost $Vmx "$guest\exitcode.txt" (Join-Path $work 'exitcode.txt') | Out-Null
$code = [int](Get-Content (Join-Path $work 'exitcode.txt'))
& $vmrun -T ws -gu $GuestUser -gp $password fileExistsInGuest $Vmx "$guest\data.zip" | Out-Null
if ($LASTEXITCODE -eq 0) {
    Invoke-Vmrun CopyFileFromGuestToHost $Vmx "$guest\data.zip" (Join-Path $work 'data.zip') | Out-Null
}
Get-Content $log
if (Test-Path (Join-Path $work 'data.zip')) { Write-Host "Data folders of failed tests: $(Join-Path $work 'data.zip')" }
exit $code
