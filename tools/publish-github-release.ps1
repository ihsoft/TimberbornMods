param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $GameVersion = "version-1.1",
    [string] $OutputRoot = ".tools/release-preview",
    [string] $Repository = "ihsoft/TimberbornMods",
    [switch] $Publish
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

function Get-PackagePath([object] $ReleaseConfig, [string] $Version) {
    $packageMode = "Build"
    if (-not [string]::IsNullOrWhiteSpace($ReleaseConfig.Package.Mode)) {
        $packageMode = [string]$ReleaseConfig.Package.Mode
    }

    if ($packageMode -eq "Build") {
        return Join-Path (Resolve-RepoPath $OutputRoot) "$($ModName)_v$Version.zip"
    }
    if ($packageMode -eq "ExistingZip") {
        if ([string]::IsNullOrWhiteSpace($ReleaseConfig.Package.Path)) {
            throw "ExistingZip package mode requires Package.Path in $releaseConfigPath"
        }
        return Resolve-RepoPath ([string]$ReleaseConfig.Package.Path)
    }
    if ($packageMode -eq "LocalModFolder") {
        if ([string]::IsNullOrWhiteSpace($ReleaseConfig.Package.OutputPath)) {
            throw "LocalModFolder package mode requires Package.OutputPath in $releaseConfigPath"
        }
        return Resolve-RepoPath ([string]$ReleaseConfig.Package.OutputPath)
    }

    throw "Unsupported package mode: $packageMode"
}

function Test-GitRefExists([string] $Ref) {
    & git rev-parse --verify --quiet $Ref *> $null
    return $LASTEXITCODE -eq 0
}

function Assert-CommandAvailable([string] $Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "$Name command was not found."
    }
}

Assert-PathExists $releaseConfigPath "Release config"
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

$zipPath = Get-PackagePath $releaseConfig $modVersion
Assert-PathExists $zipPath "Release package"
Assert-PackageFreshEnough $zipPath $releaseConfig.Package
$versionFolders = Test-ZipPackage $zipPath $scriptFileBase $releaseConfig

$tagName = "$($ModName)_$modVersion"
if (-not (Test-GitRefExists "refs/tags/$tagName")) {
    throw "Release tag does not exist locally: $tagName"
}

$tagCommit = (& git rev-list -n 1 $tagName).Trim()
$changeNotes = Get-LatestChangeNotes $changesPath $modVersion
$releaseTitle = "$([string]$manifest.Name) v$modVersion"
if ([string]::IsNullOrWhiteSpace($manifest.Name)) {
    $releaseTitle = "$ModName v$modVersion"
}
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash

Write-Host ""
Write-Host "GitHub release plan for $ModName v$modVersion"
Write-Host "Repository: $Repository"
Write-Host "Tag: $tagName"
Write-Host "Tag commit: $tagCommit"
Write-Host "Title: $releaseTitle"
Write-Host "Package: $zipPath"
Write-Host "SHA256: $hash"
Write-Host "Game version folders: $($versionFolders -join ', ')"
Write-Host ""
Write-Host "Release notes:"
Write-Host $changeNotes
Write-Host ""

if (-not $Publish) {
    Write-Host "Dry run only. Nothing was uploaded. Use -Publish only after Steam/Mod.IO publishing is verified."
    exit 0
}

if ($releaseConfig.ReadyForPublish -eq $false) {
    throw "Cannot publish: $ModName release config is marked ReadyForPublish=false."
}

Assert-CommandAvailable "gh"

& gh release view $tagName --repo $Repository *> $null
if ($LASTEXITCODE -eq 0) {
    throw "GitHub release already exists for tag $tagName."
}

$notesRoot = Resolve-RepoPath ".tools/github-release-notes"
New-Item -ItemType Directory -Path $notesRoot -Force | Out-Null
$notesPath = Join-Path $notesRoot "$tagName.md"
Set-Content -LiteralPath $notesPath -Value $changeNotes -Encoding UTF8

& gh release create $tagName $zipPath --repo $Repository --title $releaseTitle --notes-file $notesPath
if ($LASTEXITCODE -ne 0) {
    throw "GitHub release create failed for $tagName."
}

Write-Host "GitHub release created for $tagName."
