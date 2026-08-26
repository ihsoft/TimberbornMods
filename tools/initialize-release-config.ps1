param(
    [Parameter(Mandatory = $true)]
    [string] $ReadinessReportPath
)

$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "repository-paths.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-native-command.psm1") -Force

$toolRepoRoot = Split-Path -Parent $PSScriptRoot
$reportPath = Resolve-RepositoryPath $toolRepoRoot $ReadinessReportPath
if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
    throw "Readiness report not found: $reportPath"
}
$report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
if ([int]$report.SchemaVersion -ne 1 -or
        [string]$report.ArtifactType -ne "TimberbornReleaseReadiness") {
    throw "Unsupported readiness artifact."
}
if (-not [bool]$report.ReadyForGate1) {
    throw "Readiness artifact is blocked before Gate 1."
}
$bootstrap = $report.Gate1Plan.ReleaseConfigBootstrap
if (-not [bool]$bootstrap.Required -or -not [bool]$bootstrap.CanCreate) {
    throw "Readiness artifact does not authorize a first registered-line release config bootstrap."
}
$releaseRoot = [string]$report.Candidate.ReleaseRoot
$configPath = [System.IO.Path]::GetFullPath([string]$bootstrap.Path)
$releaseRootPath = [System.IO.Path]::GetFullPath($releaseRoot).TrimEnd('\', '/')
$releaseRootPrefix = $releaseRootPath + [System.IO.Path]::DirectorySeparatorChar
$expectedConfigPath = [System.IO.Path]::GetFullPath((
        Join-Path $releaseRoot "$($report.Candidate.ModName)/release.json"))
if (-not $configPath.StartsWith($releaseRootPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        $configPath -ne $expectedConfigPath) {
    throw "Bootstrap config path is not the exact registered-line release config path."
}
if (Test-Path -LiteralPath $configPath) {
    throw "Bootstrap config already exists; rerun compact readiness instead of overwriting it: $configPath"
}
$headResult = Invoke-ReleaseNativeCommand -Command "git" -Arguments @(
    "-C", $releaseRoot, "rev-parse", "HEAD")
$head = if ($headResult.Output.Count -gt 0) { [string]$headResult.Output[0] } else { "" }
if ($headResult.ExitCode -ne 0 -or $head -ne [string]$report.Gate1Plan.ExpectedHeadForPreparation) {
    throw (
        "Registered release worktree HEAD changed after readiness discovery " +
        "(expected '$($report.Gate1Plan.ExpectedHeadForPreparation)', actual '$head'). " +
        "Rerun compact readiness.")
}
$publicOwnerConfigPath = [string]$bootstrap.PublicOwnerConfigPath
if (-not (Test-Path -LiteralPath $publicOwnerConfigPath -PathType Leaf)) {
    throw "Bootstrap public-owner release config is missing. Rerun compact readiness."
}
$publicOwnerConfigHash = (
    Get-FileHash -LiteralPath $publicOwnerConfigPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($publicOwnerConfigHash -ne [string]$bootstrap.PublicOwnerConfigSha256) {
    throw "Bootstrap public-owner release config changed after readiness discovery. Rerun compact readiness."
}
$sourceIdentityPaths = [string[]]@(
    [string]$bootstrap.PathArgument,
    [string]$bootstrap.DesiredContent.ChangesPath,
    [string]$bootstrap.DesiredContent.ManifestPath)
$statusResult = Invoke-ReleaseNativeCommand -Command "git" -Arguments (
    @("-C", $releaseRoot, "status", "--porcelain=v1", "--") + $sourceIdentityPaths)
if ($statusResult.ExitCode -ne 0 -or @($statusResult.Output).Count -gt 0) {
    throw "Bootstrap source identity paths changed after readiness discovery. Rerun compact readiness."
}
$content = [string]$bootstrap.DesiredContentJson
if ([string]::IsNullOrWhiteSpace($content)) {
    throw "Bootstrap readiness artifact contains no release config content."
}
$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($content)
    $actualHash = ([System.BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant()
}
finally {
    $sha256.Dispose()
}
if ($actualHash -ne [string]$bootstrap.DesiredContentSha256) {
    throw "Bootstrap release config content hash does not match the readiness artifact."
}
$parent = Split-Path -Parent $configPath
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    throw "Bootstrap release config parent directory is missing: $parent"
}
[System.IO.File]::WriteAllText($configPath, $content, [System.Text.UTF8Encoding]::new($false))
$writtenHash = (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($writtenHash -ne [string]$bootstrap.DesiredContentSha256) {
    throw "Bootstrap release config readback hash mismatch."
}
[void](Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json)

Write-Host "Created registered-line release config: $configPath"
Write-Host "SHA256: $writtenHash"
Write-Host "Commit only $($bootstrap.PathArgument), then rerun the readiness command from the artifact."
