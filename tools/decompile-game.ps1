param(
  [string] $GameRoot = "Dependencies\GameRoot",
  [string] $OutputRoot = "_DecompiledGame",
  [string] $ToolPath = ".tools\ilspy",
  [string[]] $Include = @("Timberborn.*.dll"),
  [switch] $InstallTool,
  [switch] $Clean
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$gameRootPath = Join-Path $repoRoot $GameRoot
$managedPath = Join-Path $gameRootPath "Timberborn_Data\Managed"
$outputRootPath = Join-Path $repoRoot $OutputRoot
$toolRootPath = Join-Path $repoRoot $ToolPath
$ilspyPath = Join-Path $toolRootPath "ilspycmd.exe"

if (!(Test-Path -LiteralPath $managedPath)) {
  throw "Managed assemblies folder not found: $managedPath"
}

if (!(Test-Path -LiteralPath $ilspyPath)) {
  if (!$InstallTool) {
    throw "ilspycmd not found: $ilspyPath. Re-run with -InstallTool to install it."
  }

  New-Item -ItemType Directory -Force -Path $toolRootPath | Out-Null
  dotnet tool install ilspycmd --tool-path $toolRootPath
}

if ($Clean -and (Test-Path -LiteralPath $outputRootPath)) {
  Remove-Item -LiteralPath $outputRootPath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $outputRootPath | Out-Null

$assemblies = foreach ($pattern in $Include) {
  Get-ChildItem -LiteralPath $managedPath -Filter $pattern -File
}

$assemblies = $assemblies | Sort-Object FullName -Unique

if (!$assemblies) {
  throw "No assemblies matched: $($Include -join ', ')"
}

$failed = @()

foreach ($assembly in $assemblies) {
  $assemblyOutputPath = Join-Path $outputRootPath $assembly.BaseName
  Write-Host "Decompiling $($assembly.Name) -> $assemblyOutputPath"

  try {
    & $ilspyPath -p -o $assemblyOutputPath $assembly.FullName
  } catch {
    $failed += [pscustomobject]@{
      Assembly = $assembly.FullName
      Error = $_.Exception.Message
    }
  }
}

if ($failed.Count -gt 0) {
  $logPath = Join-Path $outputRootPath "decompile-errors.csv"
  $failed | Export-Csv -NoTypeInformation -Path $logPath
  throw "Failed to decompile $($failed.Count) assemblies. See: $logPath"
}

Write-Host "Done. Decompiled $($assemblies.Count) assemblies into $outputRootPath"
