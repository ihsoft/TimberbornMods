param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string[]] $Path,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Message,

    [string] $Owner = "",
    [string] $ExpectedHead = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
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

Invoke-WithRepositoryLock -RepositoryRoot $repoRoot -Resource "git-transaction" -Operation $operation -Owner $Owner -Action {
    Push-Location $repoRoot
    $stagedByScript = @()
    $commitCreated = $false
    try {
        $headBefore = [string](@(Invoke-Git @("rev-parse", "HEAD"))[0])
        if (-not [string]::IsNullOrWhiteSpace($ExpectedHead) -and $headBefore -ne $ExpectedHead) {
            throw "HEAD changed before the commit transaction. Expected $ExpectedHead, actual $headBefore."
        }

        $preStaged = @(Invoke-Git @("diff", "--cached", "--name-only", "--"))
        if ($preStaged.Count -gt 0) {
            throw "The Git index already contains staged paths: $($preStaged -join ', '). Do not alter another task's staged state."
        }

        foreach ($relativePath in $relativePaths) {
            $status = @(Invoke-Git @("status", "--porcelain=v1", "--untracked-files=all", "--", $relativePath))
            if ($status.Count -eq 0) {
                throw "Commit path has no current change: $relativePath"
            }
        }

        Invoke-Git (@("add", "--") + $relativePaths) | Out-Null
        $stagedByScript = @(Invoke-Git @("diff", "--cached", "--name-only", "--"))
        Assert-SamePathSet $relativePaths $stagedByScript "Staged"

        Invoke-Git @("diff", "--cached", "--check") | Out-Null
        Invoke-Git @("diff", "--cached", "--stat", "--") | ForEach-Object { Write-Host $_ }

        Invoke-Git @("commit", "-m", $Message) | ForEach-Object { Write-Host $_ }
        $commitCreated = $true

        $headAfter = [string](@(Invoke-Git @("rev-parse", "HEAD"))[0])
        if ($headAfter -eq $headBefore) {
            throw "Git reported success but HEAD did not change."
        }

        $committedPaths = @(Invoke-Git @("diff-tree", "--no-commit-id", "--name-only", "-r", $headAfter))
        Assert-SamePathSet $stagedByScript $committedPaths "Committed"

        $remainingStaged = @(Invoke-Git @("diff", "--cached", "--name-only", "--"))
        if ($remainingStaged.Count -gt 0) {
            throw "The commit completed but staged paths remain: $($remainingStaged -join ', ')."
        }

        Write-Host "Created and verified commit $headAfter"
        Write-Host "Committed paths: $($committedPaths -join ', ')"
    } catch {
        if (-not $commitCreated -and $stagedByScript.Count -gt 0) {
            & git restore --staged -- $stagedByScript 2>$null
        }
        throw
    } finally {
        Pop-Location
    }
}
