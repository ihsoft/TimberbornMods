param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $Configuration = "Release",
    [string] $GameVersion = "version-1.1",
    [string] $OutputRoot = "_MODS!",
    [string] $LocalModRoot = "",
    [string] $StagingRoot = ".tools/release-staging",
    [switch] $IncludeLegacyVersions,
    [switch] $SkipBuild,
    [switch] $Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$modRoot = Join-Path $repoRoot $ModName
$projectPath = Join-Path $modRoot "$ModName.csproj"
$manifestPath = Join-Path $modRoot "Mod/manifest.json"
$propsPath = Join-Path $modRoot "directory.build.props"
$modContentRoot = Join-Path $modRoot "Mod"
$outputRootPath = Join-Path $repoRoot $OutputRoot
$localModRootPath = $LocalModRoot
if ([string]::IsNullOrWhiteSpace($localModRootPath)) {
    $localModRootPath = Join-Path "_MODS!" $ModName
}
if (-not [System.IO.Path]::IsPathRooted($localModRootPath)) {
    $localModRootPath = Join-Path $repoRoot $localModRootPath
}
$stagingRootPath = Join-Path $repoRoot $StagingRoot
$packageStage = Join-Path $stagingRootPath $ModName
$packageRoot = Join-Path $packageStage $ModName
$versionStage = Join-Path $packageRoot $GameVersion

function Assert-PathExists([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Assert-VersionFolder([System.IO.DirectoryInfo] $VersionDirectory) {
    $manifestPath = Join-Path $VersionDirectory.FullName "manifest.json"
    $scriptDirectory = Join-Path $VersionDirectory.FullName "Scripts"
    $dllPath = Join-Path $scriptDirectory "$ModName.dll"
    $xmlPath = Join-Path $scriptDirectory "$ModName.xml"

    Assert-PathExists $manifestPath "Manifest for $($VersionDirectory.Name)"
    Assert-PathExists $dllPath "DLL for $($VersionDirectory.Name)"
    Assert-PathExists $xmlPath "XML documentation for $($VersionDirectory.Name)"
}

Assert-PathExists $modRoot "Mod directory"
Assert-PathExists $projectPath "Project file"
Assert-PathExists $manifestPath "Manifest"
Assert-PathExists $propsPath "Directory build props"
Assert-PathExists $modContentRoot "Mod content directory"

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$props = [xml](Get-Content -Raw -LiteralPath $propsPath)
$manifestVersion = [string]$manifest.Version
$assemblyVersion = [string]$props.Project.PropertyGroup.Version

if ([string]::IsNullOrWhiteSpace($manifestVersion)) {
    throw "Manifest version is empty: $manifestPath"
}
if ($manifestVersion -ne $assemblyVersion) {
    throw "Version mismatch for $ModName. Manifest=$manifestVersion, directory.build.props=$assemblyVersion"
}

if (-not $SkipBuild) {
    $disabledModCopyPath = Join-Path $stagingRootPath "__missing_mod_copy_target__"
    & dotnet build $projectPath -c $Configuration "/p:ModPath=$disabledModCopyPath"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for $projectPath"
    }
}

$targetFramework = "netstandard2.1"
$buildOutput = Join-Path $modRoot "bin/$Configuration/$targetFramework"
$dllPath = Join-Path $buildOutput "$ModName.dll"
$xmlPath = Join-Path $buildOutput "$ModName.xml"

Assert-PathExists $dllPath "Built DLL"

if (Test-Path -LiteralPath $packageStage) {
    Remove-Item -LiteralPath $packageStage -Recurse -Force
}
New-Item -ItemType Directory -Path $versionStage | Out-Null

Get-ChildItem -LiteralPath $modContentRoot -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $versionStage -Recurse -Force
}

$scriptsStage = Join-Path $versionStage "Scripts"
New-Item -ItemType Directory -Path $scriptsStage -Force | Out-Null
Copy-Item -LiteralPath $dllPath -Destination $scriptsStage -Force
if (Test-Path -LiteralPath $xmlPath) {
    Copy-Item -LiteralPath $xmlPath -Destination $scriptsStage -Force
}

$thumbnailPath = Join-Path $modContentRoot "thumbnail.jpg"
if (Test-Path -LiteralPath $thumbnailPath) {
    Copy-Item -LiteralPath $thumbnailPath -Destination $packageRoot -Force
}

$workshopDataPath = Join-Path $localModRootPath "workshop_data.json"
if (Test-Path -LiteralPath $workshopDataPath) {
    Copy-Item -LiteralPath $workshopDataPath -Destination $packageRoot -Force
}

if ($IncludeLegacyVersions) {
    if (Test-Path -LiteralPath $localModRootPath) {
        Get-ChildItem -LiteralPath $localModRootPath -Directory -Filter "version-*" | Where-Object {
            $_.Name -ne $GameVersion
        } | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $packageRoot -Recurse -Force
        }
    }
}

$versionDirectories = @(Get-ChildItem -LiteralPath $packageRoot -Directory | Where-Object {
    $_.Name -match "^version-\d+\.\d+$"
})
if ($versionDirectories.Count -eq 0) {
    throw "Package must contain at least one version-X.X folder."
}
$versionDirectories | ForEach-Object {
    Assert-VersionFolder $_
}

New-Item -ItemType Directory -Path $outputRootPath -Force | Out-Null
$zipPath = Join-Path $outputRootPath "$($ModName)_v$manifestVersion.zip"
if ((Test-Path -LiteralPath $zipPath) -and -not $Force) {
    throw "Package already exists: $zipPath. Use -Force to overwrite it."
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $packageStageFullName = (Resolve-Path -LiteralPath $packageStage).Path.TrimEnd("\") + "\"
    Get-ChildItem -LiteralPath $packageStage -Recurse -File | ForEach-Object {
        $entryName = $_.FullName.Substring($packageStageFullName.Length).Replace("\", "/")
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $_.FullName,
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Built package: $zipPath"
