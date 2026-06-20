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

function Get-SteamDescriptionTargets() {
    return @{
        Automation = [pscustomobject]@{
            PublishedFileId = "3324234282"
            Title = "Advanced Automation"
            LocalPath = "Automation/workshop/description.txt"
        }
        AutomationForModdableWeather = [pscustomobject]@{
            PublishedFileId = "3562952077"
            Title = "Automation+ModdableWeather"
            LocalPath = "AutomationForModdableWeather/workshop/description.txt"
        }
        CustomTools = [pscustomobject]@{
            PublishedFileId = "3619414212"
            Title = "CustomTools"
            LocalPath = "CustomTools/Workshop/Description.html"
        }
        SmartPower = [pscustomobject]@{
            PublishedFileId = "3305038022"
            Title = "SmartPower"
            LocalPath = "SmartPower/workshop/description.txt"
        }
        TimberCommons = [pscustomobject]@{
            PublishedFileId = "3337906807"
            Title = "TimberCommons"
            LocalPath = "TimberCommons/workshop/description.txt"
        }
        XRay = [pscustomobject]@{
            PublishedFileId = "3741998343"
            Title = "X-Ray"
            LocalPath = "XRay/workshop/description.txt"
        }
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

$targets = Get-SteamDescriptionTargets
if (-not $targets.ContainsKey($ModName)) {
    throw "Unsupported mod for Steam description update: $ModName"
}

$target = $targets[$ModName]
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
