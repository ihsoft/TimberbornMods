param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $ReleaseRoot = "",
    [string] $Configuration = "Release",
    [string] $GameVersion = "version-1.1",
    [ValidateSet("CurrentLine", "BackportPatchOnly")]
    [string] $VersionPolicy = "CurrentLine",
    [string] $GameRoot = "_GAME!",
    [string] $OutputRoot = ".tools/release-preview",
    [string] $ReportRoot = ".tools/release-preflight",
    [string] $ReleaseLine = "",
    [string[]] $AdditionalReleaseSourceReportPath = @(),
    [string] $LocalModRoot = "",
    [string] $SteamConfigPath = "",
    [string] $SteamCmdPath = "",
    [string] $SteamUserName = "",
    [string] $ModIoConfigPath = "",
    [string] $ModIoAccessTokenPath = "",
    [string] $ReleaseCommit = "",
    [string] $Repository = "ihsoft/TimberbornMods",
    [string] $ReplacementPlatforms = "",
    [ValidateSet("", "PreserveExisting", "MoveAuthorized")]
    [string] $ExistingTagDisposition = "",
    [switch] $CorrectiveReplacement,
    [switch] $IncludeLegacyVersions,
    [switch] $SkipBuild,
    [switch] $SkipUnityExport,
    [switch] $SkipPlatformDescriptions,
    [switch] $SkipPlatformTags,
    [switch] $SkipSteam,
    [switch] $SkipModIo,
    [switch] $PublishSteamVisibility,
    [switch] $PublishModIoPage,
    [switch] $DetailedOutput
)

$ErrorActionPreference = "Stop"

$toolRepoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "repository-paths.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-output.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-package-provenance.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-lines.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-versioning.psm1") -Force
$repoRoot = Resolve-RepositoryRoot $ReleaseRoot $toolRepoRoot
$releaseBranch = (& git -C $repoRoot branch --show-current).Trim()
if ([string]::IsNullOrWhiteSpace($releaseBranch)) {
    $releaseBranch = "(detached)"
}
$releaseLogDirectory = ""
$releaseStepIndex = 0

function Resolve-RepoPath([string] $Path) {
    return Resolve-RepositoryPath $repoRoot $Path
}

function Resolve-ToolPath([string] $Path) {
    return Resolve-RepositoryPath $toolRepoRoot $Path
}

