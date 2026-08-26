param(
    [Parameter(Mandatory = $true)]
    [string] $PreflightReportPath,

    [Parameter(Mandatory = $true)]
    [string] $PartialSignoffReportPath,

    [string] $ReleaseRoot = "",
    [string] $Repository = "ihsoft/TimberbornMods",
    [string] $ModIoConfigPath = "",
    [string] $ModIoAccessTokenPath = "",
    [string[]] $EvidenceTerm = @(),
    [string] $PlatformReadbackFixturePath = "",
    [switch] $SkipDiscordHandoff,
    [switch] $DetailedOutput
)

$ErrorActionPreference = "Stop"

$toolRepoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "repository-paths.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-native-command.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-output.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-notes.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-signoff.psm1") -Force

function Resolve-ToolPath([string] $Path) {
    return Resolve-RepositoryPath $toolRepoRoot $Path
}

function Assert-PathExists([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description not found: $Path"
    }
}

function Assert-CompletedPartialMutation(
    [object] $Signoff,
    [string] $Name,
    [string] $RequiredSummaryPattern = ""
) {
    $matches = @($Signoff.Operations | Where-Object {
        [string]$_.Name -eq $Name -and [string]$_.Status -eq "Succeeded" -and
            [string]$_.ExternalOutcome -eq "Confirmed"
    })
    if ($matches.Count -ne 1) {
        throw "Partial signoff must contain exactly one confirmed completed operation: $Name."
    }
    if (-not [string]::IsNullOrWhiteSpace($RequiredSummaryPattern) -and
            (@($matches[0].Summary) -join [Environment]::NewLine) -notmatch $RequiredSummaryPattern) {
        throw "Partial signoff operation $Name does not contain the required completion evidence."
    }
}

function New-RecoveryOperation(
    [string] $Name,
    [string[]] $Summary,
    [bool] $ExternalMutationPossible,
    [string] $ExternalOutcome = "Confirmed"
) {
    return [ordered]@{
        Name = $Name
        Script = $MyInvocation.MyCommand.Path
        Arguments = [string[]]@()
        Status = "Succeeded"
        ExitCode = 0
        StartedAtUtc = ""
        CompletedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        LogPath = ""
        Summary = [string[]]$Summary
        Warnings = [string[]]@()
        ExternalMutationPossible = $ExternalMutationPossible
        ExternalOutcome = $ExternalOutcome
        AffectsReleaseCompletion = $true
    }
}

function Invoke-RecoveryChild(
    [string] $Name,
    [string] $ScriptName,
    [string[]] $Arguments,
    [string] $LogDirectory,
    [int] $StepIndex
) {
    $operation = Invoke-LoggedReleaseStep `
        -Name $Name `
        -ScriptPath (Join-Path $PSScriptRoot $ScriptName) `
        -Arguments $Arguments `
        -LogDirectory $LogDirectory `
        -StepIndex $StepIndex `
        -DetailedOutput:$DetailedOutput
    return $operation
}

function Get-SourceReleaseNotes([object] $Source) {
    $changesPath = [string]$Source.ChangesPath
    Assert-PathExists $changesPath "Primary release changelog"
    $content = Get-Content -Raw -LiteralPath $changesPath
    $version = [regex]::Escape([string]$Source.SourceModVersion)
    $match = [regex]::Match(
        $content,
        "(?ms)^#\s+v?$version[^\r\n]*\r?\n(?<body>.*?)(?=^#\s+v|\z)")
    if (-not $match.Success -or [string]::IsNullOrWhiteSpace($match.Groups["body"].Value)) {
        throw "Cannot resolve release notes for v$($Source.SourceModVersion) from $changesPath."
    }
    return $match.Groups["body"].Value.Trim()
}

