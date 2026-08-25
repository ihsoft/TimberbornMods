param()

$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "release-native-command.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-date.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-output.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-package-provenance.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-versioning.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-lines.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-unity.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-native-command.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-readiness.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-signoff.psm1") -Force

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal([object] $Expected, [object] $Actual, [string] $Description) {
    if ([string]$Expected -ne [string]$Actual) {
        throw "$Description. Expected '$Expected', actual '$Actual'."
    }
}

function Invoke-TestNative([string] $Command, [string[]] $Arguments) {
    $result = Invoke-ReleaseNativeCommand -Command $Command -Arguments $Arguments
    if ($result.ExitCode -ne 0) {
        throw (
            "Test command failed: $Command $($Arguments -join ' ')`n" +
            ($result.Diagnostics -join [Environment]::NewLine))
    }
    return $result
}

function Write-TestFile([string] $Path, [string] $Content) {
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $parent)
    }
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Get-CandidateKey([object] $Candidate) {
    return @(
        [string]$Candidate.ModName,
        [string]$Candidate.Version,
        [string]$Candidate.Branch,
        [string]$Candidate.ReleaseLine,
        [string]$Candidate.GameVersion,
        [string]$Candidate.VersionPolicy,
        [string]$Candidate.ChangesPath) -join "|"
}

$previousPreference = $ErrorActionPreference
$successfulNative = Invoke-ReleaseNativeCommand -Command "cmd.exe" -Arguments @(
    "/d", "/c", "echo stdout-line & echo stderr-line 1>&2 & exit /b 0")
Assert-Equal 0 $successfulNative.ExitCode "Successful native command exit code"
Assert-Equal "stdout-line" ([string]$successfulNative.Output[0]).Trim() "Successful native command stdout"
Assert-Equal "stderr-line" ([string]$successfulNative.StandardError[0]).Trim() "Successful native command stderr"
Assert-Equal $previousPreference $ErrorActionPreference "Native command error preference restoration"

$failedNative = Invoke-ReleaseNativeCommand -Command "cmd.exe" -Arguments @(
    "/d", "/c", "echo failure-line 1>&2 & exit /b 7")
Assert-Equal 7 $failedNative.ExitCode "Failed native command exit code"
Assert-Equal "failure-line" ([string]$failedNative.StandardError[0]).Trim() "Failed native command diagnostics"
Assert-Equal $previousPreference $ErrorActionPreference "Failed native command error preference restoration"
Write-Host "PASS: native stderr capture, exit-code handling, and preference restoration"

$ordinalDate = Get-ChangelogHeadingDate "# v4.10.0 (August 22nd, 2026):"
$ordinalDateWithoutComma = Get-ChangelogHeadingDate "# v4.10.0 (August 22nd 2026)"
$isoDate = Get-ChangelogHeadingDate "# v1.0.0 (2026-08-15)"
Assert-Equal "2026-08-22" $ordinalDate.ToString("yyyy-MM-dd") "Ordinal changelog date parsing"
Assert-Equal "2026-08-22" $ordinalDateWithoutComma.ToString("yyyy-MM-dd") `
    "Ordinal changelog date parsing without a comma"
Assert-Equal "2026-08-15" $isoDate.ToString("yyyy-MM-dd") "ISO changelog date parsing"
$dateRecords = @(Get-ReleaseDateRecords -ReleaseSources @(
    [pscustomobject]@{
        TagName = "Automation_4.10.0"
        ChangelogHeading = "# v4.10.0 (August 22nd, 2026):"
    }) -CurrentLocalDate ([datetime]"2026-08-23"))
Assert-Equal 1 $dateRecords.Count "Release date record count"
Assert-True $dateRecords[0].DateDiffers "Release rollover must be detected without editing the changelog."
Write-Host "PASS: changelog date parsing and bounded rollover detection"

$temporaryBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$testRoot = Join-Path $temporaryBase ("TimberbornMods-release-tooling-test-" + [guid]::NewGuid().ToString("N"))
$fixtureRoot = Join-Path $testRoot "repository"
$lineRoot = Join-Path $testRoot "update-1.0"

try {
    [void](New-Item -ItemType Directory -Path $fixtureRoot)
    $fixtureTools = Join-Path $fixtureRoot "tools"
    [void](New-Item -ItemType Directory -Path $fixtureTools)
    @(
        "find-unpublished-releases.ps1",
        "release-lines.json",
        "release-lines.psm1",
        "release-native-command.psm1",
        "release-readiness.psm1",
        "release-unity.psm1",
        "repository-paths.psm1"
    ) | ForEach-Object {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $_) -Destination (Join-Path $fixtureTools $_)
    }

    Write-TestFile (Join-Path $fixtureRoot "Alpha/CHANGELOG.md") "# v2.0.0 (TBD)`n"
    Write-TestFile (Join-Path $fixtureRoot "XRay/CHANGELOG.md") "# v1.1.9 (2026-08-01)`n"
    Write-TestFile (Join-Path $fixtureRoot "ReadyMod/CHANGELOG.md") @'
