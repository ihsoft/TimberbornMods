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
    [string] $RecoveryAttemptReportPath = "",
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

function Get-PlatformReadbackFixture {
    if (-not [string]::IsNullOrWhiteSpace($PlatformReadbackFixturePath)) {
        $fixturePath = Resolve-ToolPath $PlatformReadbackFixturePath
        Assert-PathExists $fixturePath "Platform readback fixture"
        return Get-Content -Raw -LiteralPath $fixturePath | ConvertFrom-Json
    }
    return $null
}

function Get-SteamReadback([object] $PartialSignoff, [object] $Fixture) {
    if ($null -ne $Fixture) {
        return $Fixture.Steam
    }
    $steamId = [string]$PartialSignoff.Platforms.Steam.PublishedFileId
    $steamResponse = Invoke-RestMethod -Method Post `
        -Uri "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/" `
        -Body @{ itemcount = "1"; "publishedfileids[0]" = $steamId }
    $steamItem = $steamResponse.response.publishedfiledetails[0]
    $publicReadback = [ordered]@{
        PublishedFileId = [string]$steamItem.publishedfileid
        Result = [int]$steamItem.result
        CreatorOwnerId = [string]$steamItem.creator
        CreatorAppId = [string]$steamItem.creator_app_id
        ConsumerAppId = [string]$steamItem.consumer_app_id
        Title = [string]$steamItem.title
        TimeUpdated = [long]$steamItem.time_updated
        FileSize = [long]$steamItem.file_size
        Visibility = [int]$steamItem.visibility
        Tags = [string[]]@($steamItem.tags | ForEach-Object { [string]$_.tag })
        EvidenceSource = "SteamPublicApi.GetPublishedFileDetails"
    }
    $needsOwnerReadback = [int]$publicReadback.Result -ne 1 -or
        [string]::IsNullOrWhiteSpace([string]$publicReadback.ConsumerAppId) -or
        [string]::IsNullOrWhiteSpace([string]$publicReadback.Title) -or
        [long]$publicReadback.FileSize -le 0 -or [long]$publicReadback.TimeUpdated -le 0 -or
        @($publicReadback.Tags).Count -eq 0
    if (-not $needsOwnerReadback) {
        return $publicReadback
    }
    try {
        $ownerReadback = Get-AuthenticatedSteamReadback $steamId
        $ownerReadback["PublicResult"] = [int]$publicReadback.Result
        return $ownerReadback
    }
    catch {
        $publicReadback["OwnerReadbackUnavailable"] = $_.Exception.Message
        return $publicReadback
    }
}

function Get-AuthenticatedSteamReadback([string] $PublishedFileId) {
    $projectPath = Resolve-ToolPath "tools/SteamTagUpdater/SteamTagUpdater.csproj"
    Assert-PathExists $projectPath "Steam authenticated readback project"
    $build = Invoke-ReleaseNativeCommand -Command "dotnet" -Arguments @(
        "build", $projectPath, "-c", "Release")
    if ($build.ExitCode -ne 0) {
        throw "Steam authenticated readback build failed."
    }
    $dllPath = Resolve-ToolPath "tools/SteamTagUpdater/bin/Release/net8.0/SteamTagUpdater.dll"
    Assert-PathExists $dllPath "Steam authenticated readback executable"
    $query = Invoke-ReleaseNativeCommand -Command "dotnet" -Arguments @(
        $dllPath, "--query", $PublishedFileId)
    if ($query.ExitCode -ne 0) {
        throw "Steam authenticated owner readback failed."
    }
    $itemLine = @($query.Output | Where-Object { $_ -like "LIVE_ITEM_JSON=*" }) | Select-Object -Last 1
    $tagsLine = @($query.Output | Where-Object { $_ -like "LIVE_TAGS_JSON=*" }) | Select-Object -Last 1
    if ([string]::IsNullOrWhiteSpace($itemLine) -or [string]::IsNullOrWhiteSpace($tagsLine)) {
        throw "Steam authenticated owner readback returned incomplete machine-readable evidence."
    }
    $item = $itemLine.Substring("LIVE_ITEM_JSON=".Length) | ConvertFrom-Json
    $tags = $tagsLine.Substring("LIVE_TAGS_JSON=".Length) | ConvertFrom-Json
    $visibilityMap = @{
        k_ERemoteStoragePublishedFileVisibilityPublic = 0
        k_ERemoteStoragePublishedFileVisibilityFriendsOnly = 1
        k_ERemoteStoragePublishedFileVisibilityPrivate = 2
        k_ERemoteStoragePublishedFileVisibilityUnlisted = 3
        Public = 0
        FriendsOnly = 1
        Private = 2
        Unlisted = 3
    }
    $visibility = $null
    if ($visibilityMap.ContainsKey([string]$item.Visibility)) {
        $visibility = [int]$visibilityMap[[string]$item.Visibility]
    }
    return [ordered]@{
        PublishedFileId = [string]$item.PublishedFileId
        Result = 1
        CreatorOwnerId = [string]$item.OwnerSteamId
        CreatorAppId = $null
        ConsumerAppId = [string]$item.ConsumerAppId
        Title = [string]$item.Title
        TimeUpdated = [long]$item.Updated
        FileSize = [long]$item.FileSize
        Visibility = $visibility
        Tags = [string[]]@($tags | ForEach-Object { [string]$_ })
        EvidenceSource = "AuthenticatedOwner.SteamUGC.CreateQueryUGCDetailsRequest"
    }
}