function Get-GitHubReleaseReadback(
    [string] $TagName,
    [string] $ExpectedTitle,
    [string] $ExpectedNotes,
    [string] $ExpectedAssetName,
    [string] $ExpectedPackageSha256
) {
    $authResult = Invoke-ReleaseNativeCommand -Command "gh" -Arguments @("api", "user")
    if ($authResult.ExitCode -ne 0) {
        throw "GitHub API authentication is unavailable in the recovery execution boundary."
    }
    $apiPath = "repos/$Repository/releases/tags/$TagName"
    $apiResult = Invoke-ReleaseNativeCommand -Command "gh" -Arguments @("api", $apiPath)
    if ($apiResult.ExitCode -ne 0) {
        $releaseListResult = Invoke-ReleaseNativeCommand -Command "gh" -Arguments @(
            "release", "list", "--repo", $Repository, "--limit", "10000", "--json", "tagName")
        if ($releaseListResult.ExitCode -ne 0) {
            throw "GitHub Release lookup failed without conclusive absence evidence."
        }
        $releaseList = @(ConvertFrom-Json ($releaseListResult.Output -join [Environment]::NewLine))
        if (@($releaseList | Where-Object { [string]$_.tagName -eq $TagName }).Count -gt 0) {
            throw "GitHub Release tag was listed but its exact readback failed; outcome remains unknown."
        }
        return [ordered]@{
            Exists = $false
            Verified = $false
            TagName = $TagName
            AbsenceEvidence = "Authenticated full release listing did not contain the qualified tag."
        }
    }

    $viewResult = Invoke-ReleaseNativeCommand -Command "gh" -Arguments @(
        "release", "view", $TagName, "--repo", $Repository,
        "--json", "url,tagName,name,body,assets")
    if ($viewResult.ExitCode -ne 0) {
        throw "GitHub Release detail readback failed.`n$($viewResult.Diagnostics -join [Environment]::NewLine)"
    }
    $release = ConvertFrom-Json ($viewResult.Output -join [Environment]::NewLine)
    $assets = @($release.assets)
    $asset = @($assets | Where-Object { [string]$_.name -eq $ExpectedAssetName })
    $expectedDigest = "sha256:$($ExpectedPackageSha256.ToLowerInvariant())"
    if ([string]$release.tagName -ne $TagName -or
            [string]$release.name -ne $ExpectedTitle -or
            ([string]$release.body).Trim() -ne $ExpectedNotes.Trim() -or
            $asset.Count -ne 1 -or [string]$asset[0].digest -ne $expectedDigest) {
        throw (
            "Existing GitHub Release does not match immutable identity, title, notes, or asset digest. " +
            "Do not recreate or replace it through partial recovery.")
    }
    return [ordered]@{
        Exists = $true
        Verified = $true
        Url = [string]$release.url
        TagName = [string]$release.tagName
        Title = [string]$release.name
        NotesSha256 = Get-StringSha256 $ExpectedNotes
        Assets = [object[]]$assets
        AssetName = $ExpectedAssetName
        AssetDigest = $expectedDigest
    }
}

