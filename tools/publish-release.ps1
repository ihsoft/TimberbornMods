param(
    [Parameter(Mandatory = $true)]
    [string] $PreflightReportPath,

    [string] $ReleaseRoot = "",
    [string] $Configuration = "",
    [string] $GameVersion = "",
    [ValidateSet("", "CurrentLine", "BackportPatchOnly")]
    [string] $VersionPolicy = "",
    [string] $GameRoot = "_GAME!",
    [string] $OutputRoot = ".tools/release-preview",
    [string] $LocalModRoot = "",
    [string] $SteamConfigPath = "",
    [string] $SteamCmdPath = "",
    [string] $SteamUserName = "",
    [string] $ModIoConfigPath = "",
    [string] $ModIoAccessTokenPath = "",
    [string] $Repository = "ihsoft/TimberbornMods",
    [string] $SignoffReportRoot = ".tools/release-signoff",
    [string[]] $EvidenceTerm = @(),
    [switch] $SkipSteam,
    [switch] $SkipModIo,
    [switch] $SkipPlatformTags,
    [switch] $SkipGitTag,
    [switch] $SkipGitHubRelease,
    [switch] $SkipDiscordHandoff,
    [switch] $ReplaceExistingGitHubAsset,
    [switch] $CorrectiveReplacement,
    [switch] $PreserveChangelogDate,
    [switch] $DetailedOutput
)

$ErrorActionPreference = "Stop"

$toolRepoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "repository-paths.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-output.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-package-provenance.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-notes.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-native-command.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-date.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-signoff.psm1") -Force
$repoRoot = Resolve-RepositoryRoot $ReleaseRoot $toolRepoRoot
$releaseLogDirectory = ""
$releaseStepIndex = 0
$completedPublicSteps = New-Object System.Collections.Generic.List[string]
$publishOperations = New-Object System.Collections.Generic.List[object]
$publishWarnings = New-Object System.Collections.Generic.List[string]
$signoffReportPath = ""

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

