param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [Parameter(Mandatory = $true)]
    [string] $PackageOwnerRoot,

    [Parameter(Mandatory = $true)]
    [string] $SourceReleaseRoot,

    [Parameter(Mandatory = $true)]
    [string] $GameVersion,

    [Parameter(Mandatory = $true)]
    [string] $SourceModVersion,

    [Parameter(Mandatory = $true)]
    [ValidateSet("ImportLane", "Package")]
    [string] $Operation,

    [string] $ExpectedOwnerConfigSha256 = ""
)

$ErrorActionPreference = "Stop"

$toolRepoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "repository-paths.psm1") -Force
$ownerRoot = Resolve-RepositoryRoot $PackageOwnerRoot $toolRepoRoot
$sourceRoot = Resolve-RepositoryRoot $SourceReleaseRoot $toolRepoRoot
$ownerConfigPath = Join-Path $ownerRoot "$ModName/release.json"

function Assert-PathExists([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Assert-PackageSource(
    [string] $Path,
    [object] $ReleaseConfig,
    [switch] $SkipSourceLane
) {
    foreach ($manifestProperty in $ReleaseConfig.ManifestVersions.PSObject.Properties) {
        $lane = [string]$manifestProperty.Name
        if ($SkipSourceLane -and $lane -eq $GameVersion) {
            continue
        }
        $expectedVersion = if ($lane -eq $GameVersion) {
            $SourceModVersion
        }
        else {
            [string]$manifestProperty.Value
        }
        $lanePath = Join-Path $Path $lane
        $manifestPath = Join-Path $lanePath "manifest.json"
        Assert-PathExists $manifestPath "Combined package manifest for $lane"
        $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
        if ([string]$manifest.Version -ne $expectedVersion) {
            throw (
                "Combined package lane $lane has manifest version $($manifest.Version), " +
                "expected $expectedVersion.")
        }

        $scriptFileBase = if ([string]::IsNullOrWhiteSpace([string]$ReleaseConfig.ScriptFileBase)) {
            $ModName
        }
        else {
            [string]$ReleaseConfig.ScriptFileBase
        }
        $scriptsPath = Join-Path $lanePath "Scripts"
        Assert-PathExists (Join-Path $scriptsPath "$scriptFileBase.dll") "Combined package DLL for $lane"
        $allowMissingXml = @($ReleaseConfig.Package.AllowMissingXmlFolders) -contains $lane
        if (-not $allowMissingXml) {
            Assert-PathExists (Join-Path $scriptsPath "$scriptFileBase.xml") `
                "Combined package XML documentation for $lane"
        }
    }
}

Assert-PathExists $ownerConfigPath "Public package owner config"
$ownerConfigHash = (Get-FileHash -LiteralPath $ownerConfigPath -Algorithm SHA256).Hash.ToLowerInvariant()
if (-not [string]::IsNullOrWhiteSpace($ExpectedOwnerConfigSha256) -and
        $ownerConfigHash -ne $ExpectedOwnerConfigSha256.ToLowerInvariant()) {
    throw "Public package owner config changed after readiness discovery: $ownerConfigPath"
}

$ownerConfig = Get-Content -Raw -LiteralPath $ownerConfigPath | ConvertFrom-Json
if ([string]$ownerConfig.Package.Mode -ne "LocalModFolder") {
    throw "Combined package materialization requires Package.Mode=LocalModFolder in $ownerConfigPath"
}
$sourcePathArgument = [string]$ownerConfig.Package.SourcePath
$outputPathArgument = [string]$ownerConfig.Package.OutputPath
if ([string]::IsNullOrWhiteSpace($sourcePathArgument) -or
        [string]::IsNullOrWhiteSpace($outputPathArgument)) {
    throw "Public package owner config requires Package.SourcePath and Package.OutputPath."
}

$ownerSourcePath = Resolve-RepositoryPath $ownerRoot $sourcePathArgument
$sourceLaneRoot = Resolve-RepositoryPath $sourceRoot $sourcePathArgument
$sourceLanePath = Join-Path $sourceLaneRoot $GameVersion
$destinationLanePath = Join-Path $ownerSourcePath $GameVersion
Assert-PathExists $sourceLanePath "Materialized source lane"
$sourceManifestPath = Join-Path $sourceLanePath "manifest.json"
Assert-PathExists $sourceManifestPath "Materialized source-lane manifest"
$sourceManifest = Get-Content -Raw -LiteralPath $sourceManifestPath | ConvertFrom-Json
if ([string]$sourceManifest.Version -ne $SourceModVersion) {
    throw (
        "Materialized source lane $GameVersion has manifest version $($sourceManifest.Version), " +
        "expected source version $SourceModVersion.")
}

if ($Operation -eq "ImportLane") {
    Assert-PathExists $ownerSourcePath "Public package owner source"
    Assert-PackageSource $ownerSourcePath $ownerConfig -SkipSourceLane
    if ([System.IO.Path]::GetFullPath($sourceLanePath) -ne
            [System.IO.Path]::GetFullPath($destinationLanePath)) {
        [void](New-Item -ItemType Directory -Path $ownerSourcePath -Force)
        $temporaryLanePath = Join-Path $ownerSourcePath ".release-lane-$GameVersion-$PID"
        if (Test-Path -LiteralPath $temporaryLanePath) {
            throw "Temporary package lane already exists: $temporaryLanePath"
        }
        Copy-Item -LiteralPath $sourceLanePath -Destination $temporaryLanePath -Recurse
        try {
            if (Test-Path -LiteralPath $destinationLanePath) {
                Remove-Item -LiteralPath $destinationLanePath -Recurse -Force
            }
            Move-Item -LiteralPath $temporaryLanePath -Destination $destinationLanePath
        }
        finally {
            if (Test-Path -LiteralPath $temporaryLanePath) {
                Remove-Item -LiteralPath $temporaryLanePath -Recurse -Force
            }
        }
    }
    Assert-PackageSource $ownerSourcePath $ownerConfig
    Write-Host "Combined package lane assembled: $destinationLanePath (v$SourceModVersion)"
    exit 0
}

Assert-PackageSource $ownerSourcePath $ownerConfig
$outputPath = Resolve-RepositoryPath $ownerRoot $outputPathArgument
$stagingRoot = Resolve-RepositoryPath $toolRepoRoot ".tools/release-staging"
$stagingPath = Join-Path $stagingRoot "$ModName-combined-$PID"
if (Test-Path -LiteralPath $stagingPath) {
    throw "Combined package staging path already exists: $stagingPath"
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    [void](New-Item -ItemType Directory -Path $stagingPath -Force)
    Copy-Item -LiteralPath $ownerSourcePath -Destination $stagingPath -Recurse
    $outputDirectory = Split-Path -Parent $outputPath
    [void](New-Item -ItemType Directory -Path $outputDirectory -Force)
    if (Test-Path -LiteralPath $outputPath) {
        Remove-Item -LiteralPath $outputPath -Force
    }
    $zip = [System.IO.Compression.ZipFile]::Open(
        $outputPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $stagingRootPath = (Resolve-Path -LiteralPath $stagingPath).Path.TrimEnd('\') + '\'
        Get-ChildItem -LiteralPath $stagingPath -Recurse -File | Sort-Object FullName | ForEach-Object {
            $entryName = $_.FullName.Substring($stagingRootPath.Length).Replace('\', '/')
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
}
finally {
    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
}

$packageHash = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Combined package materialized once: $outputPath"
Write-Host "Package SHA256: $packageHash"