function Get-StringSha256([string] $Value) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.BitConverter]::ToString(
            $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-PlatformReadbacks([object] $Preflight, [object] $PartialSignoff) {
    if (-not [string]::IsNullOrWhiteSpace($PlatformReadbackFixturePath)) {
        $fixturePath = Resolve-ToolPath $PlatformReadbackFixturePath
        Assert-PathExists $fixturePath "Platform readback fixture"
        return Get-Content -Raw -LiteralPath $fixturePath | ConvertFrom-Json
    }

    $steamId = [string]$PartialSignoff.Platforms.Steam.PublishedFileId
    $steamResponse = Invoke-RestMethod -Method Post `
        -Uri "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/" `
        -Body @{ itemcount = "1"; "publishedfileids[0]" = $steamId }
    $steamItem = $steamResponse.response.publishedfiledetails[0]
    $steam = [ordered]@{
        PublishedFileId = [string]$steamItem.publishedfileid
        Result = [int]$steamItem.result
        TimeUpdated = [long]$steamItem.time_updated
        FileSize = [long]$steamItem.file_size
        Visibility = [int]$steamItem.visibility
    }

    $configPath = if ([string]::IsNullOrWhiteSpace($ModIoConfigPath)) {
        Resolve-ToolPath ".tools/modio/$($Preflight.ModName).local.json"
    }
    else {
        Resolve-ToolPath $ModIoConfigPath
    }
    $tokenPath = if ([string]::IsNullOrWhiteSpace($ModIoAccessTokenPath)) {
        Resolve-ToolPath ([string]$Preflight.PreflightOptions.ModIoAccessTokenPath)
    }
    else {
        Resolve-ToolPath $ModIoAccessTokenPath
    }
    Assert-PathExists $configPath "Mod.IO config"
    Assert-PathExists $tokenPath "Mod.IO owner token"
    $config = Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json
    $token = (Get-Content -Raw -LiteralPath $tokenPath).Trim()
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Mod.IO owner token is empty."
    }
    $headers = @{ Authorization = "Bearer $token" }
    $base = [string]$config.ApiBase
    $gameId = [string]$config.GameId
    $modId = [string]$config.ModId
    $mod = Invoke-RestMethod -Uri "$($base.TrimEnd('/'))/games/$gameId/mods/$modId" -Headers $headers
    $activeFileId = [string]$mod.modfile.id
    $file = Invoke-RestMethod `
        -Uri "$($base.TrimEnd('/'))/games/$gameId/mods/$modId/files/$activeFileId" `
        -Headers $headers
    $modIo = [ordered]@{
        ModId = [long]$modId
        ActiveFileId = [long]$activeFileId
        Version = [string]$mod.modfile.version
        DateScanned = [long]$file.date_scanned
        VirusStatus = [int]$file.virus_status
        VirusPositive = [int]$file.virus_positive
        FileSize = [long]$file.filesize
    }
    return [ordered]@{ Steam = $steam; ModIO = $modIo }
}

$preflightPath = Resolve-ToolPath $PreflightReportPath
$partialPath = Resolve-ToolPath $PartialSignoffReportPath
Assert-PathExists $preflightPath "Immutable preflight report"
Assert-PathExists $partialPath "Partial signoff report"
$preflight = Get-Content -Raw -LiteralPath $preflightPath | ConvertFrom-Json
$partial = Get-Content -Raw -LiteralPath $partialPath | ConvertFrom-Json
if ([int]$preflight.SchemaVersion -ne 5 -or -not [bool]$preflight.ReadyForPublish) {
    throw "Recovery requires a ready schema-5 immutable preflight."
}
if ([int]$partial.SchemaVersion -ne 1 -or
        [string]$partial.ArtifactType -ne "TimberbornReleaseSignoff" -or
        [string]$partial.Status -notin @("Partial", "Published")) {
    throw "Recovery requires a schema-1 partial or recovered release signoff."
}
if ([string]$partial.Identity.ModName -ne [string]$preflight.ModName -or
        [string]$partial.Identity.SourceModVersion -ne [string]$preflight.SourceModVersion -or
        [string]$partial.Identity.ReleaseCommit -ne [string]$preflight.ReleaseCommit -or
        [string]$partial.Artifacts.Package.Sha256 -ne [string]$preflight.Package.Sha256) {
    throw "Partial signoff identity or package hash does not match immutable preflight."
}
Assert-CompletedPartialMutation $partial "Steam release publish" "SteamCMD upload completed"
Assert-CompletedPartialMutation $partial "Mod.IO release publish" "Final Mod\.IO readback:"
Assert-CompletedPartialMutation $partial "Platform tag publish" "(?s)Add:\s*\(none\).*Remove:\s*\(none\)"
Assert-CompletedPartialMutation $partial "Git release tags" "Git tag created and pushed:"
if ($null -eq $preflight.PSObject.Properties["ReportPath"] -or
        [string]::IsNullOrWhiteSpace([string]$preflight.ReportPath)) {
    $preflight | Add-Member -NotePropertyName "ReportPath" -NotePropertyValue $preflightPath -Force
}

$repoRoot = Resolve-RepositoryRoot $(if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) {
        [string]$preflight.ReleaseRoot
    }
    else { $ReleaseRoot }) $toolRepoRoot
