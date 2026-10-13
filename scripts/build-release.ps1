<#
.SYNOPSIS
  Builds the two GitHub release zips of the version in Volumey.csproj, with their SHA-256 checksums.

.DESCRIPTION
  - Volumey_<version>_x64.zip: self-contained (carries the .NET 10 runtime, about 80 MB). LatestUpdateInfo.xml points the in-app
    updater to this one, so an update works on any PC, also for 1.x users who only have an older .NET.
  - Volumey_<version>_x64_framework-dependent.zip: small (about 12 MB), needs the .NET 10 Desktop Runtime. Only offered on the
    release page, for people who have it.
  Both are Release builds, so they send logs to Loggly only when the build found the input key (loggly.key or
  VOLUMEY_LOGGLY_INPUT_KEY); the script stops without one unless -NoLoggly is given.
  The Store package is built separately, with Visual Studio's MSBuild and the packaging project.

.EXAMPLE
  ./scripts/build-release.ps1                   # into artifacts\<version>
  ./scripts/build-release.ps1 -Output D:\out    # somewhere else
#>
param(
    [string]$Output = '',
    [switch]$NoLoggly
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'Volumey\Volumey.csproj'

$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> in $project" }
if (-not $NoLoggly -and -not $env:VOLUMEY_LOGGLY_INPUT_KEY -and -not (Test-Path (Join-Path $repo 'loggly.key'))) {
    throw 'No Loggly input key (loggly.key or VOLUMEY_LOGGLY_INPUT_KEY): the release would send no logs. Use -NoLoggly to build anyway.'
}
if (-not $Output) { $Output = Join-Path $repo "artifacts\$version" }
if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
New-Item -ItemType Directory $Output | Out-Null

$variants = @(
    @{ Name = "Volumey_${version}_x64"; SelfContained = 'true' },
    @{ Name = "Volumey_${version}_x64_framework-dependent"; SelfContained = 'false' }
)
foreach ($variant in $variants) {
    $folder = Join-Path $Output $variant.Name
    Write-Host "Publishing $($variant.Name)..." -ForegroundColor Cyan
    dotnet publish $project -c Release -p:Platform=x64 -r win-x64 --self-contained $variant.SelfContained -o $folder --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Publishing $($variant.Name) failed" }
    $zip = "$folder.zip"
    Compress-Archive -Path (Join-Path $folder '*') -DestinationPath $zip -Force
    Remove-Item $folder -Recurse -Force
}

$sums = Get-ChildItem $Output -Filter '*.zip' | Sort-Object Name | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host ('{0}  {1:N0} MB  {2}' -f $_.Name, ($_.Length / 1MB), $hash)
    "$hash  $($_.Name)"
}
$sums | Set-Content (Join-Path $Output 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Done: $Output" -ForegroundColor Green
Write-Host "LatestUpdateInfo.xml's <url>: https://github.com/G-Stas/Volumey/releases/download/v$version/Volumey_${version}_x64.zip"
