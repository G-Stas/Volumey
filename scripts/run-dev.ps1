<#
.SYNOPSIS
    Builds and starts a Debug build of Volumey that is isolated from an installed Volumey and from the real settings.

.DESCRIPTION
    - Debug builds use their own single-instance mutex, so they run next to an installed release build.
    - Settings go to -DataDir (VOLUMEY_SETTINGS_DIR) instead of %LOCALAPPDATA%\Volumey.
    - log4net writes to <DataDir>\volumey.log (VOLUMEY_LOG_FILE), including unhandled exceptions.
    A previous dev instance is stopped first, because it locks the exe.

.EXAMPLE
    ./scripts/run-dev.ps1                         # build + start with the settings kept in -DataDir
    ./scripts/run-dev.ps1 -Reset -ImportRealSettings   # fresh start that imports a copy of the real 1.x settings
#>
param(
    [string]$DataDir = (Join-Path $env:TEMP 'Volumey-dev'),
    [switch]$NoBuild,
    # copy the real 1.x settings file (appconfig) into DataDir and drop settings.json, so the importer runs
    [switch]$ImportRealSettings,
    # delete DataDir before starting
    [switch]$Reset,
    [switch]$Minimized
)
$ErrorActionPreference = 'Stop'

$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'Volumey\Volumey.csproj'
$exe = Join-Path $repo 'Volumey\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Volumey.exe'

Get-Process Volumey -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Stop-Process -Force
Start-Sleep -Milliseconds 300

if ($Reset -and (Test-Path $DataDir)) { Remove-Item $DataDir -Recurse -Force }
New-Item -ItemType Directory -Force $DataDir | Out-Null

if ($ImportRealSettings) {
    Copy-Item (Join-Path $env:LOCALAPPDATA 'Volumey\appconfig') $DataDir -Force
    Remove-Item (Join-Path $DataDir 'settings.json') -ErrorAction SilentlyContinue
}

if (-not $NoBuild) {
    dotnet build $project -c Debug -p:Platform=x64 --nologo -v q '-clp:ErrorsOnly;NoSummary'
    if ($LASTEXITCODE) { throw 'Build failed' }
}

$log = Join-Path $DataDir 'volumey.log'
$psi = [System.Diagnostics.ProcessStartInfo]::new($exe)
$psi.UseShellExecute = $false
$psi.WorkingDirectory = Split-Path $exe
$psi.Environment['VOLUMEY_SETTINGS_DIR'] = $DataDir
$psi.Environment['VOLUMEY_LOG_FILE'] = $log
if ($Minimized) { $psi.ArgumentList.Add('-minimized') }
$process = [System.Diagnostics.Process]::Start($psi)

[pscustomobject]@{
    Pid      = $process.Id
    DataDir  = $DataDir
    Settings = Join-Path $DataDir 'settings.json'
    Log      = $log
}