$packagePath = [string]$preflight.Package.Path
Assert-PathExists $packagePath "Immutable public-owner package"
$packageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($packageHash -ne [string]$preflight.Package.Sha256) {
    throw "Immutable public-owner package hash changed before recovery."
}

$primarySource = @($preflight.ReleaseSources | Where-Object IsPrimary)[0]
$sourceRoot = Resolve-RepositoryRoot ([string]$primarySource.ReleaseRoot) $toolRepoRoot
$sourceConfigPath = Join-Path $sourceRoot "$($preflight.ModName)/release.json"
Assert-PathExists $sourceConfigPath "Primary source release config"
$sourceConfig = Get-Content -Raw -LiteralPath $sourceConfigPath | ConvertFrom-Json
$manifestPath = Resolve-RepositoryPath $sourceRoot ([string]$sourceConfig.ManifestPath)
Assert-PathExists $manifestPath "Primary source manifest"
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$displayName = if ([string]::IsNullOrWhiteSpace([string]$manifest.Name)) {
    [string]$preflight.ModName
}
else { [string]$manifest.Name }
$title = "$displayName v$($preflight.SourceModVersion)"
if ([string]$preflight.VersionPolicy -eq "BackportPatchOnly") {
    if ([string]$preflight.GameVersion -notmatch "^version-(?<line>\d+\.\d+)$") {
        throw "Cannot derive the older Timberborn line from $($preflight.GameVersion)."
    }
    $title += " (Timberborn v$($Matches['line']))"
}
$notes = if (@($preflight.ReleaseSources).Count -gt 1) {
    Get-CombinedReleaseNotes $preflight.ReleaseSources
}
else {
    Get-SourceReleaseNotes $primarySource
}

$platformReadbacks = Get-PlatformReadbacks $preflight $partial
$steam = $platformReadbacks.Steam
$modIo = $platformReadbacks.ModIO
$expectedModIo = $partial.Platforms.ModIO.FinalReadback
if ([string]$steam.PublishedFileId -ne [string]$partial.Platforms.Steam.PublishedFileId -or
        [int]$steam.Result -ne 1 -or [long]$steam.FileSize -ne [long]$preflight.Package.Length) {
    throw "Steam final readback does not match the completed immutable publication."
}
if ($null -eq $expectedModIo -or
        [long]$modIo.ModId -ne [long]$expectedModIo.ModId -or
        [long]$modIo.ActiveFileId -ne [long]$expectedModIo.ActiveFileId -or
        [string]$modIo.Version -ne [string]$preflight.PublicPackageVersion -or
        [long]$modIo.DateScanned -le 0 -or [int]$modIo.VirusStatus -ne 1 -or
        [int]$modIo.VirusPositive -ne 0 -or [long]$modIo.FileSize -ne [long]$preflight.Package.Length) {
    throw "Mod.IO final readback does not match the completed immutable publication."
}

$tagName = [string]$preflight.TagName
$remoteTag = Invoke-ReleaseNativeCommand -Command "git" -Arguments @(
    "-C", $repoRoot, "ls-remote", "--tags", "origin", "refs/tags/$tagName")
if ($remoteTag.ExitCode -ne 0 -or $remoteTag.Output.Count -ne 1) {
    throw "Remote qualified tag readback failed for $tagName."
}
$remoteCommit = ([string]$remoteTag.Output[0] -split "\s+")[0]
if ($remoteCommit -ne [string]$preflight.ReleaseCommit) {
    throw "Remote qualified tag points to $remoteCommit, expected $($preflight.ReleaseCommit)."
}

