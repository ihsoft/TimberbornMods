param(
  [string] $GameRoot = "_GAME!",
  [string] $OutputRoot = "_ExtractedGameAssets",
  [string[]] $Archives = @("Blueprints.zip", "Localizations.zip", "Shaders.zip", "UI.zip"),
  [switch] $Clean
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$moddingAssetsPath = Join-Path $repoRoot "$GameRoot\Timberborn_Data\StreamingAssets\Modding"
$outputRootPath = Join-Path $repoRoot $OutputRoot
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

if ($Clean -and (Test-Path -LiteralPath $outputRootPath)) {
  Remove-Item -LiteralPath $outputRootPath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $outputRootPath | Out-Null

foreach ($archiveName in $Archives) {
  $archivePath = Join-Path $moddingAssetsPath $archiveName
  if (!(Test-Path -LiteralPath $archivePath)) {
    throw "Archive not found: $archivePath"
  }

  $destinationName = [System.IO.Path]::GetFileNameWithoutExtension($archiveName)
  $destinationPath = Join-Path $outputRootPath $destinationName

  if (Test-Path -LiteralPath $destinationPath) {
    Remove-Item -LiteralPath $destinationPath -Recurse -Force
  }

  Write-Host "Extracting $archiveName -> $destinationPath"
  New-Item -ItemType Directory -Force -Path $destinationPath | Out-Null
  Expand-Archive -LiteralPath $archivePath -DestinationPath $destinationPath -Force
}

Write-Host "Done. Extracted $($Archives.Count) archives into $outputRootPath"