function Get-ModIoReadback([object] $Preflight, [object] $Fixture) {
    if ($null -ne $Fixture) {
        return $Fixture.ModIO
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
    return [ordered]@{
        ModId = [long]$modId
        ActiveFileId = [long]$activeFileId
        Version = [string]$mod.modfile.version
        DateScanned = [long]$file.date_scanned
        VirusStatus = [int]$file.virus_status
        VirusPositive = [int]$file.virus_positive
        FileSize = [long]$file.filesize
    }
}

function Convert-ToUnixTimeSeconds([string] $Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }
    return [long]([DateTimeOffset]::Parse($Value).ToUniversalTime().ToUnixTimeSeconds())
}

function Get-NormalizedTags([object[]] $Tags) {
    return [string[]]@($Tags | ForEach-Object { [string]$_ } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
}

function New-EvidenceComparison(
    [string] $Field,
    [object] $Expected,
    [object] $Actual,
    [bool] $Required,
    [string] $Outcome,
    [string] $Note = ""
) {
    return [ordered]@{
        Field = $Field
        Expected = $Expected
        Actual = $Actual
        Required = $Required
        Outcome = $Outcome
        Note = $Note
    }
}

function Write-RecoveryAttemptReport([object] $Report, [string] $Path) {
    $parent = Split-Path -Parent $Path
    [void](New-Item -ItemType Directory -Path $parent -Force)
    $temporaryPath = "$Path.tmp-$PID"
    $backupPath = "$Path.backup-$PID"
    try {
        [System.IO.File]::WriteAllText(
            $temporaryPath,
            ($Report | ConvertTo-Json -Depth 12),
            [System.Text.UTF8Encoding]::new($true))
        $roundTrip = Get-Content -Raw -LiteralPath $temporaryPath | ConvertFrom-Json
        if ([int]$roundTrip.SchemaVersion -ne 1 -or
                [string]$roundTrip.ArtifactType -ne "TimberbornReleaseRecoveryAttempt") {
            throw "Temporary recovery attempt report failed schema readback: $temporaryPath"
        }
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            [System.IO.File]::Replace($temporaryPath, $Path, $backupPath)
        }
        else {
            Move-Item -LiteralPath $temporaryPath -Destination $Path
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
        if (Test-Path -LiteralPath $backupPath) {
            Remove-Item -LiteralPath $backupPath -Force
        }
    }
}

function Format-EvidenceValue([object] $Value) {
    if ($null -eq $Value) {
        return "(unavailable)"
    }
    if ($Value -is [System.Array]) {
        return (@($Value) -join ", ")
    }
    return [string]$Value
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

$attemptPath = if ([string]::IsNullOrWhiteSpace($RecoveryAttemptReportPath)) {
    $attemptRoot = Resolve-ToolPath ".tools/release-recovery"
    $lineSuffix = if ([string]::IsNullOrWhiteSpace([string]$preflight.ReleaseLine)) {
        "current"
    }
    else { [string]$preflight.ReleaseLine }
    $timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss-fff")
    Join-Path $attemptRoot "$($preflight.ModName)-$($preflight.SourceModVersion)-$lineSuffix-$timestamp.json"
}
else {
    Resolve-ToolPath $RecoveryAttemptReportPath
}
$steamOperation = @($partial.Operations | Where-Object {
    [string]$_.Name -eq "Steam release publish" -and [string]$_.Status -eq "Succeeded"
})[0]
$packageOwnerRoot = Resolve-RepositoryRoot ([string]$preflight.PackageOwnerRoot) $toolRepoRoot
$packageOwnerConfigPath = [string]$preflight.PackageOwnerConfigPath
if ([string]::IsNullOrWhiteSpace($packageOwnerConfigPath)) {
    $packageOwnerConfigPath = Join-Path $packageOwnerRoot "$($preflight.ModName)/release.json"
}
Assert-PathExists $packageOwnerConfigPath "Public package owner release config"
$packageOwnerConfig = Get-Content -Raw -LiteralPath $packageOwnerConfigPath | ConvertFrom-Json
$stagingSourcePath = Resolve-RepositoryPath $packageOwnerRoot ([string]$packageOwnerConfig.Package.SourcePath)
$workshopDataPath = Join-Path $stagingSourcePath "workshop_data.json"
Assert-PathExists $workshopDataPath "Steam staging workshop metadata"
$workshopData = Get-Content -Raw -LiteralPath $workshopDataPath | ConvertFrom-Json
$expectedSteamId = [string]$partial.Platforms.Steam.PublishedFileId
$expectedSteamAppId = if ([string]::IsNullOrWhiteSpace([string]$packageOwnerConfig.Steam.AppId)) {
    "1062090"
}
else { [string]$packageOwnerConfig.Steam.AppId }
$expectedSteamOwnerId = [string]$packageOwnerConfig.Steam.OwnerId
$expectedSteamTitle = [string]$workshopData.Name
$expectedSteamVisibility = $null
if ($workshopData.UpdateVisibility -eq $true) {
    $visibilityMap = @{ Public = 0; FriendsOnly = 1; Private = 2; Unlisted = 3 }
    if (-not $visibilityMap.ContainsKey([string]$workshopData.Visibility)) {
        throw "Unsupported intended Steam visibility: $($workshopData.Visibility)"
    }
    $expectedSteamVisibility = [int]$visibilityMap[[string]$workshopData.Visibility]
}
$expectedSteamTags = Get-NormalizedTags @($workshopData.Tags)
$minimumSteamUpdateTime = Convert-ToUnixTimeSeconds ([string]$steamOperation.StartedAtUtc)
$attempt = [ordered]@{
    SchemaVersion = 1
    ArtifactType = "TimberbornReleaseRecoveryAttempt"
    CreatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    Status = "CollectingSteamReadback"
    Stage = "SteamReadback"
    Identity = [ordered]@{
        ModName = [string]$preflight.ModName
        SourceModVersion = [string]$preflight.SourceModVersion
        PublicPackageVersion = [string]$preflight.PublicPackageVersion
        ReleaseCommit = [string]$preflight.ReleaseCommit
        TagName = [string]$preflight.TagName
    }
    Inputs = [ordered]@{
        PreflightReportPath = $preflightPath
        PartialSignoffReportPath = $partialPath
        ImmutableZip = $preflight.Package
        SteamStagingContent = $preflight.Source
        SteamReadbackLimitation = (
            "Steam public file_size describes staged Workshop content and is not comparable to immutable ZIP bytes. " +
            "The public API cannot read back the local staging-content fingerprint.")
    }
    Steam = [ordered]@{
        SuccessfulPublishOperation = [ordered]@{
            Name = [string]$steamOperation.Name
            StartedAtUtc = [string]$steamOperation.StartedAtUtc
            CompletedAtUtc = [string]$steamOperation.CompletedAtUtc
            ExternalOutcome = [string]$steamOperation.ExternalOutcome
            LogPath = [string]$steamOperation.LogPath
        }
        Expected = [ordered]@{
            PublishedFileId = $expectedSteamId
            Result = 1
            ConsumerAppId = $expectedSteamAppId
            CreatorOwnerId = if ([string]::IsNullOrWhiteSpace($expectedSteamOwnerId)) { $null } else { $expectedSteamOwnerId }
            Title = $expectedSteamTitle
            Visibility = $expectedSteamVisibility
            VisibilityIntent = "Unchanged"
            MinimumFileSize = 1
            MinimumTimeUpdated = $minimumSteamUpdateTime
            Tags = [string[]]$expectedSteamTags
        }
        Actual = $null
        Comparisons = [object[]]@()
        ConclusivelyIdentified = $false
    }
    Blockers = [string[]]@()
}
$platformFixture = Get-PlatformReadbackFixture
try {
    $steam = Get-SteamReadback $partial $platformFixture
}
catch {
    $attempt.Status = "Blocked"
    $attempt.Blockers = [string[]]@("Steam readback failed: $($_.Exception.Message)")
    Write-RecoveryAttemptReport $attempt $attemptPath
    Write-Host "Recovery attempt report: $attemptPath"
    throw
}
if ([string]::IsNullOrWhiteSpace([string]$steam.EvidenceSource)) {
    $steam | Add-Member -NotePropertyName "EvidenceSource" `
        -NotePropertyValue "FixtureOrAuthenticatedReadback" -Force
}
$actualSteamTags = Get-NormalizedTags @($steam.Tags)
$attempt.Steam.Actual = $steam
Write-RecoveryAttemptReport $attempt $attemptPath
Write-Host (
    "Steam readback: published_file_id=$($steam.PublishedFileId); result=$($steam.Result); " +
    "owner=$($steam.CreatorOwnerId); creator_app_id=$($steam.CreatorAppId); " +
    "consumer_app_id=$($steam.ConsumerAppId); title=$($steam.Title); visibility=$($steam.Visibility); " +
    "file_size=$($steam.FileSize); time_updated=$($steam.TimeUpdated); " +
    "tags=$(Format-EvidenceValue $actualSteamTags); source=$($steam.EvidenceSource)")
Write-Host "Recovery attempt report: $attemptPath"
$steamComparisons = New-Object System.Collections.Generic.List[object]
$steamComparisons.Add((New-EvidenceComparison "PublishedFileId" $expectedSteamId `
    ([string]$steam.PublishedFileId) $true $(if ([string]$steam.PublishedFileId -eq $expectedSteamId) {
        "Match"
    } else { "Mismatch" })))
$steamComparisons.Add((New-EvidenceComparison "Result" 1 ([int]$steam.Result) $true `
    $(if ([int]$steam.Result -eq 1) { "Match" } else { "Mismatch" })))
$steamComparisons.Add((New-EvidenceComparison "ConsumerAppId" $expectedSteamAppId `
    ([string]$steam.ConsumerAppId) $true $(if ([string]::IsNullOrWhiteSpace([string]$steam.ConsumerAppId)) {
        "Unavailable"
    } elseif ([string]$steam.ConsumerAppId -eq $expectedSteamAppId) { "Match" } else { "Mismatch" })))
$ownerOutcome = if ([string]::IsNullOrWhiteSpace($expectedSteamOwnerId)) {
    "UnavailableExpected"
}
elseif ([string]::IsNullOrWhiteSpace([string]$steam.CreatorOwnerId)) {
    "Unavailable"
}
elseif ([string]$steam.CreatorOwnerId -eq $expectedSteamOwnerId) { "Match" } else { "Mismatch" }
$steamComparisons.Add((New-EvidenceComparison "CreatorOwnerId" `
    $(if ([string]::IsNullOrWhiteSpace($expectedSteamOwnerId)) { $null } else { $expectedSteamOwnerId }) `
    ([string]$steam.CreatorOwnerId) (-not [string]::IsNullOrWhiteSpace($expectedSteamOwnerId)) $ownerOutcome))
$steamComparisons.Add((New-EvidenceComparison "Title" $expectedSteamTitle ([string]$steam.Title) $true `
    $(if ([string]::IsNullOrWhiteSpace([string]$steam.Title)) { "Unavailable" }
        elseif ([string]$steam.Title -eq $expectedSteamTitle) { "Match" } else { "Mismatch" })))
$visibilityOutcome = if ($null -eq $expectedSteamVisibility) {
    "UnavailableExpected"
}
elseif ($null -eq $steam.Visibility) {
    "Unavailable"
}
elseif ([int]$steam.Visibility -eq [int]$expectedSteamVisibility) { "Match" } else { "Mismatch" }
$steamComparisons.Add((New-EvidenceComparison "Visibility" $expectedSteamVisibility $steam.Visibility `
    ($null -ne $expectedSteamVisibility) $visibilityOutcome "No visibility mutation was authorized."))
$steamComparisons.Add((New-EvidenceComparison "FileSize" ">0 staged Workshop bytes" ([long]$steam.FileSize) $true `
    $(if ([long]$steam.FileSize -gt 0) { "Match" } else { "Mismatch" }) `
    "Not compared with immutable ZIP length $($preflight.Package.Length)."))
$timeOutcome = if ($null -eq $minimumSteamUpdateTime) {
    "UnavailableExpected"
}
elseif ([long]$steam.TimeUpdated -ge [long]$minimumSteamUpdateTime) { "Match" } else { "Mismatch" }
$steamComparisons.Add((New-EvidenceComparison "TimeUpdated" ">=$minimumSteamUpdateTime" `
    ([long]$steam.TimeUpdated) $true $timeOutcome))
$tagsOutcome = if ($expectedSteamTags.Count -eq 0) {
    "UnavailableExpected"
}
elseif ($actualSteamTags.Count -eq 0) {
    "Unavailable"
}
elseif (($expectedSteamTags -join "|") -eq ($actualSteamTags -join "|")) { "Match" } else { "Mismatch" }
$steamComparisons.Add((New-EvidenceComparison "Tags" ([string[]]$expectedSteamTags) `
    ([string[]]$actualSteamTags) $true $tagsOutcome))
$attempt.Steam.Comparisons = [object[]]$steamComparisons.ToArray()
$blockingSteamComparisons = @($steamComparisons | Where-Object {
    $_.Required -eq $true -and $_.Outcome -ne "Match"
})
$attempt.Steam.ConclusivelyIdentified = $blockingSteamComparisons.Count -eq 0
$attempt.Status = if ($attempt.Steam.ConclusivelyIdentified) { "SteamVerified" } else { "Blocked" }
$attempt.Blockers = [string[]]@($blockingSteamComparisons | ForEach-Object {
    "Steam $($_.Field): expected $(Format-EvidenceValue $_.Expected); " +
    "actual $(Format-EvidenceValue $_.Actual); outcome $($_.Outcome)."
})
Write-RecoveryAttemptReport $attempt $attemptPath
foreach ($comparison in $steamComparisons) {
    Write-Host (
        "Steam comparison: $($comparison.Field); expected=$(Format-EvidenceValue $comparison.Expected); " +
        "actual=$(Format-EvidenceValue $comparison.Actual); outcome=$($comparison.Outcome)")
}
if (-not $attempt.Steam.ConclusivelyIdentified) {
    throw "Steam publication identity readback did not match the recorded successful publish. See $attemptPath"
}

$modIo = Get-ModIoReadback $preflight $platformFixture
$expectedModIo = $partial.Platforms.ModIO.FinalReadback
$modIoComparisons = [object[]]@(
    (New-EvidenceComparison "ModId" ([long]$expectedModIo.ModId) ([long]$modIo.ModId) $true `
        $(if ([long]$modIo.ModId -eq [long]$expectedModIo.ModId) { "Match" } else { "Mismatch" })),
    (New-EvidenceComparison "ActiveFileId" ([long]$expectedModIo.ActiveFileId) `
        ([long]$modIo.ActiveFileId) $true $(if ([long]$modIo.ActiveFileId -eq [long]$expectedModIo.ActiveFileId) {
            "Match"
        } else { "Mismatch" })),
    (New-EvidenceComparison "Version" ([string]$preflight.PublicPackageVersion) ([string]$modIo.Version) $true `
        $(if ([string]$modIo.Version -eq [string]$preflight.PublicPackageVersion) { "Match" } else { "Mismatch" })),
    (New-EvidenceComparison "DateScanned" ">0" ([long]$modIo.DateScanned) $true `
        $(if ([long]$modIo.DateScanned -gt 0) { "Match" } else { "Mismatch" })),
    (New-EvidenceComparison "VirusStatus" 1 ([int]$modIo.VirusStatus) $true `
        $(if ([int]$modIo.VirusStatus -eq 1) { "Match" } else { "Mismatch" })),
    (New-EvidenceComparison "VirusPositive" 0 ([int]$modIo.VirusPositive) $true `
        $(if ([int]$modIo.VirusPositive -eq 0) { "Match" } else { "Mismatch" })),
    (New-EvidenceComparison "FileSize" ([long]$preflight.Package.Length) ([long]$modIo.FileSize) $true `
        $(if ([long]$modIo.FileSize -eq [long]$preflight.Package.Length) { "Match" } else { "Mismatch" }) `
        "Mod.IO consumes the immutable ZIP, so this size comparison stays within one artifact boundary."))
$blockingModIoComparisons = @($modIoComparisons | Where-Object Outcome -ne "Match")
if ($null -eq $expectedModIo -or $blockingModIoComparisons.Count -gt 0) {
    $attempt.Status = "Blocked"
    $attempt.Stage = "ModIOReadback"
    $attempt["ModIO"] = [ordered]@{
        Expected = $expectedModIo
        Actual = $modIo
        Comparisons = $modIoComparisons
        Outcome = "Mismatch"
        Verified = $false
    }
    $attempt.Blockers = [string[]]@("Mod.IO final readback did not match the completed immutable publication.")
    Write-RecoveryAttemptReport $attempt $attemptPath
    throw "Mod.IO final readback does not match the completed immutable publication."
}
$attempt.Status = "ModIOVerified"
$attempt.Stage = "ModIOVerified"
$attempt["ModIO"] = [ordered]@{
    Expected = $expectedModIo
    Actual = $modIo
    Comparisons = $modIoComparisons
    Outcome = "Match"
    Verified = $true
}
Write-RecoveryAttemptReport $attempt $attemptPath

$tagName = [string]$preflight.TagName
$remoteTag = Invoke-ReleaseNativeCommand -Command "git" -Arguments @(
    "-C", $repoRoot, "ls-remote", "--tags", "origin", "refs/tags/$tagName")
if ($remoteTag.ExitCode -ne 0 -or $remoteTag.Output.Count -ne 1) {
    $attempt.Status = "Blocked"
    $attempt.Stage = "RemoteTagReadback"
    $attempt["RemoteTag"] = [ordered]@{
        TagName = $tagName
        ExpectedCommit = [string]$preflight.ReleaseCommit
        ActualOutput = [string[]]$remoteTag.Output
        Diagnostics = [string[]]$remoteTag.Diagnostics
        Outcome = "Unavailable"
    }
    $attempt.Blockers = [string[]]@("Remote qualified tag readback failed for $tagName.")
    Write-RecoveryAttemptReport $attempt $attemptPath
    throw "Remote qualified tag readback failed for $tagName."
}
$remoteCommit = ([string]$remoteTag.Output[0] -split "\s+")[0]
if ($remoteCommit -ne [string]$preflight.ReleaseCommit) {
    $attempt.Status = "Blocked"
    $attempt.Stage = "RemoteTagReadback"
    $attempt["RemoteTag"] = [ordered]@{
        TagName = $tagName
        ExpectedCommit = [string]$preflight.ReleaseCommit
        ActualCommit = $remoteCommit
        Outcome = "Mismatch"
    }
    $attempt.Blockers = [string[]]@(
        "Remote qualified tag points to $remoteCommit, expected $($preflight.ReleaseCommit).")
    Write-RecoveryAttemptReport $attempt $attemptPath
    throw "Remote qualified tag points to $remoteCommit, expected $($preflight.ReleaseCommit)."
}
$attempt.Status = "RemoteTagVerified"
$attempt.Stage = "RemoteTagVerified"
$attempt["RemoteTag"] = [ordered]@{
    TagName = $tagName
    ExpectedCommit = [string]$preflight.ReleaseCommit
    ActualCommit = $remoteCommit
    Outcome = "Match"
}
$attempt.Blockers = [string[]]@()
Write-RecoveryAttemptReport $attempt $attemptPath

$assetName = [System.IO.Path]::GetFileName($packagePath)
try {
    $githubBefore = Get-GitHubReleaseReadback `
        $tagName $title $notes $assetName ([string]$preflight.Package.Sha256)
}
catch {
    $attempt.Status = "Blocked"
    $attempt.Stage = "GitHubReadback"
    $attempt["GitHub"] = [ordered]@{
        ExpectedTagName = $tagName
        ExpectedTitle = $title
        ExpectedAssetName = $assetName
        ExpectedAssetDigest = "sha256:$($preflight.Package.Sha256)"
        Outcome = "Unknown"
        Error = $_.Exception.Message
    }
    $attempt.Blockers = [string[]]@("GitHub Release readback failed: $($_.Exception.Message)")
    Write-RecoveryAttemptReport $attempt $attemptPath
    throw
}
$attempt.Status = if ([bool]$githubBefore.Exists) { "GitHubVerified" } else { "GitHubAbsent" }
$attempt.Stage = $attempt.Status
$attempt["GitHub"] = [ordered]@{
    ExpectedTagName = $tagName
    ExpectedTitle = $title
    ExpectedAssetName = $assetName
    ExpectedAssetDigest = "sha256:$($preflight.Package.Sha256)"
    BeforeRecovery = $githubBefore
    Outcome = if ([bool]$githubBefore.Exists) { "VerifiedExisting" } else { "ConclusiveAbsence" }
}
$attempt.Blockers = [string[]]@()
Write-RecoveryAttemptReport $attempt $attemptPath
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
        "-PreflightReportPath", $preflightPath,
        "-Repository", $Repository,
        "-TagName", $tagName,
        "-Title", $title,
        "-ExpectedPackageSha256", [string]$preflight.Package.Sha256,
        "-Publish")
    try {
        $githubOperation = Invoke-RecoveryChild `
            "GitHub release recovery publish" "publish-github-release.ps1" `
            $githubArguments $recoveryLogDirectory 1
    }
    catch {
        $attempt.Status = "Blocked"
        $attempt.Stage = "GitHubAbsent"
        $attempt.GitHub["ChildMutationAttempted"] = $true
        $attempt.GitHub["ChildOutcome"] = "FailedBeforeConfirmedMutation"
        $attempt.GitHub["Error"] = $_.Exception.Message
        $attempt.Blockers = [string[]]@("GitHub Release child failed: $($_.Exception.Message)")
        Write-RecoveryAttemptReport $attempt $attemptPath
        throw
    }
    $newOperations.Add($githubOperation)
    $githubAction = "CreatedMissingRelease"
    $attempt.Status = "GitHubMutationAwaitingReadback"
    $attempt.Stage = "GitHubMutationAwaitingReadback"
    $attempt.GitHub["ChildMutationAttempted"] = $true
    $attempt.GitHub["ChildOutcome"] = "SucceededAwaitingReadback"
    Write-RecoveryAttemptReport $attempt $attemptPath
}
try {
    $github = Get-GitHubReleaseReadback `
        $tagName $title $notes $assetName ([string]$preflight.Package.Sha256)
}
catch {
    $attempt.Status = "Blocked"
    $attempt.Stage = "GitHubReadback"
    $attempt.GitHub["FinalOutcome"] = "Unknown"
    $attempt.GitHub["Error"] = $_.Exception.Message
    $attempt.Blockers = [string[]]@("GitHub Release final readback failed: $($_.Exception.Message)")
    Write-RecoveryAttemptReport $attempt $attemptPath
    throw
}
if (-not [bool]$github.Verified) {
    $attempt.Status = "Blocked"
    $attempt.Stage = "GitHubReadback"
    $attempt.GitHub["FinalReadback"] = $github
    $attempt.GitHub["FinalOutcome"] = "Unverified"
    $attempt.Blockers = [string[]]@("GitHub Release recovery did not produce conclusive readback.")
    Write-RecoveryAttemptReport $attempt $attemptPath
    throw "GitHub Release recovery did not produce conclusive readback."
}
$github["Requested"] = $true
$attempt.Status = "GitHubVerified"
$attempt.Stage = "GitHubVerified"
$attempt.GitHub["FinalReadback"] = $github
$attempt.GitHub["FinalOutcome"] = "Verified"
$attempt.Blockers = [string[]]@()
Write-RecoveryAttemptReport $attempt $attemptPath
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
    RecoveryAttemptReportPath = $attemptPath
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
$attempt.Status = "Completed"
$attempt.Stage = "FinalSignoff"
$attempt["ModIO"] = [ordered]@{
    Actual = $modIo
    Verified = $true
}
$attempt["RemoteTag"] = [ordered]@{
    TagName = $tagName
    Commit = $remoteCommit
    Verified = $true
}
$attempt["GitHub"] = [ordered]@{
    Action = $githubAction
    Url = [string]$github.Url
    AssetName = [string]$github.AssetName
    AssetDigest = [string]$github.AssetDigest
    Verified = $true
}
$attempt["FinalSignoffReportPath"] = $partialPath
Write-RecoveryAttemptReport $attempt $attemptPath

Format-ReleaseSignoffReport $final | ForEach-Object { Write-Host $_ }
Write-Host "Recovery signoff report: $partialPath"
Write-Host "GitHub recovery action: $githubAction"
Write-Host "Steam/Mod.IO/platform tags/Git tags were read back only and were not retried."
