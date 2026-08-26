function ConvertTo-ReleaseFolderList([object] $Value) {
    if ($null -eq $Value) {
        return [string[]]@()
    }
    return [string[]]@(
        @($Value) |
            ForEach-Object { [string]$_ -split "," } |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Get-ReleaseAllowMissingXmlFolders([object] $ReleaseConfig, [string] $Context) {
    $folders = ConvertTo-ReleaseFolderList $ReleaseConfig.Package.AllowMissingXmlFolders
    $duplicates = @($folders | Group-Object | Where-Object Count -gt 1)
    if ($duplicates.Count -gt 0) {
        throw "$Context contains duplicate Package.AllowMissingXmlFolders entries: $($duplicates.Name -join ', ')."
    }
    foreach ($folder in $folders) {
        if ($folder -notmatch "^version-\d+\.\d+$") {
            throw "$Context contains an invalid Package.AllowMissingXmlFolders lane: $folder"
        }
        $manifestVersions = $ReleaseConfig.ManifestVersions
        $laneDeclared = if ($manifestVersions -is [System.Collections.IDictionary]) {
            $manifestVersions.Contains($folder)
        }
        else {
            $null -ne $manifestVersions.PSObject.Properties[$folder]
        }
        if (-not $laneDeclared) {
            throw "$Context exempts $folder, but ManifestVersions does not declare that lane."
        }
    }
    return [string[]]@($folders | Sort-Object)
}

function Assert-ReleaseAllowMissingXmlFolders(
    [string[]] $ExpectedFolders,
    [object] $ReleaseConfig,
    [string] $Context
) {
    $expected = [string[]]@(ConvertTo-ReleaseFolderList $ExpectedFolders | Sort-Object)
    $actual = [string[]]@(Get-ReleaseAllowMissingXmlFolders $ReleaseConfig $Context)
    if (($expected -join "|") -ne ($actual -join "|")) {
        throw (
            "$Context Package.AllowMissingXmlFolders differs from the bound policy. " +
            "Expected [$($expected -join ', ')], actual [$($actual -join ', ')].")
    }
    return $actual
}

function Test-ReleaseMissingXmlAllowed([string[]] $AllowMissingXmlFolders, [string] $Lane) {
    return $Lane -in @(ConvertTo-ReleaseFolderList $AllowMissingXmlFolders)
}

Export-ModuleMember -Function `
    ConvertTo-ReleaseFolderList, `
    Get-ReleaseAllowMissingXmlFolders, `
    Assert-ReleaseAllowMissingXmlFolders, `
    Test-ReleaseMissingXmlAllowed
