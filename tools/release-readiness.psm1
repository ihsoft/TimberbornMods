function Get-ReadinessProperty([object] $Object, [string] $Name) {
    if ($null -eq $Object) {
        return $null
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }
    return $property.Value
}

function Add-ReadinessPlannedChange(
    [System.Collections.Generic.List[object]] $Changes,
    [string] $Field,
    [object] $CurrentValue,
    [object] $DesiredValue,
    [string] $Reason
) {
    if ([string]$CurrentValue -eq [string]$DesiredValue) {
        return
    }
    $Changes.Add([ordered]@{
        Field = $Field
        CurrentValue = $CurrentValue
        DesiredValue = $DesiredValue
        Reason = $Reason
    })
}

function Get-ReadinessDesiredManifestVersions(
    [object] $PublicReleaseConfig,
    [string] $CandidateGameVersion,
    [string] $CandidateVersion
) {
    $desired = [ordered]@{}
    $publicVersions = Get-ReadinessProperty $PublicReleaseConfig "ManifestVersions"
    if ($null -ne $publicVersions) {
        foreach ($property in $publicVersions.PSObject.Properties | Sort-Object Name) {
            $desired[[string]$property.Name] = if ([string]$property.Name -eq $CandidateGameVersion) {
                $CandidateVersion
            }
            else {
                [string]$property.Value
            }
        }
    }
    if (-not $desired.Contains($CandidateGameVersion)) {
        $desired[$CandidateGameVersion] = $CandidateVersion
    }
    return $desired
}

function Invoke-ReadinessGit([string] $RepositoryRoot, [string[]] $Arguments, [switch] $AllowFailure) {
    $result = Invoke-ReleaseNativeCommand -Command "git" -Arguments (@("-C", $RepositoryRoot) + $Arguments)
    if ($result.ExitCode -ne 0 -and -not $AllowFailure) {
        throw "git $($Arguments -join ' ') failed.`n$($result.Diagnostics -join [Environment]::NewLine)"
    }
    return $result
}

function Get-ReadinessChangelogIdentity([string] $ChangesPath, [string] $SourceModVersion) {
    if (-not (Test-Path -LiteralPath $ChangesPath -PathType Leaf)) {
        return [ordered]@{
            Path = $ChangesPath
            Heading = ""
            Version = ""
            Date = ""
            IsPending = $false
            Exists = $false
        }
    }

    $heading = Get-Content -LiteralPath $ChangesPath | Where-Object {
        $_ -match "^#\s+v?(?<version>\d+\.\d+(?:\.\d+)?)(?:\s|\(|:|$)"
    } | Select-Object -First 1
    $version = ""
    $date = ""
    $isPending = $false
    if (-not [string]::IsNullOrWhiteSpace($heading)) {
        [void]($heading -match "^#\s+v?(?<version>\d+\.\d+(?:\.\d+)?)")
        $version = $Matches["version"]
        $isPending = $heading -match "\(TBD\)"
        if (-not $isPending) {
            $dateMatches = [regex]::Matches($heading, "\((?<date>[^)]+)\)")
            if ($dateMatches.Count -gt 0) {
                $date = $dateMatches.Item($dateMatches.Count - 1).Groups["date"].Value
            }
        }
    }

    return [ordered]@{
        Path = (Resolve-Path -LiteralPath $ChangesPath).Path
        Heading = [string]$heading
        Version = $version
        Date = $date
        IsPending = [bool]$isPending
        Exists = $true
        MatchesSourceModVersion = $version -eq $SourceModVersion
    }
}

function Get-ReadinessPublicPackageLane([object] $ReleaseConfig, [string] $FallbackGameVersion) {
    $sourceModVersion = [string](Get-ReadinessProperty $ReleaseConfig "ReleaseVersion")
    $manifestVersions = Get-ReadinessProperty $ReleaseConfig "ManifestVersions"
    if ($null -eq $manifestVersions) {
        return [ordered]@{ GameVersion = $FallbackGameVersion; ManifestVersion = $sourceModVersion }
    }

    $lanes = @($manifestVersions.PSObject.Properties | ForEach-Object {
        if ($_.Name -notmatch "^version-(?<version>\d+\.\d+(?:\.\d+)?)$") {
            throw "Unsupported ManifestVersions lane name: $($_.Name)"
        }
        [pscustomobject]@{
            GameVersion = [string]$_.Name
            SortVersion = [version]$Matches["version"]
            ManifestVersion = [string]$_.Value
        }
    } | Sort-Object SortVersion)
    if ($lanes.Count -eq 0) {
        throw "ManifestVersions contains no compatibility lanes."
    }
    return [ordered]@{
        GameVersion = [string]$lanes[-1].GameVersion
        ManifestVersion = [string]$lanes[-1].ManifestVersion
    }
}

function Get-ReadinessPackageLanes(
    [string] $SourcePath,
    [string] $ArtifactPath,
    [string] $PackageMode,
    [object] $ReleaseConfig
) {
    $configured = Get-ReadinessProperty $ReleaseConfig "ManifestVersions"
    $lanes = New-Object System.Collections.Generic.List[object]
    if ($null -eq $configured) {
        return [object[]]$lanes.ToArray()
    }

    $sourcePathConfigured = -not [string]::IsNullOrWhiteSpace($SourcePath)
    $artifactFingerprints = [object[]]@()
    $artifactInspectionError = ""
    if ($PackageMode -eq "ExistingZip" -and
            -not [string]::IsNullOrWhiteSpace($ArtifactPath) -and
            (Test-Path -LiteralPath $ArtifactPath -PathType Leaf)) {
        try {
            $artifactFingerprints = [object[]]@(Get-PackageLaneFingerprints $ArtifactPath)
        }
        catch {
            $artifactInspectionError = $_.Exception.Message
        }
    }
    foreach ($property in $configured.PSObject.Properties | Sort-Object Name) {
        $manifestPath = if ($sourcePathConfigured) {
            Join-Path $SourcePath "$($property.Name)/manifest.json"
        }
        else {
            ""
        }
        $actualVersion = ""
        $sourceManifestVersion = ""
        $manifestEvidence = "Unavailable"
        $artifactFingerprint = @($artifactFingerprints | Where-Object {
            [string]$_.GameVersion -eq [string]$property.Name
        } | Select-Object -First 1)
        $sourceManifestExists = $sourcePathConfigured -and
            (Test-Path -LiteralPath $manifestPath -PathType Leaf)
        $manifestExists = $sourceManifestExists
        if ($sourceManifestExists) {
            $manifestEvidence = "SourceFolder"
            try {
                $actualVersion = [string]((Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json).Version)
            }
            catch {
                $actualVersion = "(invalid JSON)"
            }
            $sourceManifestVersion = $actualVersion
        }
        elseif ($artifactFingerprint.Count -eq 1) {
            $manifestExists = $true
            $manifestEvidence = "PackageArtifact"
            $actualVersion = [string]$artifactFingerprint[0].ManifestVersion
        }
        elseif (-not [string]::IsNullOrWhiteSpace($artifactInspectionError)) {
            $manifestEvidence = "PackageArtifactInspectionFailed"
        }
        $lanes.Add([ordered]@{
            GameVersion = [string]$property.Name
            ExpectedManifestVersion = [string]$property.Value
            SourceManifestPath = $manifestPath
            SourceManifestExists = $sourceManifestExists
            SourceManifestVersion = $sourceManifestVersion
            ManifestExists = $manifestExists
            ManifestVersion = $actualVersion
            ManifestEvidence = $manifestEvidence
            InspectionError = $artifactInspectionError
            MatchesExpected = $actualVersion -eq [string]$property.Value
        })
    }
    return [object[]]$lanes.ToArray()
}

