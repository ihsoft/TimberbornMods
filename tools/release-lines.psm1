Import-Module (Join-Path $PSScriptRoot "release-native-command.psm1") -Force

function Get-ReleaseLineRegistry([string] $ToolsRoot) {
    $registryPath = Join-Path $ToolsRoot "release-lines.json"
    if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) {
        throw "Release-line registry not found: $registryPath"
    }
    $registry = Get-Content -Raw -LiteralPath $registryPath | ConvertFrom-Json
    if ([int]$registry.SchemaVersion -ne 1) {
        throw "Unsupported release-line registry schema: $($registry.SchemaVersion)"
    }
    return $registry
}

function Get-RegisteredReleaseLine([string] $ToolsRoot, [string] $ReleaseLine, [string] $ModName) {
    $registry = Get-ReleaseLineRegistry $ToolsRoot
    $matches = @($registry.CompatibilityLines | Where-Object {
        [string]$_.ReleaseLine -eq $ReleaseLine -and $ModName -in @($_.Mods | ForEach-Object { [string]$_ })
    })
    if ($matches.Count -ne 1) {
        throw "Expected one registered release line $ReleaseLine for $ModName, found $($matches.Count)."
    }
    return $matches[0]
}

function Get-GitWorktrees([string] $RepositoryRoot) {
    $gitResult = Invoke-ReleaseNativeCommand -Command "git" -Arguments @(
        "-C", $RepositoryRoot, "worktree", "list", "--porcelain")
    if ($gitResult.ExitCode -ne 0) {
        throw "Cannot list Git worktrees.`n$($gitResult.Diagnostics -join [Environment]::NewLine)"
    }
    $gitResult.StandardError | ForEach-Object { Write-Warning $_ }
    $lines = $gitResult.Output

    $worktrees = New-Object System.Collections.Generic.List[object]
    $current = $null
    foreach ($line in $lines) {
        if ($line -like "worktree *") {
            if ($null -ne $current) {
                $worktrees.Add($current)
            }
            $current = [pscustomobject]@{
                Path = $line.Substring("worktree ".Length)
                Branch = ""
            }
        }
        elseif ($null -ne $current -and $line -like "branch refs/heads/*") {
            $current.Branch = $line.Substring("branch refs/heads/".Length)
        }
    }
    if ($null -ne $current) {
        $worktrees.Add($current)
    }
    return [object[]]$worktrees.ToArray()
}

function Resolve-RegisteredReleaseWorktree([string] $RepositoryRoot, [string] $Branch) {
    $matches = @(Get-GitWorktrees $RepositoryRoot | Where-Object { [string]$_.Branch -eq $Branch })
    if ($matches.Count -ne 1) {
        throw "Registered release branch $Branch must have exactly one local worktree, found $($matches.Count)."
    }
    if (-not (Test-Path -LiteralPath ([string]$matches[0].Path) -PathType Container)) {
        throw "Registered release worktree is missing: $($matches[0].Path)"
    }
    return (Resolve-Path -LiteralPath ([string]$matches[0].Path)).Path
}

Export-ModuleMember -Function Get-ReleaseLineRegistry, Get-RegisteredReleaseLine, Resolve-RegisteredReleaseWorktree
