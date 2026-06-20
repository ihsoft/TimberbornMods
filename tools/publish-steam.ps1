param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $Configuration = "Release",
    [string] $GameVersion = "version-1.1",
    [string] $OutputRoot = ".tools/release-preview",
    [string] $LocalModRoot = "",
    [string] $SteamStagingRoot = ".tools/steam-staging",
    [string] $VdfRoot = ".tools/steam",
    [string] $SteamConfigPath = "",
    [string] $SteamCmdPath = "",
    [string] $SteamUserName = "",
    [string] $ChangeNotesPrefix = "",
    [switch] $IncludeLegacyVersions,
    [switch] $SkipBuild,
    [switch] $LoginOnly,
    [switch] $UpdateVisibility,
    [switch] $Publish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$modRoot = Join-Path $repoRoot $ModName
$releaseConfigPath = Join-Path $modRoot "release.json"
$buildScriptPath = Join-Path $PSScriptRoot "build-mod-package.ps1"

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

function Get-LatestChangeNotes([string] $Path, [string] $Version) {
    $content = Get-Content -Raw -LiteralPath $Path
    $escapedVersion = [regex]::Escape($Version)
    $pattern = "(?ms)^#\s+v$escapedVersion[^\r\n]*\r?\n(?<body>.*?)(?=^#\s+v|\z)"
    $match = [regex]::Match($content, $pattern)
    if (-not $match.Success) {
        throw "Cannot find changelog section for v$Version"
    }

    $body = $match.Groups["body"].Value.Trim()
    if ([string]::IsNullOrWhiteSpace($body)) {
        throw "Changelog section for v$Version is empty"
    }
    return $body
}

function Test-ZipPackage(
    [string] $ZipPath,
    [string] $ScriptFileBase,
    [object] $ReleaseConfig) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $versionFolders = @($zip.Entries | ForEach-Object {
            if ($_.FullName -match "^[^/]+/(version-\d+\.\d+)/") {
                $matches[1]
            }
        } | Sort-Object -Unique)
        if ($versionFolders.Count -eq 0) {
            throw "Package must contain at least one version-X.X folder."
        }

        foreach ($versionFolder in $versionFolders) {
            $escapedVersionFolder = [regex]::Escape($versionFolder)
            $manifestEntry = $zip.Entries | Where-Object {
                $_.FullName -match "^[^/]+/$escapedVersionFolder/manifest\.json$"
            } | Select-Object -First 1
            if ($null -eq $manifestEntry) {
                throw "Package folder $versionFolder must contain manifest.json."
            }

            $dllEntry = $zip.Entries | Where-Object {
                $_.FullName -match "^[^/]+/$escapedVersionFolder/Scripts/$([regex]::Escape($ScriptFileBase))\.dll$"
            } | Select-Object -First 1
            if ($null -eq $dllEntry) {
                throw "Package folder $versionFolder must contain Scripts/$ScriptFileBase.dll."
            }

            $allowMissingXmlFolders = @($ReleaseConfig.Package.AllowMissingXmlFolders)
            $xmlEntry = $zip.Entries | Where-Object {
                $_.FullName -match "^[^/]+/$escapedVersionFolder/Scripts/$([regex]::Escape($ScriptFileBase))\.xml$"
            } | Select-Object -First 1
            if ($null -eq $xmlEntry -and $versionFolder -notin $allowMissingXmlFolders) {
                throw "Package folder $versionFolder must contain Scripts/$ScriptFileBase.xml."
            }

            $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
            try {
                $manifest = $reader.ReadToEnd() | ConvertFrom-Json
            }
            finally {
                $reader.Dispose()
            }

            $manifestVersion = [string]$manifest.Version
            if ([string]::IsNullOrWhiteSpace($manifestVersion)) {
                throw "Package folder $versionFolder has an empty manifest Version."
            }

            $expectedVersion = $ReleaseConfig.ManifestVersions.$versionFolder
            if ($null -ne $expectedVersion -and $manifestVersion -ne [string]$expectedVersion) {
                throw "Package folder $versionFolder has manifest Version=$manifestVersion, expected $expectedVersion."
            }
        }

        return $versionFolders
    }
    finally {
        $zip.Dispose()
    }
}

function Assert-PackageFreshEnough([string] $ZipPath, [object] $PackageConfig) {
    $maxAgeDays = $PackageConfig.MaxAgeDays
    if ($null -eq $maxAgeDays) {
        return
    }

    $lastWriteTime = (Get-Item -LiteralPath $ZipPath).LastWriteTime
    $age = (Get-Date) - $lastWriteTime
    if ($age.TotalDays -gt [double]$maxAgeDays) {
        throw "Package is too old: $ZipPath. LastWriteTime=$lastWriteTime, MaxAgeDays=$maxAgeDays."
    }
}