function Assert-PathExists([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Get-ObjectPropertyValue([object] $Object, [string] $Name) {
    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Get-StringSha256([string] $Value) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        $hashBytes = $sha.ComputeHash($bytes)
        return [System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-DirectoryFingerprint([string] $Path) {
    Assert-PathExists $Path "Package source"

    $root = (Resolve-Path -LiteralPath $Path).Path
    $entries = @(
        Get-ChildItem -LiteralPath $root -Recurse -File | Sort-Object FullName | ForEach-Object {
            $relativePath = $_.FullName.Substring($root.Length).TrimStart("\", "/") -replace "\\", "/"
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$relativePath|$($_.Length)|$hash"
        }
    )
    $content = $entries -join "`n"

    return [ordered]@{
        Path = $root
        FileCount = [int]$entries.Count
        Sha256 = Get-StringSha256 $content
    }
}

function Get-FileSnapshot([string] $Path, [string] $Description) {
    Assert-PathExists $Path $Description

    $item = Get-Item -LiteralPath $Path
    return [ordered]@{
        Path = $item.FullName
        Length = [long]$item.Length
        LastWriteTimeUtc = $item.LastWriteTimeUtc.ToString("o")
        Sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Get-PackagePath([object] $ReleaseConfig) {
    $packageMode = [string](Get-ObjectPropertyValue $ReleaseConfig.Package "Mode")
    if ([string]::IsNullOrWhiteSpace($packageMode)) {
        $packageMode = "ExistingZip"
    }

    if ($packageMode -eq "ExistingZip") {
        $path = [string](Get-ObjectPropertyValue $ReleaseConfig.Package "Path")
        if ([string]::IsNullOrWhiteSpace($path)) {
            throw "ExistingZip package mode requires Package.Path."
        }
        return Resolve-RepoPath $path
    }

    if ($packageMode -eq "LocalModFolder") {
        $path = [string](Get-ObjectPropertyValue $ReleaseConfig.Package "OutputPath")
        if ([string]::IsNullOrWhiteSpace($path)) {
            throw "LocalModFolder package mode requires Package.OutputPath."
        }
        return Resolve-RepoPath $path
    }

    throw "Unsupported package mode: $packageMode"
}

function Get-SourceSnapshot([object] $ReleaseConfig) {
    $packageMode = [string](Get-ObjectPropertyValue $ReleaseConfig.Package "Mode")
    if ($packageMode -ne "LocalModFolder") {
        return $null
    }

    $sourcePath = [string](Get-ObjectPropertyValue $ReleaseConfig.Package "SourcePath")
    if ([string]::IsNullOrWhiteSpace($sourcePath)) {
        throw "LocalModFolder package mode requires Package.SourcePath."
    }

    return Get-DirectoryFingerprint (Resolve-RepoPath $sourcePath)
}

function Get-ReleaseHeading([string] $ChangesPath, [string] $Version) {
    Assert-PathExists $ChangesPath "Changelog"

    $escapedVersion = [regex]::Escape($Version)
    $line = Get-Content -LiteralPath $ChangesPath | Where-Object {
        $_ -match "^#\s+v?$escapedVersion(\s|\(|:|$)"
    } | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($line)) {
        throw "Changelog section for version $Version was not found in $ChangesPath."
    }

    return [string]$line
}

function Get-TopReleaseVersion([string] $ChangesPath) {
    $line = Get-Content -LiteralPath $ChangesPath | Where-Object {
        $_ -match "^#\s+v?(?<version>\d+\.\d+(?:\.\d+)?)(?:\s|\(|:|$)"
    } | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($line) -or $line -notmatch "^#\s+v?(?<version>\d+\.\d+(?:\.\d+)?)") {
        throw "No top-level release heading was found in $ChangesPath."
    }
    return $Matches["version"]
}

function Get-LaneFingerprint([object[]] $Fingerprints, [string] $Lane, [string] $Description) {
    $matches = @($Fingerprints | Where-Object { [string]$_.GameVersion -eq $Lane })
    if ($matches.Count -ne 1) {
        throw "$Description must contain exactly one $Lane lane, found $($matches.Count)."
    }
    return $matches[0]
}

function Assert-ReleaseSourceCurrent([object] $Source) {
    $sourceRoot = Resolve-RepositoryRoot ([string]$Source.ReleaseRoot) $toolRepoRoot
    $sourceBranch = (& git -C $sourceRoot branch --show-current).Trim()
    if ([string]::IsNullOrWhiteSpace($sourceBranch)) {
        $sourceBranch = "(detached)"
    }
    if ($sourceBranch -ne [string]$Source.Branch) {
        throw "Release source branch changed. Preflight: $($Source.Branch); current: $sourceBranch."
    }

    $sourceCommit = [string]$Source.Commit
    $sourceHead = (& git -C $sourceRoot rev-parse HEAD).Trim()
    & git -C $sourceRoot merge-base --is-ancestor $sourceCommit $sourceHead
    if ($LASTEXITCODE -ne 0) {
        throw "Release source commit $sourceCommit is not an ancestor of $sourceHead in $sourceRoot."
    }

    $criticalPaths = @($Source.ReleaseCriticalPaths | ForEach-Object { [string]$_ })
    if ($criticalPaths.Count -eq 0) {
        throw "Release source $sourceRoot contains no release-critical paths."
    }
    $laterChanges = @(& git -C $sourceRoot diff --name-only "$sourceCommit..$sourceHead" -- @criticalPaths)
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot verify later release-critical changes in $sourceRoot."
    }
    if ($laterChanges.Count -gt 0) {
        throw "Release source has later critical changes: $($laterChanges -join ', ')."
    }
    $workingChanges = @(& git -C $sourceRoot diff --name-only $sourceCommit -- @criticalPaths)
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot verify working-tree release-critical changes in $sourceRoot."
    }
    if ($workingChanges.Count -gt 0) {
        throw "Release source differs from commit ${sourceCommit}: $($workingChanges -join ', ')."
    }

    $evidencePaths = @($Source.ReleaseCriticalPathEvidence | ForEach-Object { [string]$_.Path })
    if ($evidencePaths.Count -ne $criticalPaths.Count -or
        @($criticalPaths | Where-Object { $_ -notin $evidencePaths }).Count -gt 0) {
        throw "Release source is missing release-critical path evidence."
    }
}

function Invoke-ReleaseStep([string] $Name, [string] $ScriptName, [string[]] $Arguments) {
    Write-Host ""
    Write-Host "== $Name =="

    $scriptPath = Join-Path $PSScriptRoot $ScriptName
    $script:releaseStepIndex++
    return Invoke-LoggedReleaseStep -Name $Name -ScriptPath $scriptPath -Arguments $Arguments `
        -LogDirectory $releaseLogDirectory -StepIndex $releaseStepIndex -DetailedOutput:$DetailedOutput
}

function Add-OptionalArgument([System.Collections.Generic.List[string]] $Arguments, [string] $Name, [string] $Value) {
    if (-not [string]::IsNullOrWhiteSpace($Value)) {
        $Arguments.Add($Name)
        $Arguments.Add($Value)
    }
}

function Add-SwitchArgument([System.Collections.Generic.List[string]] $Arguments, [string] $Name, [bool] $Enabled) {
    if ($Enabled) {
        $Arguments.Add($Name)
    }
}

$modRoot = Join-Path $repoRoot $ModName
$releaseConfigPath = Join-Path $modRoot "release.json"
Assert-PathExists $releaseConfigPath "Release config"

$releaseConfig = Get-Content -Raw -LiteralPath $releaseConfigPath | ConvertFrom-Json
$modVersion = [string](Get-ObjectPropertyValue $releaseConfig "ReleaseVersion")
if ([string]::IsNullOrWhiteSpace($modVersion)) {
    throw "ReleaseVersion is empty in $releaseConfigPath."
}
$releaseLogDirectory = New-ReleaseLogDirectory -RepoRoot $repoRoot -ModName $ModName -Version $modVersion -Phase "verify"

$changesPath = Resolve-RepoPath ([string](Get-ObjectPropertyValue $releaseConfig "ChangesPath"))
$packageMode = [string](Get-ObjectPropertyValue $releaseConfig.Package "Mode")
if ([string]::IsNullOrWhiteSpace($packageMode)) {
    $packageMode = "ExistingZip"
}

$changelogHeading = Get-ReleaseHeading $changesPath $modVersion
$topChangelogVersion = Get-TopReleaseVersion $changesPath
if (-not $CorrectiveReplacement -and $topChangelogVersion -ne $modVersion) {
    throw "ReleaseVersion $modVersion does not match top changelog version $topChangelogVersion in $changesPath."
}
if ($changelogHeading -match "\(TBD\)") {
    throw "Changelog heading for $ModName v$modVersion is still marked (TBD). Set and commit the concrete release date before final preflight."
}
Assert-ReleaseVersionMatchesChangelog $changesPath $modVersion $VersionPolicy
if ($VersionPolicy -eq "BackportPatchOnly") {
    if ([string]::IsNullOrWhiteSpace($ReleaseLine)) {
        throw "BackportPatchOnly requires a release-line identifier such as update-1.0."
    }
    if ($ReleaseLine -notmatch "^[A-Za-z0-9][A-Za-z0-9.-]*$") {
        throw "Release line contains unsupported tag characters: $ReleaseLine"
    }
    $registeredLine = Get-RegisteredReleaseLine $PSScriptRoot $ReleaseLine $ModName
    if ([string]$registeredLine.Branch -ne $releaseBranch) {
        throw "Release line $ReleaseLine is registered to branch $($registeredLine.Branch), not $releaseBranch."
    }
    if ([string]$registeredLine.GameVersion -ne $GameVersion) {
        throw "Release line $ReleaseLine requires game lane $($registeredLine.GameVersion), not $GameVersion."
    }
    if ([string]$registeredLine.VersionPolicy -ne $VersionPolicy) {
        throw "Release line $ReleaseLine requires version policy $($registeredLine.VersionPolicy)."
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($ReleaseLine)) {
    throw "ReleaseLine is reserved for a qualified backport release."
}
if ($CorrectiveReplacement -and $AdditionalReleaseSourceReportPath.Count -gt 0) {
    throw "Multi-source corrective replacement is not supported by the routine release workflow."
}
if ($AdditionalReleaseSourceReportPath.Count -gt 0 -and $VersionPolicy -ne "CurrentLine") {
    throw "A multi-source release must use the current compatibility line as its primary source."
}

function Invoke-Git([string[]] $Arguments) {
    $output = @(& git -C $repoRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed.`n$($output -join [Environment]::NewLine)"
    }
    return [string[]]$output
}

function Convert-ToRepositoryPath([string] $Path) {
    $absolutePath = [System.IO.Path]::GetFullPath($Path)
    $rootWithSeparator = $repoRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $absolutePath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $null
    }
    return $absolutePath.Substring($rootWithSeparator.Length).Replace('\', '/')
}

function Get-ReleaseGitSnapshot(
    [string] $RequestedCommit,
    [string[]] $IdentityPaths,
    [string[]] $CriticalPaths
) {
    $head = [string](@(Invoke-Git @("rev-parse", "HEAD"))[0])
    $commit = if ([string]::IsNullOrWhiteSpace($RequestedCommit)) {
        $candidate = @(Invoke-Git (@("log", "-1", "--format=%H", "--") + $IdentityPaths))
        if ($candidate.Count -eq 0 -or [string]::IsNullOrWhiteSpace($candidate[0])) {
            throw "Could not identify a release-preparation commit for the selected mod. Pass -ReleaseCommit explicitly."
        }
        [string]$candidate[0]
    }
    else {
        [string](@(Invoke-Git @("rev-parse", "$RequestedCommit^{commit}"))[0])
    }

    & git -C $repoRoot merge-base --is-ancestor $commit $head
    if ($LASTEXITCODE -ne 0) {
        throw "Selected release commit $commit is not an ancestor of current HEAD $head."
    }

    $identityChanges = @(Invoke-Git (@("diff-tree", "--no-commit-id", "--name-only", "-r", $commit, "--") + $IdentityPaths))
    if ($identityChanges.Count -eq 0) {
        throw "Selected release commit $commit does not change the selected mod's release identity paths. Pass the correct -ReleaseCommit."
    }

    $laterCriticalChanges = @(Invoke-Git (@("diff", "--name-only", "$commit..$head", "--") + $CriticalPaths))
    if ($laterCriticalChanges.Count -gt 0) {
        throw "Changes after release commit $commit alter release-critical paths: $($laterCriticalChanges -join ', '). Pass the correct -ReleaseCommit or prepare a new release commit."
    }

    $workingTreeCriticalChanges = @(Invoke-Git (@("diff", "--name-only", $commit, "--") + $CriticalPaths))
    if ($workingTreeCriticalChanges.Count -gt 0) {
        throw "Tracked release-critical paths differ from selected release commit ${commit}: $($workingTreeCriticalChanges -join ', '). Commit release preparation before final preflight."
    }

    $criticalPathEvidence = @($CriticalPaths | ForEach-Object {
        [ordered]@{
            Path = [string]$_
            WorkingTreeMatchesReleaseCommit = $true
        }
    })

    return [ordered]@{
        HeadAtPreflight = $head
        ReleaseCommit = $commit
        IdentityPaths = [string[]]$IdentityPaths
        CriticalPaths = [string[]]$CriticalPaths
        LaterCriticalChanges = [string[]]$laterCriticalChanges
        WorkingTreeCriticalChanges = [string[]]$workingTreeCriticalChanges
        CriticalPathEvidence = $criticalPathEvidence
    }
}

$identityPaths = New-Object System.Collections.Generic.List[string]
$identityPaths.Add("$ModName/release.json")
$changesRepoPath = Convert-ToRepositoryPath $changesPath
if (-not [string]::IsNullOrWhiteSpace($changesRepoPath)) {
    $identityPaths.Add($changesRepoPath)
}
$manifestPathValue = [string](Get-ObjectPropertyValue $releaseConfig "ManifestPath")
if (-not [string]::IsNullOrWhiteSpace($manifestPathValue)) {
    $manifestRepoPath = Convert-ToRepositoryPath (Resolve-RepoPath $manifestPathValue)
    if (-not [string]::IsNullOrWhiteSpace($manifestRepoPath)) {
        $identityPaths.Add($manifestRepoPath)
    }
}
$buildPropsPath = Join-Path $modRoot "directory.build.props"
if (Test-Path -LiteralPath $buildPropsPath) {
    $identityPaths.Add("$ModName/directory.build.props")
}
$unityModPath = "ModsUnityProject/Assets/Mods/$ModName"
$identityPaths.Add($unityModPath)

$criticalPaths = New-Object System.Collections.Generic.List[string]
$criticalPaths.Add($ModName)
$criticalPaths.Add($unityModPath)
$criticalPaths.Add("TimberDev")
$criticalPaths.Add("TimberDev.Tests")
$packageSourceValue = [string](Get-ObjectPropertyValue $releaseConfig.Package "SourcePath")
if (-not [string]::IsNullOrWhiteSpace($packageSourceValue)) {
    $packageSourceRepoPath = Convert-ToRepositoryPath (Resolve-RepoPath $packageSourceValue)
    if (-not [string]::IsNullOrWhiteSpace($packageSourceRepoPath)) {
        $criticalPaths.Add($packageSourceRepoPath)
    }
}
$gitSnapshot = Get-ReleaseGitSnapshot $ReleaseCommit `
    @($identityPaths | Sort-Object -Unique) `
    @($criticalPaths | Sort-Object -Unique)

$steamPublishedFileId = [string](Get-ObjectPropertyValue $releaseConfig.Steam "PublishedFileId")
$resolvedModIoConfigPath = if ([string]::IsNullOrWhiteSpace($ModIoConfigPath)) {
    Resolve-ToolPath ".tools/modio/$ModName.local.json"
}
else {
    Resolve-ToolPath $ModIoConfigPath
}
$modIoId = ""
if (Test-Path -LiteralPath $resolvedModIoConfigPath) {
    $modIoConfig = Get-Content -Raw -LiteralPath $resolvedModIoConfigPath | ConvertFrom-Json
    $modIoId = [string](Get-ObjectPropertyValue $modIoConfig "ModId")
}

Write-Host "Release preflight: $ModName v$modVersion"
Write-Host "  Release root: $repoRoot ($releaseBranch)"
Write-Host "  Release commit: $($gitSnapshot.ReleaseCommit)"
Write-Host "  Game compatibility lane: $GameVersion"
Write-Host "  Version policy: $VersionPolicy"
if (-not $SkipSteam) {
    $steamVisibilityIntent = if ($PublishSteamVisibility) { "publish requested" } else { "unchanged" }
    Write-Host "  Steam: PublishedFileId=$steamPublishedFileId; visibility=$steamVisibilityIntent"
}
if (-not $SkipModIo) {
    $modIoVisibilityIntent = if ($PublishModIoPage) { "publish requested" } else { "unchanged" }
    Write-Host "  Mod.IO: ModId=$modIoId; page visibility=$modIoVisibilityIntent"
}
Write-Host "  Full child logs: $releaseLogDirectory"

$steps = New-Object System.Collections.Generic.List[object]

if (-not $SkipPlatformDescriptions) {
    $descriptionArgs = New-Object System.Collections.Generic.List[string]
    $descriptionArgs.Add("-ModName")
    $descriptionArgs.Add($ModName)
    $descriptionArgs.Add("-ReleaseRoot")
    $descriptionArgs.Add($repoRoot)
    Add-SwitchArgument $descriptionArgs "-SkipSteam" $SkipSteam
    Add-SwitchArgument $descriptionArgs "-SkipModIo" $SkipModIo
    $steps.Add((Invoke-ReleaseStep "Platform description verification" "verify-platform-descriptions.ps1" $descriptionArgs.ToArray()))
}

$commonPublishArgs = New-Object System.Collections.Generic.List[string]
$commonPublishArgs.Add("-ModName")
$commonPublishArgs.Add($ModName)
$commonPublishArgs.Add("-ReleaseRoot")
$commonPublishArgs.Add($repoRoot)
$commonPublishArgs.Add("-Configuration")
$commonPublishArgs.Add($Configuration)
$commonPublishArgs.Add("-GameVersion")
$commonPublishArgs.Add($GameVersion)
$commonPublishArgs.Add("-GameRoot")
$commonPublishArgs.Add($GameRoot)
$commonPublishArgs.Add("-OutputRoot")
$commonPublishArgs.Add($OutputRoot)
Add-OptionalArgument $commonPublishArgs "-LocalModRoot" $LocalModRoot
Add-SwitchArgument $commonPublishArgs "-IncludeLegacyVersions" $IncludeLegacyVersions
Add-SwitchArgument $commonPublishArgs "-SkipBuild" $SkipBuild
Add-SwitchArgument $commonPublishArgs "-SkipUnityExport" $SkipUnityExport
Add-SwitchArgument $commonPublishArgs "-CorrectiveReplacement" $CorrectiveReplacement

if (-not $SkipSteam) {
    $steamArgs = New-Object System.Collections.Generic.List[string]
    $steamArgs.AddRange([string[]]$commonPublishArgs)
    Add-OptionalArgument $steamArgs "-SteamConfigPath" $SteamConfigPath
    Add-OptionalArgument $steamArgs "-SteamCmdPath" $SteamCmdPath
    Add-OptionalArgument $steamArgs "-SteamUserName" $SteamUserName
    Add-SwitchArgument $steamArgs "-UpdateVisibility" $PublishSteamVisibility
    $steps.Add((Invoke-ReleaseStep "Steam release dry run" "publish-steam.ps1" $steamArgs.ToArray()))
}

if (-not $SkipModIo) {
    $modIoArgs = New-Object System.Collections.Generic.List[string]
    $modIoArgs.AddRange([string[]]$commonPublishArgs)
    Add-OptionalArgument $modIoArgs "-ConfigPath" $ModIoConfigPath
    Add-OptionalArgument $modIoArgs "-AccessTokenPath" $ModIoAccessTokenPath
    Add-SwitchArgument $modIoArgs "-PublishPage" $PublishModIoPage
    $steps.Add((Invoke-ReleaseStep "Mod.IO release dry run" "publish-modio.ps1" $modIoArgs.ToArray()))
}

if (-not $SkipPlatformTags) {
    $tagArgs = New-Object System.Collections.Generic.List[string]
    $tagArgs.Add("-ModName")
    $tagArgs.Add($ModName)
    $tagArgs.Add("-ReleaseRoot")
    $tagArgs.Add($repoRoot)
    if ($SkipSteam) {
        $tagArgs.Add("-Platform")
        $tagArgs.Add("ModIO")
    }
    elseif ($SkipModIo) {
        $tagArgs.Add("-Platform")
        $tagArgs.Add("Steam")
    }
    Add-OptionalArgument $tagArgs "-LocalModRoot" $LocalModRoot
    Add-OptionalArgument $tagArgs "-SteamConfigPath" $SteamConfigPath
    Add-OptionalArgument $tagArgs "-SteamCmdPath" $SteamCmdPath
    Add-OptionalArgument $tagArgs "-SteamUserName" $SteamUserName
    Add-OptionalArgument $tagArgs "-ModIoConfigPath" $ModIoConfigPath
    Add-OptionalArgument $tagArgs "-ModIoAccessTokenPath" $ModIoAccessTokenPath
    $steps.Add((Invoke-ReleaseStep "Platform tag dry run" "update-platform-tags.ps1" $tagArgs.ToArray()))

    $materializeTagArgs = New-Object System.Collections.Generic.List[string]
    $materializeTagArgs.Add("-ModName")
    $materializeTagArgs.Add($ModName)
    $materializeTagArgs.Add("-ReleaseRoot")
    $materializeTagArgs.Add($repoRoot)
    Add-OptionalArgument $materializeTagArgs "-LocalModRoot" $LocalModRoot
    $materializeTagArgs.Add("-MaterializeLocalOnly")
    $steps.Add((Invoke-ReleaseStep "Local release tag materialization" "update-platform-tags.ps1" $materializeTagArgs.ToArray()))

    $finalPublishArgs = New-Object System.Collections.Generic.List[string]
    $finalPublishArgs.AddRange([string[]]$commonPublishArgs)
    if (-not $SkipBuild) {
        $finalPublishArgs.Add("-SkipBuild")
    }
    if (-not $SkipUnityExport) {
        $finalPublishArgs.Add("-SkipUnityExport")
    }

    if (-not $SkipSteam) {
        $finalSteamArgs = New-Object System.Collections.Generic.List[string]
        $finalSteamArgs.AddRange([string[]]$finalPublishArgs)
        Add-OptionalArgument $finalSteamArgs "-SteamConfigPath" $SteamConfigPath
        Add-OptionalArgument $finalSteamArgs "-SteamCmdPath" $SteamCmdPath
        Add-OptionalArgument $finalSteamArgs "-SteamUserName" $SteamUserName
        Add-SwitchArgument $finalSteamArgs "-UpdateVisibility" $PublishSteamVisibility
        $steps.Add((Invoke-ReleaseStep "Final Steam release dry run" "publish-steam.ps1" $finalSteamArgs.ToArray()))
    }

    if (-not $SkipModIo) {
        $finalModIoArgs = New-Object System.Collections.Generic.List[string]
        $finalModIoArgs.AddRange([string[]]$finalPublishArgs)
        Add-OptionalArgument $finalModIoArgs "-ConfigPath" $ModIoConfigPath
        Add-OptionalArgument $finalModIoArgs "-AccessTokenPath" $ModIoAccessTokenPath
        Add-SwitchArgument $finalModIoArgs "-PublishPage" $PublishModIoPage
        $steps.Add((Invoke-ReleaseStep "Final Mod.IO release dry run" "publish-modio.ps1" $finalModIoArgs.ToArray()))
    }

    $steps.Add((Invoke-ReleaseStep "Final platform tag dry run" "update-platform-tags.ps1" $tagArgs.ToArray()))
}

Write-Host ""
Write-Host "Collecting release artifact snapshots..."
$packagePath = Get-PackagePath $releaseConfig
$packageSnapshot = Get-FileSnapshot $packagePath "Release package"
$packageLaneFingerprints = @(Get-PackageLaneFingerprints $packagePath)
$primaryLaneFingerprint = Get-LaneFingerprint $packageLaneFingerprints $GameVersion "Primary release package"
$sourceSnapshot = Get-SourceSnapshot $releaseConfig
$gitStatus = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=normal)
$tagName = if ($VersionPolicy -eq "BackportPatchOnly") {
    "$($ModName)_${modVersion}_$ReleaseLine"
}
else {
    "$($ModName)_$modVersion"
}
$tagExists = $false
& git -C $repoRoot rev-parse -q --verify "refs/tags/$tagName" *> $null
if ($LASTEXITCODE -eq 0) {
    $tagExists = $true
}

$releaseSources = New-Object System.Collections.Generic.List[object]
$releaseSources.Add([ordered]@{
    ReleaseRoot = $repoRoot
    Branch = $releaseBranch
    Commit = $gitSnapshot.ReleaseCommit
    GameVersion = $GameVersion
    ModVersion = $modVersion
    ReleaseLine = $ReleaseLine
    VersionPolicy = $VersionPolicy
    TagName = $tagName
    TagExistsBeforePublish = $tagExists
    PackageLaneSha256 = [string]$primaryLaneFingerprint.Sha256
    ChangesPath = (Resolve-Path -LiteralPath $changesPath).Path
    ChangelogHeading = $changelogHeading
    ReleaseCriticalPaths = [string[]]$gitSnapshot.CriticalPaths
    ReleaseCriticalPathEvidence = [object[]]$gitSnapshot.CriticalPathEvidence
    IsPrimary = $true
})

foreach ($additionalReportValue in $AdditionalReleaseSourceReportPath) {
    $additionalReportPath = Resolve-ToolPath $additionalReportValue
    Assert-PathExists $additionalReportPath "Additional release-source preflight report"
    $additionalReport = Get-Content -Raw -LiteralPath $additionalReportPath | ConvertFrom-Json
    if ([int]$additionalReport.SchemaVersion -ne 4 -or $additionalReport.ReadyForPublish -ne $true) {
        throw "Additional release source must be a publish-ready schema-4 preflight report: $additionalReportPath"
    }
    if ([string]$additionalReport.ModName -ne $ModName) {
        throw "Additional release source is for $($additionalReport.ModName), expected $ModName."
    }

    $additionalSources = @($additionalReport.ReleaseSources)
    if ($additionalSources.Count -ne 1) {
        throw "Additional release-source report must describe exactly one source, found $($additionalSources.Count)."
    }
    $additionalSource = $additionalSources[0]
    $additionalLane = [string]$additionalSource.GameVersion
    if (@($releaseSources | Where-Object { $_.GameVersion -eq $additionalLane }).Count -gt 0) {
        throw "Release source lane is duplicated: $additionalLane"
    }
    if (@($releaseSources | Where-Object { $_.TagName -eq [string]$additionalSource.TagName }).Count -gt 0) {
        throw "Release source tag is duplicated: $($additionalSource.TagName)"
    }

    Assert-ReleaseSourceCurrent $additionalSource
    $additionalPackagePath = [string]$additionalReport.Package.Path
    Assert-PathExists $additionalPackagePath "Additional release-source package"
    $additionalPackageHash = (Get-FileHash -LiteralPath $additionalPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($additionalPackageHash -ne [string]$additionalReport.Package.Sha256) {
        throw "Additional release-source package changed after preflight: $additionalPackagePath"
    }
    $additionalFingerprints = @(Get-PackageLaneFingerprints $additionalPackagePath)
    $additionalLaneFingerprint = Get-LaneFingerprint `
        $additionalFingerprints $additionalLane "Additional release-source package"
    if ([string]$additionalLaneFingerprint.Sha256 -ne [string]$additionalSource.PackageLaneSha256) {
        throw "Additional release-source lane no longer matches its preflight evidence: $additionalLane"
    }
    $finalLaneFingerprint = Get-LaneFingerprint $packageLaneFingerprints $additionalLane "Final combined package"
    if ([string]$finalLaneFingerprint.Sha256 -ne [string]$additionalLaneFingerprint.Sha256) {
        throw "Final package lane $additionalLane does not match the additional release-source package."
    }

    $releaseSources.Add([ordered]@{
        ReleaseRoot = [string]$additionalSource.ReleaseRoot
        Branch = [string]$additionalSource.Branch
        Commit = [string]$additionalSource.Commit
        GameVersion = $additionalLane
        ModVersion = [string]$additionalSource.ModVersion
        ReleaseLine = [string]$additionalSource.ReleaseLine
        VersionPolicy = [string]$additionalSource.VersionPolicy
        TagName = [string]$additionalSource.TagName
        TagExistsBeforePublish = [bool]$additionalSource.TagExistsBeforePublish
        PackageLaneSha256 = [string]$additionalSource.PackageLaneSha256
        ChangesPath = [string]$additionalSource.ChangesPath
        ChangelogHeading = [string]$additionalSource.ChangelogHeading
        ReleaseCriticalPaths = [string[]]$additionalSource.ReleaseCriticalPaths
        ReleaseCriticalPathEvidence = [object[]]$additionalSource.ReleaseCriticalPathEvidence
        IsPrimary = $false
    })
}

$correctiveSnapshot = $null
if ($CorrectiveReplacement) {
    $platforms = @($ReplacementPlatforms -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Sort-Object -Unique)
    $allowedPlatforms = @("GitHub", "ModIO", "Steam")
    $unsupportedPlatforms = @($platforms | Where-Object { $_ -notin $allowedPlatforms })
    if ($platforms.Count -eq 0 -or $unsupportedPlatforms.Count -gt 0) {
        throw "Corrective replacement requires -ReplacementPlatforms using only: $($allowedPlatforms -join ', ')."
    }
    if ([string]::IsNullOrWhiteSpace($ExistingTagDisposition)) {
        throw "Corrective replacement requires -ExistingTagDisposition."
    }
    if (-not $tagExists) {
        throw "Corrective replacement requires the existing release tag $tagName."
    }

    $existingTagCommit = (& git -C $repoRoot rev-list -n 1 $tagName).Trim()
    $existingArtifacts = [ordered]@{}

    if ("Steam" -in $platforms) {
        $workshopDataPath = Join-Path $repoRoot "ModsUnityProject/Assets/Mods/$ModName/Data/workshop_data.json"
        Assert-PathExists $workshopDataPath "Steam workshop metadata"
        $workshopData = Get-Content -Raw -LiteralPath $workshopDataPath | ConvertFrom-Json
        $publishedFileId = [string]$workshopData.ItemId
        $steamResponse = Invoke-RestMethod -Method Post `
            -Uri "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/" `
            -Body @{ itemcount = "1"; "publishedfileids[0]" = $publishedFileId }
        $steamItem = $steamResponse.response.publishedfiledetails[0]
        $existingArtifacts.Steam = [ordered]@{
            PublishedFileId = $publishedFileId
            TimeUpdated = [long]$steamItem.time_updated
            FileSize = [long]$steamItem.file_size
        }
    }

    if ("ModIO" -in $platforms) {
        $resolvedModIoConfigPath = Resolve-ToolPath $ModIoConfigPath
        $resolvedTokenPath = Resolve-ToolPath $ModIoAccessTokenPath
        Assert-PathExists $resolvedModIoConfigPath "Mod.IO config"
        Assert-PathExists $resolvedTokenPath "Mod.IO access token"
        $modIoConfig = Get-Content -Raw -LiteralPath $resolvedModIoConfigPath | ConvertFrom-Json
        $token = (Get-Content -Raw -LiteralPath $resolvedTokenPath).Trim()
        $modIoParent = Invoke-RestMethod `
            -Uri "$($modIoConfig.ApiBase.TrimEnd('/'))/games/$($modIoConfig.GameId)/mods/$($modIoConfig.ModId)" `
            -Headers @{ Authorization = "Bearer $token" }
        $existingArtifacts.ModIO = [ordered]@{
            ModId = [long]$modIoConfig.ModId
            FileId = [long]$modIoParent.modfile.id
            Version = [string]$modIoParent.modfile.version
            Md5 = [string]$modIoParent.modfile.filehash.md5
            FileSize = [long]$modIoParent.modfile.filesize
        }
    }

    if ("GitHub" -in $platforms) {
        $releaseJson = & gh release view $tagName --repo $Repository --json url,tagName,assets
        if ($LASTEXITCODE -ne 0) {
            throw "Existing GitHub release was not found for $tagName."
        }
        $githubRelease = $releaseJson | ConvertFrom-Json
        $assetName = [System.IO.Path]::GetFileName($packageSnapshot.Path)
        $asset = @($githubRelease.assets | Where-Object { $_.name -eq $assetName })
        if ($asset.Count -ne 1) {
            throw "Expected exactly one existing GitHub asset named $assetName, found $($asset.Count)."
        }
        $existingArtifacts.GitHub = [ordered]@{
            Url = [string]$githubRelease.url
            AssetName = [string]$asset[0].name
            Digest = [string]$asset[0].digest
            Size = [long]$asset[0].size
        }
    }

    $correctiveSnapshot = [ordered]@{
        Enabled = $true
        CorrectionCommit = $gitSnapshot.ReleaseCommit
        Platforms = [string[]]$platforms
        ExistingTagCommit = $existingTagCommit
        ExistingTagDisposition = $ExistingTagDisposition
        ExistingArtifacts = $existingArtifacts
    }
}

$report = [ordered]@{
    SchemaVersion = 4
    CreatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    RepoRoot = $repoRoot
    ReleaseRoot = $repoRoot
    ReleaseBranch = $releaseBranch
    ReleaseLine = $ReleaseLine
    VersionPolicy = $VersionPolicy
    GitHead = $gitSnapshot.HeadAtPreflight
    ReleaseCommit = $gitSnapshot.ReleaseCommit
    ReleaseIdentityPaths = $gitSnapshot.IdentityPaths
    ReleaseCriticalPaths = $gitSnapshot.CriticalPaths
    ReleaseCriticalPathEvidence = $gitSnapshot.CriticalPathEvidence
    LaterReleaseCriticalChanges = $gitSnapshot.LaterCriticalChanges
    WorkingTreeReleaseCriticalChanges = $gitSnapshot.WorkingTreeCriticalChanges
    CorrectiveReplacement = $correctiveSnapshot
    GitStatus = [string[]]$gitStatus
    ModName = $ModName
    Version = $modVersion
    TagName = $tagName
    ReleaseSources = [object[]]$releaseSources.ToArray()
    TagExistsBeforePublish = $tagExists
    GameVersion = $GameVersion
    Configuration = $Configuration
    ReleaseConfigPath = (Resolve-Path -LiteralPath $releaseConfigPath).Path
    ChangesPath = (Resolve-Path -LiteralPath $changesPath).Path
    ChangelogHeading = $changelogHeading
    PackageMode = $packageMode
    Package = $packageSnapshot
    PackageLaneFingerprints = [object[]]$packageLaneFingerprints
    Source = $sourceSnapshot
    PreflightOptions = [ordered]@{
        IncludeLegacyVersions = [bool]$IncludeLegacyVersions
        SkipBuild = [bool]$SkipBuild
        SkipUnityExport = [bool]$SkipUnityExport
        SkipPlatformDescriptions = [bool]$SkipPlatformDescriptions
        SkipPlatformTags = [bool]$SkipPlatformTags
        SkipSteam = [bool]$SkipSteam
        SkipModIo = [bool]$SkipModIo
        CorrectiveReplacement = [bool]$CorrectiveReplacement
        PublishSteamVisibility = [bool]$PublishSteamVisibility
        PublishModIoPage = [bool]$PublishModIoPage
    }
    Steps = [object[]]$steps.ToArray()
    ReleaseLogDirectory = $releaseLogDirectory
    ReadyForPublish = $true
}

$reportDirectory = Resolve-RepoPath $ReportRoot
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$reportPath = Join-Path $reportDirectory "$ModName-$modVersion.json"
Write-Host "Writing preflight report..."
$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "Release preflight completed for $ModName v$modVersion."
Write-Host "Report: $reportPath"
Write-Host "Package: $($packageSnapshot.Path)"
Write-Host "Package SHA256: $($packageSnapshot.Sha256)"
if ($null -ne $sourceSnapshot) {
    Write-Host "Source fingerprint: $($sourceSnapshot.Sha256)"
}
if ($tagExists) {
    Write-Host "Warning: release tag already exists before publish: $tagName"
}
