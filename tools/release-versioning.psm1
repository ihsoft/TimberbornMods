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

function Get-PreviousDatedReleaseVersion(
    [string] $ChangesPath,
    [string] $Version,
    [string] $GameLine = ""
) {
    $content = Get-Content -Raw -LiteralPath $ChangesPath
    $headings = [regex]::Matches(
        $content,
        "(?m)^#\s+v(?<version>\d+\.\d+\.\d+|\d+\.\d+)(?<suffix>[^\r\n]*)\r?$")
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

    $candidateParts = $Version.Split('.')
    $candidatePatchLine = if ($candidateParts.Count -ge 2) {
        "$($candidateParts[0]).$($candidateParts[1])."
    }
    else {
        ""
    }
    for ($index = $currentIndex + 1; $index -lt $headings.Count; $index++) {
        $suffix = $headings[$index].Groups["suffix"].Value
        $headingVersion = $headings[$index].Groups["version"].Value
        $matchesGameLine = [string]::IsNullOrWhiteSpace($GameLine) -or
            $suffix -match "\(Timberborn\s+v$([regex]::Escape($GameLine))\)" -or
            (-not [string]::IsNullOrWhiteSpace($candidatePatchLine) -and
                $headingVersion.StartsWith($candidatePatchLine, [System.StringComparison]::Ordinal))
        $hasDatedQualifier = $suffix -match "\([^)]+\)" -and $suffix -notmatch "\(TBD\)"
        if ($matchesGameLine -and $hasDatedQualifier) {
            return $headingVersion
        }
    }

    return $null
}

function Assert-ReleaseVersionMatchesChangelog(
    [string] $ChangesPath,
    [string] $Version,
    [ValidateSet("CurrentLine", "BackportPatchOnly")]
    [string] $VersionPolicy,
    [string] $PreviousSourceModVersion = ""
) {
    $section = Get-ReleaseSection $ChangesPath $Version
    $previous = if ([string]::IsNullOrWhiteSpace($PreviousSourceModVersion)) {
        Get-PreviousDatedReleaseVersion $ChangesPath $Version
    }
    else {
        $PreviousSourceModVersion
    }

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

Export-ModuleMember -Function Get-PreviousDatedReleaseVersion, Assert-ReleaseVersionMatchesChangelog
