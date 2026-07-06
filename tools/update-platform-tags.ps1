param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [ValidateSet("All", "Steam", "ModIO")]
    [string] $Platform = "All",

    [string] $LocalModRoot = "",
    [string] $SteamConfigPath = "",
    [string] $SteamCmdPath = "",
    [string] $SteamUserName = "",
    [string] $SteamPublisherKeyPath = "",
    [string] $SteamVdfRoot = ".tools/steam-tag-updates",
    [string] $ModIoConfigPath = "",
    [string] $ModIoAccessTokenPath = "",
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

function Get-UniqueTags([string[]] $Tags) {
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $result = New-Object System.Collections.Generic.List[string]
    foreach ($tag in $Tags) {
        $trimmed = [string]$tag
        $trimmed = $trimmed.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }
        if ($seen.Add($trimmed)) {
            $result.Add($trimmed)
        }
    }
    return @($result)
}

function Get-AddedTags([string[]] $TargetTags, [string[]] $CurrentTags) {
    $current = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($tag in $CurrentTags) {
        [void]$current.Add($tag)
    }
    return @($TargetTags | Where-Object { -not $current.Contains($_) })
}

function Get-RemovedTags([string[]] $TargetTags, [string[]] $CurrentTags) {
    $target = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($tag in $TargetTags) {
        [void]$target.Add($tag)
    }
    return @($CurrentTags | Where-Object { -not $target.Contains($_) })
}

function Format-Tags([string[]] $Tags) {
    if ($Tags.Count -eq 0) {
        return "(none)"
    }
    return $Tags -join ", "
}

function Get-LocalModRootPath() {
    if (-not [string]::IsNullOrWhiteSpace($LocalModRoot)) {
        return Resolve-RepoPath $LocalModRoot
    }
    return Resolve-RepoPath (Join-Path "_MODS!" $ModName)
}

function Get-LocalWorkshopData([string] $RootPath) {
    $path = Join-Path $RootPath "workshop_data.json"
    Assert-PathExists $path "Local workshop data"
    return [pscustomobject]@{
        Path = $path
        Data = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    }
}