function Get-VersionFoldersFromDirectory([string] $PackageRoot) {
    return @(Get-ChildItem -LiteralPath $PackageRoot -Directory | Where-Object {
        $_.Name -match "^version-\d+\.\d+$"
    } | Select-Object -ExpandProperty Name | Sort-Object -Unique)
}

function New-ZipFromDirectory([string] $SourceRoot, [string] $ZipPath) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $zipDirectory = Split-Path -Parent $ZipPath
    New-Item -ItemType Directory -Path $zipDirectory -Force | Out-Null
    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }

    $zip = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $sourceRootFullName = (Resolve-Path -LiteralPath $SourceRoot).Path.TrimEnd("\") + "\"
        Get-ChildItem -LiteralPath $SourceRoot -Recurse -File | ForEach-Object {
            $entryName = $_.FullName.Substring($sourceRootFullName.Length).Replace("\", "/")
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip,
                $_.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Expand-PackageForSteam([string] $ZipPath, [string] $DestinationRoot) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $DestinationRoot) {
        Remove-Item -LiteralPath $DestinationRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $DestinationRoot | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $DestinationRoot)

    $roots = @(Get-ChildItem -LiteralPath $DestinationRoot -Directory)
    if ($roots.Count -ne 1) {
        throw "Steam staging must contain exactly one root folder after extraction: $DestinationRoot"
    }
    return $roots[0].FullName
}

function ConvertTo-VdfString([string] $Value) {
    return $Value.Replace("\", "\\").Replace('"', '\"')
}

function ConvertTo-SteamChangeNote([string] $Version, [string] $ChangeNotes) {
    $lines = $ChangeNotes.Trim() -split "\r?\n"
    $convertedLines = $lines | ForEach-Object {
        if ($_ -match "^\*\s+(?<text>.+)$") {
            "[*] " + $matches["text"]
        }
        else {
            $_
        }
    }
    return "[h3]v$Version[/h3]`r`n" + (($convertedLines -join "`r`n").Trim())
}

function Write-WorkshopVdf(
    [string] $Path,
    [string] $AppId,
    [string] $PublishedFileId,
    [string] $ContentFolder,
    [string] $PreviewFile,
    [string] $Visibility,
    [string] $Title,
    [string[]] $Tags,
    [string] $ChangeNote) {
    $tagLines = $Tags | ForEach-Object {
        "        `"$_`""
    }
    $visibilityLine = ""
    if (-not [string]::IsNullOrWhiteSpace($Visibility)) {
        $visibilityLine = "    `"visibility`" `"$Visibility`"`r`n"
    }
    $vdf = @"
"workshopitem"
{
    "appid" "$AppId"
    "publishedfileid" "$PublishedFileId"
    "contentfolder" "$(ConvertTo-VdfString $ContentFolder)"
    "previewfile" "$(ConvertTo-VdfString $PreviewFile)"
$visibilityLine    "title" "$(ConvertTo-VdfString $Title)"
    "changenote" "$(ConvertTo-VdfString $ChangeNote)"
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

function Invoke-SteamWorkshopUpload([string] $SteamCmd, [string] $UserName, [string] $VdfPath) {
    if ([string]::IsNullOrWhiteSpace($SteamCmd)) {
        throw "Cannot publish: steamcmd.exe was not found. Set SteamCmdPath in .tools/steam/steam.local.json."
    }
    if ([string]::IsNullOrWhiteSpace($UserName)) {
        throw "Cannot publish: SteamUserName is empty. Set UserName in .tools/steam/steam.local.json."
    }

    & $SteamCmd +login $UserName +workshop_build_item $VdfPath +quit
    if ($LASTEXITCODE -ne 0) {
        throw "SteamCMD failed with exit code $LASTEXITCODE."
    }
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

function Start-SteamInteractiveLogin([string] $SteamCmd, [string] $UserName) {
    Start-Process -FilePath $SteamCmd -ArgumentList @("+login", $UserName) -WorkingDirectory (Split-Path -Parent $SteamCmd)
}

Assert-PathExists $releaseConfigPath "Release config"
Assert-PathExists $buildScriptPath "Package builder"

if ($LoginOnly) {
    $loginSettings = Get-SteamLoginSettings
    Start-SteamInteractiveLogin $loginSettings.SteamCmdPath $loginSettings.UserName
    Write-Host "Started SteamCMD login window for $($loginSettings.UserName)."
    Write-Host "Approve Steam Guard if prompted, then type quit in the SteamCMD window."
    Write-Host "You can rerun this command if the mobile confirmation expires."
    exit 0
}

$releaseConfig = Get-Content -Raw -LiteralPath $releaseConfigPath | ConvertFrom-Json
$manifestPath = Join-Path $modRoot "Mod/manifest.json"
if (-not [string]::IsNullOrWhiteSpace($releaseConfig.ManifestPath)) {
    $manifestPath = Resolve-RepoPath ([string]$releaseConfig.ManifestPath)
}
$changesPath = Join-Path $modRoot "CHANGES.md"
if (-not [string]::IsNullOrWhiteSpace($releaseConfig.ChangesPath)) {
    $changesPath = Resolve-RepoPath ([string]$releaseConfig.ChangesPath)
}

Assert-PathExists $manifestPath "Manifest"
Assert-PathExists $changesPath "Changes file"

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$modVersion = [string]$manifest.Version
if (-not [string]::IsNullOrWhiteSpace($releaseConfig.ReleaseVersion)) {
    $modVersion = [string]$releaseConfig.ReleaseVersion
}
if ([string]::IsNullOrWhiteSpace($modVersion)) {
    throw "Release version is empty."
}

$packageMode = "Build"
if (-not [string]::IsNullOrWhiteSpace($releaseConfig.Package.Mode)) {
    $packageMode = [string]$releaseConfig.Package.Mode
}

if ($packageMode -eq "Build") {
    $buildArguments = @{
        ModName = $ModName
        Configuration = $Configuration
        GameVersion = $GameVersion
        OutputRoot = $OutputRoot
        Force = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($LocalModRoot)) {
        $buildArguments.LocalModRoot = $LocalModRoot
    }
    if ($IncludeLegacyVersions) {
        $buildArguments.IncludeLegacyVersions = $true
    }
    if ($SkipBuild) {
        $buildArguments.SkipBuild = $true
    }

    & $buildScriptPath @buildArguments
    $zipPath = Join-Path (Resolve-RepoPath $OutputRoot) "$($ModName)_v$modVersion.zip"
}
elseif ($packageMode -eq "ExistingZip") {
    if ([string]::IsNullOrWhiteSpace($releaseConfig.Package.Path)) {
        throw "ExistingZip package mode requires Package.Path in $releaseConfigPath"
    }
    $zipPath = Resolve-RepoPath ([string]$releaseConfig.Package.Path)
}
elseif ($packageMode -eq "LocalModFolder") {
    if ([string]::IsNullOrWhiteSpace($releaseConfig.Package.SourcePath)) {
        throw "LocalModFolder package mode requires Package.SourcePath in $releaseConfigPath"
    }
    if ([string]::IsNullOrWhiteSpace($releaseConfig.Package.OutputPath)) {
        throw "LocalModFolder package mode requires Package.OutputPath in $releaseConfigPath"
    }

    $sourcePath = Resolve-RepoPath ([string]$releaseConfig.Package.SourcePath)
    Assert-PathExists $sourcePath "Local mod folder"

    $packageStage = Resolve-RepoPath ".tools/release-staging/$ModName-local"
    if (Test-Path -LiteralPath $packageStage) {
        Remove-Item -LiteralPath $packageStage -Recurse -Force
    }
    New-Item -ItemType Directory -Path $packageStage | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $packageStage -Recurse -Force
    $packageRoot = Join-Path $packageStage (Split-Path -Leaf $sourcePath)

    $zipPath = Resolve-RepoPath ([string]$releaseConfig.Package.OutputPath)
    New-ZipFromDirectory $packageStage $zipPath
    Write-Host "Built package from local mod folder: $zipPath"
}
else {
    throw "Unsupported package mode: $packageMode"
}

Assert-PathExists $zipPath "Release package"
Assert-PackageFreshEnough $zipPath $releaseConfig.Package

$scriptFileBase = $ModName
if (-not [string]::IsNullOrWhiteSpace($releaseConfig.ScriptFileBase)) {
    $scriptFileBase = [string]$releaseConfig.ScriptFileBase
}
$versionFolders = Test-ZipPackage $zipPath $scriptFileBase $releaseConfig

$allowSingleGameVersion = $releaseConfig.Steam.AllowSingleGameVersion -eq $true
if ($versionFolders.Count -lt 2 -and -not $allowSingleGameVersion) {
    throw "Steam package contains only $($versionFolders -join ', '). Add Steam.AllowSingleGameVersion with a reason or ask before publishing."
}
if ($allowSingleGameVersion -and [string]::IsNullOrWhiteSpace($releaseConfig.Steam.CompatibilityReason)) {
    throw "Steam.AllowSingleGameVersion requires Steam.CompatibilityReason."
}

$localModRootPath = $LocalModRoot
if ([string]::IsNullOrWhiteSpace($localModRootPath)) {
    $localModRootPath = Join-Path "_MODS!" $ModName
}
$localModRootPath = Resolve-RepoPath $localModRootPath
$workshopDataPath = Join-Path $localModRootPath "workshop_data.json"
Assert-PathExists $workshopDataPath "Steam workshop data"
$workshopData = Get-Content -Raw -LiteralPath $workshopDataPath | ConvertFrom-Json

$appId = "1062090"
if (-not [string]::IsNullOrWhiteSpace($releaseConfig.Steam.AppId)) {
    $appId = [string]$releaseConfig.Steam.AppId
}
$publishedFileId = [string]$workshopData.ItemId
if (-not [string]::IsNullOrWhiteSpace($releaseConfig.Steam.PublishedFileId)) {
    $publishedFileId = [string]$releaseConfig.Steam.PublishedFileId
}
if ([string]::IsNullOrWhiteSpace($publishedFileId)) {
    throw "Steam PublishedFileId is empty."
}

$visibilityMap = @{
    "Public" = "0"
    "FriendsOnly" = "1"
    "Private" = "2"
    "Unlisted" = "3"
}
$visibilityName = [string]$workshopData.Visibility
if (-not [string]::IsNullOrWhiteSpace($releaseConfig.Steam.Visibility)) {
    $visibilityName = [string]$releaseConfig.Steam.Visibility
}
if (-not $visibilityMap.ContainsKey($visibilityName)) {
    throw "Unsupported Steam visibility: $visibilityName"
}
$configuredVisibilityUpdate = $workshopData.UpdateVisibility -eq $true -or $releaseConfig.Steam.UpdateVisibility -eq $true
if ($configuredVisibilityUpdate -and -not $UpdateVisibility) {
    throw "Steam visibility update is configured, but -UpdateVisibility was not passed. Do not change visibility unless the user explicitly asks."
}
$visibilityValue = ""
if ($UpdateVisibility) {
    $visibilityValue = $visibilityMap[$visibilityName]
}

$changeNotes = Get-LatestChangeNotes $changesPath $modVersion
if (-not [string]::IsNullOrWhiteSpace($ChangeNotesPrefix)) {
    $changeNotes = $ChangeNotesPrefix.Trim() + "`r`n" + $changeNotes.TrimStart()
}
$steamChangeNote = ConvertTo-SteamChangeNote $modVersion $changeNotes

$stagingRootPath = Resolve-RepoPath $SteamStagingRoot
$contentFolder = Expand-PackageForSteam $zipPath (Join-Path $stagingRootPath $ModName)
$previewFile = Join-Path $contentFolder "thumbnail.jpg"
if ($workshopData.UpdatePreview -eq $false) {
    $previewFile = ""
}
elseif (-not (Test-Path -LiteralPath $previewFile)) {
    throw "Steam preview file not found: $previewFile"
}

$vdfPath = Join-Path (Resolve-RepoPath $VdfRoot) "$ModName.vdf"
$tags = @($workshopData.Tags)
Write-WorkshopVdf $vdfPath $appId $publishedFileId $contentFolder $previewFile $visibilityValue `
    ([string]$workshopData.Name) $tags $steamChangeNote

Write-Host ""
Write-Host "Steam publish plan for $ModName v$modVersion"
Write-Host "Package: $zipPath"
Write-Host "Content folder: $contentFolder"
Write-Host "VDF: $vdfPath"
Write-Host "AppId: $appId"
Write-Host "PublishedFileId: $publishedFileId"
if ($UpdateVisibility) {
    Write-Host "Visibility update: $visibilityName"
}
else {
    Write-Host "Visibility update: unchanged"
}
Write-Host "Game version folders: $($versionFolders -join ', ')"
if ($releaseConfig.ReadyForPublish -eq $false) {
    Write-Host "Ready for publish: false"
}
Write-Host "Tags: $($tags -join ', ')"
if ($allowSingleGameVersion) {
    Write-Host "Single game-version override: $($releaseConfig.Steam.CompatibilityReason)"
}
Write-Host ""
Write-Host "Change note:"
Write-Host $steamChangeNote
Write-Host ""

if ($Publish) {
    if ($releaseConfig.ReadyForPublish -eq $false) {
        throw "Cannot publish: $ModName release config is marked ReadyForPublish=false."
    }
    $loginSettings = Get-SteamLoginSettings
    Write-Host "Starting SteamCMD..."
    Invoke-SteamWorkshopUpload $loginSettings.SteamCmdPath $loginSettings.UserName $vdfPath
    Write-Host "SteamCMD upload completed."
    exit 0
}

Write-Host "Dry run only. SteamCMD was not started."
