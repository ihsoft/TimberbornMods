param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $GameVersion = "version-1.1",
    [string] $UnityPath = "",
    [string] $ProjectPath = "ModsUnityProject",
    [string] $LogRoot = ".tools/unity-logs",
    [string] $LockOwner = "",
    [switch] $SkipWindowsAssetBundle,
    [switch] $SkipMacAssetBundle,
    [switch] $BuildCode,
    [switch] $BuildZipArchive,
    [switch] $NoDeleteFiles
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$coordinationModule = Join-Path $PSScriptRoot "repository-coordination.psm1"
Import-Module $coordinationModule -Force

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

function Get-UnityProjectVersion([string] $ProjectRoot) {
    $projectVersionPath = Join-Path $ProjectRoot "ProjectSettings/ProjectVersion.txt"
    Assert-PathExists $projectVersionPath "Unity project version file"
    $content = Get-Content -Raw -LiteralPath $projectVersionPath
    $match = [regex]::Match($content, "(?m)^m_EditorVersion:\s*(?<version>\S+)")
    if (-not $match.Success) {
        throw "Cannot read Unity editor version from $projectVersionPath"
    }
    return $match.Groups["version"].Value
}

function Resolve-UnityPath([string] $ConfiguredPath, [string] $EditorVersion) {
    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        $resolvedPath = Resolve-RepoPath $ConfiguredPath
        Assert-PathExists $resolvedPath "Unity executable"
        return $resolvedPath
    }

    $hubEditorPath = Join-Path "C:\Program Files\Unity\Hub\Editor" $EditorVersion
    $hubUnityPath = Join-Path $hubEditorPath "Editor/Unity.exe"
    if (Test-Path -LiteralPath $hubUnityPath) {
        return $hubUnityPath
    }

    $command = Get-Command Unity.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    throw "Unity.exe for editor version $EditorVersion was not found. Pass -UnityPath."
}

function Convert-GameVersionToCompatibilityVersion([string] $Version) {
    if ($Version -match "^version-(?<compatibility>\d+\.\d+)$") {
        return $matches["compatibility"]
    }
    if ($Version -match "^\d+\.\d+$") {
        return $Version
    }
    throw "GameVersion must look like version-X.X or X.X: $Version"
}

function ConvertTo-ProcessArgument([string] $Argument) {
    if ($Argument -notmatch '[\s"]') {
        return $Argument
    }
    return '"' + $Argument.Replace('"', '\"') + '"'
}

function Normalize-PathForComparison([string] $Path) {
    return [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
}

function Test-CommandLineReferencesProject([string] $CommandLine, [string] $ProjectRoot) {
    if ([string]::IsNullOrWhiteSpace($CommandLine)) {
        return $false
    }

    $normalizedProjectRoot = Normalize-PathForComparison $ProjectRoot
    $alternateProjectRoot = $normalizedProjectRoot.Replace('\', '/')
    return $CommandLine.IndexOf($normalizedProjectRoot, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $CommandLine.IndexOf($alternateProjectRoot, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Assert-UnityProjectNotOpen([string] $ProjectRoot) {
    $knownLockFiles = @(
        (Join-Path $ProjectRoot "Temp/UnityLockfile"),
        (Join-Path $ProjectRoot "Temp/UnityLockFile")
    )
    foreach ($lockFile in $knownLockFiles) {
        if (Test-Path -LiteralPath $lockFile) {
            throw "Unity project appears to be open or locked: $ProjectRoot. Close Unity Editor for this project before running batch export. Lock file: $lockFile"
        }
    }

    $unityProcesses = @(Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object { Test-CommandLineReferencesProject ([string]$_.CommandLine) $ProjectRoot })
    if ($unityProcesses.Count -gt 0) {
        $processList = ($unityProcesses | ForEach-Object { "pid=$($_.ProcessId)" }) -join ", "
        throw "Unity project appears to be open in Unity Editor: $ProjectRoot ($processList). Close Unity Editor before running batch export."
    }
}

$operation = "Export Unity mod $ModName for $GameVersion"
Invoke-WithRepositoryLock -RepositoryRoot $repoRoot -Resource "unity-project" -Operation $operation -Owner $LockOwner -Action {
    $projectRoot = Resolve-RepoPath $ProjectPath
    Assert-PathExists $projectRoot "Unity project"
    Assert-UnityProjectNotOpen $projectRoot
    $editorVersion = Get-UnityProjectVersion $projectRoot
    $unityExe = Resolve-UnityPath $UnityPath $editorVersion
    $compatibilityVersion = Convert-GameVersionToCompatibilityVersion $GameVersion

    $logRootPath = Resolve-RepoPath $LogRoot
    New-Item -ItemType Directory -Path $logRootPath -Force | Out-Null
    $logPath = Join-Path $logRootPath "$ModName-$GameVersion.log"

    $unityArguments = @(
        "-batchmode",
        "-quit",
        "-projectPath",
        $projectRoot,
        "-logFile",
        $logPath,
        "-executeMethod",
        "Timberborn.ModdingTools.ModBuilding.ModBuilderBatch.Build",
        "-mod",
        $ModName,
        "-compatibilityVersion",
        $compatibilityVersion,
        "-buildCode",
        $BuildCode.IsPresent.ToString().ToLowerInvariant(),
        "-buildWindowsAssetBundle",
        (-not $SkipWindowsAssetBundle).ToString().ToLowerInvariant(),
        "-buildMacAssetBundle",
        (-not $SkipMacAssetBundle).ToString().ToLowerInvariant(),
        "-deleteFiles",
        (-not $NoDeleteFiles).ToString().ToLowerInvariant(),
        "-buildZipArchive",
        $BuildZipArchive.IsPresent.ToString().ToLowerInvariant()
    )

    Write-Host "Running Unity export for $ModName $GameVersion"
    Write-Host "Unity: $unityExe"
    Write-Host "Project: $projectRoot"
    Write-Host "Log: $logPath"

    $argumentList = ($unityArguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join " "
    $process = Start-Process -FilePath $unityExe -ArgumentList $argumentList -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Unity export failed with exit code $($process.ExitCode). See log: $logPath"
    }

    $exportedModRoot = Resolve-RepoPath (Join-Path "_MODS!" $ModName)
    $exportedVersionRoot = Join-Path $exportedModRoot $GameVersion
    Assert-PathExists $exportedVersionRoot "Exported compatibility lane"
    foreach ($releaseMetadataFile in @("workshop_data.json", "thumbnail.jpg")) {
        $laneMetadataPath = Join-Path $exportedVersionRoot $releaseMetadataFile
        if (Test-Path -LiteralPath $laneMetadataPath) {
            Copy-Item -LiteralPath $laneMetadataPath -Destination (Join-Path $exportedModRoot $releaseMetadataFile) -Force
        }
    }

    Write-Host "Unity export completed for $ModName $GameVersion"
}