function Save-LocalWorkshopData([string] $Path, [object] $Data) {
    $Data | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-VersionCompatibilityTags([string] $RootPath) {
    $versionFolders = @(Get-ChildItem -LiteralPath $RootPath -Directory | Where-Object {
        $_.Name -match "^version-(?<major>\d+)\.(?<minor>\d+)(?:\.\d+)?$"
    } | Sort-Object Name)

    $tags = @($versionFolders | ForEach-Object {
        if ($_.Name -match "^version-(?<major>\d+)\.(?<minor>\d+)(?:\.\d+)?$") {
            "Update $($matches["major"]).$($matches["minor"])"
        }
    })

    return [pscustomobject]@{
        VersionFolders = @($versionFolders | Select-Object -ExpandProperty Name)
        Tags = Get-UniqueTags $tags
    }
}

function Convert-ToModIoTag([string] $Tag) {
    $map = @{
        "Quality of life" = "QoL"
        "New content" = "New in-game content"
    }
    if ($map.ContainsKey($Tag)) {
        return $map[$Tag]
    }
    return $Tag
}

function Test-VersionTag([string] $Tag) {
    return $Tag -match "^Update \d+\.\d+$"
}

function Get-AdditionalCompatibilityTags([object] $ReleaseConfig) {
    $tags = @()
    if ($null -ne $ReleaseConfig.PlatformTags -and
            $null -ne $ReleaseConfig.PlatformTags.AdditionalCompatibilityTags) {
        $tags = @($ReleaseConfig.PlatformTags.AdditionalCompatibilityTags | ForEach-Object { [string]$_ })
    }

    foreach ($tag in $tags) {
        if (-not (Test-VersionTag $tag)) {
            throw "PlatformTags.AdditionalCompatibilityTags can only contain Update X.Y tags: $tag"
        }
    }

    return Get-UniqueTags $tags
}

function Get-TargetSteamTags([string[]] $LocalTags, [string[]] $VersionTags, [object] $ReleaseConfig) {
    $additionalCompatibilityTags = Get-AdditionalCompatibilityTags $ReleaseConfig
    $nonVersionTags = @($LocalTags | Where-Object { -not (Test-VersionTag $_) })
    return Get-UniqueTags (@("Mod") + $VersionTags + $additionalCompatibilityTags + $nonVersionTags)
}

function Get-TargetTags([string[]] $LocalTags, [string[]] $VersionTags, [string] $TargetPlatform, [object] $ReleaseConfig) {
    $sourceTags = Get-TargetSteamTags $LocalTags $VersionTags $ReleaseConfig
    if ($TargetPlatform -eq "ModIO") {
        $sourceTags = @($sourceTags | ForEach-Object { Convert-ToModIoTag $_ })
    }
    return Get-UniqueTags $sourceTags
}

function Update-LocalSteamTags([object] $WorkshopData, [string[]] $TargetTags) {
    $WorkshopData.Tags = @($TargetTags)
}

function Assert-SteamTagsValid([string[]] $Tags) {
    foreach ($tag in $Tags) {
        if ($tag.Length -gt 255) {
            throw "Steam tag is longer than 255 characters: $tag"
        }
        if ($tag.Contains(",")) {
            throw "Steam tag cannot contain comma: $tag"
        }
        if ($tag -match "\p{Cc}") {
            throw "Steam tag contains control characters: $tag"
        }
    }
}

function Get-SteamDetails([string] $PublishedFileId) {
    $body = "itemcount=1&publishedfileids%5B0%5D=$PublishedFileId"
    $response = Invoke-RestMethod `
        -Uri "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/" `
        -Method Post `
        -Body $body `
        -ContentType "application/x-www-form-urlencoded"
    $item = $response.response.publishedfiledetails[0]
    if ($item.result -ne 1) {
        throw "Steam API failed for $PublishedFileId with result $($item.result)."
    }
    return $item
}

function Resolve-SteamPublisherKeyPath([string] $Path) {
    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        return Resolve-RepoPath $Path
    }
    return Resolve-RepoPath ".tools/steam/publisher.key.txt"
}

function Read-SteamPublisherKey() {
    $path = Resolve-SteamPublisherKeyPath $SteamPublisherKeyPath
    Assert-PathExists $path "Steam publisher API key"
    $key = (Get-Content -Raw -LiteralPath $path).Trim()
    if ([string]::IsNullOrWhiteSpace($key)) {
        throw "Steam publisher API key is empty: $path"
    }
    return $key
}

