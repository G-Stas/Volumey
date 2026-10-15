<#
.SYNOPSIS
  Runs the staged UI tests inside a VM. run-ui-tests-vm.ps1 copies it into the guest next to the app (app\) and the tests (tests\).

.DESCRIPTION
  Writes the test output to result.log and its exit code to exitcode.txt one folder up. The data folders of failed tests
  (%TEMP%\Volumey-uitests) go into data.zip there. Runs on Windows PowerShell 5.1, which every Windows 10 has.
#>
param(
    [string]$Filter = '',
    [switch]$NoAudio
)
$ErrorActionPreference = 'Continue'
$out = Split-Path $PSScriptRoot -Parent
$log = Join-Path $out 'result.log'
$data = Join-Path $env:TEMP 'Volumey-uitests'
Remove-Item $log, (Join-Path $out 'exitcode.txt'), (Join-Path $out 'data.zip') -ErrorAction SilentlyContinue
Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue

$filters = @()
if ($NoAudio) { $filters += 'TestCategory!=Audio' }
if ($Filter) { $filters += "FullyQualifiedName~$Filter" }
$arguments = @('--no-ansi', '--progress', 'off')
if ($filters) { $arguments += @('--filter', ($filters -join '&')) }

$env:VOLUMEY_UITEST_EXE = Join-Path $PSScriptRoot 'app\Volumey.exe'
#output into files and waiting for the test process alone: an app a failed test left running inherits the output handles,
#and a pipe (*>) would wait for it too
$errors = Join-Path $out 'errors.log'
#the test process shares this console and writes in its code page: UTF-8, not the OEM one
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$tests = Start-Process (Join-Path $PSScriptRoot 'tests\Volumey.UITests.exe') -ArgumentList ($arguments | ForEach-Object { "`"$_`"" }) `
    -NoNewWindow -PassThru -RedirectStandardOutput $log -RedirectStandardError $errors
#Windows PowerShell only keeps the exit code when the handle was taken while the process ran
$null = $tests.Handle
$tests.WaitForExit()
$code = $tests.ExitCode
if ((Get-Item $errors).Length -gt 0) { Get-Content $errors | Add-Content $log }
Remove-Item $errors

#apps a failed test left running (Volumey, the test audio app's copies)
Get-Process | Where-Object { $_.Path -and ($_.Path.StartsWith($PSScriptRoot) -or $_.Path.StartsWith($data)) } | Stop-Process -Force -ErrorAction SilentlyContinue
Set-Content (Join-Path $out 'exitcode.txt') $code

#the folders a failed test kept, without the copies of the test audio app
$kept = @(Get-ChildItem $data -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'apps' })
if ($kept) { Compress-Archive -Path $kept.FullName -DestinationPath (Join-Path $out 'data.zip') }
exit $code