function Invoke-Git {
    param(
        [string[]] $Arguments,
        [string] $RepositoryRoot = $repoRoot
    )

    $gitResult = Invoke-ReleaseNativeCommand -Command "git" -Arguments (
        @("-C", $RepositoryRoot) + $Arguments)
    if ($gitResult.ExitCode -ne 0) {
        throw "git $($Arguments -join ' ') failed.`n$($gitResult.Diagnostics -join [Environment]::NewLine)"
    }
    $gitResult.StandardError | ForEach-Object { Write-Warning $_ }
    return [string[]]$gitResult.Output
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

function Get-LaneFingerprint([object[]] $Fingerprints, [string] $Lane) {
    $matches = @($Fingerprints | Where-Object { [string]$_.GameVersion -eq $Lane })
    if ($matches.Count -ne 1) {
        throw "Final release package must contain exactly one $Lane lane, found $($matches.Count)."
    }
    return $matches[0]
}

function Get-TimberbornGameLine([string] $VersionFolder) {
    if ($VersionFolder -notmatch "^version-(?<version>\d+\.\d+)$") {
        throw "Cannot derive a Timberborn release line from $VersionFolder."
    }
    return $Matches["version"]
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
    $laterChanges = @(Invoke-Git `
        -Arguments (@("diff", "--name-only", "$sourceCommit..$sourceHead", "--") + $criticalPaths) `
        -RepositoryRoot $sourceRoot)
    if ($laterChanges.Count -gt 0) {
        throw "Release source has later critical changes: $($laterChanges -join ', ')."
    }
    $workingChanges = @(Invoke-Git `
        -Arguments (@("diff", "--name-only", $sourceCommit, "--") + $criticalPaths) `
        -RepositoryRoot $sourceRoot)
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
    try {
        $operation = Invoke-LoggedReleaseStep -Name $Name -ScriptPath $scriptPath -Arguments $Arguments `
            -LogDirectory $releaseLogDirectory -StepIndex $releaseStepIndex -DetailedOutput:$DetailedOutput
        if ($Name -eq "Discord release handoff") {
            $operation.AffectsReleaseCompletion = $false
        }
        $publishOperations.Add($operation)
    }
    catch {
        $failedOperation = $_.Exception.Data["ReleaseOperation"]
        if ($null -ne $failedOperation) {
            if ($Name -eq "Discord release handoff") {
                $failedOperation.AffectsReleaseCompletion = $false
            }
            $publishOperations.Add($failedOperation)
        }
        if ($completedPublicSteps.Count -gt 0) {
            Write-Warning "Partial success before failure: $($completedPublicSteps -join ', ')."
        }
        Write-Warning "The failing step may have changed external state. Verify it before retrying."
        throw
    }

    if ($Name -ne "Discord release handoff") {
        $completedPublicSteps.Add($Name)
    }
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

function Get-SignoffExternalEvidence([bool] $FinalSuccess) {
    $steamOperations = @($publishOperations | Where-Object Name -eq "Steam release publish")
    $modIoOperations = @($publishOperations | Where-Object Name -eq "Mod.IO release publish")
    $steamPublishedFileId = [string](Get-ObjectPropertyValue $releaseConfig.Steam "PublishedFileId")
    $steamReadback = $null
    if ($FinalSuccess -and $steamOperations.Count -gt 0 -and
            -not [string]::IsNullOrWhiteSpace($steamPublishedFileId)) {
        try {
            $steamResponse = Invoke-RestMethod -Method Post `
                -Uri "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/" `
                -Body @{ itemcount = "1"; "publishedfileids[0]" = $steamPublishedFileId }
            $steamItem = $steamResponse.response.publishedfiledetails[0]
            if ([int]$steamItem.result -eq 1) {
                $steamReadback = [ordered]@{
                    PublishedFileId = [string]$steamItem.publishedfileid
                    Result = [int]$steamItem.result
                    TimeUpdated = [long]$steamItem.time_updated
                    FileSize = [long]$steamItem.file_size
                    Visibility = [int]$steamItem.visibility
                }
            }
            else {
                $publishWarnings.Add("Final public Steam readback returned result $($steamItem.result).")
            }
        }
        catch {
            $publishWarnings.Add("Final public Steam readback failed: $($_.Exception.Message)")
        }
    }
    $gitEvidence = New-Object System.Collections.Generic.List[object]
    foreach ($releaseSource in @($report.ReleaseSources)) {
        $sourceTag = [string]$releaseSource.TagName
        $sourceCommit = [string]$releaseSource.Commit
        $localResult = Invoke-ReleaseNativeCommand -Command "git" -Arguments @(
            "-C", [string]$releaseSource.ReleaseRoot, "rev-list", "-n", "1", $sourceTag)
        $localCommit = if ($localResult.ExitCode -eq 0 -and $localResult.Output.Count -gt 0) {
            [string]$localResult.Output[0]
        }
        else {
            ""
        }
        $remoteCommit = ""
        if ($FinalSuccess -and -not $SkipGitTag) {
            $remoteResult = Invoke-ReleaseNativeCommand -Command "git" -Arguments @(
                "-C", $repoRoot, "ls-remote", "--tags", "origin", "refs/tags/$sourceTag")
            if ($remoteResult.ExitCode -eq 0 -and $remoteResult.Output.Count -gt 0) {
                $remoteCommit = ([string]$remoteResult.Output[0] -split "\s+")[0]
            }
        }
        $gitEvidence.Add([ordered]@{
            TagName = $sourceTag
            ExpectedCommit = $sourceCommit
            LocalCommit = $localCommit
            RemoteCommit = $remoteCommit
            LocalVerified = $localCommit -eq $sourceCommit
            RemoteVerified = $SkipGitTag -or $remoteCommit -eq $sourceCommit
        })
    }

    $githubEvidence = [ordered]@{
        Requested = -not $SkipGitHubRelease
        Url = ""
        TagName = [string]$report.TagName
        Assets = [object[]]@()
        Verified = $false
    }
    if ($FinalSuccess -and -not $SkipGitHubRelease) {
        $githubResult = Invoke-ReleaseNativeCommand -Command "gh" -Arguments @(
            "release", "view", [string]$report.TagName, "--repo", $Repository,
            "--json", "url,tagName,assets")
        if ($githubResult.ExitCode -eq 0) {
            $github = ConvertFrom-Json ($githubResult.Output -join [Environment]::NewLine)
            $githubEvidence.Url = [string]$github.url
            $githubEvidence.TagName = [string]$github.tagName
            $githubEvidence.Assets = [object[]]@($github.assets)
            $githubEvidence.Verified = $true
        }
        else {
            $publishWarnings.Add("Final GitHub release readback failed; inspect the GitHub child log before signoff.")
        }
    }

    return [ordered]@{
        Steam = [ordered]@{
            Requested = -not $SkipSteam -and -not [bool]$report.PreflightOptions.SkipSteam
            PublishedFileId = $steamPublishedFileId
            VisibilityIntent = if ($report.PreflightOptions.PublishSteamVisibility) { "Publish" } else { "Unchanged" }
            VisibilityResult = if ($FinalSuccess -and $steamOperations.Count -gt 0) {
                if ($report.PreflightOptions.PublishSteamVisibility) { "PublishedAndVerifiedByChild" } else { "Unchanged" }
            }
            else {
                "UnknownOrNotRequested"
            }
            FinalReadback = $steamReadback
            FinalReadbackVerified = $null -ne $steamReadback -and
                [string]$steamReadback.PublishedFileId -eq $steamPublishedFileId
            Operations = [object[]]$steamOperations
        }
        ModIO = [ordered]@{
            Requested = -not $SkipModIo -and -not [bool]$report.PreflightOptions.SkipModIo
            ModId = $modIoId
            PageVisibilityIntent = if ($report.PreflightOptions.PublishModIoPage) { "Publish" } else { "Unchanged" }
            VisibilityResult = if ($FinalSuccess -and $modIoOperations.Count -gt 0) {
                if ($report.PreflightOptions.PublishModIoPage) { "PublishedAndVerifiedByChild" } else { "Unchanged" }
            }
            else {
                "UnknownOrNotRequested"
            }
            Operations = [object[]]$modIoOperations
        }
        Git = [ordered]@{
            Requested = -not $SkipGitTag
            Tags = [object[]]$gitEvidence.ToArray()
        }
        GitHub = $githubEvidence
    }
}

function Add-FailedDirectOperation([string] $Name, [string] $Summary, [string] $ExternalOutcome) {
    $publishOperations.Add([ordered]@{
        Name = $Name
        Script = "git"
        Arguments = [string[]]@()
        Status = "Failed"
        ExitCode = [int]$LASTEXITCODE
        StartedAtUtc = ""
        CompletedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        LogPath = ""
        Summary = [string[]]@($Summary)
        Warnings = [string[]]@($Summary)
        ExternalMutationPossible = $true
        ExternalOutcome = $ExternalOutcome
        AffectsReleaseCompletion = $true
    })
}

function Save-ReleaseSignoffArtifact([bool] $FinalSuccess, [string] $Interruption) {
    if ($null -eq $report -or [string]::IsNullOrWhiteSpace([string]$report.ModName)) {
        return ""
    }
    try {
        if ($null -eq $report.PSObject.Properties["ReportPath"]) {
            $report | Add-Member -NotePropertyName "ReportPath" -NotePropertyValue $reportPath
        }

        $readiness = $null
        $readinessPath = [string]$report.Readiness.ReportPath
        if (-not [string]::IsNullOrWhiteSpace($readinessPath) -and
                (Test-Path -LiteralPath $readinessPath -PathType Leaf)) {
            $readiness = Get-Content -Raw -LiteralPath $readinessPath | ConvertFrom-Json
        }

        $issueEvidence = [ordered]@{
            Boundary = "EvidenceCandidatesOnly"
            MutationsPerformed = $false
            Query = "Not reached because publication did not complete."
            Terms = [string[]]@()
            Candidates = [object[]]@()
            Decision = "PendingHumanReview"
        }
        if ($FinalSuccess) {
            try {
                $openIssues = Get-OpenGitHubIssues $Repository
                $issueEvidence = Get-ReleaseIssueCandidates $report $openIssues $EvidenceTerm
            }
            catch {
                $publishWarnings.Add($_.Exception.Message)
                $issueEvidence.Query = "Collection failed; see incident inventory."
            }
        }

        $wikiRoot = "$toolRepoRoot.wiki"
        $wikiEvidence = Get-ReleaseWikiCandidates $report $wikiRoot $EvidenceTerm
        if (-not $wikiEvidence.Available) {
            $publishWarnings.Add("Wiki evidence checkout is unavailable: $wikiRoot")
        }

        $externalEvidence = Get-SignoffExternalEvidence $FinalSuccess
        $overrides = [ordered]@{
            CorrectiveReplacement = [bool]$CorrectiveReplacement
            PreserveChangelogDate = [bool]$PreserveChangelogDate
            ReplaceExistingGitHubAsset = [bool]$ReplaceExistingGitHubAsset
            SkipSteam = [bool]$SkipSteam
            SkipModIo = [bool]$SkipModIo
            SkipPlatformTags = [bool]$SkipPlatformTags
            SkipGitTag = [bool]$SkipGitTag
            SkipGitHubRelease = [bool]$SkipGitHubRelease
            SkipDiscordHandoff = [bool]$SkipDiscordHandoff
        }
        $signoff = New-ReleaseSignoffReport `
            -Preflight $report `
            -Readiness $readiness `
            -Operations ([object[]]@(@($report.Steps) + @($publishOperations.ToArray()))) `
            -IssueEvidence $issueEvidence `
            -WikiEvidence $wikiEvidence `
            -ExternalEvidence $externalEvidence `
            -Overrides $overrides `
            -Warnings ([string[]]$publishWarnings.ToArray()) `
            -PrerequisiteInterruption $Interruption
        $reportDirectory = Resolve-ToolPath $SignoffReportRoot
        $lineSuffix = if ([string]::IsNullOrWhiteSpace([string]$report.ReleaseLine)) {
            "current"
        }
        else {
            [string]$report.ReleaseLine
        }
        $script:signoffReportPath = Join-Path $reportDirectory "$($report.ModName)-$($report.SourceModVersion)-$lineSuffix.json"
        Write-ReleaseSignoffReport $signoff $signoffReportPath
        return $signoff
    }
    catch {
        Write-Warning "Could not write release signoff artifact: $($_.Exception.Message)"
        return ""
    }
}

$report = $null
trap {
    $failureMessage = $_.Exception.Message
    $signoff = Save-ReleaseSignoffArtifact $false $failureMessage
    if ($signoff -isnot [string]) {
        Write-Host ""
        Format-ReleaseSignoffReport $signoff | ForEach-Object { Write-Host $_ }
        Write-Host "  Report: $signoffReportPath"
    }
    $ErrorActionPreference = "Continue"
    Write-Host "Release publish failed: $failureMessage"
    exit 1
}

$reportPath = Resolve-RepoPath $PreflightReportPath
Assert-PathExists $reportPath "Preflight report"
$report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
if ([int]$report.SchemaVersion -ne 5) {
    throw "Unsupported preflight report schema version: $($report.SchemaVersion)"
}
$preflightModIoAccessTokenPath = [string]$report.PreflightOptions.ModIoAccessTokenPath
if ([string]::IsNullOrWhiteSpace($ModIoAccessTokenPath)) {
    $ModIoAccessTokenPath = $preflightModIoAccessTokenPath
}
elseif (-not [string]::IsNullOrWhiteSpace($preflightModIoAccessTokenPath) -and
        (Resolve-ToolPath $ModIoAccessTokenPath) -ne (Resolve-ToolPath $preflightModIoAccessTokenPath)) {
    throw "Mod.IO owner token argument does not match the credential bound by immutable preflight."
}

$releaseSources = @($report.ReleaseSources)
if ($releaseSources.Count -eq 0) {
    throw "Preflight report contains no release sources."
}
$primarySources = @($releaseSources | Where-Object { $_.IsPrimary -eq $true })
if ($primarySources.Count -ne 1) {
    throw "Preflight report must contain exactly one primary release source, found $($primarySources.Count)."
}
$primarySource = $primarySources[0]
if ($releaseSources.Count -gt 1 -and [string]$primarySource.VersionPolicy -ne "CurrentLine") {
    throw "A multi-source release must use the current compatibility line as its primary source."
}

$recordedReleaseRoot = (Resolve-Path -LiteralPath ([string]$report.ReleaseRoot)).Path
if ($recordedReleaseRoot -ne $repoRoot) {
    throw "Preflight report belongs to release root $recordedReleaseRoot. Pass -ReleaseRoot with that exact worktree."
}
if ((Resolve-Path -LiteralPath ([string]$primarySource.ReleaseRoot)).Path -ne $repoRoot) {
    throw "Primary release source does not match the report release root."
}

$recordedVersionPolicy = [string]$report.VersionPolicy
if ([string]::IsNullOrWhiteSpace($VersionPolicy)) {
    $VersionPolicy = $recordedVersionPolicy
}
elseif ($VersionPolicy -ne $recordedVersionPolicy) {
    throw "Version policy must match the immutable preflight report: $recordedVersionPolicy."
}

if ($report.ReadyForPublish -ne $true) {
    throw "Preflight report is not marked ReadyForPublish."
}

$reportCorrective = $null -ne $report.CorrectiveReplacement -and $report.CorrectiveReplacement.Enabled -eq $true
if ($CorrectiveReplacement -ne $reportCorrective) {
    throw "Corrective replacement mode must match the immutable preflight report."
}
if ($CorrectiveReplacement -and $releaseSources.Count -gt 1) {
    throw "Multi-source corrective replacement is not supported by the routine release workflow."
}
$reportBackportPackageUpdate = $null -ne $report.ModIoBackportPackageUpdate -and
    $report.ModIoBackportPackageUpdate.Enabled -eq $true
if ($reportBackportPackageUpdate) {
    if ($CorrectiveReplacement -or [string]$primarySource.VersionPolicy -ne "BackportPatchOnly") {
        throw "Mod.IO backport package update evidence is invalid for this release mode."
    }
    if ([long]$report.ModIoBackportPackageUpdate.ExistingActiveFileId -le 0 -or
            [string]$report.ModIoBackportPackageUpdate.ExistingActiveVersion -ne
                [string]$report.PublicPackageVersion) {
        throw "Mod.IO backport package update evidence has an invalid active file identity."
    }
}
if ($CorrectiveReplacement) {
    $replacementPlatforms = @($report.CorrectiveReplacement.Platforms | ForEach-Object { [string]$_ })
    if ("Steam" -in $replacementPlatforms -and $SkipSteam) {
        throw "Corrective preflight requires Steam replacement, but -SkipSteam was supplied."
    }
    if ("ModIO" -in $replacementPlatforms -and $SkipModIo) {
        throw "Corrective preflight requires Mod.IO replacement, but -SkipModIo was supplied."
    }
    if ("GitHub" -in $replacementPlatforms -and $SkipGitHubRelease) {
        throw "Corrective preflight requires GitHub asset replacement, but -SkipGitHubRelease was supplied."
    }
    if ([string]$report.CorrectiveReplacement.ExistingTagDisposition -eq "PreserveExisting" -and -not $SkipGitTag) {
        throw "Corrective preflight preserves the existing tag; publish with -SkipGitTag."
    }
}

$modName = [string]$report.ModName
$sourceModVersion = [string]$report.SourceModVersion
$publicPackageVersion = [string]$report.PublicPackageVersion
$tagName = [string]$report.TagName
$releaseCommit = [string]$report.ReleaseCommit
if ([string]::IsNullOrWhiteSpace($modName) -or [string]::IsNullOrWhiteSpace($sourceModVersion) -or
    [string]::IsNullOrWhiteSpace($publicPackageVersion) -or
    [string]::IsNullOrWhiteSpace($releaseCommit)) {
    throw "Preflight report is missing mod name, source/public version, or release commit."
}
if ([string]$primarySource.Commit -ne $releaseCommit -or
    [string]$primarySource.SourceModVersion -ne $sourceModVersion -or
    [string]$primarySource.TagName -ne $tagName) {
    throw "Primary release source does not match the report's public release identity."
}
$releaseLogDirectory = New-ReleaseLogDirectory `
    -RepoRoot $repoRoot -ModName $modName -Version $sourceModVersion -Phase "publish"

if ([string]::IsNullOrWhiteSpace($Configuration)) {
    $Configuration = [string]$report.Configuration
}
if ([string]::IsNullOrWhiteSpace($GameVersion)) {
    $GameVersion = [string]$report.GameVersion
}

$currentHead = (& git -C $repoRoot rev-parse HEAD).Trim()
$currentBranch = (& git -C $repoRoot branch --show-current).Trim()
if ([string]::IsNullOrWhiteSpace($currentBranch)) {
    $currentBranch = "(detached)"
}
if ($currentBranch -ne [string]$report.ReleaseBranch) {
    throw "Release worktree branch changed since preflight. Preflight: $($report.ReleaseBranch); current: $currentBranch."
}
& git -C $repoRoot merge-base --is-ancestor $releaseCommit $currentHead
if ($LASTEXITCODE -ne 0) {
    throw "Recorded release commit $releaseCommit is not an ancestor of current HEAD $currentHead."
}
$criticalPaths = @($report.ReleaseCriticalPaths | ForEach-Object { [string]$_ })
if ($criticalPaths.Count -eq 0) {
    throw "Preflight report contains no release-critical paths."
}
$laterCriticalChanges = @(Invoke-Git (@("diff", "--name-only", "$releaseCommit..$currentHead", "--") + $criticalPaths))
if ($laterCriticalChanges.Count -gt 0) {
    throw "Changes after recorded release commit $releaseCommit alter release-critical paths: $($laterCriticalChanges -join ', ')."
}
$workingTreeCriticalChanges = @(Invoke-Git (@("diff", "--name-only", $releaseCommit, "--") + $criticalPaths))
if ($workingTreeCriticalChanges.Count -gt 0) {
    throw "Tracked release-critical paths differ from recorded release commit ${releaseCommit}: $($workingTreeCriticalChanges -join ', '). Stop before any public change."
}
$pathEvidence = @($report.ReleaseCriticalPathEvidence | ForEach-Object { [string]$_.Path })
if ($pathEvidence.Count -ne $criticalPaths.Count -or
    @($criticalPaths | Where-Object { $_ -notin $pathEvidence }).Count -gt 0) {
    throw "Preflight report is missing release-critical path evidence for the current release scope."
}

$releaseConfigPath = [string]$report.ReleaseConfigPath
Assert-PathExists $releaseConfigPath "Release config"
$releaseConfig = Get-Content -Raw -LiteralPath $releaseConfigPath | ConvertFrom-Json
$currentVersion = [string](Get-ObjectPropertyValue $releaseConfig "ReleaseVersion")
if ($currentVersion -ne $sourceModVersion) {
    throw "ReleaseVersion changed since preflight. Preflight: $sourceModVersion; current: $currentVersion"
}

if ($null -ne $report.Source) {
    $sourcePath = [string]$report.Source.Path
    $currentSource = Get-DirectoryFingerprint $sourcePath
    if ($currentSource.Sha256 -ne [string]$report.Source.Sha256) {
        throw "Package source changed since preflight. Preflight: $($report.Source.Sha256); current: $($currentSource.Sha256)"
    }
}

$packagePath = [string]$report.Package.Path
Assert-PathExists $packagePath "Release package"
$currentPackageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($currentPackageHash -ne [string]$report.Package.Sha256) {
    throw "Release package changed since preflight. Preflight: $($report.Package.Sha256); current: $currentPackageHash"
}
$packageLaneFingerprints = @(Get-PackageLaneFingerprints $packagePath)
$publicPackageLaneFingerprint = Get-PublicPackageLaneFingerprint $packageLaneFingerprints
if ([string]$publicPackageLaneFingerprint.GameVersion -ne [string]$report.PublicPackageGameVersion -or
        [string]$publicPackageLaneFingerprint.ManifestVersion -ne $publicPackageVersion) {
    throw (
        "Public package identity changed since preflight. Expected $($report.PublicPackageGameVersion) " +
        "v$publicPackageVersion; current $($publicPackageLaneFingerprint.GameVersion) " +
        "v$($publicPackageLaneFingerprint.ManifestVersion).")
}
foreach ($releaseSource in $releaseSources) {
    Assert-ReleaseSourceCurrent $releaseSource
    $laneFingerprint = Get-LaneFingerprint $packageLaneFingerprints ([string]$releaseSource.GameVersion)
    if ([string]$laneFingerprint.Sha256 -ne [string]$releaseSource.PackageLaneSha256) {
        throw "Final package lane $($releaseSource.GameVersion) does not match release source $($releaseSource.Commit)."
    }
    if ([string]$laneFingerprint.ManifestVersion -ne [string]$releaseSource.SourceModVersion) {
        throw (
            "Final package lane $($releaseSource.GameVersion) has manifest version " +
            "$($laneFingerprint.ManifestVersion), expected source mod version $($releaseSource.SourceModVersion).")
    }
}

$combinedReleaseNotesPath = ""
if ($releaseSources.Count -gt 1) {
    $combinedNotes = Get-CombinedReleaseNotes $releaseSources
    $combinedNotesRoot = Resolve-ToolPath ".tools/release-notes"
    New-Item -ItemType Directory -Path $combinedNotesRoot -Force | Out-Null
    $combinedReleaseNotesPath = Join-Path $combinedNotesRoot "$tagName.md"
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($combinedReleaseNotesPath, $combinedNotes, $utf8NoBom)
}

$steamPublishedFileId = [string](Get-ObjectPropertyValue $releaseConfig.Steam "PublishedFileId")
$resolvedModIoConfigPath = if ([string]::IsNullOrWhiteSpace($ModIoConfigPath)) {
    Resolve-ToolPath ".tools/modio/$modName.local.json"
}
else {
    Resolve-ToolPath $ModIoConfigPath
}
$modIoId = ""
if (Test-Path -LiteralPath $resolvedModIoConfigPath) {
    $summaryModIoConfig = Get-Content -Raw -LiteralPath $resolvedModIoConfigPath | ConvertFrom-Json
    $modIoId = [string](Get-ObjectPropertyValue $summaryModIoConfig "ModId")
}

$sourceReleaseLabel = "v$sourceModVersion"
if ($VersionPolicy -eq "BackportPatchOnly") {
    $sourceReleaseLabel += " (Timberborn v$(Get-TimberbornGameLine $GameVersion))"
}
$manifestPath = Resolve-RepoPath ([string](Get-ObjectPropertyValue $releaseConfig "ManifestPath"))
Assert-PathExists $manifestPath "Release manifest"
$releaseManifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$displayModName = [string]$releaseManifest.Name
if ([string]::IsNullOrWhiteSpace($displayModName)) {
    $displayModName = $modName
}
$sourceReleaseTitle = "$displayModName $sourceReleaseLabel"
$isBackportPackageUpdate = $null -ne $report.ModIoBackportPackageUpdate -and
    $report.ModIoBackportPackageUpdate.Enabled -eq $true

Write-Host "Release publish: $sourceReleaseTitle"
Write-Host "  Release root: $repoRoot ($currentBranch)"
Write-Host "  Release commit: $releaseCommit"
Write-Host "  Game compatibility lane: $GameVersion"
Write-Host "  Version policy: $VersionPolicy"
Write-Host "  Source mod version: $sourceModVersion"
Write-Host "  Public package version: $publicPackageVersion ($($report.PublicPackageGameVersion))"
Write-Host "  Package: $($report.Package.Path)"
Write-Host "  Package SHA256: $($report.Package.Sha256)"
if ($null -ne $report.Source) {
    Write-Host "  Source fingerprint: $($report.Source.Sha256)"
}
if (-not $SkipSteam -and -not [bool]$report.PreflightOptions.SkipSteam) {
    $steamVisibilityIntent = if ([bool]$report.PreflightOptions.PublishSteamVisibility) { "publish requested" } else { "unchanged" }
    Write-Host "  Steam: PublishedFileId=$steamPublishedFileId; visibility=$steamVisibilityIntent"
}
if (-not $SkipModIo -and -not [bool]$report.PreflightOptions.SkipModIo) {
    $modIoVisibilityIntent = if ([bool]$report.PreflightOptions.PublishModIoPage) { "publish requested" } else { "unchanged" }
    Write-Host "  Mod.IO: ModId=$modIoId; page visibility=$modIoVisibilityIntent"
    if ($isBackportPackageUpdate) {
        Write-Host (
            "  Mod.IO backport package update: existing active file " +
            "$($report.ModIoBackportPackageUpdate.ExistingActiveFileId) " +
            "v$($report.ModIoBackportPackageUpdate.ExistingActiveVersion); " +
            "new distinct file required as v$publicPackageVersion")
    }
}
Write-Host "  Git tag: $tagName"
if ($releaseSources.Count -gt 1) {
    Write-Host "  Release sources:"
    foreach ($releaseSource in $releaseSources) {
        Write-Host "    $($releaseSource.GameVersion): $($releaseSource.Commit) -> $($releaseSource.TagName)"
    }
}
Write-Host "  Full child logs: $releaseLogDirectory"

$publishesSteam = -not $SkipSteam -and -not [bool]$report.PreflightOptions.SkipSteam
$publishesModIo = -not $SkipModIo -and -not [bool]$report.PreflightOptions.SkipModIo
$publishesPlatformTags = -not $SkipPlatformTags -and
    -not [bool]$report.PreflightOptions.SkipPlatformTags -and $publishesModIo
$hasPublicMutation = $publishesSteam -or $publishesModIo -or $publishesPlatformTags -or
    -not $SkipGitTag -or -not $SkipGitHubRelease
if ($hasPublicMutation) {
    $releaseDateRecords = @(Get-ReleaseDateRecords $releaseSources (Get-Date))
    foreach ($dateRecord in $releaseDateRecords) {
        Write-Host (
            "  Committed changelog date ($($dateRecord.TagName)): " +
            "$($dateRecord.ChangelogDate.ToString('yyyy-MM-dd')); " +
            "current local date: $($dateRecord.CurrentLocalDate.ToString('yyyy-MM-dd'))")
    }
    $dateMismatches = @($releaseDateRecords | Where-Object DateDiffers)
    if ($dateMismatches.Count -gt 0) {
        if (-not $PreserveChangelogDate) {
            throw (
                "The committed changelog date differs from the current local date. Ask whether to preserve the " +
                "immutable snapshot or prepare a newly dated release, then rerun with -PreserveChangelogDate only " +
                "after explicit confirmation.")
        }
        Write-Host "  Changelog date preservation: explicitly confirmed"
    }
}

if (-not $SkipGitTag) {
    foreach ($releaseSource in $releaseSources) {
        $sourceRoot = [string]$releaseSource.ReleaseRoot
        $sourceTag = [string]$releaseSource.TagName
        & git -C $sourceRoot rev-parse -q --verify "refs/tags/$sourceTag" *> $null
        if ($LASTEXITCODE -eq 0) {
            throw "Release tag already exists before publish: $sourceTag"
        }
    }
}

$commonPublishArgs = New-Object System.Collections.Generic.List[string]
$commonPublishArgs.Add("-ModName")
$commonPublishArgs.Add($modName)
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
Add-SwitchArgument $commonPublishArgs "-IncludeLegacyVersions" ([bool]$report.PreflightOptions.IncludeLegacyVersions)
Add-SwitchArgument $commonPublishArgs "-SkipBuild" $true
Add-SwitchArgument $commonPublishArgs "-SkipUnityExport" $true
Add-OptionalArgument $commonPublishArgs "-ExpectedPackageSha256" ([string]$report.Package.Sha256)
Add-SwitchArgument $commonPublishArgs "-CorrectiveReplacement" $CorrectiveReplacement

if (-not $SkipSteam -and -not [bool]$report.PreflightOptions.SkipSteam) {
    $steamArgs = New-Object System.Collections.Generic.List[string]
    $steamArgs.AddRange([string[]]$commonPublishArgs)
    Add-OptionalArgument $steamArgs "-SteamConfigPath" $SteamConfigPath
    Add-OptionalArgument $steamArgs "-SteamCmdPath" $SteamCmdPath
    Add-OptionalArgument $steamArgs "-SteamUserName" $SteamUserName
    Add-OptionalArgument $steamArgs "-ChangeNoteTitle" $sourceReleaseLabel
    Add-SwitchArgument $steamArgs "-UpdateVisibility" ([bool]$report.PreflightOptions.PublishSteamVisibility)
    $steamArgs.Add("-Publish")
    Invoke-ReleaseStep "Steam release publish" "publish-steam.ps1" $steamArgs.ToArray()
}

if (-not $SkipModIo -and -not [bool]$report.PreflightOptions.SkipModIo) {
    $modIoArgs = New-Object System.Collections.Generic.List[string]
    $modIoArgs.AddRange([string[]]$commonPublishArgs)
    Add-OptionalArgument $modIoArgs "-ConfigPath" $ModIoConfigPath
    Add-OptionalArgument $modIoArgs "-AccessTokenPath" $ModIoAccessTokenPath
    Add-OptionalArgument $modIoArgs "-PublicPackageVersion" $publicPackageVersion
    if ($VersionPolicy -eq "BackportPatchOnly") {
        Add-OptionalArgument $modIoArgs "-ChangeNotesPrefix" $sourceReleaseTitle
    }
    if ($isBackportPackageUpdate) {
        Add-OptionalArgument $modIoArgs "-ExpectedActiveFileId" `
            ([string]$report.ModIoBackportPackageUpdate.ExistingActiveFileId)
    }
    Add-SwitchArgument $modIoArgs "-BackportPackageUpdate" $isBackportPackageUpdate
    Add-SwitchArgument $modIoArgs "-PublishPage" ([bool]$report.PreflightOptions.PublishModIoPage)
    $modIoArgs.Add("-Publish")
    Invoke-ReleaseStep "Mod.IO release publish" "publish-modio.ps1" $modIoArgs.ToArray()
}

