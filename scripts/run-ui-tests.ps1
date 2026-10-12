<#
.SYNOPSIS
  Builds the Debug app and runs the UI tests (Volumey.UITests) against it.

.DESCRIPTION
  The UI tests start the app with an isolated data folder, drive it through UI Automation and press real keys
  (Ctrl+Alt+Shift with F13-F24, keys no keyboard has). They change real app and device volume and mute and put them back;
  they never change the default device. Don't use the keyboard or mouse while they run (about 2 minutes).
  A running dev build has to be closed first (same exe and single-instance lock); the script stops if one runs.
  A failed test keeps its data folder (settings.json, volumey.log) in %TEMP%\Volumey-uitests and prints its path.

.EXAMPLE
  ./scripts/run-ui-tests.ps1                          # everything
  ./scripts/run-ui-tests.ps1 -NoAudio                 # without the tests that need an output device (what CI runs)
  ./scripts/run-ui-tests.ps1 -Filter MediaKeysTests   # one class (or a test name)
#>
param(
    [string]$Filter = '',
    [switch]$NoAudio,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $repo 'Volumey\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Volumey.exe'

$running = Get-Process Volumey -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
if ($running) { throw "Close the running dev build first (pid $($running.Id -join ', '))" }

if (-not $NoBuild) {
    dotnet build (Join-Path $repo 'Volumey\Volumey.csproj') -c Debug -p:Platform=x64 --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Building the app failed' }
    dotnet build (Join-Path $repo 'Volumey.UITests\Volumey.UITests.csproj') -c Debug -p:Platform=x64 --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Building the UI tests failed' }
}

$filters = @()
if ($NoAudio) { $filters += 'TestCategory!=Audio' }
if ($Filter) { $filters += "FullyQualifiedName~$Filter" }

$arguments = @('test', '--project', (Join-Path $repo 'Volumey.UITests\Volumey.UITests.csproj'), '-c', 'Debug', '-p:Platform=x64', '--no-build')
if ($filters) { $arguments += @('--filter', ($filters -join '&')) }

Write-Host 'UI tests running: hands off the keyboard and mouse.' -ForegroundColor Yellow
#failing tests write to stderr: with 'Stop', a caller that redirects it (the GitHub workflow) would end the run at the first one
$ErrorActionPreference = 'Continue'
dotnet @arguments
exit $LASTEXITCODE
