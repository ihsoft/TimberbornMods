param(
    [string[]] $ModName = @(),
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

function Normalize-SteamDescription([string] $Text) {
    return (($Text -replace "`r`n", "`n") -replace "`r", "`n").TrimEnd()
}

function Get-VisibleHtmlText([string] $Html) {
    Add-Type -AssemblyName System.Web

    $text = [System.Web.HttpUtility]::HtmlDecode($Html)
    $text = $text -replace "<br\s*/?>", "`n"
    $text = $text -replace "</p>|</div>|</h\d>|</li>", "`n"
    $text = $text -replace "<[^>]+>", " "
    $text = $text -replace "\[github\.com\]", ""
    $text = $text -replace "\s+", " "
    return $text.Trim()
}

function Test-SelectedMod([string] $Name) {
    return $ModName.Count -eq 0 -or $ModName -contains $Name
}

function Get-SteamDescriptionTargets() {
    return @(
        [pscustomobject]@{
            ModName = "Automation"
            PublishedFileId = "3324234282"
            LocalPath = "Automation/workshop/description.txt"
        },
        [pscustomobject]@{
            ModName = "AutomationForModdableWeather"
            PublishedFileId = "3562952077"
            LocalPath = "AutomationForModdableWeather/workshop/description.txt"
        },
        [pscustomobject]@{
            ModName = "CustomTools"
            PublishedFileId = "3619414212"
            LocalPath = "CustomTools/Workshop/Description.html"
        },
        [pscustomobject]@{
            ModName = "SmartPower"
            PublishedFileId = "3305038022"
            LocalPath = "SmartPower/workshop/description.txt"
        },
        [pscustomobject]@{
            ModName = "TimberCommons"
            PublishedFileId = "3337906807"
            LocalPath = "TimberCommons/workshop/description.txt"
        },
        [pscustomobject]@{
            ModName = "XRay"
            PublishedFileId = "3741998343"
            LocalPath = "XRay/workshop/description.txt"
        }
    ) | Where-Object { Test-SelectedMod $_.ModName }
}

function Get-ModIoDescriptionTargets() {
    $configRoot = Resolve-RepoPath ".tools/modio"
    if (-not (Test-Path -LiteralPath $configRoot)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $configRoot -Filter "*.local.json" | ForEach-Object {
        $targetModName = $_.BaseName -replace "\.local$", ""
        if (-not (Test-SelectedMod $targetModName)) {
            return
        }

        $localPath = "$targetModName/workshop/description-ModIO.html"
        if ($targetModName -eq "CustomTools") {
            $localPath = "CustomTools/Workshop/ModIO-Description.html"
        }

        [pscustomobject]@{
            ModName = $targetModName
            ConfigPath = $_.FullName
            LocalPath = $localPath
        }
    })
}

function Get-ModIoAccessToken() {
    $configRoot = Resolve-RepoPath ".tools/modio"
    $tokenFiles = @(Get-ChildItem -LiteralPath $configRoot -Filter "*.token.txt" -ErrorAction SilentlyContinue)
    foreach ($tokenFile in $tokenFiles) {
        $token = (Get-Content -Raw -LiteralPath $tokenFile.FullName).Trim()
        if (-not [string]::IsNullOrWhiteSpace($token)) {
            return [pscustomobject]@{
                Source = $tokenFile.BaseName -replace "\.token$", ""
                Value = $token
            }
        }
    }

    return $null
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

function Get-ModIoDescription([object] $Target, [string] $AccessToken) {
    $config = Get-Content -Raw -LiteralPath $Target.ConfigPath | ConvertFrom-Json
    $endpoint = "$($config.ApiBase.TrimEnd("/"))/games/$($config.GameId)/mods/$($config.ModId)"
    $response = Invoke-RestMethod -Uri $endpoint -Headers @{ Authorization = "Bearer $AccessToken" }
    return [string]$response.description
}

$results = New-Object System.Collections.Generic.List[object]

if (-not $SkipSteam) {
    foreach ($target in Get-SteamDescriptionTargets) {
        $localPath = Resolve-RepoPath $target.LocalPath
        Assert-PathExists $localPath "Steam local description"

        $local = Get-Content -Raw -LiteralPath $localPath
        $remote = Get-SteamDescription $target.PublishedFileId
        $match = (Normalize-SteamDescription $local) -eq (Normalize-SteamDescription $remote)

        $results.Add([pscustomobject]@{
            Platform = "Steam"
            ModName = $target.ModName
            Match = $match
            LocalPath = $target.LocalPath
            Remote = $target.PublishedFileId
            Comparison = "Exact text"
        })
    }
}

if (-not $SkipModIo) {
    $token = Get-ModIoAccessToken
    if ($null -eq $token) {
        throw "No Mod.IO token found under .tools/modio/*.token.txt."
    }

    foreach ($target in Get-ModIoDescriptionTargets) {
        $localPath = Resolve-RepoPath $target.LocalPath
        Assert-PathExists $localPath "Mod.IO local description"

        $local = Get-Content -Raw -LiteralPath $localPath
        $remote = Get-ModIoDescription $target $token.Value
        $match = (Get-VisibleHtmlText $local) -eq (Get-VisibleHtmlText $remote)

        $results.Add([pscustomobject]@{
            Platform = "Mod.IO"
            ModName = $target.ModName
            Match = $match
            LocalPath = $target.LocalPath
            Remote = "config:$($target.ConfigPath)"
            Comparison = "Visible HTML text"
        })
    }
}

$results | Sort-Object Platform, ModName | Format-Table -AutoSize

$failed = @($results | Where-Object { -not $_.Match })
if ($failed.Count -gt 0) {
    Write-Error "Platform description synchronization failed for $($failed.Count) target(s)."
    exit 1
}

Write-Host "All checked platform descriptions are synchronized."
