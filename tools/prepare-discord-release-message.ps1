param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $Version = "",
    [string] $Repository = "ihsoft/TimberbornMods",
    [switch] $Prepare
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$modRoot = Join-Path $repoRoot $ModName
$releaseConfigPath = Join-Path $modRoot "release.json"

function Assert-PathExists([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Resolve-RepoPath([string] $Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }
    return Join-Path $repoRoot $Path
}

function Get-ChangeNotes([string] $Path, [string] $ReleaseVersion) {
    $content = Get-Content -Raw -LiteralPath $Path
    $escapedVersion = [regex]::Escape($ReleaseVersion)
    $pattern = "(?ms)^#\s+v$escapedVersion[^\r\n]*\r?\n(?<body>.*?)(?=^#\s+v|\z)"
    $match = [regex]::Match($content, $pattern)
    if (-not $match.Success) {
        throw "Cannot find changelog section for v$ReleaseVersion."
    }

    $notes = $match.Groups["body"].Value.Trim()
    if ([string]::IsNullOrWhiteSpace($notes)) {
        throw "Changelog section for v$ReleaseVersion is empty."
    }
    return $notes
}

function Get-DiscordChannelUrl([string[]] $DescriptionPaths) {
    $channelPattern = "https://discord\.com/channels/\d+/\d+"
    $channelUrls = @(
        $DescriptionPaths | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object {
            $content = Get-Content -Raw -LiteralPath $_
            [regex]::Matches($content, $channelPattern) | ForEach-Object { $_.Value }
        } | Sort-Object -Unique
    )

    if ($channelUrls.Count -eq 0) {
        throw "No direct Discord channel URL was found in the platform descriptions for $ModName."
    }
    if ($channelUrls.Count -gt 1) {
        throw "Platform descriptions contain different Discord channel URLs for ${ModName}: $($channelUrls -join ', ')."
    }
    return $channelUrls[0]
}

Assert-PathExists $releaseConfigPath "Release config"
$releaseConfig = Get-Content -Raw -LiteralPath $releaseConfigPath | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = [string]$releaseConfig.ReleaseVersion
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Release version is missing for $ModName."
}

$manifestPath = Resolve-RepoPath ([string]$releaseConfig.ManifestPath)
$changesPath = Resolve-RepoPath ([string]$releaseConfig.ChangesPath)
Assert-PathExists $manifestPath "Manifest"
Assert-PathExists $changesPath "Changelog"

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$displayName = [string]$manifest.Name
if ([string]::IsNullOrWhiteSpace($displayName)) {
    $displayName = $ModName
}

$descriptionPaths = @(
    (Join-Path $modRoot "workshop/description.txt"),
    (Join-Path $modRoot "workshop/description-ModIO.html")
)
$channelUrl = Get-DiscordChannelUrl $descriptionPaths
$notes = Get-ChangeNotes $changesPath $Version
$tagName = "${ModName}_${Version}"
$releaseUrl = "https://github.com/$Repository/releases/tag/$tagName"
$message = "$displayName v$Version has been released!`r`n`r`n$notes`r`n`r`nDownload and full changelog:`r`n$releaseUrl"

if ($message.Length -gt 2000) {
    throw "Discord release message is $($message.Length) characters; review it manually before posting because " +
        "Discord messages are limited to 2000 characters."
}

Write-Host "Discord release handoff for $ModName v$Version"
Write-Host "Channel: $channelUrl"
Write-Host "Message length: $($message.Length) characters"
Write-Host ""
Write-Host $message

if (-not $Prepare) {
    Write-Host ""
    Write-Host "Dry run only. Pass -Prepare to copy the message and open the Discord channel."
    exit 0
}

Set-Clipboard -Value $message
$channelMatch = [regex]::Match($channelUrl, "/channels/(?<server>\d+)/(?<channel>\d+)")
$discordAppUrl = "discord://-/channels/$($channelMatch.Groups['server'].Value)/$($channelMatch.Groups['channel'].Value)"
try {
    Start-Process $discordAppUrl
}
catch {
    Write-Warning "Could not open the Discord app link; opening the channel URL in the default browser."
    Start-Process $channelUrl
}

Write-Host ""
Write-Host "Message copied to the clipboard and Discord channel opened. Review it and send it manually."
