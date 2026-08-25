function Get-UnityProjectVersion([string] $ProjectRoot) {
    $projectVersionPath = Join-Path $ProjectRoot "ProjectSettings/ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $projectVersionPath -PathType Leaf)) {
        throw "Unity project version file not found: $projectVersionPath"
    }

    $versionLine = Get-Content -LiteralPath $projectVersionPath | Where-Object {
        $_ -match "^m_EditorVersion:\s*(?<version>\S+)"
    } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($versionLine) -or
            $versionLine -notmatch "^m_EditorVersion:\s*(?<version>\S+)") {
        throw "Cannot read Unity editor version from $projectVersionPath"
    }
    return $Matches["version"]
}

function Resolve-CanonicalUnityPath(
    [string] $ConfiguredPath,
    [string] $EditorVersion,
    [switch] $AllowMissing
) {
    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        $resolvedPath = [System.IO.Path]::GetFullPath($ConfiguredPath)
        if (Test-Path -LiteralPath $resolvedPath -PathType Leaf) {
            return $resolvedPath
        }
        if ($AllowMissing) {
            return $null
        }
        throw "Unity executable not found: $resolvedPath"
    }

    $hubUnityPath = Join-Path "C:\Program Files\Unity\Hub\Editor\$EditorVersion" "Editor/Unity.exe"
    if (Test-Path -LiteralPath $hubUnityPath -PathType Leaf) {
        return $hubUnityPath
    }

    if ($AllowMissing) {
        return $null
    }
    throw "Unity.exe for editor version $EditorVersion was not found. Pass -UnityPath."
}

Export-ModuleMember -Function Get-UnityProjectVersion, Resolve-CanonicalUnityPath
