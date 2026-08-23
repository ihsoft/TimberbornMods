param(
    [string] $RepositoryRoot = "",
    [switch] $AsJson
)

$ErrorActionPreference = "Stop"

$toolRepoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "repository-paths.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-lines.psm1") -Force
$repoRoot = Resolve-RepositoryRoot $RepositoryRoot $toolRepoRoot

function Get-UnpublishedSections(
    [string] $ReleaseRoot,
    [string] $Branch,
    [string] $ReleaseLine,
    [string] $GameVersion,
    [string] $VersionPolicy,
    [string[]] $ModNames
) {
    $changelogPaths = if ($ModNames.Count -gt 0) {
        @($ModNames | ForEach-Object {
            @("$_/CHANGELOG.md", "$_/CHANGES.md") | Where-Object {
                Test-Path -LiteralPath (Join-Path $ReleaseRoot $_) -PathType Leaf
            }
        })
    }
    else {
        @(& git -C $ReleaseRoot ls-files "*/CHANGELOG.md" "*/CHANGES.md")
    }

    foreach ($relativePath in @($changelogPaths | Sort-Object -Unique)) {
        $changesPath = Join-Path $ReleaseRoot $relativePath
        $content = Get-Content -Raw -LiteralPath $changesPath
        $matches = [regex]::Matches(
            $content,
            "(?m)^#\s+v?(?<version>\d+\.\d+(?:\.\d+)?)[^\r\n]*\(TBD\)[^\r\n]*$")
        foreach ($match in $matches) {
            [pscustomobject]@{
                ModName = ($relativePath -replace "\\", "/").Split('/')[0]
                Version = $match.Groups["version"].Value
                ReleaseRoot = $ReleaseRoot
                Branch = $Branch
                ReleaseLine = $ReleaseLine
                GameVersion = $GameVersion
                VersionPolicy = $VersionPolicy
                ChangesPath = ($relativePath -replace "\\", "/")
                Heading = $match.Value
            }
        }
    }
}

$currentBranch = (& git -C $repoRoot branch --show-current).Trim()
if ([string]::IsNullOrWhiteSpace($currentBranch)) {
    $currentBranch = "(detached)"
}
$candidates = New-Object System.Collections.Generic.List[object]
@(Get-UnpublishedSections $repoRoot $currentBranch "" "" "CurrentLine" @()) | ForEach-Object {
    $candidates.Add($_)
}

$registry = Get-ReleaseLineRegistry $PSScriptRoot
foreach ($line in $registry.CompatibilityLines) {
    $worktreeRoot = Resolve-RegisteredReleaseWorktree $repoRoot ([string]$line.Branch)
    @(Get-UnpublishedSections `
        $worktreeRoot `
        ([string]$line.Branch) `
        ([string]$line.ReleaseLine) `
        ([string]$line.GameVersion) `
        ([string]$line.VersionPolicy) `
        ([string[]]$line.Mods)) | ForEach-Object {
        $candidates.Add($_)
    }
}

if ($AsJson) {
    [object[]]$candidates.ToArray() | ConvertTo-Json -Depth 5
}
elseif ($candidates.Count -eq 0) {
    Write-Host "No unpublished mod versions were found in the current or registered compatibility worktrees."
}
else {
    $candidates | Format-Table ModName, Version, Branch, ReleaseLine, GameVersion, VersionPolicy, ChangesPath -AutoSize
}
