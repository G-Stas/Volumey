<#
.SYNOPSIS
  Starts processes that play silence, so Volumey shows them as apps in the mixer.

.DESCRIPTION
  Each process is a copy of scripts/test-audio-app under its own exe name (TestApp1.exe, TestApp2.exe, ...),
  because the mixer groups apps by exe path: copies with one name would show as a single row.
  They only change their own session volume, never the device's or other apps'.
  Uses: many rows in the mixer (issue #66), apps starting and exiting (memory, issue #87),
  and an OSD for an outside volume change (-Volume).

.EXAMPLE
  ./scripts/test-sessions.ps1 -Count 12 -Seconds 30
  ./scripts/test-sessions.ps1 -Volume 0.35 -After 3     # one app that changes its own volume after 3 s
#>
param(
    [int]$Count = 1,
    [int]$Seconds = 30,
    # set the session volume (0..1) after -After seconds, like a change in the Windows mixer
    [Nullable[double]]$Volume = $null,
    [int]$After = 3
)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) 'Volumey-test-audio'
$build = Join-Path $root 'build'
$project = Join-Path $PSScriptRoot 'test-audio-app\TestAudioApp.csproj'

if(-not (Test-Path (Join-Path $build 'testaudio.exe')) -or
   (Get-Item $project).LastWriteTime -gt (Get-Item (Join-Path $build 'testaudio.exe')).LastWriteTime)
{
    dotnet publish $project -c Release -o $build | Out-Null
    if($LASTEXITCODE -ne 0) { throw 'Building the test audio app failed' }
}

$arguments = @("$Seconds")
if($null -ne $Volume) { $arguments += @($Volume.ToString([Globalization.CultureInfo]::InvariantCulture), "$After") }

foreach($n in 1..$Count)
{
    #the exe has the dll name built in, so only the exe is renamed
    $dir = Join-Path $root "app$n"
    New-Item -ItemType Directory -Force $dir | Out-Null
    Copy-Item (Join-Path $build '*') $dir -Recurse -Force
    $exe = Join-Path $dir "TestApp$n.exe"
    Copy-Item (Join-Path $dir 'testaudio.exe') $exe -Force
    Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
}
