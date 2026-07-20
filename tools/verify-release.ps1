param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $Configuration = "Release",
    [string] $GameVersion = "version-1.1",
    [string] $GameRoot = "_GAME!",
    [string] $OutputRoot = ".tools/release-preview",
    [string] $ReportRoot = ".tools/release-preflight",
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
    [switch] $PublishModIoPage
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-RepoPath([string] $Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }
    return Join-Path $repoRoot $Path
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

function Invoke-ReleaseStep([string] $Name, [string] $ScriptName, [string[]] $Arguments) {
    Write-Host ""
    Write-Host "== $Name =="

    $scriptPath = Join-Path $PSScriptRoot $ScriptName
    Assert-PathExists $scriptPath $Name

    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object {
        Write-Host $_
    }
    if ($exitCode -ne 0) {
        throw "$Name failed with exit code $exitCode."
    }

    return [ordered]@{
        Name = $Name
        Script = $scriptPath
        Arguments = [string[]]$Arguments
        CompletedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
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

$modRoot = Join-Path $repoRoot $ModName
$releaseConfigPath = Join-Path $modRoot "release.json"
Assert-PathExists $releaseConfigPath "Release config"

$releaseConfig = Get-Content -Raw -LiteralPath $releaseConfigPath | ConvertFrom-Json
$modVersion = [string](Get-ObjectPropertyValue $releaseConfig "ReleaseVersion")
if ([string]::IsNullOrWhiteSpace($modVersion)) {
    throw "ReleaseVersion is empty in $releaseConfigPath."
}

$changesPath = Resolve-RepoPath ([string](Get-ObjectPropertyValue $releaseConfig "ChangesPath"))
$packageMode = [string](Get-ObjectPropertyValue $releaseConfig.Package "Mode")
if ([string]::IsNullOrWhiteSpace($packageMode)) {
    $packageMode = "ExistingZip"
}

$changelogHeading = Get-ReleaseHeading $changesPath $modVersion
if ($changelogHeading -match "\(TBD\)") {
    throw "Changelog heading for $ModName v$modVersion is still marked (TBD). Set and commit the concrete release date before final preflight."
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

    return [ordered]@{
        HeadAtPreflight = $head
        ReleaseCommit = $commit
        IdentityPaths = [string[]]$IdentityPaths
        CriticalPaths = [string[]]$CriticalPaths
        LaterCriticalChanges = [string[]]$laterCriticalChanges
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

$steps = New-Object System.Collections.Generic.List[object]

if (-not $SkipPlatformDescriptions) {
    $descriptionArgs = New-Object System.Collections.Generic.List[string]
    $descriptionArgs.Add("-ModName")
    $descriptionArgs.Add($ModName)
    Add-SwitchArgument $descriptionArgs "-SkipSteam" $SkipSteam
    Add-SwitchArgument $descriptionArgs "-SkipModIo" $SkipModIo
    $steps.Add((Invoke-ReleaseStep "Platform description verification" "verify-platform-descriptions.ps1" $descriptionArgs.ToArray()))
}

$commonPublishArgs = New-Object System.Collections.Generic.List[string]
$commonPublishArgs.Add("-ModName")
$commonPublishArgs.Add($ModName)
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
$sourceSnapshot = Get-SourceSnapshot $releaseConfig
$gitStatus = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=normal)
$tagName = "$($ModName)_$modVersion"
$tagExists = $false
& git -C $repoRoot rev-parse -q --verify "refs/tags/$tagName" *> $null
if ($LASTEXITCODE -eq 0) {
    $tagExists = $true
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
        $resolvedModIoConfigPath = Resolve-RepoPath $ModIoConfigPath
        $resolvedTokenPath = Resolve-RepoPath $ModIoAccessTokenPath
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
    SchemaVersion = 2
    CreatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    RepoRoot = $repoRoot
    GitHead = $gitSnapshot.HeadAtPreflight
    ReleaseCommit = $gitSnapshot.ReleaseCommit
    ReleaseIdentityPaths = $gitSnapshot.IdentityPaths
    ReleaseCriticalPaths = $gitSnapshot.CriticalPaths
    LaterReleaseCriticalChanges = $gitSnapshot.LaterCriticalChanges
    CorrectiveReplacement = $correctiveSnapshot
    GitStatus = [string[]]$gitStatus
    ModName = $ModName
    Version = $modVersion
    TagName = $tagName
    TagExistsBeforePublish = $tagExists
    GameVersion = $GameVersion
    Configuration = $Configuration
    ReleaseConfigPath = (Resolve-Path -LiteralPath $releaseConfigPath).Path
    ChangesPath = (Resolve-Path -LiteralPath $changesPath).Path
    ChangelogHeading = $changelogHeading
    PackageMode = $packageMode
    Package = $packageSnapshot
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
