function Get-StringSha256([string] $Value) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        $hashBytes = $sha.ComputeHash($bytes)
        return [System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-StreamSha256([System.IO.Stream] $Stream) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha.ComputeHash($Stream)
        return [System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-PackageLaneFingerprints([string] $PackagePath) {
    if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
        throw "Release package not found: $PackagePath"
    }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
    try {
        $laneEntries = @{}
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) {
                continue
            }

            $pathParts = $entry.FullName.Replace("\", "/").Split('/')
            $laneIndex = -1
            for ($index = 0; $index -lt $pathParts.Count; $index++) {
                if ($pathParts[$index] -match "^version-\d+\.\d+$") {
                    $laneIndex = $index
                    break
                }
            }
            if ($laneIndex -lt 0) {
                continue
            }

            $lane = $pathParts[$laneIndex]
            $relativePath = ($pathParts[($laneIndex + 1)..($pathParts.Count - 1)] -join '/')
            $stream = $entry.Open()
            try {
                $entryHash = Get-StreamSha256 $stream
            }
            finally {
                $stream.Dispose()
            }

            if (-not $laneEntries.ContainsKey($lane)) {
                $laneEntries[$lane] = New-Object System.Collections.Generic.List[string]
            }
            $laneEntries[$lane].Add("$relativePath|$($entry.Length)|$entryHash")
        }

        $result = New-Object System.Collections.Generic.List[object]
        foreach ($lane in @($laneEntries.Keys | Sort-Object)) {
            $entries = @($laneEntries[$lane] | Sort-Object)
            $result.Add([ordered]@{
                GameVersion = $lane
                FileCount = [int]$entries.Count
                Sha256 = Get-StringSha256 ($entries -join "`n")
            })
        }
        return [object[]]$result.ToArray()
    }
    finally {
        $archive.Dispose()
    }
}

Export-ModuleMember -Function Get-PackageLaneFingerprints
