param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string[]] $Path,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Message,

    [string] $RepositoryRoot = "",
    [string] $CoordinationRoot = "",
    [string] $Owner = "",
    [string] $ExpectedHead = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$toolRepoRoot = Split-Path -Parent $PSScriptRoot
$repositoryPathsModule = Join-Path $PSScriptRoot "repository-paths.psm1"
Import-Module $repositoryPathsModule -Force
$repoRoot = Resolve-RepositoryRoot $RepositoryRoot $toolRepoRoot
$coordinationRoot = Resolve-RepositoryRoot $CoordinationRoot $toolRepoRoot
$coordinationModule = Join-Path $PSScriptRoot "repository-coordination.psm1"
Import-Module $coordinationModule -Force

function Invoke-Git([string[]] $Arguments) {
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = @(& git @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }

    if ($exitCode -ne 0) {
        $details = ($output | ForEach-Object { [string]$_ }) -join [System.Environment]::NewLine
        throw "git $($Arguments -join ' ') failed with exit code $exitCode.`n$details"
    }
    return $output
}

function ConvertTo-NativeArgument([string] $Argument) {
    if ($Argument.Length -gt 0 -and $Argument -notmatch '[\s"]') {
        return $Argument
    }

    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.Append([char]34)
    $backslashCount = 0
    foreach ($character in $Argument.ToCharArray()) {
        if ($character -eq [char]92) {
            $backslashCount++
            continue
        }

        if ($character -eq [char]34) {
            [void]$builder.Append(([string][char]92) * (($backslashCount * 2) + 1))
            [void]$builder.Append($character)
        } else {
            [void]$builder.Append(([string][char]92) * $backslashCount)
            [void]$builder.Append($character)
        }
        $backslashCount = 0
    }

    [void]$builder.Append(([string][char]92) * ($backslashCount * 2))
    [void]$builder.Append([char]34)
    return $builder.ToString()
}

function Invoke-GitPathList([string[]] $Arguments) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "git"
    $startInfo.Arguments = (@($Arguments | ForEach-Object { ConvertTo-NativeArgument $_ })) -join " "
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $utf8 = [System.Text.UTF8Encoding]::new($false, $true)
    $startInfo.StandardOutputEncoding = $utf8
    $startInfo.StandardErrorEncoding = $utf8

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        [void]$process.Start()
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $output = $stdoutTask.GetAwaiter().GetResult()
        $errorOutput = $stderrTask.GetAwaiter().GetResult()
        $exitCode = $process.ExitCode
    } finally {
        $process.Dispose()
    }

    if ($exitCode -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $exitCode.`n$errorOutput"
    }
    if ($output.Length -gt 0 -and $output[$output.Length - 1] -ne [char]0) {
        throw "git $($Arguments -join ' ') returned a path list without a terminating NUL byte."
    }

    return @($output.Split([char]0) | Where-Object { $_.Length -gt 0 })
}

function Convert-ToRepositoryPath([string] $InputPath) {
    $absolutePath = if ([System.IO.Path]::IsPathRooted($InputPath)) {
        [System.IO.Path]::GetFullPath($InputPath)
    } else {
        [System.IO.Path]::GetFullPath((Join-Path $repoRoot $InputPath))
    }

    $rootWithSeparator = $repoRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $absolutePath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Commit path is outside the repository: $InputPath"
    }
    if (Test-Path -LiteralPath $absolutePath -PathType Container) {
        throw "Commit paths must name exact files, not directories: $InputPath"
    }

    $relativePath = $absolutePath.Substring($rootWithSeparator.Length).Replace('\', '/')
    return $relativePath
}

function Assert-SamePathSet([string[]] $Expected, [string[]] $Actual, [string] $Description) {
    $expectedSet = @($Expected | Sort-Object -Unique)
    $actualSet = @($Actual | Sort-Object -Unique)
    $difference = @(Compare-Object -ReferenceObject $expectedSet -DifferenceObject $actualSet)
    if ($difference.Count -gt 0) {
        $expectedText = $expectedSet -join ", "
        $actualText = $actualSet -join ", "
        throw "$Description path mismatch. Expected: [$expectedText]. Actual: [$actualText]."
    }
}

$inputPaths = @(
    $Path |
        ForEach-Object { $_ -split ',' } |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
)
$relativePaths = @($inputPaths | ForEach-Object { Convert-ToRepositoryPath $_ } | Sort-Object -Unique)
$operation = "Commit repository changes: $Message"

Invoke-WithRepositoryLock -RepositoryRoot $coordinationRoot -Resource "git-transaction" -Operation $operation `
    -Owner $Owner -Action {
    Push-Location $repoRoot
    $stagedByScript = @()
    $stagingAttempted = $false
    $commitCreated = $false
    try {
        $headBefore = [string](@(Invoke-Git @("rev-parse", "HEAD"))[0])
        if (-not [string]::IsNullOrWhiteSpace($ExpectedHead) -and $headBefore -ne $ExpectedHead) {
            throw "HEAD changed before the commit transaction. Expected $ExpectedHead, actual $headBefore."
        }

        $preStaged = @(Invoke-GitPathList @("diff", "--cached", "--name-only", "-z", "--"))
        if ($preStaged.Count -gt 0) {
            throw "The Git index already contains staged paths: $($preStaged -join ', '). Do not alter another task's staged state."
        }

        foreach ($relativePath in $relativePaths) {
            $status = @(Invoke-Git @("status", "--porcelain=v1", "--untracked-files=all", "--", $relativePath))
            if ($status.Count -eq 0) {
                throw "Commit path has no current change: $relativePath"
            }
        }

        $stagingAttempted = $true
        Invoke-Git (@("add", "--") + $relativePaths) | Out-Null
        $stagedByScript = @(
            Invoke-GitPathList @("diff", "--cached", "--no-renames", "--name-only", "-z", "--")
        )
        Assert-SamePathSet $relativePaths $stagedByScript "Staged"

        Invoke-Git @("diff", "--cached", "--check") | Out-Null
        Invoke-Git @("diff", "--cached", "--stat", "--") | ForEach-Object { Write-Host $_ }

        Invoke-Git @("commit", "-m", $Message) | ForEach-Object { Write-Host $_ }
        $commitCreated = $true

        $headAfter = [string](@(Invoke-Git @("rev-parse", "HEAD"))[0])
        if ($headAfter -eq $headBefore) {
            throw "Git reported success but HEAD did not change."
        }

        $committedPaths = @(
            Invoke-GitPathList @(
                "diff-tree", "--no-renames", "--no-commit-id", "--name-only", "-z", "-r", $headAfter
            )
        )
        Assert-SamePathSet $stagedByScript $committedPaths "Committed"

        $remainingStaged = @(
            Invoke-GitPathList @("diff", "--cached", "--no-renames", "--name-only", "-z", "--")
        )
        if ($remainingStaged.Count -gt 0) {
            throw "The commit completed but staged paths remain: $($remainingStaged -join ', ')."
        }

        Write-Host "Created and verified commit $headAfter"
        Write-Host "Committed paths: $($committedPaths -join ', ')"
    } catch {
        if (-not $commitCreated -and $stagingAttempted) {
            & git restore --staged -- $relativePaths 2>$null
        }
        throw
    } finally {
        Pop-Location
    }
}
