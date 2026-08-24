param(
    [Parameter(Mandatory = $true)]
    [string] $SourcePreflightReportPath,

    [Parameter(Mandatory = $true)]
    [string] $DestinationReleaseRoot,

    [string] $GameVersion = "",
    [switch] $Force
)

$ErrorActionPreference = "Stop"

$toolRepoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "repository-paths.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-package-provenance.psm1") -Force
$destinationRoot = Resolve-RepositoryRoot $DestinationReleaseRoot $toolRepoRoot

function Resolve-ToolPath([string] $Path) {
    return Resolve-RepositoryPath $toolRepoRoot $Path
}

function Assert-PathExists([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

$reportPath = Resolve-ToolPath $SourcePreflightReportPath
Assert-PathExists $reportPath "Source preflight report"
$report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
if ([int]$report.SchemaVersion -ne 5 -or $report.ReadyForPublish -ne $true) {
    throw "Source report must be a publish-ready schema-5 preflight report."
}

$sources = @($report.ReleaseSources)
if ($sources.Count -ne 1) {
    throw "Lane import requires a single-source preflight report, found $($sources.Count) sources."
}
$source = $sources[0]
if ([string]::IsNullOrWhiteSpace($GameVersion)) {
    $GameVersion = [string]$source.GameVersion
}
if ($GameVersion -ne [string]$source.GameVersion) {
    throw "Requested lane $GameVersion does not match source report lane $($source.GameVersion)."
}

$packagePath = [string]$report.Package.Path
Assert-PathExists $packagePath "Source release package"
$packageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($packageHash -ne [string]$report.Package.Sha256) {
    throw "Source release package changed after preflight."
}
$fingerprints = @(Get-PackageLaneFingerprints $packagePath)
$sourceFingerprint = @($fingerprints | Where-Object { $_.GameVersion -eq $GameVersion })
if ($sourceFingerprint.Count -ne 1 -or $sourceFingerprint[0].Sha256 -ne [string]$source.PackageLaneSha256) {
    throw "Source package lane $GameVersion does not match its preflight evidence."
}
if ([string]$sourceFingerprint[0].ManifestVersion -ne [string]$source.SourceModVersion) {
    throw "Source package lane $GameVersion manifest version does not match its source mod version."
}

$destinationConfigPath = Join-Path $destinationRoot "$($report.ModName)/release.json"
Assert-PathExists $destinationConfigPath "Destination release config"
$destinationConfig = Get-Content -Raw -LiteralPath $destinationConfigPath | ConvertFrom-Json
if ([string]$destinationConfig.Package.Mode -ne "LocalModFolder") {
    throw "Destination release must use Package.Mode=LocalModFolder for lane import."
}
$destinationSource = Resolve-RepositoryPath $destinationRoot ([string]$destinationConfig.Package.SourcePath)
Assert-PathExists $destinationSource "Destination package source"
$destinationLane = Join-Path $destinationSource $GameVersion
$temporaryDestinationRoot = Join-Path $destinationSource ".release-lane-import-$PID"
$temporaryDestinationLane = Join-Path $temporaryDestinationRoot $GameVersion
if ((Test-Path -LiteralPath $destinationLane) -and -not $Force) {
    throw "Destination lane already exists: $destinationLane. Use -Force only after verifying it may be replaced."
}
if (Test-Path -LiteralPath $temporaryDestinationRoot) {
    throw "Temporary destination root already exists: $temporaryDestinationRoot"
}

$stagingRoot = Resolve-ToolPath ".tools/release-lane-import"
$stagingDirectory = Join-Path $stagingRoot "$($report.ModName)-$GameVersion-$PID"
if (Test-Path -LiteralPath $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
try {
    Expand-Archive -LiteralPath $packagePath -DestinationPath $stagingDirectory -Force
    $laneCandidates = @(Get-ChildItem -LiteralPath $stagingDirectory -Directory -Recurse | Where-Object {
        $_.Name -eq $GameVersion
    })
    if ($laneCandidates.Count -ne 1) {
        throw "Expected exactly one $GameVersion directory in source package, found $($laneCandidates.Count)."
    }

    New-Item -ItemType Directory -Path $temporaryDestinationRoot | Out-Null
    Copy-Item -LiteralPath $laneCandidates[0].FullName -Destination $temporaryDestinationLane -Recurse -Force
    $verificationZip = Join-Path $stagingRoot "$($report.ModName)-$GameVersion-verification-$PID.zip"
    if (Test-Path -LiteralPath $verificationZip) {
        Remove-Item -LiteralPath $verificationZip -Force
    }
    Compress-Archive -LiteralPath $temporaryDestinationLane -DestinationPath $verificationZip -CompressionLevel Optimal
    $importedFingerprint = @(Get-PackageLaneFingerprints $verificationZip | Where-Object {
        $_.GameVersion -eq $GameVersion
    })
    if ($importedFingerprint.Count -ne 1 -or $importedFingerprint[0].Sha256 -ne $sourceFingerprint[0].Sha256) {
        throw "Imported lane fingerprint does not match the source preflight package."
    }
    Remove-Item -LiteralPath $verificationZip -Force

    if (Test-Path -LiteralPath $destinationLane) {
        Remove-Item -LiteralPath $destinationLane -Recurse -Force
    }
    Move-Item -LiteralPath $temporaryDestinationLane -Destination $destinationLane
}
finally {
    if (Test-Path -LiteralPath $temporaryDestinationRoot) {
        Remove-Item -LiteralPath $temporaryDestinationRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

Write-Host "Imported verified $GameVersion lane into $destinationSource."
Write-Host "Source package: $packagePath"
Write-Host "Lane SHA256: $($sourceFingerprint[0].Sha256)"