function Get-ReadinessProbeResults(
    [string] $ToolsRoot,
    [string] $ReleaseRoot,
    [string] $ModName,
    [string] $UnityPath,
    [string] $ModIoAccessTokenPath,
    [object] $ProbeOverride
) {
    if ($null -ne $ProbeOverride) {
        return $ProbeOverride
    }

    $projectRoot = Join-Path $ReleaseRoot "ModsUnityProject"
    $unityVersion = ""
    $canonicalEditor = $null
    $unityError = ""
    try {
        $unityVersion = Get-UnityProjectVersion $projectRoot
        $canonicalEditor = if ([string]::IsNullOrWhiteSpace($UnityPath)) {
            Resolve-CanonicalUnityPath -EditorVersion $unityVersion -AllowMissing
        }
        else {
            $configuredUnityPath = if ([System.IO.Path]::IsPathRooted($UnityPath)) {
                $UnityPath
            }
            else {
                Join-Path $ReleaseRoot $UnityPath
            }
            Resolve-CanonicalUnityPath -ConfiguredPath $configuredUnityPath -EditorVersion $unityVersion -AllowMissing
        }
    }
    catch {
        $unityError = $_.Exception.Message
    }

    $ghCommand = Get-Command gh -ErrorAction SilentlyContinue
    $ghAuthResult = if ($null -eq $ghCommand) {
        [pscustomobject]@{ ExitCode = -1; Output = [string[]]@(); Diagnostics = [string[]]@("gh not found") }
    }
    else {
        Invoke-ReleaseNativeCommand -Command "gh" -Arguments @("auth", "status", "--hostname", "github.com")
    }
    $ghApiResult = if ($null -eq $ghCommand) {
        [pscustomobject]@{ ExitCode = -1; Output = [string[]]@(); Diagnostics = [string[]]@("gh not found") }
    }
    else {
        Invoke-ReleaseNativeCommand -Command "gh" -Arguments @("api", "user", "--jq", ".login")
    }
    $ghAccount = if ($ghApiResult.ExitCode -eq 0 -and $ghApiResult.Output.Count -gt 0) {
        [string]$ghApiResult.Output[0]
    }
    else {
        ""
    }
    $steamConfigPath = Join-Path $ToolsRoot ".tools/steam/steam.local.json"
    $modIoConfigPath = Join-Path $ToolsRoot ".tools/modio/$ModName.local.json"
    $defaultModIoTokenArgument = ".tools/modio/$ModName.token.txt"
    $modIoTokenArgument = if ([string]::IsNullOrWhiteSpace($ModIoAccessTokenPath)) {
        $defaultModIoTokenArgument
    }
    else {
        $ModIoAccessTokenPath
    }
    $modIoTokenPath = if ([System.IO.Path]::IsPathRooted($modIoTokenArgument)) {
        [System.IO.Path]::GetFullPath($modIoTokenArgument)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $ToolsRoot $modIoTokenArgument))
    }
    $modIoTokenSelection = if ([string]::IsNullOrWhiteSpace($ModIoAccessTokenPath)) {
        "PreferredPerModOwnerToken"
    }
    elseif ($modIoTokenPath -eq [System.IO.Path]::GetFullPath((
            Join-Path $ToolsRoot $defaultModIoTokenArgument))) {
        "ExplicitPerModOwnerToken"
    }
    else {
        "ExplicitAlternateOwnerToken"
    }
    $modIoCredentialRoot = [System.IO.Path]::GetFullPath(
        (Join-Path $ToolsRoot ".tools/modio")).TrimEnd('\', '/')
    $modIoCredentialRootPrefix = $modIoCredentialRoot + [System.IO.Path]::DirectorySeparatorChar
    $modIoTokenPathIsPortable = $modIoTokenPath.StartsWith(
        $modIoCredentialRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)
    $toolsRootPath = [System.IO.Path]::GetFullPath($ToolsRoot).TrimEnd('\', '/')
    $toolsRootPrefix = $toolsRootPath + [System.IO.Path]::DirectorySeparatorChar
    $modIoTokenRuntimeArgument = if ($modIoTokenPathIsPortable) {
        $modIoTokenPath.Substring($toolsRootPrefix.Length).Replace('\', '/')
    }
    else {
        ""
    }
    $modIoTokenPathScope = if ($modIoTokenPathIsPortable) {
        "RepositoryRelative"
    }
    else {
        "UnsupportedExternalPath"
    }
    $steamCmdPath = ""
    $steamUserName = ""
    if (Test-Path -LiteralPath $steamConfigPath -PathType Leaf) {
        try {
            $steamConfig = Get-Content -Raw -LiteralPath $steamConfigPath | ConvertFrom-Json
            $steamCmdPath = [string]$steamConfig.SteamCmdPath
            $steamUserName = [string]$steamConfig.UserName
        }
        catch {
            $steamCmdPath = ""
        }
    }
    $modIoId = ""
    $modIoGameId = ""
    $modIoApiBase = ""
    if (Test-Path -LiteralPath $modIoConfigPath -PathType Leaf) {
        try {
            $modIoConfig = Get-Content -Raw -LiteralPath $modIoConfigPath | ConvertFrom-Json
            $modIoId = [string]$modIoConfig.ModId
            $modIoGameId = [string]$modIoConfig.GameId
            $modIoApiBase = [string]$modIoConfig.ApiBase
        }
        catch {
            $modIoId = ""
        }
    }
    return [ordered]@{
        Unity = [ordered]@{
            ProjectVersion = $unityVersion
            CanonicalEditorPath = [string]$canonicalEditor
            Ready = -not [string]::IsNullOrWhiteSpace([string]$canonicalEditor)
            Error = $unityError
        }
        GitHub = [ordered]@{
            CliAvailable = $null -ne $ghCommand
            AuthReady = $ghAuthResult.ExitCode -eq 0 -and $ghApiResult.ExitCode -eq 0
            AuthStatusExitCode = [int]$ghAuthResult.ExitCode
            ApiUserExitCode = [int]$ghApiResult.ExitCode
            Account = $ghAccount
            EvidenceScope = "CurrentProcessCredentialAccessBoundary"
            StoredCredentialValidity = if ($ghAuthResult.ExitCode -eq 0 -and $ghApiResult.ExitCode -eq 0) {
                "ConfirmedInExecutionBoundary"
            }
            else {
                "Unknown"
            }
            CredentialMutationRecommended = $false
        }
        Steam = [ordered]@{
            ConfigPath = $steamConfigPath
            ConfigExists = Test-Path -LiteralPath $steamConfigPath -PathType Leaf
            SteamCmdPath = $steamCmdPath
            SteamCmdExists = -not [string]::IsNullOrWhiteSpace($steamCmdPath) -and
                (Test-Path -LiteralPath $steamCmdPath -PathType Leaf)
            UserNameConfigured = -not [string]::IsNullOrWhiteSpace($steamUserName)
            DesktopClientRunning = @(Get-Process steam -ErrorAction SilentlyContinue).Count -gt 0
            UploadLoginMode = "SteamCMDConfiguredUser"
            ExitCommand = "quit"
            ExplicitLogoutCommand = $false
        }
        ModIO = [ordered]@{
            ConfigPath = $modIoConfigPath
            ConfigExists = Test-Path -LiteralPath $modIoConfigPath -PathType Leaf
            AccessTokenPath = $modIoTokenPath
            AccessTokenArgument = $modIoTokenRuntimeArgument
            AccessTokenSelection = $modIoTokenSelection
            AccessTokenPathScope = $modIoTokenPathScope
            AccessTokenExists = $modIoTokenPathIsPortable -and
                (Test-Path -LiteralPath $modIoTokenPath -PathType Leaf) -and
                (Get-Item -LiteralPath $modIoTokenPath).Length -gt 0
            ModId = $modIoId
            GameId = $modIoGameId
            ApiBaseConfigured = -not [string]::IsNullOrWhiteSpace($modIoApiBase)
        }
    }
}

function Get-ReleaseReadinessReport {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ToolsRoot,

        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string] $ModName,

        [string] $ReleaseLine = "",
        [string] $UnityPath = "",
        [string] $ModIoAccessTokenPath = "",
        [object] $ProbeOverride = $null
    )

    $toolsRoot = (Resolve-Path -LiteralPath $ToolsRoot).Path
    $repositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $releaseRoot = $repositoryRoot
    $gameVersion = "version-1.1"
    $versionPolicy = "CurrentLine"
    $branch = [string](Invoke-ReadinessGit $releaseRoot @("branch", "--show-current")).Output[0]
    if (-not [string]::IsNullOrWhiteSpace($ReleaseLine)) {
        $registered = Get-RegisteredReleaseLine (Join-Path $toolsRoot "tools") $ReleaseLine $ModName
        $releaseRoot = Resolve-RegisteredReleaseWorktree $repositoryRoot ([string]$registered.Branch)
        $branch = [string]$registered.Branch
        $gameVersion = [string]$registered.GameVersion
        $versionPolicy = [string]$registered.VersionPolicy
    }

    $blockers = New-Object System.Collections.Generic.List[object]
    $warnings = New-Object System.Collections.Generic.List[string]
    $releaseConfigPath = Join-Path $releaseRoot "$ModName/release.json"
    if (-not (Test-Path -LiteralPath $releaseConfigPath -PathType Leaf)) {
        throw "Release config not found: $releaseConfigPath"
    }
    $releaseConfig = Get-Content -Raw -LiteralPath $releaseConfigPath | ConvertFrom-Json
    $configuredSourceModVersion = [string](Get-ReadinessProperty $releaseConfig "ReleaseVersion")
    $configReadyForPublish = Get-ReadinessProperty $releaseConfig "ReadyForPublish"
    $publicReleaseConfigPath = Join-Path $repositoryRoot "$ModName/release.json"
    if (-not (Test-Path -LiteralPath $publicReleaseConfigPath -PathType Leaf)) {
        throw "Public release config not found: $publicReleaseConfigPath"
    }
    $publicReleaseConfig = Get-Content -Raw -LiteralPath $publicReleaseConfigPath | ConvertFrom-Json
    $publicLane = Get-ReadinessPublicPackageLane $publicReleaseConfig "version-1.1"
    $changesPathValue = [string](Get-ReadinessProperty $releaseConfig "ChangesPath")
    $changesPath = if ([string]::IsNullOrWhiteSpace($changesPathValue)) {
        ""
    }
    else {
        Join-Path $releaseRoot $changesPathValue
    }
    $changelog = if ([string]::IsNullOrWhiteSpace($changesPath)) {
        [ordered]@{
            Path = ""
            Heading = ""
            Version = ""
            Date = ""
            IsPending = $false
            Exists = $false
            MatchesSourceModVersion = $false
        }
    }
    else {
        Get-ReadinessChangelogIdentity $changesPath $configuredSourceModVersion
    }
    $sourceModVersion = if ($changelog.Exists -and -not [string]::IsNullOrWhiteSpace([string]$changelog.Version)) {
        [string]$changelog.Version
    }
    else {
        $configuredSourceModVersion
    }
    $changelog.MatchesSourceModVersion = $changelog.Exists -and
        [string]$changelog.Version -eq $sourceModVersion
    $changelog["MatchesReleaseMetadataVersion"] = $changelog.Exists -and
        [string]$changelog.Version -eq $configuredSourceModVersion
    $candidateGameLine = $gameVersion -replace "^version-", ""
    $previousGameLine = if ($versionPolicy -eq "BackportPatchOnly") { $candidateGameLine } else { "" }
    $previousSourceModVersion = if ($changelog.Exists) {
        [string](Get-PreviousDatedReleaseVersion $changesPath $sourceModVersion $previousGameLine)
    }
    else {
        ""
    }
    $plannedMetadataChanges = New-Object System.Collections.Generic.List[object]

    if ([string]::IsNullOrWhiteSpace($sourceModVersion)) {
        $blockers.Add([ordered]@{ Code = "CandidateVersionMissing"; Message = "Candidate version is unavailable." })
    }
    if ($null -ne $configReadyForPublish -and $configReadyForPublish -ne $true) {
        Add-ReadinessPlannedChange `
            $plannedMetadataChanges "ReadyForPublish" $configReadyForPublish $true `
            "Gate 1 prepares release metadata before immutable preflight."
    }
    if ([string]::IsNullOrWhiteSpace($changesPathValue)) {
        $blockers.Add([ordered]@{ Code = "ChangelogPathMissing"; Message = "ChangesPath is empty." })
    }
    elseif (-not $changelog.Exists) {
        $blockers.Add([ordered]@{ Code = "ChangelogMissing"; Message = "Changelog is missing: $changesPath" })
    }
    elseif ($versionPolicy -eq "BackportPatchOnly" -and
            [string]$changelog.Heading -notmatch "\(Timberborn\s+v$([regex]::Escape($candidateGameLine))\)") {
        $blockers.Add([ordered]@{
            Code = "ChangelogGameLineMismatch"
            Message = "Top changelog heading is not qualified for Timberborn v$candidateGameLine."
        })
    }

    if ($changelog.Exists -and $changelog.IsPending) {
        Add-ReadinessPlannedChange `
            $plannedMetadataChanges "Changelog.Date" "TBD" (Get-Date).ToString("yyyy-MM-dd") `
            "Gate 1 dates and commits the candidate before immutable preflight."
    }
    if ($versionPolicy -eq "BackportPatchOnly" -and
            [string]::IsNullOrWhiteSpace($previousSourceModVersion)) {
        $blockers.Add([ordered]@{
            Code = "PreviousLineReleaseMissing"
            Message = "No previous dated Timberborn v$candidateGameLine release was found in the changelog."
        })
    }

    $manifestPathValue = [string](Get-ReadinessProperty $releaseConfig "ManifestPath")
    $manifestPath = if ([string]::IsNullOrWhiteSpace($manifestPathValue)) {
        ""
    }
    else {
        Join-Path $releaseRoot $manifestPathValue
    }
    if ([string]::IsNullOrWhiteSpace($manifestPathValue)) {
        $blockers.Add([ordered]@{ Code = "ManifestPathMissing"; Message = "ManifestPath is empty." })
    }
    elseif (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        $blockers.Add([ordered]@{ Code = "ManifestMissing"; Message = "Release manifest is missing: $manifestPath" })
    }

    Add-ReadinessPlannedChange `
        $plannedMetadataChanges "ReleaseVersion" $configuredSourceModVersion $sourceModVersion `
        "Candidate identity comes from the target worktree's top changelog heading."
    $configuredPreviousSourceModVersion = [string](
        Get-ReadinessProperty $releaseConfig "PreviousSourceModVersion")
    if ($versionPolicy -eq "BackportPatchOnly") {
        Add-ReadinessPlannedChange `
            $plannedMetadataChanges "PreviousSourceModVersion" `
            $configuredPreviousSourceModVersion $previousSourceModVersion `
            "Previous source identity comes from the latest dated release for the target game line."
    }
    $desiredManifestVersions = Get-ReadinessDesiredManifestVersions `
        $publicReleaseConfig $gameVersion $sourceModVersion
    $configuredManifestVersions = Get-ReadinessProperty $releaseConfig "ManifestVersions"
    foreach ($desiredManifest in $desiredManifestVersions.GetEnumerator()) {
        $configuredManifestVersion = if ($null -eq $configuredManifestVersions) {
            ""
        }
        else {
            [string](Get-ReadinessProperty $configuredManifestVersions ([string]$desiredManifest.Key))
        }
        Add-ReadinessPlannedChange `
            $plannedMetadataChanges "ManifestVersions.$($desiredManifest.Key)" `
            $configuredManifestVersion ([string]$desiredManifest.Value) `
            $(if ([string]$desiredManifest.Key -eq $gameVersion) {
                "Target compatibility lane receives the pending candidate."
            }
            else {
                "Public lanes follow the current public package owner."
            })
    }
    $publicSteamPublishedFileId = [string](
        Get-ReadinessProperty $publicReleaseConfig.Steam "PublishedFileId")
    $configuredSteamPublishedFileId = [string](
        Get-ReadinessProperty $releaseConfig.Steam "PublishedFileId")
    Add-ReadinessPlannedChange `
        $plannedMetadataChanges "Steam.PublishedFileId" `
        $configuredSteamPublishedFileId $publicSteamPublishedFileId `
        "Public platform identity belongs to the current public package owner."

    $packageOwnerConfig = if ([string]::IsNullOrWhiteSpace($ReleaseLine)) {
        $releaseConfig
    }
    else {
        $publicReleaseConfig
    }
    $packageOwnerRoot = if ([string]::IsNullOrWhiteSpace($ReleaseLine)) {
        $releaseRoot
    }
    else {
        $repositoryRoot
    }
    $packageOwnerConfigPath = Join-Path $packageOwnerRoot "$ModName/release.json"
    $packageOwnerConfigSha256 = if (Test-Path -LiteralPath $packageOwnerConfigPath -PathType Leaf) {
        (Get-FileHash -LiteralPath $packageOwnerConfigPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    else {
        ""
    }
    $publicManifestPathValue = [string](Get-ReadinessProperty $packageOwnerConfig "ManifestPath")
    $authoritativeWorkshopDataValue = if ([string]::IsNullOrWhiteSpace($publicManifestPathValue)) {
        ""
    }
    else {
        Join-Path (Split-Path -Parent $publicManifestPathValue) "Data/workshop_data.json"
    }
    $authoritativeWorkshopDataPath = if ([string]::IsNullOrWhiteSpace($authoritativeWorkshopDataValue)) {
        ""
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $packageOwnerRoot $authoritativeWorkshopDataValue))
    }
    $authoritativeWorkshopDataExists = -not [string]::IsNullOrWhiteSpace($authoritativeWorkshopDataPath) -and
        (Test-Path -LiteralPath $authoritativeWorkshopDataPath -PathType Leaf)
    $authoritativeWorkshopDataSha256 = ""
    $authoritativeWorkshopDataTags = @()
    $authoritativeWorkshopDataParseReady = $false
    $authoritativeWorkshopTagIntentDeclared = $false
    if ($authoritativeWorkshopDataExists) {
        $authoritativeWorkshopDataSha256 = (
            Get-FileHash -LiteralPath $authoritativeWorkshopDataPath -Algorithm SHA256).Hash.ToLowerInvariant()
        try {
            $authoritativeWorkshopData = Get-Content -Raw -LiteralPath $authoritativeWorkshopDataPath |
                ConvertFrom-Json
            $authoritativeWorkshopTagIntentDeclared = $null -ne
                $authoritativeWorkshopData.PSObject.Properties["Tags"]
            $authoritativeWorkshopDataTags = [string[]]@(
                $authoritativeWorkshopData.Tags | ForEach-Object { [string]$_ })
            $authoritativeWorkshopDataParseReady = $true
            if (-not $authoritativeWorkshopTagIntentDeclared) {
                $blockers.Add([ordered]@{
                    Code = "AuthoritativeWorkshopTagIntentMissing"
                    Message = (
                        "Public-owner workshop metadata does not declare Tags. An absent generated tag list " +
                        "cannot authorize removal of public taxonomy tags.")
                })
            }
        }
        catch {
            $blockers.Add([ordered]@{
                Code = "AuthoritativeWorkshopMetadataInvalid"
                Message = "Public-owner workshop metadata is not valid JSON: $authoritativeWorkshopDataPath"
            })
        }
    }
    elseif (-not [string]::IsNullOrWhiteSpace($ReleaseLine)) {
        $blockers.Add([ordered]@{
            Code = "AuthoritativeWorkshopMetadataMissing"
            Message = (
                "Registered-line package materialization requires the public owner's tracked workshop metadata: " +
                $authoritativeWorkshopDataPath)
        })
    }
    $packageMode = [string](Get-ReadinessProperty $packageOwnerConfig.Package "Mode")
    if ([string]::IsNullOrWhiteSpace($packageMode)) {
        $packageMode = "ExistingZip"
    }
    $packageSourceValue = [string](Get-ReadinessProperty $packageOwnerConfig.Package "SourcePath")
    $packageSourcePath = if ([string]::IsNullOrWhiteSpace($packageSourceValue)) {
        ""
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $packageOwnerRoot $packageSourceValue))
    }
    $packageSourceExists = -not [string]::IsNullOrWhiteSpace($packageSourcePath) -and
        (Test-Path -LiteralPath $packageSourcePath -PathType Container)
    $sourceLaneMaterializationPath = if ([string]::IsNullOrWhiteSpace($packageSourceValue)) {
        ""
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $releaseRoot $packageSourceValue))
    }
    $packageArtifactValue = if ($packageMode -eq "LocalModFolder") {
        [string](Get-ReadinessProperty $packageOwnerConfig.Package "OutputPath")
    }
    else {
        [string](Get-ReadinessProperty $packageOwnerConfig.Package "Path")
    }
    $packageArtifactPath = if ([string]::IsNullOrWhiteSpace($packageArtifactValue)) {
        ""
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $packageOwnerRoot $packageArtifactValue))
    }
    $packageArtifactExists = -not [string]::IsNullOrWhiteSpace($packageArtifactPath) -and
        (Test-Path -LiteralPath $packageArtifactPath -PathType Leaf)
    $desiredPackageShape = [pscustomobject]@{
        ManifestVersions = [pscustomobject]$desiredManifestVersions
    }
    $packageLanes = Get-ReadinessPackageLanes `
        $packageSourcePath $packageArtifactPath $packageMode $desiredPackageShape
    $packagePreparationState = "Ready"
    if ($packageMode -eq "LocalModFolder") {
        if ([string]::IsNullOrWhiteSpace($packageSourceValue)) {
            $packagePreparationState = "BlockedMissingSourcePath"
            $blockers.Add([ordered]@{
                Code = "PackageSourcePathMissing"
                Message = "LocalModFolder package mode requires Package.SourcePath."
            })
        }
        elseif (-not $packageSourceExists) {
            $packagePreparationState = "AwaitingGate1Materialization"
        }
        if ([string]::IsNullOrWhiteSpace($packageArtifactValue)) {
            $blockers.Add([ordered]@{
                Code = "PackageOutputPathMissing"
                Message = "LocalModFolder package mode requires Package.OutputPath."
            })
        }
    }
    elseif ($packageMode -eq "ExistingZip") {
        if ([string]::IsNullOrWhiteSpace($packageArtifactValue)) {
            $packagePreparationState = "BlockedMissingArtifactPath"
            $blockers.Add([ordered]@{
                Code = "PackageArtifactPathMissing"
                Message = "ExistingZip package mode requires Package.Path."
            })
        }
        elseif (-not $packageArtifactExists) {
            $packagePreparationState = "BlockedMissingExistingArtifact"
            $blockers.Add([ordered]@{
                Code = "PackageArtifactMissing"
                Message = "ExistingZip package artifact is missing: $packageArtifactPath"
            })
        }
    }
    else {
        $packagePreparationState = "BlockedUnsupportedMode"
        $blockers.Add([ordered]@{
            Code = "PackageModeUnsupported"
            Message = "Unsupported package mode: $packageMode"
        })
    }
    if ($packagePreparationState -eq "AwaitingGate1Materialization") {
        $warnings.Add("Package source is not materialized yet; Gate 1 must export/build it at the configured path.")
    }
    foreach ($lane in @($packageLanes | Where-Object { -not $_.MatchesExpected })) {
        $warnings.Add(
            "Package lane $($lane.GameVersion) is absent or differs from configured manifest " +
            "v$($lane.ExpectedManifestVersion); Gate 1 must refresh or validate it.")
    }

    $criticalPaths = @(
        $ModName,
        "${ModName}.Tests",
        "ModsUnityProject/Assets/Mods/$ModName",
        "TimberDev",
        "TimberDev.Tests")
    $dirty = @((Invoke-ReadinessGit $releaseRoot (@("status", "--porcelain=v1", "--") + $criticalPaths)).Output)
    $releaseToolCriticalPaths = [string[]]@(
        "tools/build-local-mod-scripts.ps1",
        "tools/export-unity-mod.ps1",
        "tools/get-release-readiness.ps1",
        "tools/import-release-lane.ps1",
        "tools/materialize-release-package.ps1",
        "tools/prepare-discord-release-message.ps1",
        "tools/publish-github-release.ps1",
        "tools/publish-modio.ps1",
        "tools/publish-release.ps1",
        "tools/publish-steam.ps1",
        "tools/release-lines.json",
        "tools/release-lines.psm1",
        "tools/release-native-command.psm1",
        "tools/release-notes.psm1",
        "tools/release-output.psm1",
        "tools/release-package-provenance.psm1",
        "tools/release-readiness.psm1",
        "tools/release-signoff.psm1",
        "tools/release-unity.psm1",
        "tools/release-versioning.psm1",
        "tools/repository-paths.psm1",
        "tools/update-platform-tags.ps1",
        "tools/verify-platform-descriptions.ps1",
        "tools/verify-release.ps1")
    $toolingDirty = @((Invoke-ReadinessGit $toolsRoot (
        @("status", "--porcelain=v1", "--") + $releaseToolCriticalPaths)).Output)
    $releaseCriticalDirt = @($dirty + $toolingDirty | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($releaseCriticalDirt.Count -gt 0) {
        $blockers.Add([ordered]@{
            Code = "ReleaseCriticalDirt"
            Message = "Release-critical tracked paths contain uncommitted changes."
            Paths = [string[]]$releaseCriticalDirt
        })
    }

    $identityPaths = @("$ModName/release.json")
    if (-not [string]::IsNullOrWhiteSpace($changesPathValue)) {
        $identityPaths += $changesPathValue
    }
    $headResult = Invoke-ReadinessGit $releaseRoot @("rev-parse", "HEAD")
    $headAtDiscovery = if ($headResult.Output.Count -gt 0) { [string]$headResult.Output[0] } else { "" }
    $releaseCommitResult = Invoke-ReadinessGit $releaseRoot (@("log", "-1", "--format=%H", "--") + $identityPaths)
    $releaseCommit = if ($releaseCommitResult.Output.Count -gt 0) { [string]$releaseCommitResult.Output[0] } else { "" }
    if ([string]::IsNullOrWhiteSpace($releaseCommit)) {
        $blockers.Add([ordered]@{ Code = "ReleaseCommitMissing"; Message = "No release identity commit was found." })
    }

    $tagName = if ($versionPolicy -eq "BackportPatchOnly") {
        "$($ModName)_${sourceModVersion}_$ReleaseLine"
    }
    else {
        "$($ModName)_$sourceModVersion"
    }
    $tagResult = Invoke-ReadinessGit $releaseRoot @("rev-parse", "-q", "--verify", "refs/tags/$tagName") -AllowFailure
    if ($tagResult.ExitCode -eq 0) {
        $blockers.Add([ordered]@{
            Code = "IntendedTagExists"
            Message = "The routine release tag already exists: $tagName"
        })
    }
    $baselineResult = Invoke-ReadinessGit $releaseRoot @(
        "describe", "--tags", "--abbrev=0", "--match", "$ModName`_*", "$releaseCommit^") -AllowFailure
    $baselineTag = if ($baselineResult.ExitCode -eq 0 -and $baselineResult.Output.Count -gt 0) {
        [string]$baselineResult.Output[0]
    }
    else {
        ""
    }

    $probe = Get-ReadinessProbeResults `
        $toolsRoot $releaseRoot $ModName $UnityPath $ModIoAccessTokenPath $ProbeOverride
    if ([bool]$probe.Steam.DesktopClientRunning) {
        $warnings.Add(
            "Desktop Steam is running. Publication will authenticate a separate SteamCMD process with the " +
            "configured user; correlate any Steam Cloud disruption with that concurrent login.")
    }
    if (-not [bool]$probe.Unity.Ready) {
        $blockers.Add([ordered]@{
            Code = "UnityEditorMissing"
            Message = "Canonical Unity editor for project version $($probe.Unity.ProjectVersion) was not found."
        })
    }
    if (-not [bool]$probe.GitHub.CliAvailable -or -not [bool]$probe.GitHub.AuthReady) {
        $blockers.Add([ordered]@{
            Code = "GitHubAuthMissing"
            Message = (
                "GitHub auth/API access is unavailable in the readiness process credential-access boundary. " +
                "This does not prove that the stored credential is invalid; rerun readiness with the approved " +
                "release execution boundary before any logout or device reauthentication.")
        })
    }
    if (-not [bool]$probe.Steam.ConfigExists -or -not [bool]$probe.Steam.SteamCmdExists -or
            -not [bool]$probe.Steam.UserNameConfigured) {
        $blockers.Add([ordered]@{
            Code = "SteamConfigMissing"
            Message = "Steam config, SteamCMD path, or Steam user name is missing."
        })
    }
    if (-not [bool]$probe.ModIO.ConfigExists -or [string]::IsNullOrWhiteSpace([string]$probe.ModIO.ModId) -or
            [string]::IsNullOrWhiteSpace([string]$probe.ModIO.GameId) -or
            -not [bool]$probe.ModIO.ApiBaseConfigured) {
        $blockers.Add([ordered]@{ Code = "ModIoConfigMissing"; Message = "Mod.IO config or ModId is missing." })
    }
    if (-not [bool]$probe.ModIO.AccessTokenExists) {
        $blockers.Add([ordered]@{
            Code = "ModIoCredentialMissing"
            Message = (
                "Selected Mod.IO owner token is missing or empty. Prefer the per-mod token when present; " +
                "otherwise rerun readiness with -ModIoAccessTokenPath pointing to another known valid owner " +
                "token under .tools/modio for the same account/game scope. Do not copy, rename, or guess token files.")
        })
    }

    $steamPublishedFileId = $publicSteamPublishedFileId
    if ([string]::IsNullOrWhiteSpace($steamPublishedFileId)) {
        $blockers.Add([ordered]@{ Code = "SteamIdentityMissing"; Message = "Steam PublishedFileId is missing." })
    }

    $externalPrerequisiteCodes = [string[]]@(
        "GitHubAuthMissing",
        "ModIoConfigMissing",
        "ModIoCredentialMissing",
        "SteamConfigMissing",
        "SteamIdentityMissing")
    $gate1Blockers = @($blockers | Where-Object { [string]$_.Code -notin $externalPrerequisiteCodes })
    $externalPrerequisiteBlockers = @($blockers | Where-Object {
        [string]$_.Code -in $externalPrerequisiteCodes
    })
    $preparationRequired = $plannedMetadataChanges.Count -gt 0
    $readyForGate1 = $gate1Blockers.Count -eq 0
    $readyForPreflight = $blockers.Count -eq 0 -and -not $preparationRequired

    $gate1Arguments = @("-ModName", $ModName, "-ReleaseRoot", $releaseRoot, "-GameVersion", $gameVersion,
        "-VersionPolicy", $versionPolicy, "-ReleaseCommit", $releaseCommit,
        "-PackageOwnerRoot", $packageOwnerRoot)
    if (-not [string]::IsNullOrWhiteSpace($ReleaseLine)) {
        $gate1Arguments += @("-ReleaseLine", $ReleaseLine)
    }
    if ($authoritativeWorkshopDataExists -and $authoritativeWorkshopDataParseReady -and
            $authoritativeWorkshopTagIntentDeclared) {
        $gate1Arguments += @("-AuthoritativeWorkshopDataPath", $authoritativeWorkshopDataPath)
    }
    $selectedModIoTokenArgument = [string]$probe.ModIO.AccessTokenArgument
    if ([bool]$probe.ModIO.AccessTokenExists -and
            -not [string]::IsNullOrWhiteSpace($selectedModIoTokenArgument)) {
        $gate1Arguments += @("-ModIoAccessTokenPath", $selectedModIoTokenArgument)
    }
    return [ordered]@{
        SchemaVersion = 1
        ArtifactType = "TimberbornReleaseReadiness"
        CreatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        Candidate = [ordered]@{
            ModName = $ModName
            ReleaseRoot = $releaseRoot
            Branch = $branch
            ReleaseLine = $ReleaseLine
            GameVersion = $gameVersion
            VersionPolicy = $versionPolicy
            SourceModVersion = $sourceModVersion
            PreviousSourceModVersion = $previousSourceModVersion
            PublicPackageVersion = [string]$publicLane.ManifestVersion
            PublicPackageGameVersion = [string]$publicLane.GameVersion
            PublicPackageOwnerRoot = $repositoryRoot
            ConfiguredReleaseVersion = $configuredSourceModVersion
            ConfigReadyForPublish = if ($null -eq $configReadyForPublish) { "Unspecified" } else { [bool]$configReadyForPublish }
        }
        Changelog = $changelog
        Git = [ordered]@{
            HeadAtDiscovery = $headAtDiscovery
            ReleaseCommit = $releaseCommit
            BaselineTag = $baselineTag
            IntendedTag = $tagName
            IntendedTagExists = $tagResult.ExitCode -eq 0
            ReleaseCriticalDirt = [string[]]$releaseCriticalDirt
        }
        Package = [ordered]@{
            OwnerRoot = $packageOwnerRoot
            OwnerConfigPath = $packageOwnerConfigPath
            OwnerConfigSha256 = $packageOwnerConfigSha256
            AuthoritativeWorkshopData = [ordered]@{
                Path = $authoritativeWorkshopDataPath
                PathArgument = $authoritativeWorkshopDataValue
                Sha256 = $authoritativeWorkshopDataSha256
                Exists = $authoritativeWorkshopDataExists
                ParseReady = $authoritativeWorkshopDataParseReady
                TagIntentDeclared = $authoritativeWorkshopTagIntentDeclared
                Tags = [string[]]$authoritativeWorkshopDataTags
                Source = "PublicOwnerTrackedManifestSibling"
            }
            Mode = $packageMode
            SourcePath = $packageSourcePath
            SourcePathArgument = $packageSourceValue
            SourceLaneMaterializationPath = $sourceLaneMaterializationPath
            SourcePathConfigured = -not [string]::IsNullOrWhiteSpace($packageSourceValue)
            SourceExists = $packageSourceExists
            ArtifactPath = $packageArtifactPath
            ArtifactPathArgument = $packageArtifactValue
            ArtifactPathConfigured = -not [string]::IsNullOrWhiteSpace($packageArtifactValue)
            ArtifactExists = $packageArtifactExists
            PreparationState = $packagePreparationState
            Gate1MaterializesSource = $packageMode -eq "LocalModFolder"
            Lanes = [object[]]$packageLanes
        }
        Platforms = [ordered]@{
            Steam = [ordered]@{
                PublishedFileId = $steamPublishedFileId
                IdentitySource = "CurrentPublicReleaseConfig"
                VisibilityIntent = "Unchanged"
                ConfigReady = [bool]$probe.Steam.ConfigExists -and [bool]$probe.Steam.SteamCmdExists -and
                    [bool]$probe.Steam.UserNameConfigured
                DesktopClientRunning = [bool]$probe.Steam.DesktopClientRunning
                UploadLoginMode = [string]$probe.Steam.UploadLoginMode
                ExitCommand = [string]$probe.Steam.ExitCommand
                ExplicitLogoutCommand = [bool]$probe.Steam.ExplicitLogoutCommand
            }
            ModIO = [ordered]@{
                ModId = [string]$probe.ModIO.ModId
                ConfigSource = "MainToolingOwnerConfig"
                CredentialSource = [string]$probe.ModIO.AccessTokenSelection
                CredentialPathScope = [string]$probe.ModIO.AccessTokenPathScope
                PageVisibilityIntent = "Unchanged"
                ConfigReady = [bool]$probe.ModIO.ConfigExists
                CredentialReady = [bool]$probe.ModIO.AccessTokenExists
            }
            GitHub = [ordered]@{
                CliAvailable = [bool]$probe.GitHub.CliAvailable
                AuthReady = [bool]$probe.GitHub.AuthReady
                AuthStatusExitCode = [int](Get-ReadinessProperty $probe.GitHub "AuthStatusExitCode")
                ApiUserExitCode = [int](Get-ReadinessProperty $probe.GitHub "ApiUserExitCode")
                Account = [string](Get-ReadinessProperty $probe.GitHub "Account")
                EvidenceScope = "CurrentProcessCredentialAccessBoundary"
                StoredCredentialValidity = if ([bool]$probe.GitHub.AuthReady) {
                    "ConfirmedInExecutionBoundary"
                }
                else {
                    "Unknown"
                }
                CredentialMutationRecommended = $false
            }
        }
        Unity = $probe.Unity
        Gate1Plan = [ordered]@{
            EntryPoint = "tools/verify-release.ps1"
            Arguments = [string[]]$gate1Arguments
            ExpectedHeadForPreparation = $headAtDiscovery
            PreparationRequired = $preparationRequired
            PreparationState = if ($preparationRequired) { "AwaitingMetadataPreparation" } else { "Prepared" }
            NextAction = if ($preparationRequired) {
                "ApplyPlannedMetadataChangesCommitAndRerunReadiness"
            }
            elseif ($externalPrerequisiteBlockers.Count -gt 0) {
                "ResolveExternalPrerequisitesAndRerunReadiness"
            }
            else {
                "RunImmutablePreflight"
            }
            EntryPointAvailable = $readyForPreflight
            PlannedMetadataChanges = [object[]]$plannedMetadataChanges.ToArray()
            RerunReadinessCommand = @(
                "powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                "tools/get-release-readiness.ps1", "-ModName", $ModName) + $(if (
                    [string]::IsNullOrWhiteSpace($ReleaseLine)) { @() } else { @("-ReleaseLine", $ReleaseLine) }) +
                $(if ([bool]$probe.ModIO.AccessTokenExists -and
                        -not [string]::IsNullOrWhiteSpace($selectedModIoTokenArgument)) {
                    @("-ModIoAccessTokenPath", $selectedModIoTokenArgument)
                }
                else { @() })
            MutatesPublicPlatforms = $false
            OperationOrder = [string[]]@(
                "Platform description verification",
                "Unity package source export",
                "C# package source build",
                "Combined package lane assembly",
                "Local release tag materialization",
                "Combined package materialization",
                "Steam release dry run",
                "Mod.IO release dry run",
                "Final platform tag dry run",
                "Immutable preflight report")
        }
        Warnings = [string[]]$warnings.ToArray()
        Blockers = [object[]]$blockers.ToArray()
        Gate1Blockers = [object[]]$gate1Blockers
        ExternalPrerequisiteBlockers = [object[]]$externalPrerequisiteBlockers
        ReadyForGate1 = $readyForGate1
        ReadyForPreflight = $readyForPreflight
        Ready = $readyForGate1
    }
}

function Format-ReleaseReadinessReport([object] $Report) {
    $status = if ($Report.ReadyForPreflight) {
        "READY FOR PREFLIGHT"
    }
    elseif (-not $Report.Gate1Plan.PreparationRequired -and
            @($Report.ExternalPrerequisiteBlockers).Count -gt 0 -and
            @($Report.Gate1Blockers).Count -eq 0) {
        "WAITING FOR PREFLIGHT PREREQUISITES"
    }
    elseif ($Report.ReadyForGate1) {
        "READY FOR GATE 1"
    }
    else {
        "BLOCKED"
    }
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("Release readiness: $status")
    $lines.Add(
        "  Candidate: $($Report.Candidate.ModName) source v$($Report.Candidate.SourceModVersion); " +
        "previous v$($Report.Candidate.PreviousSourceModVersion); public v$($Report.Candidate.PublicPackageVersion)")
    $lines.Add("  Worktree: $($Report.Candidate.ReleaseRoot) ($($Report.Candidate.Branch))")
    $lines.Add(
        "  Line: $($Report.Candidate.GameVersion); policy=$($Report.Candidate.VersionPolicy); " +
        "release-line=$($Report.Candidate.ReleaseLine)")
    $lines.Add("  Changelog: $($Report.Changelog.Heading)")
    $lines.Add(
        "  Package: mode=$($Report.Package.Mode); preparation=$($Report.Package.PreparationState); " +
        "source materialized=$($Report.Package.SourceExists); artifact materialized=$($Report.Package.ArtifactExists)")
    $lines.Add(
        "  Workshop metadata: bound=$($Report.Package.AuthoritativeWorkshopData.ParseReady); " +
        "tags=$($Report.Package.AuthoritativeWorkshopData.Tags -join ', '); " +
        "sha256=$($Report.Package.AuthoritativeWorkshopData.Sha256)")
    $lines.Add(
        "  Metadata: $($Report.Gate1Plan.PreparationState); " +
        "planned changes=$(@($Report.Gate1Plan.PlannedMetadataChanges).Count)")
    foreach ($change in @($Report.Gate1Plan.PlannedMetadataChanges)) {
        $lines.Add("    $($change.Field): '$($change.CurrentValue)' -> '$($change.DesiredValue)'")
    }
    $lines.Add("  Commit: $($Report.Git.ReleaseCommit); baseline=$($Report.Git.BaselineTag); tag=$($Report.Git.IntendedTag)")
    $lines.Add("  Steam: $($Report.Platforms.Steam.PublishedFileId); visibility=unchanged")
    $lines.Add(
        "  Steam session: $($Report.Platforms.Steam.UploadLoginMode); " +
        "exit=$($Report.Platforms.Steam.ExitCommand); " +
        "explicit logout=$($Report.Platforms.Steam.ExplicitLogoutCommand); " +
        "desktop client running=$($Report.Platforms.Steam.DesktopClientRunning)")
    $lines.Add("  Mod.IO: $($Report.Platforms.ModIO.ModId); page visibility=unchanged")
    $lines.Add("  Unity: $($Report.Unity.ProjectVersion); editor=$($Report.Unity.CanonicalEditorPath)")
    $lines.Add(
        "  GitHub auth in current credential-access boundary: $($Report.Platforms.GitHub.AuthReady); " +
        "stored credential validity=$($Report.Platforms.GitHub.StoredCredentialValidity); " +
        "credential mutation recommended=False")
    if (@($Report.Gate1Blockers).Count -gt 0) {
        $lines.Add("  Gate 1 blockers ($(@($Report.Gate1Blockers).Count)):")
        foreach ($blocker in @($Report.Gate1Blockers)) {
            $lines.Add("    [$($blocker.Code)] $($blocker.Message)")
        }
    }
    if (@($Report.ExternalPrerequisiteBlockers).Count -gt 0) {
        $lines.Add(
            "  Remaining preflight prerequisites ($(@($Report.ExternalPrerequisiteBlockers).Count)):")
        foreach ($blocker in @($Report.ExternalPrerequisiteBlockers)) {
            $lines.Add("    [$($blocker.Code)] $($blocker.Message)")
        }
    }
    if (@($Report.Warnings).Count -gt 0) {
        $lines.Add("  Warnings ($(@($Report.Warnings).Count)):")
        foreach ($warning in @($Report.Warnings)) {
            $lines.Add("    $warning")
        }
    }
    $lines.Add("  Gate 1: $($Report.Gate1Plan.EntryPoint) $($Report.Gate1Plan.Arguments -join ' ')")
    return [string[]]$lines.ToArray()
}

Export-ModuleMember -Function Get-ReleaseReadinessReport, Format-ReleaseReadinessReport
