[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [string] $GameRoot = "_GAME!",
  [string] $OutputRoot = "_ExtractedGameAssets",
  [string[]] $Archives = @("Blueprints.zip", "Localizations.zip", "Shaders.zip", "UI.zip"),
  [switch] $Clean
)

$ErrorActionPreference = "Stop"

$provenanceFileName = "generation-provenance.json"

function Get-GameIdentity([string] $gameRootPath) {
  $streamingAssetsPath = Join-Path $gameRootPath "Timberborn_Data\StreamingAssets"
  $versionNumbersPath = Join-Path $streamingAssetsPath "VersionNumbers.json"
  $versionTextPath = Join-Path $streamingAssetsPath "Version.txt"

  if (!(Test-Path -LiteralPath $versionNumbersPath -PathType Leaf)) {
    throw "Game version numbers file not found: $versionNumbersPath"
  }
  if (!(Test-Path -LiteralPath $versionTextPath -PathType Leaf)) {
    throw "Game version text file not found: $versionTextPath"
  }

  $versionNumbers = Get-Content -LiteralPath $versionNumbersPath -Raw | ConvertFrom-Json
  if (!$versionNumbers.CurrentVersion) {
    throw "CurrentVersion is missing in: $versionNumbersPath"
  }
  $versionText = (Get-Content -LiteralPath $versionTextPath -Raw).Trim()
  if ([string]::IsNullOrWhiteSpace($versionText)) {
    throw "Game version text is empty: $versionTextPath"
  }

  return [ordered]@{
    CurrentVersion = [string] $versionNumbers.CurrentVersion
    VersionText = $versionText
  }
}

function Get-Sha256([string] $path) {
  return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-RepositoryRelativePath([string] $repoRootPath, [string] $path) {
  $root = [System.IO.Path]::GetFullPath($repoRootPath).TrimEnd('\', '/')
  $fullPath = [System.IO.Path]::GetFullPath($path)
  $rootPrefix = $root + [System.IO.Path]::DirectorySeparatorChar
  if (!$fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Path is outside the repository: $fullPath"
  }
  return $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
}

function Write-GenerationProvenance([string] $path, [object] $provenance) {
  $temporaryPath = "$path.$([System.Guid]::NewGuid().ToString('N')).tmp"
  try {
    $json = $provenance | ConvertTo-Json -Depth 8
    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($temporaryPath, $json + [System.Environment]::NewLine, $utf8WithoutBom)
    Move-Item -LiteralPath $temporaryPath -Destination $path -Force
  } finally {
    if (Test-Path -LiteralPath $temporaryPath) {
      Remove-Item -LiteralPath $temporaryPath -Force
    }
  }
}

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$gameRootPath = Join-Path $repoRoot $GameRoot
$moddingAssetsPath = Join-Path $gameRootPath "Timberborn_Data\StreamingAssets\Modding"
$outputRootPath = Join-Path $repoRoot $OutputRoot
$provenancePath = Join-Path $outputRootPath $provenanceFileName
$repoRootFullPath = [System.IO.Path]::GetFullPath($repoRoot)
$outputRootFullPath = [System.IO.Path]::GetFullPath($outputRootPath)

$isRepoRoot = $outputRootFullPath -eq $repoRootFullPath
$isUnderRepo = $outputRootFullPath.StartsWith($repoRootFullPath + [System.IO.Path]::DirectorySeparatorChar)

if ($isRepoRoot -or !$isUnderRepo) {
  throw "Output root must be inside the repository: $outputRootFullPath"
}

if (!(Test-Path -LiteralPath $moddingAssetsPath)) {
  throw "Game modding assets folder not found: $moddingAssetsPath"
}

$archiveFiles = foreach ($archiveName in $Archives) {
  $archivePath = Join-Path $moddingAssetsPath $archiveName
  if (!(Test-Path -LiteralPath $archivePath -PathType Leaf)) {
    throw "Archive not found: $archivePath"
  }
  Get-Item -LiteralPath $archivePath
}
$archiveFiles = @($archiveFiles | Sort-Object FullName -Unique)
if ($archiveFiles.Count -eq 0) {
  throw "No archives requested."
}

$gameIdentity = Get-GameIdentity $gameRootPath
$scriptRelativePath = Get-RepositoryRelativePath $repoRoot $PSCommandPath
$inputRecords = @($archiveFiles | ForEach-Object {
  [ordered]@{
    Path = "Timberborn_Data/StreamingAssets/Modding/$($_.Name)"
    Length = $_.Length
    Sha256 = Get-Sha256 $_.FullName
  }
})

if (!$PSCmdlet.ShouldProcess(
    $outputRootPath, "Extract $($archiveFiles.Count) game modding archives and write provenance")) {
  Write-Host "Done. Verified extraction plan for $($archiveFiles.Count) archives into $outputRootPath"
  return
}

if (Test-Path -LiteralPath $provenancePath) {
  Remove-Item -LiteralPath $provenancePath -Force
}
if ($Clean -and (Test-Path -LiteralPath $outputRootPath)) {
  Remove-Item -LiteralPath $outputRootPath -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $outputRootPath | Out-Null

foreach ($archiveFile in $archiveFiles) {
  $destinationName = [System.IO.Path]::GetFileNameWithoutExtension($archiveFile.Name)
  $destinationPath = Join-Path $outputRootPath $destinationName

  if (Test-Path -LiteralPath $destinationPath) {
    Remove-Item -LiteralPath $destinationPath -Recurse -Force
  }

  Write-Host "Extracting $($archiveFile.Name) -> $destinationPath"
  New-Item -ItemType Directory -Force -Path $destinationPath | Out-Null
  Expand-Archive -LiteralPath $archiveFile.FullName -DestinationPath $destinationPath -Force
}

$provenance = [ordered]@{
  SchemaVersion = 1
  ResourceKind = "ExtractedGameModdingAssets"
  GeneratedAtUtc = [System.DateTime]::UtcNow.ToString("o", [System.Globalization.CultureInfo]::InvariantCulture)
  Game = $gameIdentity
  Generator = [ordered]@{
    Script = $scriptRelativePath
    ScriptSha256 = Get-Sha256 $PSCommandPath
  }
  Options = [ordered]@{
    Archives = [string[]] @($archiveFiles.Name)
    Clean = [bool] $Clean
  }
  Inputs = $inputRecords
}
Write-GenerationProvenance $provenancePath $provenance

Write-Host "Done. Extracted $($archiveFiles.Count) archives into $outputRootPath"
Write-Host "Provenance: game $($gameIdentity.CurrentVersion) ($($gameIdentity.VersionText)), $($inputRecords.Count) inputs -> $provenanceFileName"
