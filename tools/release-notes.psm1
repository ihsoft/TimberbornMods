function Get-ReleaseChangeNotes([string] $ChangesPath, [string] $Version) {
    $content = Get-Content -Raw -LiteralPath $ChangesPath
    $escapedVersion = [regex]::Escape($Version)
    $pattern = "(?ms)^#\s+v$escapedVersion[^\r\n]*\r?\n(?<body>.*?)(?=^#\s+v|\z)"
    $match = [regex]::Match($content, $pattern)
    if (-not $match.Success) {
        throw "Cannot find changelog section for v$Version in $ChangesPath."
    }

    $notes = $match.Groups["body"].Value.Trim()
    if ([string]::IsNullOrWhiteSpace($notes)) {
        throw "Changelog section for v$Version is empty in $ChangesPath."
    }
    return $notes
}

function Get-ReleaseLineDisplayName([object] $Source) {
    $gameVersion = [string]$Source.GameVersion
    if ($gameVersion -match "^version-(?<version>\d+\.\d+)$") {
        return "Update $($Matches['version'])"
    }
    return $gameVersion
}

function Get-CombinedReleaseNotes([object[]] $ReleaseSources) {
    if ($ReleaseSources.Count -eq 0) {
        throw "At least one release source is required."
    }
    if ($ReleaseSources.Count -eq 1) {
        return Get-ReleaseChangeNotes ([string]$ReleaseSources[0].ChangesPath) `
            ([string]$ReleaseSources[0].ModVersion)
    }

    $sections = @($ReleaseSources | ForEach-Object {
        $lineName = Get-ReleaseLineDisplayName $_
        $version = [string]$_.ModVersion
        $notes = Get-ReleaseChangeNotes ([string]$_.ChangesPath) $version
        "## $lineName - v$version`r`n`r`n$notes"
    })
    return $sections -join "`r`n`r`n"
}

Export-ModuleMember -Function Get-CombinedReleaseNotes, Get-ReleaseChangeNotes
