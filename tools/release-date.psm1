function Get-ChangelogHeadingDate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Heading
    )

    $match = [regex]::Match($Heading, "\((?<date>[^()]+)\)\s*:?\s*$")
    if (-not $match.Success -or $match.Groups["date"].Value -eq "TBD") {
        throw "Changelog heading does not contain a concrete release date: $Heading"
    }

    $dateText = $match.Groups["date"].Value -replace "(?<=\d)(st|nd|rd|th)\b", ""
    $parsedDate = [datetime]::MinValue
    $parsed = [datetime]::TryParse(
        $dateText,
        [System.Globalization.CultureInfo]::GetCultureInfo("en-US"),
        [System.Globalization.DateTimeStyles]::AllowWhiteSpaces,
        [ref]$parsedDate)
    if (-not $parsed) {
        throw "Cannot parse changelog release date from heading: $Heading"
    }

    return $parsedDate.Date
}

function Get-ReleaseDateRecords {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object[]] $ReleaseSources,

        [datetime] $CurrentLocalDate = (Get-Date)
    )

    return [object[]]@($ReleaseSources | ForEach-Object {
        $changelogDate = Get-ChangelogHeadingDate ([string]$_.ChangelogHeading)
        [pscustomobject]@{
            TagName = [string]$_.TagName
            ChangelogDate = $changelogDate
            CurrentLocalDate = $CurrentLocalDate.Date
            DateDiffers = $changelogDate -ne $CurrentLocalDate.Date
        }
    })
}

Export-ModuleMember -Function Get-ChangelogHeadingDate, Get-ReleaseDateRecords
