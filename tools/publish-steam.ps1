param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $Configuration = "Release",
    [string] $GameVersion = "version-1.1",
    [string] $GameRoot = "_GAME!",
    [string] $OutputRoot = ".tools/release-preview",
    [string] $LocalModRoot = "",
    [string] $SteamStagingRoot = ".tools/steam-staging",
    [string] $VdfRoot = ".tools/steam",
    [string] $SteamConfigPath = "",
    [string] $SteamCmdPath = "",
    [string] $SteamUserName = "",
    [string] $ChangeNotesPrefix = "",
    [string] $ExpectedPackageSha256 = "",
    [switch] $CorrectiveReplacement,
    [switch] $IncludeLegacyVersions,
    [switch] $SkipBuild,
    [switch] $SkipUnityExport,
    [switch] $LoginOnly,
    [switch] $UpdateVisibility,
    [switch] $Publish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$modRoot = Join-Path $repoRoot $ModName
$releaseConfigPath = Join-Path $modRoot "release.json"
$buildScriptPath = Join-Path $PSScriptRoot "build-mod-package.ps1"
$unityExportScriptPath = Join-Path $PSScriptRoot "export-unity-mod.ps1"

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

function Invoke-UnityExport([string] $Name, [string] $VersionFolder) {
    Assert-PathExists $unityExportScriptPath "Unity export script"
    & $unityExportScriptPath -ModName $Name -GameVersion $VersionFolder
}

function Assert-RepositoryReleaseVersions(
    [object] $Manifest,
    [string] $ManifestPath,
    [string] $Version) {
    $manifestVersion = [string]$Manifest.Version
    if ($manifestVersion -ne $Version) {
        throw "Manifest version mismatch before Unity export. Manifest=$manifestVersion, Release=$Version. Update $ManifestPath first."
    }

    $propsPath = Join-Path $modRoot "directory.build.props"
    Assert-PathExists $propsPath "Directory build props"
    $props = [xml](Get-Content -Raw -LiteralPath $propsPath)
    $versions = @($props.Project.PropertyGroup | ForEach-Object {
        [string]$_.Version
    } | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    })
    if ($versions.Count -eq 0) {
        throw "Version is missing in $propsPath"
    }

    $projectVersion = $versions[0]
    if ($projectVersion -ne $Version) {
        throw "Project version mismatch before build. directory.build.props=$projectVersion, Release=$Version. Update $propsPath first."
    }
}

function Get-InstalledGameVersionInfo([string] $RootPath) {
    $gameRootPath = Resolve-RepoPath $RootPath
    $versionNumbersPath = Join-Path $gameRootPath "Timberborn_Data/StreamingAssets/VersionNumbers.json"
    Assert-PathExists $versionNumbersPath "Timberborn version numbers"
    $versionNumbers = Get-Content -Raw -LiteralPath $versionNumbersPath | ConvertFrom-Json
    $currentVersion = [string]$versionNumbers.CurrentVersion
    if ([string]::IsNullOrWhiteSpace($currentVersion)) {
        throw "CurrentVersion is empty in $versionNumbersPath"
    }

    $versionTextPath = Join-Path $gameRootPath "Timberborn_Data/StreamingAssets/Version.txt"
    $versionText = ""
    if (Test-Path -LiteralPath $versionTextPath) {
        $versionText = (Get-Content -Raw -LiteralPath $versionTextPath).Trim()
    }

    return [pscustomobject]@{
        CurrentVersion = $currentVersion
        VersionText = $versionText
        VersionNumbersPath = $versionNumbersPath
    }
}

function Assert-InstalledGameVersionCompatible(
    [object] $ReleaseConfig,
    [object] $Manifest,
    [string] $VersionFolder,
    [string] $RootPath) {
    $compatibility = $ReleaseConfig.GameVersionCompatibility.$VersionFolder
    if ($null -eq $compatibility) {
        throw "No game-version compatibility mapping for $VersionFolder in $releaseConfigPath"
    }

    $gameVersionInfo = Get-InstalledGameVersionInfo $RootPath
    $currentVersion = [version]$gameVersionInfo.CurrentVersion
    $minimumVersion = [version]([string]$compatibility.MinimumGameVersion)
    $maximumVersion = [version]([string]$compatibility.MaximumGameVersion)
    if ($currentVersion -lt $minimumVersion -or $currentVersion -gt $maximumVersion) {
        throw "Installed Timberborn $($gameVersionInfo.CurrentVersion) is outside $VersionFolder compatibility range $minimumVersion..$maximumVersion."
    }

    $manifestMinimumGameVersion = [string]$Manifest.MinimumGameVersion
    if (-not [string]::IsNullOrWhiteSpace($manifestMinimumGameVersion) -and
            $currentVersion -lt [version]$manifestMinimumGameVersion) {
        throw "Installed Timberborn $($gameVersionInfo.CurrentVersion) is older than manifest MinimumGameVersion=$manifestMinimumGameVersion."
    }

    if ([string]::IsNullOrWhiteSpace($gameVersionInfo.VersionText)) {
        Write-Host "Installed Timberborn version: $($gameVersionInfo.CurrentVersion)"
    }
    else {
        Write-Host "Installed Timberborn version: $($gameVersionInfo.CurrentVersion) ($($gameVersionInfo.VersionText))"
    }
}

