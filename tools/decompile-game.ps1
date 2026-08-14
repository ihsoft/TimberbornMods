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
  $logPath = Join-Path $outputRootPath "decompile-errors.csv"
  $failed | Export-Csv -NoTypeInformation -Path $logPath
  throw "Failed to decompile $($failed.Count) assemblies. See: $logPath"
}

Write-Host "Done. Decompiled $($assemblies.Count) assemblies with up to $MaxParallelism workers into $outputRootPath"