function Invoke-SteamTagsUpdate(
    [string] $PublishedFileId,
    [string] $AppId,
    [string[]] $TargetTags) {
    $projectPath = Resolve-RepoPath "tools/SteamTagUpdater/SteamTagUpdater.csproj"
    Assert-PathExists $projectPath "Steam tag updater project"
    & dotnet build $projectPath -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Steam tag updater build failed with exit code $LASTEXITCODE."
    }

    $exePath = Resolve-RepoPath "tools/SteamTagUpdater/bin/Release/net8.0/SteamTagUpdater.exe"
    Assert-PathExists $exePath "Steam tag updater executable"
    $arguments = @($PublishedFileId) + $TargetTags
    Push-Location (Split-Path -Parent $exePath)
    try {
        & $exePath @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Steam tag updater failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function ConvertTo-VdfString([string] $Value) {
    return $Value.Replace("\", "\\").Replace('"', '\"')
}

function Write-SteamTagsVdf(
    [string] $Path,
    [string] $AppId,
    [string] $PublishedFileId,
    [string] $Title,
    [string[]] $Tags) {
    $tagLines = $Tags | ForEach-Object {
        "        `"$(ConvertTo-VdfString $_)`""
    }
    $vdf = @"
"workshopitem"
{
    "appid" "$AppId"
    "publishedfileid" "$PublishedFileId"
    "title" "$(ConvertTo-VdfString $Title)"
    "tags"
    {
$($tagLines -join "`r`n")
    }
}
"@
    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    Set-Content -LiteralPath $Path -Value $vdf -Encoding UTF8
}

function Read-SteamConfig([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        $Path = Join-Path $repoRoot ".tools/steam/steam.local.json"
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

    $commonPaths = @(
        "C:\steamcmd\steamcmd.exe",
        "C:\SteamCMD\steamcmd.exe",
        "C:\Program Files (x86)\Steam\steamcmd.exe",
        "C:\Program Files\Steam\steamcmd.exe",
        (Join-Path $repoRoot ".tools/steamcmd/steamcmd.exe")
    )
    foreach ($path in $commonPaths) {
        if (Test-Path -LiteralPath $path) {
            return $path
        }
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

function Invoke-SteamTagsUpdateWithSteamCmd([string] $VdfPath) {
    $loginSettings = Get-SteamLoginSettings
    & $loginSettings.SteamCmdPath +login $loginSettings.UserName +workshop_build_item $VdfPath +quit
    if ($LASTEXITCODE -ne 0) {
        throw "SteamCMD failed with exit code $LASTEXITCODE."
    }
}

function Resolve-ModIoConfigPath([string] $Path) {
    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        return Resolve-RepoPath $Path
    }
    return Resolve-RepoPath ".tools/modio/$ModName.local.json"
}

function Resolve-ModIoAccessTokenPath([string] $Path) {
    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        return Resolve-RepoPath $Path
    }
    return Resolve-RepoPath ".tools/modio/$ModName.token.txt"
}

function Get-ModIoHeaders([string] $AccessToken) {
    return @{
        Authorization = "Bearer $AccessToken"
        Accept = "application/json"
    }
}

function Get-ModIoGame([object] $Config, [string] $AccessToken) {
    $endpoint = "$($Config.ApiBase.TrimEnd("/"))/games/$($Config.GameId)"
    return Invoke-RestMethod -Uri $endpoint -Headers (Get-ModIoHeaders $AccessToken)
}

function Get-ModIoTags([object] $Config, [string] $AccessToken) {
    $endpoint = "$($Config.ApiBase.TrimEnd("/"))/games/$($Config.GameId)/mods/$($Config.ModId)/tags"
    $response = Invoke-RestMethod -Uri $endpoint -Headers (Get-ModIoHeaders $AccessToken)
    $items = $response
    if ($null -ne $response.data) {
        $items = $response.data
    }
    return @($items | ForEach-Object {
        if ($null -ne $_.name) {
            [string]$_.name
        }
        elseif ($null -ne $_.tag) {
            [string]$_.tag
        }
    })
}

function Invoke-ModIoTagsRequest(
    [object] $Config,
    [string] $AccessToken,
    [string] $Method,
    [string[]] $Tags) {
    if ($Tags.Count -eq 0) {
        return
    }

    Add-Type -AssemblyName System.Net.Http

    $endpoint = "$($Config.ApiBase.TrimEnd("/"))/games/$($Config.GameId)/mods/$($Config.ModId)/tags"
    $encodedTags = @($Tags | ForEach-Object {
        "tags[]=$([System.Net.WebUtility]::UrlEncode($_))"
    })
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $endpoint)
    $request.Headers.Authorization =
        [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $AccessToken)
    $request.Headers.Accept.ParseAdd("application/json")
    $requestBody = [System.Text.Encoding]::UTF8.GetBytes($encodedTags -join "&")
    $request.Content = [System.Net.Http.ByteArrayContent]::new($requestBody)
    $request.Content.Headers.ContentType =
        [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/x-www-form-urlencoded")

    $client = [System.Net.Http.HttpClient]::new()
    try {
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw "Mod.IO tags $Method failed: HTTP $([int]$response.StatusCode) $($response.ReasonPhrase)`n$responseBody"
        }
    }
    finally {
        $request.Dispose()
        $client.Dispose()
    }
}

function Assert-ModIoTagsSupported([string[]] $TargetTags, [object] $Game) {
    $supportedTags = @($Game.tag_options | ForEach-Object { $_.tags } | ForEach-Object { [string]$_ })
    $supported = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($tag in $supportedTags) {
        [void]$supported.Add($tag)
    }

    $unsupported = @($TargetTags | Where-Object { -not $supported.Contains($_) })
    return [pscustomobject]@{
        SupportedTags = Get-UniqueTags $supportedTags
        UnsupportedTags = $unsupported
    }
}

$localModRootPath = Get-LocalModRootPath
Assert-PathExists $localModRootPath "Local mod root"

$workshopDataInfo = Get-LocalWorkshopData $localModRootPath
$workshopData = $workshopDataInfo.Data
$localTags = Get-UniqueTags @($workshopData.Tags)
$versionInfo = Get-VersionCompatibilityTags $localModRootPath
$releaseConfigPath = Join-Path (Join-Path $repoRoot $ModName) "release.json"
$releaseConfig = $null
if (Test-Path -LiteralPath $releaseConfigPath) {
    $releaseConfig = Get-Content -Raw -LiteralPath $releaseConfigPath | ConvertFrom-Json
}
$additionalCompatibilityTags = Get-AdditionalCompatibilityTags $releaseConfig

Write-Host "Platform tag synchronization plan for $ModName"
Write-Host "Local mod root: $localModRootPath"
Write-Host "Local workshop data: $($workshopDataInfo.Path)"
Write-Host "Version folders: $(Format-Tags $versionInfo.VersionFolders)"
Write-Host "Compatibility tags from version folders: $(Format-Tags $versionInfo.Tags)"
Write-Host "Additional compatibility tags: $(Format-Tags $additionalCompatibilityTags)"
Write-Host "Local tags from workshop_data.json: $(Format-Tags $localTags)"
Write-Host ""

if ($Platform -eq "All" -or $Platform -eq "Steam") {
    $appId = "1062090"
    if ($null -ne $releaseConfig -and -not [string]::IsNullOrWhiteSpace($releaseConfig.Steam.AppId)) {
        $appId = [string]$releaseConfig.Steam.AppId
    }

    $publishedFileId = [string]$workshopData.ItemId
    if ($null -ne $releaseConfig -and -not [string]::IsNullOrWhiteSpace($releaseConfig.Steam.PublishedFileId)) {
        $publishedFileId = [string]$releaseConfig.Steam.PublishedFileId
    }
    if ([string]::IsNullOrWhiteSpace($publishedFileId)) {
        throw "Steam PublishedFileId is empty."
    }

    $targetTags = Get-TargetTags $localTags $versionInfo.Tags "Steam" $releaseConfig
    Assert-SteamTagsValid $targetTags
    $vdfPath = Join-Path (Resolve-RepoPath $SteamVdfRoot) "$ModName-tags.vdf"
    Write-SteamTagsVdf $vdfPath $appId $publishedFileId ([string]$workshopData.Name) $targetTags

    $steamDetails = Get-SteamDetails $publishedFileId
    $currentTags = Get-UniqueTags @($steamDetails.tags | ForEach-Object { [string]$_.tag })
    $addTags = Get-AddedTags $targetTags $currentTags
    $removeTags = Get-RemovedTags $targetTags $currentTags
    $alreadySynced = $addTags.Count -eq 0 -and $removeTags.Count -eq 0
    $localAddTags = Get-AddedTags $targetTags $localTags
    $localRemoveTags = Get-RemovedTags $targetTags $localTags
    $localAlreadySynced = $localAddTags.Count -eq 0 -and $localRemoveTags.Count -eq 0

    Write-Host "Steam"
    Write-Host "  PublishedFileId: $publishedFileId"
    Write-Host "  Updater: tools/SteamTagUpdater"
    Write-Host "  VDF preview: $vdfPath"
    Write-Host "  Supported tags: not exposed by public Steam API; validating tag syntax only"
    Write-Host "  Current tags: $(Format-Tags $currentTags)"
    Write-Host "  Target tags: $(Format-Tags $targetTags)"
    Write-Host "  Add: $(Format-Tags $addTags)"
    Write-Host "  Remove: $(Format-Tags $removeTags)"
    Write-Host "  Already synchronized: $alreadySynced"
    if ($Publish) {
        if ($localAlreadySynced) {
            Write-Host "  Local workshop_data.json already matches target Steam tags."
        }
        else {
            Update-LocalSteamTags $workshopData $targetTags
            Save-LocalWorkshopData $workshopDataInfo.Path $workshopData
            Write-Host "  Local workshop_data.json was updated for Steam."
        }
        if (-not $alreadySynced) {
            Invoke-SteamTagsUpdate $publishedFileId $appId $targetTags
            Start-Sleep -Seconds 2
            $updatedDetails = Get-SteamDetails $publishedFileId
            $updatedTags = Get-UniqueTags @($updatedDetails.tags | ForEach-Object { [string]$_.tag })
            $remainingAdds = Get-AddedTags $targetTags $updatedTags
            $remainingRemoves = Get-RemovedTags $targetTags $updatedTags
            if ($remainingAdds.Count -ne 0 -or $remainingRemoves.Count -ne 0) {
                throw "Steam tags update completed, but live tags do not match target. Live: $(Format-Tags $updatedTags)"
            }
            Write-Host "  Steam tags are synchronized."
        }
    }
    Write-Host ""
}

if ($Platform -eq "All" -or $Platform -eq "ModIO") {
    $configPath = Resolve-ModIoConfigPath $ModIoConfigPath
    $accessTokenPath = Resolve-ModIoAccessTokenPath $ModIoAccessTokenPath
    Assert-PathExists $configPath "Mod.IO config"
    Assert-PathExists $accessTokenPath "Mod.IO access token"

    $config = Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json
    $accessToken = (Get-Content -Raw -LiteralPath $accessTokenPath).Trim()
    if ([string]::IsNullOrWhiteSpace($accessToken)) {
        throw "Mod.IO access token is empty: $accessTokenPath"
    }

    $targetTags = Get-TargetTags $localTags $versionInfo.Tags "ModIO" $releaseConfig
    $game = Get-ModIoGame $config $accessToken
    $support = Assert-ModIoTagsSupported $targetTags $game
    $currentTags = Get-UniqueTags (Get-ModIoTags $config $accessToken)
    $addTags = Get-AddedTags $targetTags $currentTags
    $removeTags = Get-RemovedTags $targetTags $currentTags
    $alreadySynced = $addTags.Count -eq 0 -and $removeTags.Count -eq 0

    Write-Host "Mod.IO"
    Write-Host "  Config: $configPath"
    Write-Host "  Endpoint: $($config.ApiBase.TrimEnd("/"))/games/$($config.GameId)/mods/$($config.ModId)/tags"
    Write-Host "  Supported tags: $(Format-Tags $support.SupportedTags)"
    Write-Host "  Unsupported target tags: $(Format-Tags $support.UnsupportedTags)"
    Write-Host "  Current tags: $(Format-Tags $currentTags)"
    Write-Host "  Target tags: $(Format-Tags $targetTags)"
    Write-Host "  Add: $(Format-Tags $addTags)"
    Write-Host "  Remove: $(Format-Tags $removeTags)"
    Write-Host "  Already synchronized: $alreadySynced"

    if ($support.UnsupportedTags.Count -ne 0) {
        throw "Cannot synchronize Mod.IO tags until unsupported target tags are removed or mapped: $(Format-Tags $support.UnsupportedTags)"
    }

    if ($Publish -and -not $alreadySynced) {
        Invoke-ModIoTagsRequest $config $accessToken "POST" $addTags
        Invoke-ModIoTagsRequest $config $accessToken "DELETE" $removeTags
        Start-Sleep -Seconds 2
        $updatedTags = Get-UniqueTags (Get-ModIoTags $config $accessToken)
        $remainingAdds = Get-AddedTags $targetTags $updatedTags
        $remainingRemoves = Get-RemovedTags $targetTags $updatedTags
        if ($remainingAdds.Count -ne 0 -or $remainingRemoves.Count -ne 0) {
            throw "Mod.IO tags update completed, but live tags do not match target. Live: $(Format-Tags $updatedTags)"
        }
        Write-Host "  Mod.IO tags are synchronized."
    }
    Write-Host ""
}

if (-not $Publish) {
    Write-Host "Dry run only. Nothing was uploaded. Use -Publish only after an explicit tag update request."
}
