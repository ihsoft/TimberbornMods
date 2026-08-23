function Get-ReleaseSection([string] $ChangesPath, [string] $Version) {
    $content = Get-Content -Raw -LiteralPath $ChangesPath
    $escapedVersion = [regex]::Escape($Version)
    $pattern = "(?ms)^#\s+v$escapedVersion[^\r\n]*\r?\n(?<body>.*?)(?=^#\s+v|\z)"
    $match = [regex]::Match($content, $pattern)
    if (-not $match.Success) {
        throw "Changelog section for version $Version was not found in $ChangesPath."
    }

    return $match.Groups["body"].Value
}

function Get-PreviousDatedReleaseVersion([string] $ChangesPath, [string] $Version) {
    $content = Get-Content -Raw -LiteralPath $ChangesPath
    $headings = [regex]::Matches(
        $content,
        "(?m)^#\s+v(?<version>\d+\.\d+\.\d+|\d+\.\d+)(?:\s+\((?<date>[^)]+)\))?")
    $currentIndex = -1
    for ($index = 0; $index -lt $headings.Count; $index++) {
        if ($headings[$index].Groups["version"].Value -eq $Version) {
            $currentIndex = $index
            break
        }
    }

    if ($currentIndex -lt 0) {
        throw "Cannot locate changelog heading for version $Version in $ChangesPath."
    }

    for ($index = $currentIndex + 1; $index -lt $headings.Count; $index++) {
        $date = $headings[$index].Groups["date"].Value
        if (-not [string]::IsNullOrWhiteSpace($date) -and $date -ne "TBD") {
            return $headings[$index].Groups["version"].Value
        }
    }

    return $null
}

function Assert-ReleaseVersionMatchesChangelog(
    [string] $ChangesPath,
    [string] $Version,
    [ValidateSet("CurrentLine", "BackportPatchOnly")]
    [string] $VersionPolicy
) {
    $section = Get-ReleaseSection $ChangesPath $Version
    $previous = Get-PreviousDatedReleaseVersion $ChangesPath $Version

    if ($VersionPolicy -eq "BackportPatchOnly") {
        if ($null -eq $previous) {
            throw "Cannot validate backport version $Version because no previous dated release was found in $ChangesPath."
        }

        $previousParts = $previous.Split('.') | ForEach-Object { [int]$_ }
        $expected = "$($previousParts[0]).$($previousParts[1]).$($previousParts[2] + 1)"
        if ($Version -ne $expected) {
            throw "Backport release version $Version is invalid for $ChangesPath. BackportPatchOnly requires $expected after $previous."
        }
        return
    }

    $hasFeature = $section -match "(?m)^\*\s+\[Feature(?:\s+#\d+)?\]"
    if (-not $hasFeature) {
        return
    }

    if ($null -eq $previous) {
        if ($Version -eq "1.0.0") {
            return
        }
        throw "Cannot validate feature version $Version because no previous dated release was found in $ChangesPath."
    }

    $previousParts = $previous.Split('.') | ForEach-Object { [int]$_ }
    $expected = "$($previousParts[0]).$($previousParts[1] + 1).0"
    if ($Version -ne $expected) {
        throw "Feature release version $Version is invalid for $ChangesPath. A [Feature] entry requires the next minor version $expected after $previous."
    }
}

Export-ModuleMember -Function Assert-ReleaseVersionMatchesChangelog
