param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $Configuration = "Release",
    [string] $GameVersion = "version-1.1",
    [string] $OutputRoot = ".tools/release-preview",
    [string] $LocalModRoot = "",
    [string] $ConfigPath = "",
    [string] $AccessTokenPath = "",
    [string] $ChangeNotesPrefix = "",
    [switch] $IncludeLegacyVersions,
    [switch] $SkipBuild,
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
        throw "Cannot find CHANGES.md section for v$Version"
    }

    $body = $match.Groups["body"].Value.Trim()
    if ([string]::IsNullOrWhiteSpace($body)) {
        throw "CHANGES.md section for v$Version is empty"
    }
    return $body
}

function Get-ZipVersionFolders([string] $ZipPath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $folders = $zip.Entries | ForEach-Object {
            if ($_.FullName -match "^[^/]+/(version-\d+\.\d+)/") {
                $matches[1]
            }
        } | Sort-Object -Unique
        return @($folders)
    }
    finally {
        $zip.Dispose()
    }
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
            $manifestEntry = $zip.Entries | Where-Object {
                $_.FullName -match "^[^/]+/$([regex]::Escape($versionFolder))/manifest\.json$"
            } | Select-Object -First 1
            if ($null -eq $manifestEntry) {
                throw "Package folder $versionFolder must contain manifest.json."
            }

            $dllEntry = $zip.Entries | Where-Object {
                $_.FullName -match "^[^/]+/$([regex]::Escape($versionFolder))/Scripts/$([regex]::Escape($ScriptFileBase))\.dll$"
            } | Select-Object -First 1
            if ($null -eq $dllEntry) {
                throw "Package folder $versionFolder must contain Scripts/$ScriptFileBase.dll."
            }

            $xmlEntry = $zip.Entries | Where-Object {
                $_.FullName -match "^[^/]+/$([regex]::Escape($versionFolder))/Scripts/$([regex]::Escape($ScriptFileBase))\.xml$"
            } | Select-Object -First 1
            if ($null -eq $xmlEntry) {
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

function Compare-VersionText([string] $Left, [string] $Right) {
    $leftVersion = [version]$Left
    $rightVersion = [version]$Right
    return $leftVersion.CompareTo($rightVersion)
}

function Get-CompatibilitySuffix($ReleaseConfig, [string[]] $VersionFolders) {
    $minimumGameVersion = $null
    $maximumGameVersion = $null

    foreach ($versionFolder in $VersionFolders) {
        $compatibility = $ReleaseConfig.GameVersionCompatibility.$versionFolder
        if ($null -eq $compatibility) {
            throw "No compatibility mapping for $versionFolder in $releaseConfigPath"
        }

        $folderMinimum = [string]$compatibility.MinimumGameVersion
        $folderMaximum = [string]$compatibility.MaximumGameVersion
        if ([string]::IsNullOrWhiteSpace($folderMinimum) -or [string]::IsNullOrWhiteSpace($folderMaximum)) {
            throw "Incomplete compatibility mapping for $versionFolder in $releaseConfigPath"
        }

        if ($null -eq $minimumGameVersion -or (Compare-VersionText $folderMinimum $minimumGameVersion) -lt 0) {
            $minimumGameVersion = $folderMinimum
        }
        if ($null -eq $maximumGameVersion -or (Compare-VersionText $folderMaximum $maximumGameVersion) -gt 0) {
            $maximumGameVersion = $folderMaximum
        }
    }

    return @"
---
MinimumGameVersion: $minimumGameVersion
MaximumGameVersion: $maximumGameVersion
---
"@
}

function Read-ModIoConfig([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        $Path = Join-Path $repoRoot ".tools/modio/$ModName.local.json"
    }
    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Read-ModIoAccessToken([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        $Path = Join-Path $repoRoot ".tools/modio/$ModName.token.txt"
    }
    if (Test-Path -LiteralPath $Path) {
        return (Get-Content -Raw -LiteralPath $Path).Trim()
    }

    return ""
}

function Publish-Modfile(
    [string] $ApiBase,
    [string] $GameId,
    [string] $ModId,
    [string] $AccessToken,
    [string] $ZipPath,
    [string] $Version,
    [string] $ChangeNotes) {
    Add-Type -AssemblyName System.Net.Http

    $endpoint = "$($ApiBase.TrimEnd("/"))/games/$GameId/mods/$ModId/files"
    $client = [System.Net.Http.HttpClient]::new()
    $content = [System.Net.Http.MultipartFormDataContent]::new()
    $fileStream = $null
    try {
        $client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new(
            "Bearer",
            $AccessToken)

        $content.Add([System.Net.Http.StringContent]::new($Version), "version")
        $content.Add([System.Net.Http.StringContent]::new($ChangeNotes), "changelog")

        $fileStream = [System.IO.File]::OpenRead($ZipPath)
        $fileContent = [System.Net.Http.StreamContent]::new($fileStream)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/zip")
        $content.Add($fileContent, "filedata", [System.IO.Path]::GetFileName($ZipPath))

        $response = $client.PostAsync($endpoint, $content).GetAwaiter().GetResult()
        $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw "Mod.IO upload failed: HTTP $([int]$response.StatusCode) $($response.ReasonPhrase)`n$responseBody"
        }
        return $responseBody
    }
    finally {
        if ($null -ne $fileStream) {
            $fileStream.Dispose()
        }
        $content.Dispose()
        $client.Dispose()
    }
}

Assert-PathExists $releaseConfigPath "Release config"
Assert-PathExists $buildScriptPath "Package builder"

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
    throw "Manifest version is empty: $manifestPath"
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

    $outputRootPath = Resolve-RepoPath $OutputRoot
    $zipPath = Join-Path $outputRootPath "$($ModName)_v$modVersion.zip"
}
elseif ($packageMode -eq "ExistingZip") {
    if ([string]::IsNullOrWhiteSpace($releaseConfig.Package.Path)) {
        throw "ExistingZip package mode requires Package.Path in $releaseConfigPath"
    }
    $zipPath = Resolve-RepoPath ([string]$releaseConfig.Package.Path)
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

$changeNotes = Get-LatestChangeNotes $changesPath $modVersion
if (-not [string]::IsNullOrWhiteSpace($ChangeNotesPrefix)) {
    $changeNotes = $ChangeNotesPrefix.Trim() + "`r`n" + $changeNotes.TrimStart()
}
$compatibilitySuffix = Get-CompatibilitySuffix $releaseConfig $versionFolders
$modIoChangeNotes = ($changeNotes.TrimEnd() + "`r`n`r`n" + $compatibilitySuffix.Trim()).Trim()
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
$modIoConfig = Read-ModIoConfig $ConfigPath

$apiBase = ""
$gameId = ""
$modId = ""
if ($null -ne $modIoConfig) {
    $apiBase = [string]$modIoConfig.ApiBase
    $gameId = [string]$modIoConfig.GameId
    $modId = [string]$modIoConfig.ModId
}
$endpoint = ""
if (-not [string]::IsNullOrWhiteSpace($apiBase) -and
        -not [string]::IsNullOrWhiteSpace($gameId) -and
        -not [string]::IsNullOrWhiteSpace($modId)) {
    $endpoint = "$($apiBase.TrimEnd("/"))/games/$gameId/mods/$modId/files"
}

Write-Host ""
Write-Host "Mod.IO publish plan for $ModName v$modVersion"
Write-Host "Package: $zipPath"
Write-Host "SHA256: $hash"
Write-Host "Game version folders: $($versionFolders -join ', ')"
if ($releaseConfig.ReadyForPublish -eq $false) {
    Write-Host "Ready for publish: false"
}
if ([string]::IsNullOrWhiteSpace($endpoint)) {
    Write-Host "Endpoint: not configured"
    Write-Host "Config: create .tools/modio/$ModName.local.json or pass -ConfigPath"
}
else {
    Write-Host "Endpoint: $endpoint"
}
Write-Host ""
Write-Host "Change notes:"
Write-Host $modIoChangeNotes
Write-Host ""

if (-not $Publish) {
    Write-Host "Dry run only. Nothing was uploaded. Use -Publish only after an explicit publish request."
    exit 0
}

if ($null -eq $modIoConfig) {
    throw "Cannot publish: Mod.IO config is missing."
}
if ($releaseConfig.ReadyForPublish -eq $false) {
    throw "Cannot publish: $ModName release config is marked ReadyForPublish=false."
}
if ([string]::IsNullOrWhiteSpace($apiBase) -or
        [string]::IsNullOrWhiteSpace($gameId) -or
        [string]::IsNullOrWhiteSpace($modId)) {
    throw "Cannot publish: Mod.IO config must define ApiBase, GameId, and ModId."
}

$accessToken = Read-ModIoAccessToken $AccessTokenPath
if ([string]::IsNullOrWhiteSpace($accessToken)) {
    throw "Cannot publish: create .tools/modio/$ModName.token.txt."
}

Write-Host "Publishing to Mod.IO..."
$responseBody = Publish-Modfile $apiBase $gameId $modId $accessToken $zipPath $modVersion $modIoChangeNotes
Write-Host "Published to Mod.IO."
Write-Host $responseBody
