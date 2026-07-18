param(
    [Parameter(Mandatory = $true)]
    [string] $PreflightReportPath,

    [string] $Configuration = "",
    [string] $GameVersion = "",
    [string] $GameRoot = "_GAME!",
    [string] $OutputRoot = ".tools/release-preview",
    [string] $LocalModRoot = "",
    [string] $SteamConfigPath = "",
    [string] $SteamCmdPath = "",
    [string] $SteamUserName = "",
    [string] $ModIoConfigPath = "",
    [string] $ModIoAccessTokenPath = "",
    [string] $Repository = "ihsoft/TimberbornMods",
    [switch] $SkipSteam,
    [switch] $SkipModIo,
    [switch] $SkipPlatformTags,
    [switch] $SkipGitTag,
    [switch] $SkipGitHubRelease
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

function Invoke-Git([string[]] $Arguments) {
    $output = @(& git -C $repoRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed.`n$($output -join [Environment]::NewLine)"
    }
    return [string[]]$output
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

$reportPath = Resolve-RepoPath $PreflightReportPath
Assert-PathExists $reportPath "Preflight report"
$report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json

if ([int]$report.SchemaVersion -ne 2) {
    throw "Unsupported preflight report schema version: $($report.SchemaVersion)"
}

if ($report.ReadyForPublish -ne $true) {
    throw "Preflight report is not marked ReadyForPublish."
}

$modName = [string]$report.ModName
$modVersion = [string]$report.Version
$tagName = [string]$report.TagName
$releaseCommit = [string]$report.ReleaseCommit
if ([string]::IsNullOrWhiteSpace($modName) -or [string]::IsNullOrWhiteSpace($modVersion) -or
    [string]::IsNullOrWhiteSpace($releaseCommit)) {
    throw "Preflight report is missing mod name, version, or release commit."
}

if ([string]::IsNullOrWhiteSpace($Configuration)) {
    $Configuration = [string]$report.Configuration
}
if ([string]::IsNullOrWhiteSpace($GameVersion)) {
    $GameVersion = [string]$report.GameVersion
}

$currentHead = (& git -C $repoRoot rev-parse HEAD).Trim()
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

$releaseConfigPath = [string]$report.ReleaseConfigPath
Assert-PathExists $releaseConfigPath "Release config"
$releaseConfig = Get-Content -Raw -LiteralPath $releaseConfigPath | ConvertFrom-Json
$currentVersion = [string](Get-ObjectPropertyValue $releaseConfig "ReleaseVersion")
if ($currentVersion -ne $modVersion) {
    throw "ReleaseVersion changed since preflight. Preflight: $modVersion; current: $currentVersion"
}

if ($null -ne $report.Source) {
    $sourcePath = [string]$report.Source.Path
    $currentSource = Get-DirectoryFingerprint $sourcePath
    if ($currentSource.Sha256 -ne [string]$report.Source.Sha256) {
        throw "Package source changed since preflight. Preflight: $($report.Source.Sha256); current: $($currentSource.Sha256)"
    }
}
elseif ($null -ne $report.Package) {
    $packagePath = [string]$report.Package.Path
    Assert-PathExists $packagePath "Release package"
    $currentPackageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($currentPackageHash -ne [string]$report.Package.Sha256) {
        throw "Release package changed since preflight. Preflight: $($report.Package.Sha256); current: $currentPackageHash"
    }
}

& git -C $repoRoot rev-parse -q --verify "refs/tags/$tagName" *> $null
if ($LASTEXITCODE -eq 0 -and -not $SkipGitTag) {
    throw "Release tag already exists before publish: $tagName"
}

$commonPublishArgs = New-Object System.Collections.Generic.List[string]
$commonPublishArgs.Add("-ModName")
$commonPublishArgs.Add($modName)
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

if (-not $SkipSteam -and -not [bool]$report.PreflightOptions.SkipSteam) {
    $steamArgs = New-Object System.Collections.Generic.List[string]
    $steamArgs.AddRange([string[]]$commonPublishArgs)
    Add-OptionalArgument $steamArgs "-SteamConfigPath" $SteamConfigPath
    Add-OptionalArgument $steamArgs "-SteamCmdPath" $SteamCmdPath
    Add-OptionalArgument $steamArgs "-SteamUserName" $SteamUserName
    $steamArgs.Add("-Publish")
    Invoke-ReleaseStep "Steam release publish" "publish-steam.ps1" $steamArgs.ToArray()
}

if (-not $SkipModIo -and -not [bool]$report.PreflightOptions.SkipModIo) {
    $modIoArgs = New-Object System.Collections.Generic.List[string]
    $modIoArgs.AddRange([string[]]$commonPublishArgs)
    Add-OptionalArgument $modIoArgs "-ConfigPath" $ModIoConfigPath
    Add-OptionalArgument $modIoArgs "-AccessTokenPath" $ModIoAccessTokenPath
    $modIoArgs.Add("-Publish")
    Invoke-ReleaseStep "Mod.IO release publish" "publish-modio.ps1" $modIoArgs.ToArray()
}

if (-not $SkipPlatformTags -and -not [bool]$report.PreflightOptions.SkipPlatformTags -and -not $SkipModIo -and
    -not [bool]$report.PreflightOptions.SkipModIo) {
    $tagArgs = New-Object System.Collections.Generic.List[string]
    $tagArgs.Add("-ModName")
    $tagArgs.Add($modName)
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
    Write-Host "== Git release tag =="
    & git -C $repoRoot tag $tagName $releaseCommit
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create Git tag $tagName."
    }

    & git -C $repoRoot push origin $tagName
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to push Git tag $tagName."
    }

    Write-Host "Git tag created and pushed: $tagName -> $releaseCommit"
}

if (-not $SkipGitHubRelease) {
    $githubArgs = New-Object System.Collections.Generic.List[string]
    $githubArgs.Add("-ModName")
    $githubArgs.Add($modName)
    $githubArgs.Add("-Repository")
    $githubArgs.Add($Repository)
    $githubArgs.Add("-Publish")
    Invoke-ReleaseStep "GitHub release publish" "publish-github-release.ps1" $githubArgs.ToArray()
}

Write-Host ""
Write-Host "Release publish completed for $modName v$modVersion."
Write-Host "Issue closing and Wiki handoff remain explicit follow-up steps."