$assetName = [System.IO.Path]::GetFileName($packagePath)
$githubBefore = Get-GitHubReleaseReadback `
    $tagName $title $notes $assetName ([string]$preflight.Package.Sha256)
$recoveryLogDirectory = New-ReleaseLogDirectory `
    -RepoRoot $repoRoot -ModName ([string]$preflight.ModName) `
    -Version ([string]$preflight.SourceModVersion) -Phase "recover"
$newOperations = New-Object System.Collections.Generic.List[object]
$newOperations.Add((New-RecoveryOperation "Recovery Steam final readback" @(
    "Steam final readback: published_file_id=$($steam.PublishedFileId); result=$($steam.Result); " +
    "time_updated=$($steam.TimeUpdated); file_size=$($steam.FileSize); visibility=$($steam.Visibility)") $false))
$newOperations.Add((New-RecoveryOperation "Recovery Mod.IO final readback" @(
    "Final Mod.IO readback: mod_id=$($modIo.ModId); active_file_id=$($modIo.ActiveFileId); " +
    "version=$($modIo.Version); date_scanned=$($modIo.DateScanned); virus_status=$($modIo.VirusStatus); " +
    "virus_positive=$($modIo.VirusPositive); file_size=$($modIo.FileSize)") $false))
$newOperations.Add((New-RecoveryOperation "Recovery remote tag readback" @(
    "Remote tag verified: $tagName -> $remoteCommit") $false))

$githubAction = "VerifiedExisting"
if (-not [bool]$githubBefore.Exists) {
    $githubArguments = @(
        "-ModName", [string]$preflight.ModName,
        "-ReleaseRoot", $sourceRoot,
        "-PackageOwnerRoot", [string]$preflight.PackageOwnerRoot,
        "-PackagePath", $packagePath,
        "-SourceModVersion", [string]$preflight.SourceModVersion,
        "-Repository", $Repository,
        "-TagName", $tagName,
        "-Title", $title,
        "-ExpectedPackageSha256", [string]$preflight.Package.Sha256,
        "-Publish")
    $githubOperation = Invoke-RecoveryChild `
        "GitHub release recovery publish" "publish-github-release.ps1" `
        $githubArguments $recoveryLogDirectory 1
    $newOperations.Add($githubOperation)
    $githubAction = "CreatedMissingRelease"
}
$github = Get-GitHubReleaseReadback `
    $tagName $title $notes $assetName ([string]$preflight.Package.Sha256)
if (-not [bool]$github.Verified) {
    throw "GitHub Release recovery did not produce conclusive readback."
}
$github["Requested"] = $true
$newOperations.Add((New-RecoveryOperation "Recovery GitHub release readback" @(
    "GitHub release verified: $($github.Url); asset=$($github.AssetName); digest=$($github.AssetDigest)") $false))

$discordStatus = "Skipped"
if (-not $SkipDiscordHandoff) {
    $discordArguments = @(
        "-ModName", [string]$preflight.ModName,
        "-ReleaseRoot", $sourceRoot,
        "-Version", [string]$preflight.SourceModVersion,
        "-Title", $title,
        "-Prepare")
    $discordOperation = Invoke-RecoveryChild `
        "Discord release handoff" "prepare-discord-release-message.ps1" `
        $discordArguments $recoveryLogDirectory 2
    $discordOperation.AffectsReleaseCompletion = $false
    $newOperations.Add($discordOperation)
    $discordStatus = "Prepared"
}

