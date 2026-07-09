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
    [switch] $IncludeLegacyVersions,
    [switch] $SkipBuild,
    [switch] $SkipUnityExport,
    [switch] $SkipPlatformDescriptions,
    [switch] $SkipPlatformTags,
    [switch] $SkipSteam,
    [switch] $SkipModIo
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

if (-not $SkipSteam) {
    $steamArgs = New-Object System.Collections.Generic.List[string]
    $steamArgs.AddRange([string[]]$commonPublishArgs)
    Add-OptionalArgument $steamArgs "-SteamConfigPath" $SteamConfigPath
    Add-OptionalArgument $steamArgs "-SteamCmdPath" $SteamCmdPath
    Add-OptionalArgument $steamArgs "-SteamUserName" $SteamUserName
    $steps.Add((Invoke-ReleaseStep "Steam release dry run" "publish-steam.ps1" $steamArgs.ToArray()))
}

if (-not $SkipModIo) {
    $modIoArgs = New-Object System.Collections.Generic.List[string]
    $modIoArgs.AddRange([string[]]$commonPublishArgs)
    Add-OptionalArgument $modIoArgs "-ConfigPath" $ModIoConfigPath
    Add-OptionalArgument $modIoArgs "-AccessTokenPath" $ModIoAccessTokenPath
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
}

Write-Host ""
Write-Host "Collecting release artifact snapshots..."
$packagePath = Get-PackagePath $releaseConfig
$packageSnapshot = Get-FileSnapshot $packagePath "Release package"
$sourceSnapshot = Get-SourceSnapshot $releaseConfig
$gitHead = (& git -C $repoRoot rev-parse HEAD).Trim()
$gitStatus = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=normal)
$tagName = "$($ModName)_$modVersion"
$tagExists = $false
& git -C $repoRoot rev-parse -q --verify "refs/tags/$tagName" *> $null
if ($LASTEXITCODE -eq 0) {
    $tagExists = $true
}

$report = [ordered]@{
    SchemaVersion = 1
    CreatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    RepoRoot = $repoRoot
    GitHead = $gitHead
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