# v3.0.0 (August 24th, 2026)

* [Feature #42] Add `WidgetSignal` release evidence.

# v2.9.0 (August 1st, 2026)
'@
    Write-TestFile (Join-Path $fixtureRoot "ReadyMod/release.json") @'
{
  "ManifestPath": "ModsUnityProject/Assets/Mods/ReadyMod/manifest.json",
  "ChangesPath": "ReadyMod/CHANGELOG.md",
  "ReleaseVersion": "3.0.0",
  "Package": {
    "Mode": "LocalModFolder",
    "SourcePath": "package/ReadyMod",
    "OutputPath": "package/ReadyMod_v3.0.0.zip"
  },
  "ManifestVersions": {
    "version-1.0": "2.9.4",
    "version-1.1": "3.0.0"
  },
  "Steam": { "PublishedFileId": "12345" }
}
'@
    Write-TestFile `
        (Join-Path $fixtureRoot "ModsUnityProject/Assets/Mods/ReadyMod/manifest.json") `
        '{"Name":"Ready Mod","Version":"3.0.0"}'
    Write-TestFile `
        (Join-Path $fixtureRoot "ModsUnityProject/ProjectSettings/ProjectVersion.txt") `
        "m_EditorVersion: 6000.5.2f1`n"
    Write-TestFile `
        (Join-Path $fixtureRoot "package/ReadyMod/version-1.0/manifest.json") `
        '{"Name":"Ready Mod","Version":"2.9.4"}'
    Write-TestFile `
        (Join-Path $fixtureRoot "package/ReadyMod/version-1.1/manifest.json") `
        '{"Name":"Ready Mod","Version":"3.0.0"}'
    [void](Invoke-TestNative "git" @("-C", $fixtureRoot, "init", "-b", "main"))
    [void](Invoke-TestNative "git" @("-C", $fixtureRoot, "config", "user.name", "Release Tooling Test"))
    [void](Invoke-TestNative "git" @("-C", $fixtureRoot, "config", "user.email", "release-tooling@example.invalid"))
    [void](Invoke-TestNative "git" @("-C", $fixtureRoot, "add", "."))
    [void](Invoke-TestNative "git" @("-C", $fixtureRoot, "commit", "-m", "Initial fixture"))
    [void](Invoke-TestNative "git" @(
        "-C", $fixtureRoot, "worktree", "add", "-b", "update-1.0", $lineRoot, "HEAD"))

    Write-TestFile (Join-Path $lineRoot "XRay/CHANGELOG.md") "# v1.2.0 (TBD)`n"
    [void](Invoke-TestNative "git" @("-C", $lineRoot, "add", "XRay/CHANGELOG.md"))
    [void](Invoke-TestNative "git" @("-C", $lineRoot, "commit", "-m", "Add backport candidate"))

    $inventoryScript = Join-Path $fixtureTools "find-unpublished-releases.ps1"
    $humanOutput = (& $inventoryScript -RepositoryRoot $fixtureRoot | Out-String)
    $jsonOutput = (& $inventoryScript -RepositoryRoot $fixtureRoot -AsJson | Out-String)
    $jsonCandidates = @((ConvertFrom-Json $jsonOutput) | ForEach-Object { $_ })
    $candidateKeys = @($jsonCandidates | ForEach-Object { Get-CandidateKey $_ } | Sort-Object)
    $expectedKeys = @(
        "Alpha|2.0.0|main|||CurrentLine|Alpha/CHANGELOG.md",
        "XRay|1.2.0|update-1.0|update-1.0|version-1.0|BackportPatchOnly|XRay/CHANGELOG.md"
    ) | Sort-Object
    Assert-Equal ($expectedKeys -join "`n") ($candidateKeys -join "`n") "Inventory JSON candidate set"

    $humanRows = @($humanOutput -split "`r?`n" | Where-Object { $_ -match "^(Alpha|XRay)\s+\d" })
    Assert-Equal $jsonCandidates.Count $humanRows.Count "Inventory default/JSON candidate count parity"
    Assert-True ($humanOutput -match "(?m)^Alpha\s+2\.0\.0\s+main\s+CurrentLine\s+Alpha/CHANGELOG\.md\s*$") `
        "The current-line candidate did not render as a real table row."
    Assert-True (
        $humanOutput -match (
            "(?m)^XRay\s+1\.2\.0\s+update-1\.0\s+update-1\.0\s+version-1\.0\s+" +
            "BackportPatchOnly\s+XRay/CHANGELOG\.md\s*$")) `
        "The registered-line candidate did not render as a real table row."
    Write-Host "PASS: inventory default/JSON parity for current and registered release lines"

    Write-TestFile (Join-Path $lineRoot "XRay/CHANGELOG.md") @'
# v1.2.0 (Timberborn v1.0) (August 24th, 2026)

* [Fix #77] Preserve `LegacySignal` on Update 1.0.

# v1.1.9 (Timberborn v1.0) (August 1st, 2026)
'@
    Write-TestFile (Join-Path $lineRoot "XRay/release.json") @'
{
  "ManifestPath": "ModsUnityProject/Assets/Mods/XRay/manifest.json",
  "ChangesPath": "XRay/CHANGELOG.md",
  "ReleaseVersion": "1.2.0",
  "Package": {
    "Mode": "LocalModFolder",
    "SourcePath": "package/XRay",
    "OutputPath": "package/XRay_v1.2.0.zip"
  },
  "ManifestVersions": {
    "version-1.0": "1.2.0",
    "version-1.1": "1.3.0"
  },
  "Steam": { "PublishedFileId": "67890" }
}
'@
    Write-TestFile `
        (Join-Path $lineRoot "ModsUnityProject/Assets/Mods/XRay/manifest.json") `
        '{"Name":"X-Ray","Version":"1.2.0"}'
    Write-TestFile `
        (Join-Path $lineRoot "ModsUnityProject/ProjectSettings/ProjectVersion.txt") `
        "m_EditorVersion: 6000.5.2f1`n"
    Write-TestFile `
        (Join-Path $lineRoot "package/XRay/version-1.0/manifest.json") `
        '{"Name":"X-Ray","Version":"1.2.0"}'
    Write-TestFile `
        (Join-Path $lineRoot "package/XRay/version-1.1/manifest.json") `
        '{"Name":"X-Ray","Version":"1.3.0"}'
    [void](Invoke-TestNative "git" @("-C", $lineRoot, "add", "."))
    [void](Invoke-TestNative "git" @("-C", $lineRoot, "commit", "-m", "Prepare backport readiness fixture"))

    $readyProbe = [pscustomobject]@{
        Unity = [pscustomobject]@{
            ProjectVersion = "6000.5.2f1"
            CanonicalEditorPath = "C:\Fixture\Unity.exe"
            Ready = $true
            Error = ""
        }
        GitHub = [pscustomobject]@{ CliAvailable = $true; AuthReady = $true; Account = "fixture" }
        Steam = [pscustomobject]@{
            ConfigPath = "steam.local.json"
            ConfigExists = $true
            SteamCmdPath = "steamcmd.exe"
            SteamCmdExists = $true
            UserNameConfigured = $true
            DesktopClientRunning = $true
            UploadLoginMode = "SteamCMDConfiguredUser"
            ExitCommand = "quit"
            ExplicitLogoutCommand = $false
        }
        ModIO = [pscustomobject]@{
            ConfigPath = "modio.local.json"
            ConfigExists = $true
            AccessTokenPath = "modio.token.txt"
            AccessTokenExists = $true
            ModId = "24680"
            GameId = "1974"
            ApiBaseConfigured = $true
        }
    }
    $currentReadiness = Get-ReleaseReadinessReport `
        -ToolsRoot $fixtureRoot `
        -RepositoryRoot $fixtureRoot `
        -ModName "ReadyMod" `
        -ProbeOverride $readyProbe
    Assert-True $currentReadiness.Ready "Current-line readiness fixture should be ready."
    Assert-Equal "3.0.0" $currentReadiness.Candidate.SourceModVersion "Current source version"
    Assert-Equal "3.0.0" $currentReadiness.Candidate.PublicPackageVersion "Current public package version"
    Assert-Equal "CurrentLine" $currentReadiness.Candidate.VersionPolicy "Current version policy"
    Assert-True (@($currentReadiness.Warnings | Where-Object { $_ -match "Desktop Steam" }).Count -eq 1) `
        "Readiness must surface the concurrent desktop Steam/SteamCMD session boundary."

    $backportReadiness = Get-ReleaseReadinessReport `
        -ToolsRoot $fixtureRoot `
        -RepositoryRoot $fixtureRoot `
        -ModName "XRay" `
        -ReleaseLine "update-1.0" `
        -ProbeOverride $readyProbe
    Assert-True $backportReadiness.Ready "Registered backport readiness fixture should be ready."
    Assert-Equal "1.2.0" $backportReadiness.Candidate.SourceModVersion "Backport source version"
    Assert-Equal "1.3.0" $backportReadiness.Candidate.PublicPackageVersion "Backport public package version"
    Assert-Equal "BackportPatchOnly" $backportReadiness.Candidate.VersionPolicy "Backport version policy"
    Assert-Equal "XRay_1.2.0_update-1.0" $backportReadiness.Git.IntendedTag "Qualified backport tag"

    $humanReadiness = Format-ReleaseReadinessReport $backportReadiness | Out-String
    $jsonReadiness = $backportReadiness | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    @(
        $jsonReadiness.Candidate.ModName,
        $jsonReadiness.Candidate.SourceModVersion,
        $jsonReadiness.Candidate.PublicPackageVersion,
        $jsonReadiness.Candidate.ReleaseRoot,
        $jsonReadiness.Git.ReleaseCommit,
        $jsonReadiness.Git.IntendedTag
    ) | ForEach-Object {
        Assert-True $humanReadiness.Contains([string]$_) "Readiness human/JSON parity omitted: $_"
    }

    $blockedProbe = [pscustomobject]@{
        Unity = [pscustomobject]@{ ProjectVersion = "6000.5.2f1"; CanonicalEditorPath = ""; Ready = $false; Error = "missing" }
        GitHub = [pscustomobject]@{ CliAvailable = $true; AuthReady = $false; Account = "" }
        Steam = [pscustomobject]@{
            ConfigPath = "missing"
            ConfigExists = $false
            SteamCmdPath = ""
            SteamCmdExists = $false
            UserNameConfigured = $false
            DesktopClientRunning = $false
            UploadLoginMode = "SteamCMDConfiguredUser"
            ExitCommand = "quit"
            ExplicitLogoutCommand = $false
        }
        ModIO = [pscustomobject]@{
            ConfigPath = "missing"
            ConfigExists = $false
            AccessTokenPath = "secret-sentinel-must-not-leak"
            AccessTokenExists = $false
            ModId = ""
            GameId = ""
            ApiBaseConfigured = $false
        }
    }
    $blockedReadiness = Get-ReleaseReadinessReport `
        -ToolsRoot $fixtureRoot `
        -RepositoryRoot $fixtureRoot `
        -ModName "ReadyMod" `
        -ProbeOverride $blockedProbe
    $blockerCodes = @($blockedReadiness.Blockers | ForEach-Object Code)
    @("UnityEditorMissing", "GitHubAuthMissing", "SteamConfigMissing", "ModIoConfigMissing", "ModIoCredentialMissing") |
        ForEach-Object { Assert-True ($_ -in $blockerCodes) "Missing readiness blocker: $_" }
    $blockedJson = $blockedReadiness | ConvertTo-Json -Depth 8
    Assert-True (-not $blockedJson.Contains("secret-sentinel-must-not-leak")) `
        "Readiness artifact leaked a credential path supplied only to the probe."
    Write-Host "PASS: current/backport readiness, human/JSON parity, blockers, and secret boundary"

    $backportChangesPath = Join-Path $testRoot "backport-changelog.md"
    Write-TestFile $backportChangesPath @'
# v4.0.10 (Timberborn v1.0) (August 23rd, 2026)

* [Update] Backport the current feature set.

# v4.7.0 (June 30th, 2026)

* [Feature] Current-line feature.
'@
    Assert-ReleaseVersionMatchesChangelog `
        $backportChangesPath "4.0.10" "BackportPatchOnly" "4.0.9"
    $implicitBaselineRejected = $false
    try {
        Assert-ReleaseVersionMatchesChangelog $backportChangesPath "4.0.10" "BackportPatchOnly"
    }
    catch {
        $implicitBaselineRejected = $_.Exception.Message.Contains("requires 4.7.1 after 4.7.0")
    }
    Assert-True $implicitBaselineRejected `
        "Shared changelog history must require an explicit previous source mod version for a backport."
    Write-Host "PASS: explicit backport source-version baseline"

    $packageFixtureRoot = Join-Path $testRoot "package-fixture"
    Write-TestFile `
        (Join-Path $packageFixtureRoot "Automation/version-1.0/manifest.json") `
        '{"Name":"Advanced Automation","Version":"4.0.10"}'
    Write-TestFile `
        (Join-Path $packageFixtureRoot "Automation/version-1.0/Scripts/AdvancedAutomation.dll") `
        "legacy-lane"
    Write-TestFile `
        (Join-Path $packageFixtureRoot "Automation/version-1.1/manifest.json") `
        '{"Name":"Advanced Automation","Version":"4.10.0"}'
    Write-TestFile `
        (Join-Path $packageFixtureRoot "Automation/version-1.1/Scripts/AdvancedAutomation.dll") `
        "current-lane"
    $packageFixtureZip = Join-Path $testRoot "Automation.zip"
    Compress-Archive -LiteralPath (Join-Path $packageFixtureRoot "Automation") `
        -DestinationPath $packageFixtureZip
    $packageFingerprints = @(Get-PackageLaneFingerprints $packageFixtureZip)
    $legacyFingerprint = @($packageFingerprints | Where-Object GameVersion -eq "version-1.0")
    Assert-Equal 1 $legacyFingerprint.Count "Legacy package lane fingerprint count"
    Assert-Equal "4.0.10" $legacyFingerprint[0].ManifestVersion "Legacy package lane manifest version"
    $publicPackageLane = Get-PublicPackageLaneFingerprint $packageFingerprints
    Assert-Equal "version-1.1" $publicPackageLane.GameVersion "Public package lane"
    Assert-Equal "4.10.0" $publicPackageLane.ManifestVersion "Public package version"
    Write-Host "PASS: source/public package version separation from final manifest lanes"

    $summaryChild = Join-Path $testRoot "summary-child.ps1"
    Write-TestFile $summaryChild @'
param([string] $FixtureName)

Write-Host "unimportant child detail"
Write-Host "Unity export completed for $FixtureName version-1.1"
Write-Host "C# build completed for $FixtureName version-1.1: fixture.dll"
Write-Host "Built package from local mod folder: fixture.zip"
Write-Host "Final Mod.IO readback: mod_id=1; active_file_id=2; version=3.0.0; date_scanned=4; virus_status=1; virus_positive=0; file_size=5"
'@
    $summaryLogRoot = Join-Path $testRoot "logs"
    [void](New-Item -ItemType Directory -Path $summaryLogRoot)
    $compactOutput = (& {
        Invoke-LoggedReleaseStep `
            -Name "Compact summary fixture" `
            -ScriptPath $summaryChild `
            -Arguments @("-FixtureName", "Fixture") `
            -LogDirectory $summaryLogRoot `
            -StepIndex 1
    } 6>&1 | Out-String)
    @(
        "Unity export completed for Fixture version-1.1",
        "C# build completed for Fixture version-1.1: fixture.dll",
        "Built package from local mod folder: fixture.zip",
        "Final Mod.IO readback:",
        "mod_id=1",
        "active_file_id=2",
        "version=3.0.0",
        "date_scanned=4",
        "virus_status=1",
        "virus_positive=0",
        "file_size=5"
    ) | ForEach-Object {
        Assert-True $compactOutput.Contains($_) "Compact output omitted nested operation evidence: $_"
    }
    Assert-True (-not $compactOutput.Contains("unimportant child detail")) `
        "Compact output unexpectedly exposed an unselected child detail."
    Write-Host "PASS: compact output exposes nested package mutations and final Mod.IO readback"

    $wikiRoot = Join-Path $testRoot "wiki"
    Write-TestFile `
        (Join-Path $wikiRoot "Ready-Mod.md") `
        "# Ready Mod`n`nWidgetSignal is part of the scripting API.`n"
    Write-TestFile (Join-Path $wikiRoot "Unrelated.md") "# Unrelated`n"
    $preflightFixture = [pscustomobject]@{
        ModName = "ReadyMod"
        SourceModVersion = "3.0.0"
        PublicPackageVersion = "3.0.0"
        PublicPackageGameVersion = "version-1.1"
        ReleaseCommit = $currentReadiness.Git.ReleaseCommit
        ReleaseLine = ""
        ReleaseRoot = $fixtureRoot
        VersionPolicy = "CurrentLine"
        TagName = "ReadyMod_3.0.0"
        Package = [pscustomobject]@{ Path = "ReadyMod.zip"; Sha256 = "package-sha"; Length = 123 }
        Source = [pscustomobject]@{ Path = "package/ReadyMod"; Sha256 = "source-sha"; FileCount = 4 }
        PackageLaneFingerprints = [object[]]@(
            [pscustomobject]@{ GameVersion = "version-1.0"; ManifestVersion = "2.9.4"; Sha256 = "lane-old" },
            [pscustomobject]@{ GameVersion = "version-1.1"; ManifestVersion = "3.0.0"; Sha256 = "lane-current" })
        ReleaseSources = [object[]]@([pscustomobject]@{
            ReleaseRoot = $fixtureRoot
            Branch = "main"
            Commit = $currentReadiness.Git.ReleaseCommit
            GameVersion = "version-1.1"
            SourceModVersion = "3.0.0"
            ReleaseLine = ""
            VersionPolicy = "CurrentLine"
            TagName = "ReadyMod_3.0.0"
            ChangesPath = (Join-Path $fixtureRoot "ReadyMod/CHANGELOG.md")
            IsPrimary = $true
        })
        PreflightOptions = [pscustomobject]@{
            PublishSteamVisibility = $false
            PublishModIoPage = $false
        }
        ReleaseLogDirectory = "preflight-logs"
        ReportPath = "preflight.json"
    }
    $readinessFixture = $currentReadiness | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $readinessFixture | Add-Member -NotePropertyName "ReportPath" -NotePropertyValue "readiness.json"
    $openIssues = [object[]]@(
        [pscustomobject]@{
            number = 42
            title = "ReadyMod WidgetSignal"
            url = "https://example.invalid/issues/42"
            body = "Release candidate"
            labels = [object[]]@([pscustomobject]@{ name = "ReadyMod" })
        },
        [pscustomobject]@{
            number = 43
            title = "ReadyMod follow-up"
            url = "https://example.invalid/issues/43"
            body = "Needs semantic review"
            labels = [object[]]@([pscustomobject]@{ name = "ReadyMod" })
        },
        [pscustomobject]@{
            number = 99
            title = "Other mod"
            url = "https://example.invalid/issues/99"
            body = "WidgetSignal appears incidentally"
            labels = [object[]]@([pscustomobject]@{ name = "Other" })
        })
    $issueEvidence = Get-ReleaseIssueCandidates $preflightFixture $openIssues
    Assert-Equal 2 @($issueEvidence.Candidates).Count "Issue candidate boundary"
    Assert-True (-not $issueEvidence.MutationsPerformed) "Issue evidence must never mutate issues."
    Assert-True (@($issueEvidence.Candidates | Where-Object Number -eq 99).Count -eq 0) `
        "Issue evidence included a term-only issue outside the mod scope."
    $wikiEvidence = Get-ReleaseWikiCandidates $preflightFixture $wikiRoot
    Assert-Equal 1 @($wikiEvidence.References).Count "Wiki evidence reference count"
    Assert-True (-not $wikiEvidence.MutationsPerformed) "Wiki evidence must never mutate pages."

    $modIoLog = Join-Path $testRoot "modio-final.log"
    Write-TestFile $modIoLog (
        "Final Mod.IO readback: mod_id=24680; active_file_id=13579; version=3.0.0; " +
        "date_scanned=123456; virus_status=1; virus_positive=0; file_size=98765`n")
    $successfulOperations = [object[]]@(
        [pscustomobject]@{
            Name = "Steam release publish"
            Status = "Succeeded"
            ExitCode = 0
            Summary = [string[]]@("SteamCMD upload completed.")
            Warnings = [string[]]@()
            LogPath = ""
            ExternalMutationPossible = $true
            ExternalOutcome = "Confirmed"
        },
        [pscustomobject]@{
            Name = "Mod.IO release publish"
            Status = "Succeeded"
            ExitCode = 0
            Summary = [string[]]@()
            Warnings = [string[]]@()
            LogPath = $modIoLog
            ExternalMutationPossible = $true
            ExternalOutcome = "Confirmed"
        })
    $externalEvidence = [pscustomobject]@{
        Steam = [pscustomobject]@{ PublishedFileId = "12345"; VisibilityResult = "Unchanged" }
        ModIO = [pscustomobject]@{ ModId = "24680"; VisibilityResult = "Unchanged" }
        Git = [pscustomobject]@{ Tags = [object[]]@([pscustomobject]@{ TagName = "ReadyMod_3.0.0"; RemoteVerified = $true }) }
        GitHub = [pscustomobject]@{ Url = "https://example.invalid/release"; Verified = $true }
    }
    $signoff = New-ReleaseSignoffReport `
        -Preflight $preflightFixture `
        -Readiness $readinessFixture `
        -Operations $successfulOperations `
        -IssueEvidence $issueEvidence `
        -WikiEvidence $wikiEvidence `
        -ExternalEvidence $externalEvidence `
        -Overrides ([pscustomobject]@{ PreserveChangelogDate = $false })
    Assert-Equal 1 $signoff.SchemaVersion "Signoff schema version"
    Assert-Equal "Published" $signoff.Status "Successful signoff status"
    Assert-Equal 13579 $signoff.Platforms.ModIO.FinalReadback.ActiveFileId "Final Mod.IO active file evidence"
    Assert-Equal "Pending" $signoff.MentorOutcome.Status "Mentor outcome gate"
    Assert-True (-not $signoff.FullySignedOff) "A pending MentorOutcome must prevent complete signoff."
    Assert-Equal 0 @($signoff.IncidentInventory.DiscoveryPreflightPublishDifferences.Differences).Count `
        "Matching discovery/preflight identities should have no differences."

    $signoffRoundTripPath = Join-Path $testRoot "signoff-roundtrip.json"
    Write-ReleaseSignoffReport $signoff $signoffRoundTripPath
    $signoffRoundTrip = Get-Content -Raw -LiteralPath $signoffRoundTripPath | ConvertFrom-Json
    Assert-Equal "package-sha" $signoffRoundTrip.Artifacts.Package.Sha256 "Signoff package hash round trip"
    Assert-Equal "EvidenceCandidatesOnly" $signoffRoundTrip.IssueAudit.Boundary "Issue evidence boundary round trip"

    $failedOperation = [pscustomobject]@{
        Name = "Mod.IO release publish"
        Status = "Failed"
        ExitCode = 7
        Summary = [string[]]@("Uploaded to Mod.IO as file id 999.")
        Warnings = [string[]]@("ambiguous fixture warning")
        LogPath = "failed-modio.log"
        ExternalMutationPossible = $true
        ExternalOutcome = "Unknown"
    }
    $partialSignoff = New-ReleaseSignoffReport `
        -Preflight $preflightFixture `
        -Readiness $readinessFixture `
        -Operations ([object[]]@($successfulOperations[0], $failedOperation, $failedOperation)) `
        -IssueEvidence $issueEvidence `
        -WikiEvidence $wikiEvidence `
        -ExternalEvidence $externalEvidence `
        -Overrides ([pscustomobject]@{ CorrectiveReplacement = $true }) `
        -PrerequisiteInterruption "fixture interruption"
    Assert-Equal "Partial" $partialSignoff.Status "Partial-success status"
    Assert-Equal 2 @($partialSignoff.IncidentInventory.FailedOperations).Count "Failed child operation inventory"
    Assert-Equal 1 @($partialSignoff.IncidentInventory.RetriedOperations).Count "Retried operation inventory"
    Assert-Equal 2 @($partialSignoff.IncidentInventory.AmbiguousOrUnknownOutcomes).Count `
        "Unknown external outcome inventory"
    Assert-Equal "Unresolved" $partialSignoff.MentorOutcome.Status "Partial release MentorOutcome"
    $discordFailure = [pscustomobject]@{
        Name = "Discord release handoff"
        Status = "Failed"
        ExitCode = 1
        Summary = [string[]]@()
        Warnings = [string[]]@("Discord fixture warning")
        LogPath = "discord.log"
        ExternalMutationPossible = $false
        ExternalOutcome = "NotApplicable"
        AffectsReleaseCompletion = $false
    }
    $discordWarningSignoff = New-ReleaseSignoffReport `
        -Preflight $preflightFixture `
        -Readiness $readinessFixture `
        -Operations ([object[]]@($successfulOperations + $discordFailure)) `
        -IssueEvidence $issueEvidence `
        -WikiEvidence $wikiEvidence `
        -ExternalEvidence $externalEvidence `
        -Overrides ([pscustomobject]@{})
    Assert-Equal "Published" $discordWarningSignoff.Status `
        "Discord preparation failure must remain a post-release warning"
    Assert-Equal 1 @($discordWarningSignoff.IncidentInventory.FailedOperations).Count `
        "Discord failure must remain visible in the incident inventory"
    Write-Host "PASS: signoff schema/evidence, incident inventory, issue/Wiki boundaries, and local round trip"
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
        $safePrefix = $temporaryBase.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
        $safeName = Split-Path -Leaf $resolvedTestRoot
        if (-not $resolvedTestRoot.StartsWith($safePrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
                -not $safeName.StartsWith("TimberbornMods-release-tooling-test-", [System.StringComparison]::Ordinal)) {
            throw "Refusing to remove unexpected test path: $resolvedTestRoot"
        }
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}

$verifyText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "verify-release.ps1")
$unityStepIndex = $verifyText.IndexOf('"Unity package source export"', [System.StringComparison]::Ordinal)
$buildStepIndex = $verifyText.IndexOf('"C# package source build"', [System.StringComparison]::Ordinal)
$steamStepIndex = $verifyText.IndexOf('"Steam release dry run"', [System.StringComparison]::Ordinal)
Assert-True ($unityStepIndex -ge 0 -and $unityStepIndex -lt $buildStepIndex -and $buildStepIndex -lt $steamStepIndex) `
    "Preflight must order shared Unity export, shared C# build, then platform dry runs."
Assert-True $verifyText.Contains('Add-SwitchArgument $commonPublishArgs "-SkipBuild" ($SkipBuild -or $preparesLocalModSource)') `
    "Platform dry runs are not forced to skip the shared C# build."
Assert-True $verifyText.Contains(
    'Add-SwitchArgument $commonPublishArgs "-SkipUnityExport" ($SkipUnityExport -or $preparesLocalModSource)') `
    "Platform dry runs are not forced to skip the shared Unity export."
Assert-True $verifyText.Contains("SchemaVersion = 5") `
    "Preflight report schema does not record the source/public version contract."
Assert-True $verifyText.Contains("PublicPackageVersion = `$publicPackageVersion") `
    "Preflight report is missing PublicPackageVersion."
Assert-True $verifyText.Contains('Assert-PathExists $resolvedReadinessReportPath "Release readiness report"') `
    "Preflight does not require the compact readiness artifact."
Assert-True $verifyText.Contains("Readiness = [ordered]@{") `
    "Preflight does not retain discovery/readiness evidence."

$publishText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "publish-release.ps1")
Assert-True $publishText.Contains('[switch] $PreserveChangelogDate') `
    "Release publication is missing the explicit changelog-date preservation confirmation."
Assert-True $publishText.Contains('Committed changelog date') `
    "Release publication does not identify the committed changelog date."
Assert-True $publishText.Contains('current local date:') `
    "Release publication does not surface committed and current dates."
Assert-True $publishText.Contains("Save-ReleaseSignoffArtifact `$true") `
    "Release publication does not write a successful final signoff artifact."
Assert-True $publishText.Contains("Save-ReleaseSignoffArtifact `$false") `
    "Release publication does not preserve a partial/failed signoff artifact."
Assert-True $publishText.Contains("Get-OpenGitHubIssues") `
    "Release publication does not collect compact issue evidence candidates."
Assert-True $publishText.Contains("Get-ReleaseWikiCandidates") `
    "Release publication does not collect compact Wiki evidence candidates."

$modIoText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "publish-modio.ps1")
$initialResponseIndex = $modIoText.IndexOf("Initial Mod.IO upload response", [System.StringComparison]::Ordinal)
$finalReadbackIndex = $modIoText.LastIndexOf("Final Mod.IO readback:", [System.StringComparison]::Ordinal)
Assert-True ($initialResponseIndex -ge 0 -and $initialResponseIndex -lt $finalReadbackIndex) `
    "The final Mod.IO readback must follow the initial upload response."
Assert-True $modIoText.Contains('[switch] $BackportPackageUpdate') `
    "Mod.IO publishing is missing explicit backport package update mode."
Assert-True $modIoText.Contains("Mod.IO backport upload did not create a distinct file id.") `
    "Mod.IO publishing does not require a distinct file id for a backport package update."

$steamText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "publish-steam.ps1")
Assert-True $steamText.Contains('& $SteamCmd +login $UserName +workshop_build_item $VdfPath +quit') `
    "Steam publication no longer uses the documented login, workshop upload, and SteamCMD quit sequence."
Assert-True ($steamText -notmatch "(?i)\+(logout|logoff)\b") `
    "Steam publication must not log off the configured account explicitly."
Assert-True ($publishText -notmatch "(?i)\+(logout|logoff)\b") `
    "Unified publication must not add an explicit Steam account logoff."
Write-Host "PASS: preflight ordering, platform skip contract, rollover, final readback, and Steam session boundary"

Write-Host "All release tooling tests passed."
