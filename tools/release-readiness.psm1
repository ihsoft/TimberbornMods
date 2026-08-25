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

function Get-ReadinessPackageLanes([string] $SourcePath, [object] $ReleaseConfig) {
    $configured = Get-ReadinessProperty $ReleaseConfig "ManifestVersions"
    $lanes = New-Object System.Collections.Generic.List[object]
    if ($null -eq $configured) {
        return [object[]]$lanes.ToArray()
    }

    foreach ($property in $configured.PSObject.Properties | Sort-Object Name) {
        $manifestPath = Join-Path $SourcePath "$($property.Name)/manifest.json"
        $actualVersion = ""
        if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
            try {
                $actualVersion = [string]((Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json).Version)
            }
            catch {
                $actualVersion = "(invalid JSON)"
            }
        }
        $lanes.Add([ordered]@{
            GameVersion = [string]$property.Name
            ExpectedManifestVersion = [string]$property.Value
            SourceManifestExists = Test-Path -LiteralPath $manifestPath -PathType Leaf
            SourceManifestVersion = $actualVersion
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
        $configuredUnityPath = if ([string]::IsNullOrWhiteSpace($UnityPath) -or
                [System.IO.Path]::IsPathRooted($UnityPath)) {
            $UnityPath
        }
        else {
            Join-Path $ReleaseRoot $UnityPath
        }
        $canonicalEditor = Resolve-CanonicalUnityPath $configuredUnityPath $unityVersion -AllowMissing
    }
    catch {
        $unityError = $_.Exception.Message
    }

    $ghCommand = Get-Command gh -ErrorAction SilentlyContinue
    $ghResult = if ($null -eq $ghCommand) {
        [pscustomobject]@{ ExitCode = -1; Output = [string[]]@(); Diagnostics = [string[]]@("gh not found") }
    }
    else {
        Invoke-ReleaseNativeCommand -Command "gh" -Arguments @("auth", "status", "--hostname", "github.com")
    }
    $steamConfigPath = Join-Path $ToolsRoot ".tools/steam/steam.local.json"
    $modIoConfigPath = Join-Path $ToolsRoot ".tools/modio/$ModName.local.json"
    $modIoTokenPath = Join-Path $ToolsRoot ".tools/modio/$ModName.token.txt"
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
            AuthReady = $ghResult.ExitCode -eq 0
            Account = ""
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
            AccessTokenExists = (Test-Path -LiteralPath $modIoTokenPath -PathType Leaf) -and
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
    $sourceModVersion = [string](Get-ReadinessProperty $releaseConfig "ReleaseVersion")
    $configReadyForPublish = Get-ReadinessProperty $releaseConfig "ReadyForPublish"
    $publicLane = Get-ReadinessPublicPackageLane $releaseConfig $gameVersion
    $changesPath = Join-Path $releaseRoot ([string](Get-ReadinessProperty $releaseConfig "ChangesPath"))
    $changelog = Get-ReadinessChangelogIdentity $changesPath $sourceModVersion

    if ([string]::IsNullOrWhiteSpace($sourceModVersion)) {
        $blockers.Add([ordered]@{ Code = "ReleaseVersionMissing"; Message = "ReleaseVersion is empty." })
    }
    if ($null -ne $configReadyForPublish -and $configReadyForPublish -ne $true) {
        $blockers.Add([ordered]@{
            Code = "ReleaseConfigNotReady"
            Message = "release.json explicitly marks the candidate as not ready for publish."
        })
    }
    if (-not $changelog.Exists) {
        $blockers.Add([ordered]@{ Code = "ChangelogMissing"; Message = "Changelog is missing: $changesPath" })
    }
    elseif (-not $changelog.MatchesSourceModVersion) {
        $blockers.Add([ordered]@{
            Code = "ChangelogVersionMismatch"
            Message = "Top changelog version $($changelog.Version) does not match ReleaseVersion $sourceModVersion."
        })
    }
    elseif ($changelog.IsPending) {
        $blockers.Add([ordered]@{
            Code = "ChangelogDatePending"
            Message = "The release heading is still marked (TBD); date and commit it before final preflight."
        })
    }

    $manifestPath = Join-Path $releaseRoot ([string](Get-ReadinessProperty $releaseConfig "ManifestPath"))
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        $blockers.Add([ordered]@{ Code = "ManifestMissing"; Message = "Release manifest is missing: $manifestPath" })
    }

    $packageSourceValue = [string](Get-ReadinessProperty $releaseConfig.Package "SourcePath")
    $packageSourcePath = if ([string]::IsNullOrWhiteSpace($packageSourceValue)) {
        ""
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $releaseRoot $packageSourceValue))
    }
    $packageLanes = Get-ReadinessPackageLanes $packageSourcePath $releaseConfig
    if (-not [string]::IsNullOrWhiteSpace($packageSourcePath) -and
            -not (Test-Path -LiteralPath $packageSourcePath -PathType Container)) {
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

    $identityPaths = @("$ModName/release.json", [string](Get-ReadinessProperty $releaseConfig "ChangesPath"))
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

    $probe = Get-ReadinessProbeResults $toolsRoot $releaseRoot $ModName $UnityPath $ProbeOverride
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
        $blockers.Add([ordered]@{ Code = "GitHubAuthMissing"; Message = "GitHub CLI authentication is not ready." })
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
        $blockers.Add([ordered]@{ Code = "ModIoCredentialMissing"; Message = "Mod.IO owner token file is missing." })
    }

    $steamPublishedFileId = [string](Get-ReadinessProperty $releaseConfig.Steam "PublishedFileId")
    if ([string]::IsNullOrWhiteSpace($steamPublishedFileId)) {
        $blockers.Add([ordered]@{ Code = "SteamIdentityMissing"; Message = "Steam PublishedFileId is missing." })
    }

    $gate1Arguments = @("-ModName", $ModName, "-ReleaseRoot", $releaseRoot, "-GameVersion", $gameVersion,
        "-VersionPolicy", $versionPolicy, "-ReleaseCommit", $releaseCommit)
    if (-not [string]::IsNullOrWhiteSpace($ReleaseLine)) {
        $gate1Arguments += @("-ReleaseLine", $ReleaseLine)
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
            PublicPackageVersion = [string]$publicLane.ManifestVersion
            PublicPackageGameVersion = [string]$publicLane.GameVersion
            ConfigReadyForPublish = if ($null -eq $configReadyForPublish) { "Unspecified" } else { [bool]$configReadyForPublish }
        }
        Changelog = $changelog
        Git = [ordered]@{
            ReleaseCommit = $releaseCommit
            BaselineTag = $baselineTag
            IntendedTag = $tagName
            IntendedTagExists = $tagResult.ExitCode -eq 0
            ReleaseCriticalDirt = [string[]]$releaseCriticalDirt
        }
        Package = [ordered]@{
            Mode = [string](Get-ReadinessProperty $releaseConfig.Package "Mode")
            SourcePath = $packageSourcePath
            SourceExists = -not [string]::IsNullOrWhiteSpace($packageSourcePath) -and
                (Test-Path -LiteralPath $packageSourcePath -PathType Container)
            Lanes = [object[]]$packageLanes
        }
        Platforms = [ordered]@{
            Steam = [ordered]@{
                PublishedFileId = $steamPublishedFileId
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
                PageVisibilityIntent = "Unchanged"
                ConfigReady = [bool]$probe.ModIO.ConfigExists
                CredentialReady = [bool]$probe.ModIO.AccessTokenExists
            }
            GitHub = [ordered]@{
                CliAvailable = [bool]$probe.GitHub.CliAvailable
                AuthReady = [bool]$probe.GitHub.AuthReady
            }
        }
        Unity = $probe.Unity
        Gate1Plan = [ordered]@{
            EntryPoint = "tools/verify-release.ps1"
            Arguments = [string[]]$gate1Arguments
            MutatesPublicPlatforms = $false
            OperationOrder = [string[]]@(
                "Platform description verification",
                "Unity package source export",
                "C# package source build",
                "Steam release dry run",
                "Mod.IO release dry run",
                "Platform tag dry run",
                "Immutable preflight report")
        }
        Warnings = [string[]]$warnings.ToArray()
        Blockers = [object[]]$blockers.ToArray()
        Ready = $blockers.Count -eq 0
    }
}