function Assert-UnityExportedVersionFolder(
    [string] $SourcePath,
    [string] $VersionFolder,
    [string] $ExpectedManifestVersion) {
    $versionPath = Join-Path $SourcePath $VersionFolder
    Assert-PathExists $versionPath "Unity-exported version folder"
    $exportedManifestPath = Join-Path $versionPath "manifest.json"
    Assert-PathExists $exportedManifestPath "Unity-exported manifest"
    $exportedManifest = Get-Content -Raw -LiteralPath $exportedManifestPath | ConvertFrom-Json
    $exportedManifestVersion = [string]$exportedManifest.Version
    if ($exportedManifestVersion -ne $ExpectedManifestVersion) {
        throw "Unity-exported manifest version mismatch. Manifest=$exportedManifestVersion, expected $ExpectedManifestVersion at $exportedManifestPath"
    }
}

function Build-LocalModScripts(
    [string] $Name,
    [string] $ConfigurationName,
    [string] $SourcePath,
    [string] $VersionFolder,
    [string] $ScriptFileBase,
    [string] $ExpectedVersion) {
    $projectPath = Join-Path (Join-Path $repoRoot $Name) "$Name.csproj"
    Assert-PathExists $projectPath "Project file"
    $modPath = Split-Path -Parent $SourcePath
    & dotnet build $projectPath -c $ConfigurationName "/p:ModPath=$modPath"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for $projectPath"
    }

    $scriptsPath = Join-Path (Join-Path $SourcePath $VersionFolder) "Scripts"
    $dllPath = Join-Path $scriptsPath "$ScriptFileBase.dll"
    $xmlPath = Join-Path $scriptsPath "$ScriptFileBase.xml"
    Assert-PathExists $dllPath "Built DLL copied to local mod folder"
    Assert-PathExists $xmlPath "Built XML documentation copied to local mod folder"

    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dllPath).Version
    $expectedAssemblyVersion = [version]$ExpectedVersion
    if ($assemblyVersion.Major -ne $expectedAssemblyVersion.Major -or
            $assemblyVersion.Minor -ne $expectedAssemblyVersion.Minor -or
            $assemblyVersion.Build -ne $expectedAssemblyVersion.Build) {
        throw "Built DLL version mismatch. DLL=$assemblyVersion, expected $ExpectedVersion at $dllPath"
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

function Format-Tags([string[]] $Tags) {
    if ($Tags.Count -eq 0) {
        return "(none)"
    }
    return $Tags -join ", "
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

function Test-VersionTag([string] $Tag) {
    return $Tag -match "^Update \d+\.\d+$"
}

function Convert-VersionFolderToSteamTag([string] $VersionFolder) {
    if ($VersionFolder -match "^version-(?<major>\d+)\.(?<minor>\d+)(?:\.\d+)?$") {
        return "Update $($matches["major"]).$($matches["minor"])"
    }
    throw "Unsupported version folder for Steam tag conversion: $VersionFolder"
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

function Get-TargetSteamTags([string[]] $LocalTags, [string[]] $VersionFolders, [object] $ReleaseConfig) {
    $versionTags = @($VersionFolders | ForEach-Object { Convert-VersionFolderToSteamTag $_ })
    $additionalCompatibilityTags = Get-AdditionalCompatibilityTags $ReleaseConfig
    $nonVersionTags = @($LocalTags | Where-Object { -not (Test-VersionTag $_) })
    return Get-UniqueTags (@("Mod") + $versionTags + $additionalCompatibilityTags + $nonVersionTags)
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

function Save-WorkshopData([string] $Path, [object] $WorkshopData) {
    $WorkshopData | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path -Encoding UTF8
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

function Get-LiveSteamTags([string] $PublishedFileId) {
    $details = Get-SteamDetails $PublishedFileId
    return Get-UniqueTags @($details.tags | ForEach-Object { [string]$_.tag })
}

function Invoke-SteamTagsUpdate([string] $PublishedFileId, [string[]] $TargetTags) {
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

$scriptFileBase = $ModName
if (-not [string]::IsNullOrWhiteSpace($releaseConfig.ScriptFileBase)) {
    $scriptFileBase = [string]$releaseConfig.ScriptFileBase
}

$packageMode = "Build"
if (-not [string]::IsNullOrWhiteSpace($releaseConfig.Package.Mode)) {
    $packageMode = [string]$releaseConfig.Package.Mode
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedPackageSha256)) {
    if ($packageMode -eq "ExistingZip") {
        $zipPath = Resolve-RepoPath ([string]$releaseConfig.Package.Path)
    }
    elseif ($packageMode -eq "LocalModFolder") {
        $zipPath = Resolve-RepoPath ([string]$releaseConfig.Package.OutputPath)
    }
    else {
        throw "Immutable preflight publishing is not supported for package mode: $packageMode"
    }
    Write-Host "Using immutable preflight package: $zipPath"
}
elseif ($packageMode -eq "Build") {
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
    $expectedExportedManifestVersion = $releaseConfig.ManifestVersions.$GameVersion
    if ($null -eq $expectedExportedManifestVersion) {
        $expectedExportedManifestVersion = $modVersion
    }
    Assert-RepositoryReleaseVersions $manifest $manifestPath $modVersion
    Assert-InstalledGameVersionCompatible $releaseConfig $manifest $GameVersion $GameRoot
    if (-not $SkipUnityExport) {
        Invoke-UnityExport $ModName $GameVersion
    }
    Assert-PathExists $sourcePath "Local mod folder"
    Assert-UnityExportedVersionFolder $sourcePath $GameVersion ([string]$expectedExportedManifestVersion)
    if (-not $SkipBuild) {
        Build-LocalModScripts $ModName $Configuration $sourcePath $GameVersion $scriptFileBase $modVersion
    }

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
if (-not [string]::IsNullOrWhiteSpace($ExpectedPackageSha256)) {
    $actualPackageSha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualPackageSha256 -ne $ExpectedPackageSha256.ToLowerInvariant()) {
        throw "Release package changed since final preflight. Expected: $ExpectedPackageSha256; actual: $actualPackageSha256"
    }
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
$localTags = Get-UniqueTags @($workshopData.Tags)
$tags = Get-TargetSteamTags $localTags $versionFolders $releaseConfig
$localTagAdds = Get-AddedTags $tags $localTags
$localTagRemoves = Get-RemovedTags $tags $localTags
$localTagsSynchronized = $localTagAdds.Count -eq 0 -and $localTagRemoves.Count -eq 0
if ($Publish -and -not $localTagsSynchronized) {
    throw "Local workshop_data.json does not contain the final derived tags. Materialize tags and create a new final preflight report before publishing."
}

$liveTags = Get-LiveSteamTags $publishedFileId
$liveTagAdds = Get-AddedTags $tags $liveTags
$liveTagRemoves = Get-RemovedTags $tags $liveTags
$liveTagsSynchronized = $liveTagAdds.Count -eq 0 -and $liveTagRemoves.Count -eq 0

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
if (-not $localTagsSynchronized) {
    if ($Publish) {
        Write-Host "Local workshop_data.json tag update: applied"
    }
    else {
        Write-Host "Local workshop_data.json tag update: needed"
    }
    Write-Host "  Add locally: $(Format-Tags $localTagAdds)"
    Write-Host "  Remove locally: $(Format-Tags $localTagRemoves)"
}
Write-Host "Live Steam tags: $(Format-Tags $liveTags)"
Write-Host "Corrective same-version replacement: $([bool]$CorrectiveReplacement)"
if (-not $liveTagsSynchronized) {
    if ($Publish) {
        Write-Host "Live Steam tag update: will update before SteamCMD upload"
    }
    else {
        Write-Host "Live Steam tag update: needed before SteamCMD upload"
    }
    Write-Host "  Add live: $(Format-Tags $liveTagAdds)"
    Write-Host "  Remove live: $(Format-Tags $liveTagRemoves)"
}
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
    if (-not $liveTagsSynchronized) {
        Invoke-SteamTagsUpdate $publishedFileId $tags
        Start-Sleep -Seconds 2
        $updatedLiveTags = Get-LiveSteamTags $publishedFileId
        $remainingLiveAdds = Get-AddedTags $tags $updatedLiveTags
        $remainingLiveRemoves = Get-RemovedTags $tags $updatedLiveTags
        if ($remainingLiveAdds.Count -ne 0 -or $remainingLiveRemoves.Count -ne 0) {
            throw "Steam tags update completed, but live tags do not match target. Live: $(Format-Tags $updatedLiveTags)"
        }
        Write-Host "Steam tags are synchronized."
    }
    $loginSettings = Get-SteamLoginSettings
    Write-Host "Starting SteamCMD..."
    Invoke-SteamWorkshopUpload $loginSettings.SteamCmdPath $loginSettings.UserName $vdfPath
    Write-Host "SteamCMD upload completed."
    exit 0
}

Write-Host "Dry run only. SteamCMD was not started."
