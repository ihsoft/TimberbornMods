function Resolve-RepositoryRoot([string] $ConfiguredRoot, [string] $DefaultRoot) {
    $candidate = $ConfiguredRoot
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $candidate = $DefaultRoot
    }
    elseif (-not [System.IO.Path]::IsPathRooted($candidate)) {
        $candidate = Join-Path $DefaultRoot $candidate
    }

    if (-not (Test-Path -LiteralPath $candidate -PathType Container)) {
        throw "Repository root not found: $candidate"
    }

    $resolvedRoot = (Resolve-Path -LiteralPath $candidate).Path
    $gitRoot = @(& git -C $resolvedRoot rev-parse --show-toplevel 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Repository root is not a Git worktree: $resolvedRoot`n$($gitRoot -join [Environment]::NewLine)"
    }

    $resolvedGitRoot = (Resolve-Path -LiteralPath ([string]$gitRoot[0])).Path
    if ($resolvedGitRoot -ne $resolvedRoot) {
        throw "Configured repository root resolves inside another worktree: $resolvedRoot (Git root: $resolvedGitRoot)"
    }

    return $resolvedRoot
}

function Resolve-RepositoryPath([string] $RepositoryRoot, [string] $Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }
    return [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
}

Export-ModuleMember -Function Resolve-RepositoryRoot, Resolve-RepositoryPath
