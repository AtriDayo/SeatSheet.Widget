param(
    [Parameter(Mandatory)][ValidateSet('widget', 'plugin')][string]$Product,
    [string]$Tag,
    [string]$Artifact
)
$ErrorActionPreference = 'Stop'
$versionRepo = Split-Path $PSScriptRoot -Parent
$projectName = if ($Product -eq 'widget') { 'SeatSheet.Widget' } else { 'SeatSheet.ClassIslandPlugin' }
$projectPath = Join-Path $versionRepo "src/$projectName/$projectName.csproj"
[xml]$projectXml = Get-Content -LiteralPath $projectPath -Raw
$releaseVersion = [string]$projectXml.Project.PropertyGroup.Version
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$') {
    throw "Invalid or missing project version: $releaseVersion"
}
if ($Tag -and $Tag -cne "$Product-v$releaseVersion") {
    throw "Tag $Tag does not match $Product project version $releaseVersion."
}
$numericVersion = ($releaseVersion -split '-')[0] + '.0'
if ($Product -eq 'plugin') {
    $manifest = Get-Content -LiteralPath (Join-Path $versionRepo "src/$projectName/manifest.yml") -Raw
    if ($manifest -notmatch '(?m)^version:\s*(\d+\.\d+\.\d+\.\d+)\s*$' -or $Matches[1] -cne $numericVersion) {
        throw 'Plugin manifest version does not match the project version.'
    }
}
if ($Artifact) {
    $artifactPath = (Resolve-Path -LiteralPath $Artifact).Path
    if ($Product -eq 'widget') {
        if ((Split-Path $artifactPath -Leaf) -cne 'SeatSheet.Widget.exe') { throw 'Wrong desktop artifact name.' }
        $fileInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($artifactPath)
        if ($fileInfo.ProductVersion -cne $releaseVersion -or $fileInfo.FileVersion -cne $numericVersion) {
            throw 'Desktop binary version does not match the project version.'
        }
    } else {
        if ((Split-Path $artifactPath -Leaf) -cne 'SeatSheet.ClassIslandPlugin.cipx') { throw 'Wrong plugin artifact name.' }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $packageZip = [System.IO.Compression.ZipFile]::OpenRead($artifactPath)
        try {
            $manifestEntry = $packageZip.GetEntry('manifest.yml')
            if (-not $manifestEntry) { throw 'Plugin package has no manifest.' }
            $manifestReader = [System.IO.StreamReader]::new($manifestEntry.Open())
            try { $packageManifest = $manifestReader.ReadToEnd() } finally { $manifestReader.Dispose() }
            if ($packageManifest -notmatch '(?m)^version:\s*(\d+\.\d+\.\d+\.\d+)\s*$' -or $Matches[1] -cne $numericVersion) {
                throw 'Packaged plugin manifest version does not match the project version.'
            }
            $assemblyEntry = $packageZip.GetEntry('SeatSheet.ClassIslandPlugin.dll')
            if (-not $assemblyEntry) { throw 'Plugin package has no entry assembly.' }
            $assemblyTemp = [System.IO.Path]::GetTempFileName()
            try {
                $assemblyStream = [System.IO.File]::Create($assemblyTemp)
                $entryStream = $assemblyEntry.Open()
                try { $entryStream.CopyTo($assemblyStream) } finally { $entryStream.Dispose(); $assemblyStream.Dispose() }
                $assemblyInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($assemblyTemp)
                if (($assemblyInfo.ProductVersion -split '\+')[0] -cne $releaseVersion -or $assemblyInfo.FileVersion -cne $numericVersion) {
                    throw 'Packaged plugin assembly version does not match the project version.'
                }
            } finally { Remove-Item -LiteralPath $assemblyTemp -Force }
        } finally { $packageZip.Dispose() }
    }
}
Write-Output $releaseVersion
