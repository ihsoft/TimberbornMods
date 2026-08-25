param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $RepositoryRoot = "",
    [string] $ReleaseLine = "",
    [string] $UnityPath = "",
    [string] $ReportRoot = ".tools/release-readiness",
    [switch] $AsJson,
    [switch] $NoReport
)

$ErrorActionPreference = "Stop"

$toolRepoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "repository-paths.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-lines.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-unity.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-native-command.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-package-provenance.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-versioning.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-readiness.psm1") -Force
$repoRoot = Resolve-RepositoryRoot $RepositoryRoot $toolRepoRoot

$report = Get-ReleaseReadinessReport `
    -ToolsRoot $toolRepoRoot `
    -RepositoryRoot $repoRoot `
    -ModName $ModName `
    -ReleaseLine $ReleaseLine `
    -UnityPath $UnityPath

if (-not $NoReport) {
    $reportDirectory = Resolve-RepositoryPath $toolRepoRoot $ReportRoot
    [void](New-Item -ItemType Directory -Path $reportDirectory -Force)
    $lineSuffix = if ([string]::IsNullOrWhiteSpace($ReleaseLine)) { "current" } else { $ReleaseLine }
    $reportPath = Join-Path $reportDirectory "$ModName-$($report.Candidate.SourceModVersion)-$lineSuffix.json"
    $report["ReportPath"] = $reportPath
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
}

if ($AsJson) {
    $report | ConvertTo-Json -Depth 8 -Compress
}
else {
    Format-ReleaseReadinessReport $report | ForEach-Object { Write-Host $_ }
    if (-not $NoReport) {
        Write-Host "  Report: $($report.ReportPath)"
    }
}

if (-not $report.ReadyForGate1) {
    exit 2
}