if (-not $SkipPlatformTags -and -not [bool]$report.PreflightOptions.SkipPlatformTags -and -not $SkipModIo -and
    -not [bool]$report.PreflightOptions.SkipModIo) {
    $tagArgs = New-Object System.Collections.Generic.List[string]
    $tagArgs.Add("-ModName")
    $tagArgs.Add($modName)
    $tagArgs.Add("-ReleaseRoot")
    $tagArgs.Add($repoRoot)
    $tagArgs.Add("-Platform")
    $tagArgs.Add("ModIO")
    Add-OptionalArgument $tagArgs "-LocalModRoot" $LocalModRoot
    Add-OptionalArgument $tagArgs "-SteamConfigPath" $SteamConfigPath
    Add-OptionalArgument $tagArgs "-SteamCmdPath" $SteamCmdPath
    Add-OptionalArgument $tagArgs "-SteamUserName" $SteamUserName
    Add-OptionalArgument $tagArgs "-ModIoConfigPath" $ModIoConfigPath
    Add-OptionalArgument $tagArgs "-ModIoAccessTokenPath" $ModIoAccessTokenPath
    $tagArgs.Add("-Publish")
    Invoke-ReleaseStep "Platform tag publish" "update-platform-tags.ps1" $tagArgs.ToArray()
}

