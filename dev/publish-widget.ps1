# Shared by local publishing and GitHub Actions; only publishes the desktop project.
$ErrorActionPreference = 'Stop'
$publishRepoRoot = Split-Path $PSScriptRoot -Parent
$publishProject = Join-Path $publishRepoRoot 'src\SeatSheet.Widget\SeatSheet.Widget.csproj'
$publishOutput = Join-Path $publishRepoRoot 'artifacts\widget\win-x64'
& (Join-Path $PSScriptRoot 'check-release-version.ps1') -Product widget
dotnet publish $publishProject -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $publishOutput
if ($LASTEXITCODE -ne 0) { throw 'Desktop publishing failed.' }
$publishFiles = @(Get-ChildItem -LiteralPath $publishOutput -File)
if ($publishFiles.Count -ne 1 -or $publishFiles[0].Name -ne 'SeatSheet.Widget.exe') {
    throw 'Expected exactly one SeatSheet.Widget.exe in the publish output.'
}
& (Join-Path $PSScriptRoot 'check-release-version.ps1') -Product widget -Artifact (Join-Path $publishOutput 'SeatSheet.Widget.exe')
Write-Output "Desktop EXE: $($publishFiles[0].FullName)"