$issueEvidence = [ordered]@{
    Boundary = "EvidenceCandidatesOnly"
    MutationsPerformed = $false
    Query = "Collection failed; see incident inventory."
    Terms = [string[]]@()
    Candidates = [object[]]@()
    Decision = "PendingHumanReview"
}
$warnings = New-Object System.Collections.Generic.List[string]
try {
    $issueEvidence = Get-ReleaseIssueCandidates `
        $preflight (Get-OpenGitHubIssues $Repository) $EvidenceTerm
}
catch {
    $warnings.Add($_.Exception.Message)
}
$wikiEvidence = Get-ReleaseWikiCandidates $preflight "$toolRepoRoot.wiki" $EvidenceTerm
if (-not [bool]$wikiEvidence.Available) {
    $warnings.Add("Wiki evidence checkout is unavailable: $toolRepoRoot.wiki")
}

$historicalOperations = @($partial.Operations | ForEach-Object {
    $_ | ConvertTo-Json -Depth 12 | ConvertFrom-Json
})
foreach ($operation in @($historicalOperations | Where-Object {
    [string]$_.Name -eq "GitHub release publish" -and [string]$_.Status -eq "Failed"
})) {
    $operation.AffectsReleaseCompletion = $false
    $operation | Add-Member -NotePropertyName "Recovered" -NotePropertyValue $true -Force
    $operation | Add-Member -NotePropertyName "RecoveryOutcome" -NotePropertyValue $githubAction -Force
}
$operations = [object[]]@($historicalOperations + $newOperations.ToArray())
$externalEvidence = [ordered]@{
    Steam = [ordered]@{
        Requested = $true
        PublishedFileId = [string]$steam.PublishedFileId
        VisibilityIntent = "Unchanged"
        VisibilityResult = "Unchanged"
        FinalReadback = $steam
        FinalReadbackVerified = $true
        Operations = [object[]]@($operations | Where-Object Name -match "Steam")
    }
    ModIO = [ordered]@{
        Requested = $true
        ModId = [string]$modIo.ModId
        PageVisibilityIntent = "Unchanged"
        VisibilityResult = "Unchanged"
        Operations = [object[]]@($operations | Where-Object Name -match "Mod.IO")
    }
    Git = [ordered]@{
        Requested = $true
        Tags = [object[]]@([ordered]@{
            TagName = $tagName
            ExpectedCommit = [string]$preflight.ReleaseCommit
            LocalCommit = [string]$preflight.ReleaseCommit
            RemoteCommit = $remoteCommit
            LocalVerified = $true
            RemoteVerified = $true
        })
    }
    GitHub = $github
}
$readiness = $null
$readinessPath = [string]$partial.Logs.ReadinessReportPath
if (-not [string]::IsNullOrWhiteSpace($readinessPath) -and
        (Test-Path -LiteralPath $readinessPath -PathType Leaf)) {
    $readiness = Get-Content -Raw -LiteralPath $readinessPath | ConvertFrom-Json
}
$overrides = [ordered]@{
    RecoveryMode = "BoundedPartialSuccess"
    ReusedPublicAuthorization = $true
    SteamMutationRetried = $false
    ModIoMutationRetried = $false
    PlatformTagMutationRetried = $false
    GitTagMutationRetried = $false
    GitHubAction = $githubAction
    SkipDiscordHandoff = [bool]$SkipDiscordHandoff
}
$final = New-ReleaseSignoffReport `
    -Preflight $preflight `
    -Readiness $readiness `
    -Operations $operations `
    -IssueEvidence $issueEvidence `
    -WikiEvidence $wikiEvidence `
    -ExternalEvidence $externalEvidence `
    -Overrides $overrides `
    -Warnings ([string[]]$warnings.ToArray())
$history = New-Object System.Collections.Generic.List[object]
foreach ($existingHistory in @($partial.PartialSuccess.RecoveryHistory)) {
    if ($null -ne $existingHistory) {
        $history.Add($existingHistory)
    }
}
$history.Add([pscustomobject][ordered]@{
    RecoveredAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    SourceSignoffPath = $partialPath
    PreviousStatus = [string]$partial.Status
    CompletedBeforeRecovery = [string[]]@($partial.PartialSuccess.CompletedMutations)
    Readbacks = [string[]]@("Steam", "Mod.IO", "RemoteTag", "GitHubRelease")
    GitHubAction = $githubAction
    DiscordState = $discordStatus
    ReusedPublicAuthorization = $true
})
$final.PartialSuccess["RecoveryHistory"] = [object[]]$history.ToArray()
$final.PartialSuccess["RecoveredFromPartial"] = $true
Write-ReleaseSignoffReport $final $partialPath

Format-ReleaseSignoffReport $final | ForEach-Object { Write-Host $_ }
Write-Host "Recovery signoff report: $partialPath"
Write-Host "GitHub recovery action: $githubAction"
Write-Host "Steam/Mod.IO/platform tags/Git tags were read back only and were not retried."