if (-not $SkipGitTag) {
    Write-Host ""
    Write-Host "== Git release tags =="
    $tagsToPush = New-Object System.Collections.Generic.List[string]
    foreach ($releaseSource in $releaseSources) {
        $sourceTag = [string]$releaseSource.TagName
        $sourceCommit = [string]$releaseSource.Commit
        & git -C $repoRoot tag $sourceTag $sourceCommit
        if ($LASTEXITCODE -ne 0) {
            Add-FailedDirectOperation `
                "Git release tags" `
                "Failed to create Git tag $sourceTag; earlier local tags in the batch may exist." `
                "Unknown"
            if ($completedPublicSteps.Count -gt 0) {
                Write-Warning "Partial success before failure: $($completedPublicSteps -join ', ')."
            }
            throw "Failed to create Git tag $sourceTag."
        }
        $tagsToPush.Add($sourceTag)
    }

    $tagArguments = [string[]]$tagsToPush.ToArray()
    & git -C $repoRoot push --atomic origin @tagArguments
    if ($LASTEXITCODE -ne 0) {
        Add-FailedDirectOperation `
            "Git release tags" `
            "Atomic tag push failed; read back remote tags before retrying." `
            "Unknown"
        if ($completedPublicSteps.Count -gt 0) {
            Write-Warning "Partial success before failure: $($completedPublicSteps -join ', '), local Git tag creation."
        }
        throw "Failed to atomically push release tags: $($tagsToPush -join ', ')."
    }

    foreach ($releaseSource in $releaseSources) {
        $sourceTag = [string]$releaseSource.TagName
        $sourceCommit = [string]$releaseSource.Commit
        $completedPublicSteps.Add("Git release tag $sourceTag")
        Write-Host "Git tag created and pushed: $sourceTag -> $sourceCommit"
    }
    $publishOperations.Add([ordered]@{
        Name = "Git release tags"
        Script = "git"
        Arguments = [string[]]@("tag", "push --atomic")
        Status = "Succeeded"
        ExitCode = 0
        StartedAtUtc = ""
        CompletedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        LogPath = ""
        Summary = [string[]]@($releaseSources | ForEach-Object {
            "Git tag created and pushed: $($_.TagName) -> $($_.Commit)"
        })
        Warnings = [string[]]@()
        ExternalMutationPossible = $true
        ExternalOutcome = "Confirmed"
        AffectsReleaseCompletion = $true
    })
}

if (-not $SkipGitHubRelease) {
    $githubArgs = New-Object System.Collections.Generic.List[string]
    $githubArgs.Add("-ModName")
    $githubArgs.Add($modName)
    $githubArgs.Add("-ReleaseRoot")
    $githubArgs.Add($repoRoot)
    $githubArgs.Add("-Repository")
    $githubArgs.Add($Repository)
    $githubArgs.Add("-TagName")
    $githubArgs.Add($tagName)
    Add-OptionalArgument $githubArgs "-Title" $sourceReleaseTitle
    Add-OptionalArgument $githubArgs "-ReleaseNotesPath" $combinedReleaseNotesPath
    Add-OptionalArgument $githubArgs "-ExpectedPackageSha256" ([string]$report.Package.Sha256)
    Add-SwitchArgument $githubArgs "-ReplaceExistingAsset" ($ReplaceExistingGitHubAsset -or $CorrectiveReplacement)
    $githubArgs.Add("-Publish")
    Invoke-ReleaseStep "GitHub release publish" "publish-github-release.ps1" $githubArgs.ToArray()
}

if (-not $SkipDiscordHandoff) {
    $discordArgs = New-Object System.Collections.Generic.List[string]
    $discordArgs.Add("-ModName")
    $discordArgs.Add($modName)
    $discordArgs.Add("-ReleaseRoot")
    $discordArgs.Add($repoRoot)
    $discordArgs.Add("-Version")
    $discordArgs.Add($sourceModVersion)
    Add-OptionalArgument $discordArgs "-Title" $sourceReleaseTitle
    Add-OptionalArgument $discordArgs "-ReleaseNotesPath" $combinedReleaseNotesPath
    $discordArgs.Add("-Prepare")
    try {
        Invoke-ReleaseStep "Discord release handoff" "prepare-discord-release-message.ps1" $discordArgs.ToArray()
    }
    catch {
        Write-Warning (
            "Release publishing succeeded, but the Discord handoff could not be prepared: $($_.Exception.Message)")
    }
}

$signoff = Save-ReleaseSignoffArtifact $true ""
if ($signoff -is [string]) {
    throw "Release publishing completed, but the final signoff artifact could not be written."
}

Write-Host ""
Write-Host "Release publish completed for $sourceReleaseTitle."
Format-ReleaseSignoffReport $signoff | ForEach-Object { Write-Host $_ }
Write-Host "Signoff report: $signoffReportPath"
Write-Host "Discord message sending, issue closing, Wiki decisions, and MentorOutcome remain explicit follow-up steps."
