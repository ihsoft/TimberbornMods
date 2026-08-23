param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $ReleaseRoot = "",
    [string] $Configuration = "Release",
    [string] $GameVersion = "version-1.1"
)

$ErrorActionPreference = "Stop"

$toolRepoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "repository-paths.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-native-command.psm1") -Force
$repoRoot = Resolve-RepositoryRoot $ReleaseRoot $toolRepoRoot
$modRoot = Join-Path $repoRoot $ModName
$releaseConfigPath = Join-Path $modRoot "release.json"
$projectPath = Join-Path $modRoot "$ModName.csproj"
$propsPath = Join-Path $modRoot "directory.build.props"

function Assert-PathExists([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

Assert-PathExists $releaseConfigPath "Release config"
Assert-PathExists $projectPath "Project file"
Assert-PathExists $propsPath "Directory build props"

$releaseConfig = Get-Content -Raw -LiteralPath $releaseConfigPath | ConvertFrom-Json
if ([string]$releaseConfig.Package.Mode -ne "LocalModFolder") {
    throw "C# package-source build requires Package.Mode=LocalModFolder in $releaseConfigPath"
}

$sourcePath = Resolve-RepositoryPath $repoRoot ([string]$releaseConfig.Package.SourcePath)
Assert-PathExists $sourcePath "Local mod package source"
$versionPath = Join-Path $sourcePath $GameVersion
$exportedManifestPath = Join-Path $versionPath "manifest.json"
Assert-PathExists $exportedManifestPath "Exported manifest"

$releaseVersion = [string]$releaseConfig.ReleaseVersion
$expectedManifestVersion = [string]$releaseConfig.ManifestVersions.$GameVersion
if ([string]::IsNullOrWhiteSpace($expectedManifestVersion)) {
    $expectedManifestVersion = $releaseVersion
}
$exportedManifest = Get-Content -Raw -LiteralPath $exportedManifestPath | ConvertFrom-Json
if ([string]$exportedManifest.Version -ne $expectedManifestVersion) {
    throw "Exported manifest version is $($exportedManifest.Version), expected $expectedManifestVersion."
}

$props = [xml](Get-Content -Raw -LiteralPath $propsPath)
$projectVersion = [string]$props.Project.PropertyGroup.Version
if ($projectVersion -ne $releaseVersion) {
    throw "Project version is $projectVersion, expected release version $releaseVersion."
}

$modPath = Split-Path -Parent $sourcePath
$buildResult = Invoke-ReleaseNativeCommand -Command "dotnet" -Arguments @(
    "build",
    $projectPath,
    "-c",
    $Configuration,
    "/p:ModPath=$modPath")
$buildResult.Diagnostics | ForEach-Object { Write-Host $_ }
if ($buildResult.ExitCode -ne 0) {
    throw "dotnet build failed for $projectPath with exit code $($buildResult.ExitCode)."
}

$scriptFileBase = if ([string]::IsNullOrWhiteSpace([string]$releaseConfig.ScriptFileBase)) {
    $ModName
}
else {
    [string]$releaseConfig.ScriptFileBase
}
$scriptsPath = Join-Path $versionPath "Scripts"
$dllPath = Join-Path $scriptsPath "$scriptFileBase.dll"
$xmlPath = Join-Path $scriptsPath "$scriptFileBase.xml"
Assert-PathExists $dllPath "Built DLL copied to local mod folder"
Assert-PathExists $xmlPath "Built XML documentation copied to local mod folder"

$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dllPath).Version
$expectedAssemblyVersion = [version]$releaseVersion
if ($assemblyVersion.Major -ne $expectedAssemblyVersion.Major -or
        $assemblyVersion.Minor -ne $expectedAssemblyVersion.Minor -or
        $assemblyVersion.Build -ne $expectedAssemblyVersion.Build) {
    throw "Built DLL version is $assemblyVersion, expected $releaseVersion at $dllPath"
}

Write-Host "C# build completed for $ModName ${GameVersion}: $dllPath (assembly $assemblyVersion)"