function Format-ReleaseReadinessReport([object] $Report) {
    $status = if ($Report.Ready) { "READY" } else { "BLOCKED" }
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("Release readiness: $status")
    $lines.Add(
        "  Candidate: $($Report.Candidate.ModName) source v$($Report.Candidate.SourceModVersion); " +
        "public v$($Report.Candidate.PublicPackageVersion)")
    $lines.Add("  Worktree: $($Report.Candidate.ReleaseRoot) ($($Report.Candidate.Branch))")
    $lines.Add(
        "  Line: $($Report.Candidate.GameVersion); policy=$($Report.Candidate.VersionPolicy); " +
        "release-line=$($Report.Candidate.ReleaseLine)")
    $lines.Add("  Changelog: $($Report.Changelog.Heading)")
    $lines.Add("  Commit: $($Report.Git.ReleaseCommit); baseline=$($Report.Git.BaselineTag); tag=$($Report.Git.IntendedTag)")
    $lines.Add("  Steam: $($Report.Platforms.Steam.PublishedFileId); visibility=unchanged")
    $lines.Add(
        "  Steam session: $($Report.Platforms.Steam.UploadLoginMode); " +
        "exit=$($Report.Platforms.Steam.ExitCommand); " +
        "explicit logout=$($Report.Platforms.Steam.ExplicitLogoutCommand); " +
        "desktop client running=$($Report.Platforms.Steam.DesktopClientRunning)")
    $lines.Add("  Mod.IO: $($Report.Platforms.ModIO.ModId); page visibility=unchanged")
    $lines.Add("  Unity: $($Report.Unity.ProjectVersion); editor=$($Report.Unity.CanonicalEditorPath)")
    $lines.Add("  GitHub auth: $($Report.Platforms.GitHub.AuthReady)")
    if (@($Report.Blockers).Count -gt 0) {
        $lines.Add("  Blockers ($(@($Report.Blockers).Count)):")
        foreach ($blocker in @($Report.Blockers)) {
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
