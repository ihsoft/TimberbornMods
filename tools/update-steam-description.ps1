param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $SteamConfigPath = "",
    [string] $SteamCmdPath = "",
    [string] $SteamUserName = "",
    [string] $VdfRoot = ".tools/steam-description-updates",
    [switch] $Publish
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

function Normalize-SteamDescription([string] $Text) {
    return (($Text -replace "`r`n", "`n") -replace "`r", "`n").TrimEnd()
}

function Get-SteamDescriptionTargetFromReleaseConfig([string] $Name) {
    $releaseConfigPath = Resolve-RepoPath "$Name/release.json"
    if (-not (Test-Path -LiteralPath $releaseConfigPath)) {
        return $null
    }

    $releaseConfig = Get-Content -Raw -LiteralPath $releaseConfigPath | ConvertFrom-Json
    if ($null -eq $releaseConfig.Steam -or [string]::IsNullOrWhiteSpace([string]$releaseConfig.Steam.PublishedFileId)) {
        return $null
    }

    $localPath = "$Name/workshop/description.txt"
    $resolvedLocalPath = Resolve-RepoPath $localPath
    if (-not (Test-Path -LiteralPath $resolvedLocalPath)) {
        return $null
    }

    $title = $Name
    if (-not [string]::IsNullOrWhiteSpace([string]$releaseConfig.ManifestPath)) {
        $manifestPath = Resolve-RepoPath ([string]$releaseConfig.ManifestPath)
        if (Test-Path -LiteralPath $manifestPath) {
            $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
            if (-not [string]::IsNullOrWhiteSpace([string]$manifest.Name)) {
                $title = [string]$manifest.Name
            }
        }
    }

    return [pscustomobject]@{
        PublishedFileId = [string]$releaseConfig.Steam.PublishedFileId
        Title = $title
        LocalPath = $localPath
    }
}

function Read-SteamConfig([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        $Path = Resolve-RepoPath ".tools/steam/steam.local.json"
    }
    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Resolve-SteamCmdPath([string] $ConfiguredPath) {
    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        $resolvedPath = Resolve-RepoPath $ConfiguredPath
        if (Test-Path -LiteralPath $resolvedPath) {
            return $resolvedPath
        }
        throw "SteamCMD not found: $resolvedPath"
    }

    $command = Get-Command steamcmd.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $localSteamCmd = Resolve-RepoPath ".tools/steamcmd/steamcmd.exe"
    if (Test-Path -LiteralPath $localSteamCmd) {
        return $localSteamCmd
    }

    return ""
}

function Get-SteamLoginSettings() {
    $steamConfig = Read-SteamConfig $SteamConfigPath
    if ($null -ne $steamConfig) {
        if ([string]::IsNullOrWhiteSpace($SteamCmdPath)) {
            $script:SteamCmdPath = [string]$steamConfig.SteamCmdPath
        }
        if ([string]::IsNullOrWhiteSpace($SteamUserName)) {
            $script:SteamUserName = [string]$steamConfig.UserName
        }
    }

    $resolvedSteamCmdPath = Resolve-SteamCmdPath $SteamCmdPath
    if ([string]::IsNullOrWhiteSpace($resolvedSteamCmdPath)) {
        throw "steamcmd.exe was not found. Set SteamCmdPath in .tools/steam/steam.local.json."
    }
    if ([string]::IsNullOrWhiteSpace($SteamUserName)) {
        throw "Steam user name is empty. Set UserName in .tools/steam/steam.local.json."
    }

    return [pscustomobject]@{
        SteamCmdPath = $resolvedSteamCmdPath
        UserName = $SteamUserName
    }
}

function ConvertTo-VdfString([string] $Value) {
    return $Value.Replace("\", "\\")
}

function Write-DescriptionVdf(
    [string] $Path,
    [string] $PublishedFileId,
    [string] $Title,
    [string] $Description) {
    if ($Description.Contains('"')) {
        throw "Steam description contains double quotes. Replace them or extend VDF escaping before publishing."
    }

    $vdf = @"
"workshopitem"
{
    "appid" "1062090"
    "publishedfileid" "$PublishedFileId"
    "title" "$Title"
    "description" "$(ConvertTo-VdfString $Description)"
}
"@
    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    Set-Content -LiteralPath $Path -Value $vdf -Encoding UTF8
}

function Get-SteamDescription([string] $PublishedFileId) {
    $body = "itemcount=1&publishedfileids%5B0%5D=$PublishedFileId"
    $response = Invoke-WebRequest `
        -Uri "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/" `
        -Method Post `
        -Body $body `
        -ContentType "application/x-www-form-urlencoded" `
        -UseBasicParsing
    $item = ($response.Content | ConvertFrom-Json).response.publishedfiledetails[0]
    if ($item.result -ne 1) {
        throw "Steam API failed for $PublishedFileId with result $($item.result)."
    }
    return [string]$item.description
}

$target = Get-SteamDescriptionTargetFromReleaseConfig $ModName
if ($null -eq $target) {
    throw "Steam description metadata is incomplete for $ModName. Expected release.json with Steam.PublishedFileId and workshop/description.txt."
}

$localPath = Resolve-RepoPath $target.LocalPath
Assert-PathExists $localPath "Local Steam description"

$description = Get-Content -Raw -LiteralPath $localPath
$vdfPath = Join-Path (Resolve-RepoPath $VdfRoot) "$ModName-description.vdf"
Write-DescriptionVdf $vdfPath $target.PublishedFileId $target.Title $description

Write-Host "Steam description update plan for $ModName"
Write-Host "PublishedFileId: $($target.PublishedFileId)"
Write-Host "Local description: $($target.LocalPath)"
Write-Host "VDF: $vdfPath"

$current = Get-SteamDescription $target.PublishedFileId
$alreadySynced = (Normalize-SteamDescription $description) -eq (Normalize-SteamDescription $current)
Write-Host "Already synchronized: $alreadySynced"

if (-not $Publish) {
    Write-Host "Dry run only. Nothing was uploaded. Use -Publish only after an explicit description update request."
    exit 0
}

$loginSettings = Get-SteamLoginSettings
& $loginSettings.SteamCmdPath +login $loginSettings.UserName +workshop_build_item $vdfPath +quit
if ($LASTEXITCODE -ne 0) {
    throw "SteamCMD failed with exit code $LASTEXITCODE."
}

$updated = Get-SteamDescription $target.PublishedFileId
if ((Normalize-SteamDescription $description) -ne (Normalize-SteamDescription $updated)) {
    throw "Steam description update completed, but live description does not exactly match local description."
}

Write-Host "Steam description is synchronized."
