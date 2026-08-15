param(
  [string] $GameRoot = "Dependencies\GameRoot",
  [string] $OutputRoot = "_DecompiledGame",
  [string] $ToolPath = ".tools\ilspy",
  [string[]] $Include = @("Timberborn.*.dll"),
  [ValidateRange(1, 64)]
  [int] $MaxParallelism = [Math]::Min([Environment]::ProcessorCount, 8),
  [switch] $InstallTool,
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
$managedPath = Join-Path $gameRootPath "Timberborn_Data\Managed"
$outputRootPath = Join-Path $repoRoot $OutputRoot
$toolRootPath = Join-Path $repoRoot $ToolPath
$ilspyPath = Join-Path $toolRootPath "ilspycmd.exe"
$provenancePath = Join-Path $outputRootPath $provenanceFileName
$errorLogPath = Join-Path $outputRootPath "decompile-errors.csv"

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

$assemblies = foreach ($pattern in $Include) {
  Get-ChildItem -LiteralPath $managedPath -Filter $pattern -File
}

$assemblies = $assemblies | Sort-Object FullName -Unique

if (!$assemblies) {
  throw "No assemblies matched: $($Include -join ', ')"
}

$gameIdentity = Get-GameIdentity $gameRootPath
$ilspyVersionOutput = @(& $ilspyPath --version 2>&1)
if ($LASTEXITCODE -ne 0) {
  throw "Failed to get ilspycmd version: $($ilspyVersionOutput -join [System.Environment]::NewLine)"
}
$ilspyVersionLine = $ilspyVersionOutput | Where-Object { [string]$_ -match '^ilspycmd:\s*(.+)$' } | Select-Object -First 1
if (!$ilspyVersionLine -or !([string]$ilspyVersionLine -match '^ilspycmd:\s*(.+)$')) {
  throw "Unexpected ilspycmd version output: $($ilspyVersionOutput -join [System.Environment]::NewLine)"
}
$ilspyVersion = $Matches[1].Trim()
$scriptRelativePath = Get-RepositoryRelativePath $repoRoot $PSCommandPath
$inputRecords = @($assemblies | ForEach-Object {
  [ordered]@{
    Path = "Timberborn_Data/Managed/$($_.Name)"
    Length = $_.Length
    Sha256 = Get-Sha256 $_.FullName
  }
})

if (Test-Path -LiteralPath $provenancePath) {
  Remove-Item -LiteralPath $provenancePath -Force
}
if (Test-Path -LiteralPath $errorLogPath) {
  Remove-Item -LiteralPath $errorLogPath -Force
}
if ($Clean -and (Test-Path -LiteralPath $outputRootPath)) {
  Remove-Item -LiteralPath $outputRootPath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $outputRootPath | Out-Null

$pending = [System.Collections.Queue]::new()
foreach ($assembly in $assemblies) {
  $pending.Enqueue($assembly)
}

$failed = @()
$running = [System.Collections.ArrayList]::new()

while ($pending.Count -gt 0 -or $running.Count -gt 0) {
  while ($pending.Count -gt 0 -and $running.Count -lt $MaxParallelism) {
    $assembly = $pending.Dequeue()
    $assemblyOutputPath = Join-Path $outputRootPath $assembly.BaseName
    $arguments = @(
      "-p",
      "--disable-updatecheck",
      "-o",
      "`"$assemblyOutputPath`"",
      "`"$($assembly.FullName)`""
    )

    Write-Host "Decompiling $($assembly.Name) -> $assemblyOutputPath"

    try {
      $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
      $startInfo.FileName = $ilspyPath
      $startInfo.Arguments = $arguments -join " "
      $startInfo.UseShellExecute = $false
      $startInfo.CreateNoWindow = $true
      $startInfo.RedirectStandardOutput = $true
      $startInfo.RedirectStandardError = $true

      $process = [System.Diagnostics.Process]::new()
      $process.StartInfo = $startInfo
      [void] $process.Start()
      [void] $running.Add([pscustomobject]@{
        Assembly = $assembly
        Process = $process
        Stdout = $process.StandardOutput.ReadToEndAsync()
        Stderr = $process.StandardError.ReadToEndAsync()
      })
    } catch {
      $failed += [pscustomobject]@{
        Assembly = $assembly.FullName
        ExitCode = $null
        Error = $_.Exception.Message
      }
    }
  }

  $completed = @($running | Where-Object { $_.Process.HasExited })
  if ($completed.Count -eq 0) {
    Start-Sleep -Milliseconds 50
    continue
  }

  foreach ($work in $completed) {
    $work.Process.WaitForExit()
    $exitCode = $work.Process.ExitCode
    $stdout = $work.Stdout.Result
    $stderr = $work.Stderr.Result
    $work.Process.Dispose()
    [void] $running.Remove($work)

    if ($exitCode -ne 0) {
      $errorText = [string] $stderr
      if ([string]::IsNullOrWhiteSpace($errorText)) {
        $errorText = [string] $stdout
      }
      $failed += [pscustomobject]@{
        Assembly = $work.Assembly.FullName
        ExitCode = $exitCode
        Error = $errorText.Trim()
      }
    }
  }
}

if ($failed.Count -gt 0) {
  $failed | Export-Csv -NoTypeInformation -Path $errorLogPath
  throw "Failed to decompile $($failed.Count) assemblies. See: $errorLogPath"
}

$provenance = [ordered]@{
  SchemaVersion = 1
  ResourceKind = "DecompiledGameAssemblies"
  GeneratedAtUtc = [System.DateTime]::UtcNow.ToString("o", [System.Globalization.CultureInfo]::InvariantCulture)
  Game = $gameIdentity
  Generator = [ordered]@{
    Script = $scriptRelativePath
    ScriptSha256 = Get-Sha256 $PSCommandPath
    ExternalTool = [ordered]@{
      Name = "ilspycmd"
      Version = $ilspyVersion
    }
  }
  Options = [ordered]@{
    IncludePatterns = [string[]] @($Include | Sort-Object -Unique)
    MaxParallelism = $MaxParallelism
    Clean = [bool] $Clean
  }
  Inputs = $inputRecords
}
Write-GenerationProvenance $provenancePath $provenance

Write-Host "Done. Decompiled $($assemblies.Count) assemblies with up to $MaxParallelism workers into $outputRootPath"
Write-Host "Provenance: game $($gameIdentity.CurrentVersion) ($($gameIdentity.VersionText)), $($inputRecords.Count) inputs -> $provenanceFileName"
