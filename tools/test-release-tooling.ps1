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
        "get-release-readiness.ps1",
        "materialize-release-package.ps1",
        "prepare-discord-release-message.ps1",
        "publish-github-release.ps1",
        "recover-release.ps1",
        "release-lines.json",
        "release-lines.psm1",
        "release-native-command.psm1",
        "release-notes.psm1",
        "release-output.psm1",
        "release-package-provenance.psm1",
        "release-readiness.psm1",
        "release-signoff.psm1",
        "release-unity.psm1",
        "release-versioning.psm1",
        "repository-paths.psm1",
        "update-platform-tags.ps1"
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
    Write-TestFile (Join-Path $fixtureRoot "PathlessMod/release.json") @'
{
  "ManifestPath": "",
  "ChangesPath": "",
  "ReleaseVersion": "1.0.0",
  "Package": {
    "Mode": "LocalModFolder",
    "SourcePath": "",
    "OutputPath": ""
  },
  "ManifestVersions": {
    "version-1.1": "1.0.0"
  },
  "Steam": { "PublishedFileId": "13579" }
}
'@
    Write-TestFile (Join-Path $fixtureRoot "XRay/release.json") @'
{
  "ManifestPath": "ModsUnityProject/Assets/Mods/XRay/manifest.json",
  "ChangesPath": "XRay/CHANGELOG.md",
  "ReleaseVersion": "1.3.0",
  "Package": {
    "Mode": "LocalModFolder",
    "SourcePath": "package/XRayPublic",
    "OutputPath": "package/XRay_v1.3.0.zip"
  },
  "ManifestVersions": {
    "version-1.0": "1.1.9",
    "version-1.1": "1.3.0"
  },
  "Steam": { "PublishedFileId": "67890" }
}
'@
    Write-TestFile (Join-Path $fixtureRoot "SmartPower/release.json") @'
{
  "ManifestPath": "ModsUnityProject/Assets/Mods/SmartPower/manifest.json",
  "ChangesPath": "SmartPower/CHANGELOG.md",
  "ReleaseVersion": "1.18.0",
  "Package": {
    "Mode": "LocalModFolder",
    "SourcePath": "package/SmartPower",
    "OutputPath": "package/SmartPower_v1.18.0.zip"
  },
  "ManifestVersions": {
    "version-1.0": "1.16.3",
    "version-1.1": "1.18.0"
  },
  "Steam": { "PublishedFileId": "3305038022" }
}
'@
    Write-TestFile (Join-Path $fixtureRoot "Automation/release.json") @'
{
  "ManifestPath": "ModsUnityProject/Assets/Mods/Automation/manifest.json",
  "ChangesPath": "Automation/CHANGELOG.md",
  "ReleaseVersion": "4.10.0",
  "Package": {
    "Mode": "LocalModFolder",
    "SourcePath": "package/Automation",
    "OutputPath": "package/Automation_v4.10.0.zip"
  },
  "ManifestVersions": {
    "version-1.0": "4.0.10",
    "version-1.1": "4.10.0"
  },
  "Steam": { "PublishedFileId": "3324234282" }
}
'@
    Write-TestFile `
        (Join-Path $fixtureRoot "ModsUnityProject/Assets/Mods/ReadyMod/manifest.json") `
        '{"Name":"Ready Mod","Version":"3.0.0"}'
    Write-TestFile `
        (Join-Path $fixtureRoot "ModsUnityProject/Assets/Mods/SmartPower/Data/workshop_data.json") `
        '{"ItemId":"3305038022","Name":"SmartPower","Visibility":"Private","Tags":["Quality of life"]}'
    Write-TestFile `
        (Join-Path $fixtureRoot "ModsUnityProject/Assets/Mods/Automation/Data/workshop_data.json") `
        '{"ItemId":"3324234282","Name":"Automation","Tags":["Quality of life"]}'
    Write-TestFile `
        (Join-Path $fixtureRoot "ModsUnityProject/Assets/Mods/XRay/Data/workshop_data.json") `
        '{"ItemId":"67890","Name":"XRay","Tags":["Quality of life"]}'
    Write-TestFile `
        (Join-Path $fixtureRoot "ModsUnityProject/ProjectSettings/ProjectVersion.txt") `
        "m_EditorVersion: 6000.5.2f1`n"
    Write-TestFile `
        (Join-Path $fixtureRoot "package/ReadyMod/version-1.0/manifest.json") `
        '{"Name":"Ready Mod","Version":"2.9.4"}'
    Write-TestFile `
        (Join-Path $fixtureRoot "package/ReadyMod/version-1.1/manifest.json") `
        '{"Name":"Ready Mod","Version":"3.0.0"}'
    Write-TestFile (Join-Path $fixtureRoot "package/XRayPublic/version-1.0/manifest.json") `
        '{"Name":"X-Ray","Version":"1.2.0"}'
    Write-TestFile (Join-Path $fixtureRoot "package/XRayPublic/version-1.1/manifest.json") `
        '{"Name":"X-Ray","Version":"1.3.0"}'
    Write-TestFile (Join-Path $fixtureRoot "package/SmartPower/version-1.0/manifest.json") `
        '{"Name":"Smart Power","Version":"1.16.4"}'
    Write-TestFile (Join-Path $fixtureRoot "package/SmartPower/version-1.1/manifest.json") `
        '{"Name":"Smart Power","Version":"1.18.0"}'
    Write-TestFile (Join-Path $fixtureRoot "package/SmartPower/workshop_data.json") `
        '{"ItemId":"3305038022","Name":"SmartPower","Visibility":"Private","UpdateVisibility":false}'
    Write-TestFile (Join-Path $fixtureRoot "package/Automation/version-1.0/manifest.json") `
        '{"Name":"Advanced Automation","Version":"4.0.11"}'
    Write-TestFile (Join-Path $fixtureRoot "package/Automation/version-1.1/manifest.json") `
        '{"Name":"Advanced Automation","Version":"4.10.0"}'
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

    Write-TestFile (Join-Path $lineRoot "SmartPower/CHANGELOG.md") @'
# v1.16.4 (Timberborn v1.0) (TBD)

* [Fix] Prepare an older-line fixture.

# v1.16.3 (Timberborn v1.0) (August 1st, 2026)

* [Fix] Previous older-line release.
'@
    Write-TestFile (Join-Path $lineRoot "SmartPower/release.json") @'
{
  "ManifestPath": "ModsUnityProject/Assets/Mods/SmartPower/manifest.json",
  "ChangesPath": "SmartPower/CHANGELOG.md",
  "ReleaseVersion": "1.17.0",
  "Package": {
    "Mode": "ExistingZip",
    "Path": "package/SmartPower_v1.17.0.zip"
  },
  "ManifestVersions": {
    "version-1.0": "1.16.3",
    "version-1.1": "1.17.0"
  }
}
'@
    Write-TestFile `
        (Join-Path $lineRoot "ModsUnityProject/Assets/Mods/SmartPower/manifest.json") `
        '{"Name":"Smart Power","Version":"1.16.4"}'
    Write-TestFile (Join-Path $lineRoot "Automation/CHANGELOG.md") @'
# v4.0.11 (Timberborn v1.0) (August 24th, 2026)

* [Fix] Prepared older-line release.

# v4.0.10 (Timberborn v1.0) (August 1st, 2026)

* [Fix] Previous older-line release.
'@
    Write-TestFile (Join-Path $lineRoot "Automation/release.json") @'
{
  "ManifestPath": "ModsUnityProject/Assets/Mods/Automation/manifest.json",
  "ChangesPath": "Automation/CHANGELOG.md",
  "ReleaseVersion": "4.0.11",
  "PreviousSourceModVersion": "4.0.10",
  "Package": {
    "Mode": "LocalModFolder",
    "SourcePath": "package/Automation",
    "OutputPath": "package/Automation_v4.0.11.zip"
  },
  "ManifestVersions": {
    "version-1.0": "4.0.11",
    "version-1.1": "4.10.0"
  },
  "Steam": { "PublishedFileId": "3324234282" }
}
'@
    Write-TestFile `
        (Join-Path $lineRoot "ModsUnityProject/Assets/Mods/Automation/manifest.json") `
        '{"Name":"Advanced Automation","Version":"4.0.11"}'
    [void](Invoke-TestNative "git" @("-C", $lineRoot, "add", "."))
    [void](Invoke-TestNative "git" @("-C", $lineRoot, "commit", "-m", "Add unmaterialized SmartPower fixture"))
    Write-TestFile (Join-Path $lineRoot "UnrelatedLineWork.txt") "later unrelated release-line work"
    [void](Invoke-TestNative "git" @("-C", $lineRoot, "add", "UnrelatedLineWork.txt"))
    [void](Invoke-TestNative "git" @("-C", $lineRoot, "commit", "-m", "Add unrelated release-line work"))
    Write-TestFile (Join-Path $fixtureRoot ".tools/modio/SmartPower.local.json") `
        '{"GameId":"3659","ModId":"3168318","ApiBase":"https://example.invalid/v1"}'
    Write-TestFile (Join-Path $fixtureRoot ".tools/modio/Automation.token.txt") `
        "fixture-known-owner-token"

    $readinessArtifact = Join-Path `
        $fixtureRoot ".tools/release-readiness/SmartPower-1.16.4-update-1.0.json"
    Push-Location $fixtureRoot
    try {
        $endToEndOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass `
            -File tools/get-release-readiness.ps1 -ModName SmartPower -ReleaseLine update-1.0 2>&1)
        $endToEndExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    Assert-Equal 0 $endToEndExitCode "Registered metadata-preparation readiness exit code"
    Assert-True (Test-Path -LiteralPath $readinessArtifact -PathType Leaf) `
        "Registered metadata-preparation readiness must emit a schema-1 artifact."
    $endToEndReadiness = Get-Content -Raw -LiteralPath $readinessArtifact | ConvertFrom-Json
    Assert-Equal 1 $endToEndReadiness.SchemaVersion "Registered metadata-preparation readiness schema"
    Assert-Equal "TimberbornReleaseReadiness" $endToEndReadiness.ArtifactType `
        "Registered metadata-preparation readiness artifact type"
    Assert-Equal "1.16.4" $endToEndReadiness.Candidate.SourceModVersion `
        "Registered candidate source version"
    Assert-Equal "1.16.3" $endToEndReadiness.Candidate.PreviousSourceModVersion `
        "Registered candidate previous source version"
    Assert-Equal "1.18.0" $endToEndReadiness.Candidate.PublicPackageVersion `
        "Registered candidate public package version"
    Assert-Equal "3305038022" $endToEndReadiness.Platforms.Steam.PublishedFileId `
        "Registered candidate inherited public Steam identity"
    Assert-True $endToEndReadiness.Gate1Plan.PreparationRequired `
        "Stale compatibility metadata must produce a preparation plan."
    Assert-True $endToEndReadiness.ReadyForGate1 `
        "Metadata preparation must be allowed without a second user gate."
    Assert-True (-not $endToEndReadiness.ReadyForPreflight) `
        "A pre-preparation artifact must not satisfy immutable preflight."
    Assert-Equal 0 @($endToEndReadiness.Gate1Blockers).Count `
        "External prerequisites must remain distinct from Gate 1 blockers"
    $plannedChangesByField = @{}
    foreach ($plannedChange in @($endToEndReadiness.Gate1Plan.PlannedMetadataChanges)) {
        $plannedChangesByField[[string]$plannedChange.Field] = [string]$plannedChange.DesiredValue
    }
    Assert-Equal "1.16.4" $plannedChangesByField["ReleaseVersion"] `
        "Planned compatibility ReleaseVersion"
    Assert-Equal "1.16.3" $plannedChangesByField["PreviousSourceModVersion"] `
        "Planned compatibility previous source version"
    Assert-Equal "1.16.4" $plannedChangesByField["ManifestVersions.version-1.0"] `
        "Planned target-lane manifest version"
    Assert-Equal "1.18.0" $plannedChangesByField["ManifestVersions.version-1.1"] `
        "Planned public-lane manifest version"
    Assert-Equal "3305038022" $plannedChangesByField["Steam.PublishedFileId"] `
        "Planned inherited Steam identity"
    $endToEndBlockers = @($endToEndReadiness.Blockers | ForEach-Object Code)
    Assert-True ("ChangelogVersionMismatch" -notin $endToEndBlockers) `
        "Stale compatibility ReleaseVersion must not become a changelog blocker."
    Assert-True ("SteamIdentityMissing" -notin $endToEndBlockers) `
        "Registered readiness must inherit the public Steam identity."
    Assert-Equal ".tools/release-readiness/SmartPower-1.16.4-update-1.0.json" `
        $endToEndReadiness.Gate1Plan.ReadinessReportPath `
        "Gate 1 plan must bind its exact readiness artifact"
    Assert-True ("-ReadinessReportPath" -in @($endToEndReadiness.Gate1Plan.Arguments)) `
        "Gate 1 arguments must carry the matching readiness artifact."
    Assert-Equal $fixtureRoot $endToEndReadiness.Package.OwnerRoot `
        "Registered readiness public package owner root"
    Assert-Equal (Join-Path $fixtureRoot "SmartPower/release.json") `
        $endToEndReadiness.Package.OwnerConfigPath `
        "Registered readiness public package owner config"
    Assert-Equal (Join-Path $fixtureRoot "package/SmartPower_v1.18.0.zip") `
        $endToEndReadiness.Package.ArtifactPath `
        "Registered readiness public package artifact"
    Assert-True ("-PackageOwnerRoot" -in @($endToEndReadiness.Gate1Plan.Arguments)) `
        "Gate 1 arguments must carry public package ownership."
    $authoritativeWorkshopDataPath = Join-Path `
        $fixtureRoot "ModsUnityProject/Assets/Mods/SmartPower/Data/workshop_data.json"
    $authoritativeWorkshopDataHash = (
        Get-FileHash -LiteralPath $authoritativeWorkshopDataPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-Equal $authoritativeWorkshopDataPath `
        $endToEndReadiness.Package.AuthoritativeWorkshopData.Path `
        "Readiness-bound public-owner workshop metadata path"
    Assert-Equal $authoritativeWorkshopDataHash `
        $endToEndReadiness.Package.AuthoritativeWorkshopData.Sha256 `
        "Readiness-bound public-owner workshop metadata hash"
    Assert-Equal "Quality of life" `
        (@($endToEndReadiness.Package.AuthoritativeWorkshopData.Tags) -join "|") `
        "Readiness-bound public-owner taxonomy intent"
    Assert-True ("-AuthoritativeWorkshopDataPath" -in @($endToEndReadiness.Gate1Plan.Arguments)) `
        "Gate 1 arguments must carry authoritative public-owner workshop metadata."
    Assert-Equal $endToEndReadiness.Git.HeadAtDiscovery `
        $endToEndReadiness.Gate1Plan.ExpectedHeadForPreparation `
        "Metadata preparation must use worktree HEAD rather than the prior release identity commit"
    Assert-True ($endToEndReadiness.Git.ReleaseCommit -ne $endToEndReadiness.Git.HeadAtDiscovery) `
        "Fixture must distinguish the release identity commit from later unrelated worktree HEAD."
    Assert-True `
        (-not (($endToEndOutput | Out-String) -match "Cannot bind argument to parameter 'Path'")) `
        "Optional empty paths must not escape as PowerShell parameter-binding failures."
    Write-Host "PASS: exact registered readiness command models public ownership and metadata preparation"

    Push-Location $fixtureRoot
    try {
        $alternateTokenOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass `
            -File tools/get-release-readiness.ps1 -ModName SmartPower -ReleaseLine update-1.0 `
            -ModIoAccessTokenPath .tools/modio/Automation.token.txt 2>&1)
        $alternateTokenExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    Assert-Equal 0 $alternateTokenExitCode "Registered alternate-owner-token readiness exit code"
    $alternateTokenReadiness = Get-Content -Raw -LiteralPath $readinessArtifact | ConvertFrom-Json
    Assert-Equal "ExplicitAlternateOwnerToken" `
        $alternateTokenReadiness.Platforms.ModIO.CredentialSource `
        "Alternate owner-token credential classification"
    Assert-Equal "RepositoryRelative" $alternateTokenReadiness.Platforms.ModIO.CredentialPathScope `
        "Alternate owner-token portable path scope"
    Assert-True $alternateTokenReadiness.Platforms.ModIO.CredentialReady `
        "Known explicit alternate owner token must satisfy credential readiness."
    Assert-True ("ModIoCredentialMissing" -notin @($alternateTokenReadiness.Blockers | ForEach-Object Code)) `
        "Known explicit alternate owner token must not produce a false blocker."
    $alternateGate1Arguments = [string[]]@($alternateTokenReadiness.Gate1Plan.Arguments)
    $tokenArgumentIndex = [Array]::IndexOf($alternateGate1Arguments, "-ModIoAccessTokenPath")
    Assert-True ($tokenArgumentIndex -ge 0) "Gate 1 plan omitted the selected alternate owner token."
    Assert-Equal ".tools/modio/Automation.token.txt" $alternateGate1Arguments[$tokenArgumentIndex + 1] `
        "Gate 1 alternate owner-token argument"
    Assert-True (-not (($alternateTokenReadiness.Platforms.ModIO | ConvertTo-Json -Depth 4) -match
            [regex]::Escape($fixtureRoot))) `
        "Readiness credential evidence must not serialize the fixture's machine-specific root."
    Assert-True (-not (($alternateTokenOutput | Out-String).Contains("fixture-known-owner-token"))) `
        "Readiness output leaked alternate owner-token contents."
    Write-Host "PASS: registered readiness carries a known alternate Mod.IO owner token without secret leakage"

    $preparedPublicConfigPath = Join-Path $fixtureRoot "SmartPower/release.json"
    $preparedPublicConfig = (Get-Content -Raw -LiteralPath $preparedPublicConfigPath).Replace(
        '"version-1.0": "1.16.3"',
        '"version-1.0": "1.16.4"')
    Write-TestFile $preparedPublicConfigPath $preparedPublicConfig

    @(
        (Join-Path $fixtureRoot "package/SmartPower/version-1.0/Scripts/SmartPower.dll"),
        (Join-Path $fixtureRoot "package/SmartPower/version-1.0/Scripts/SmartPower.xml"),
        (Join-Path $fixtureRoot "package/SmartPower/version-1.1/Scripts/SmartPower.dll"),
        (Join-Path $fixtureRoot "package/SmartPower/version-1.1/Scripts/SmartPower.xml"),
        (Join-Path $lineRoot "package/SmartPower/version-1.0/Scripts/SmartPower.dll"),
        (Join-Path $lineRoot "package/SmartPower/version-1.0/Scripts/SmartPower.xml")
    ) | ForEach-Object { Write-TestFile $_ "fixture-package-content" }
    Write-TestFile (Join-Path $lineRoot "package/SmartPower_v1.17.0.zip") "stale-registered-artifact"
    $staleRegisteredHash = (Get-FileHash `
        -LiteralPath (Join-Path $lineRoot "package/SmartPower_v1.17.0.zip") `
        -Algorithm SHA256).Hash
    $ownerConfigHash = (Get-FileHash `
        -LiteralPath (Join-Path $fixtureRoot "SmartPower/release.json") `
        -Algorithm SHA256).Hash
    & (Join-Path $fixtureTools "materialize-release-package.ps1") `
        -ModName SmartPower `
        -PackageOwnerRoot $fixtureRoot `
        -SourceReleaseRoot $lineRoot `
        -GameVersion version-1.0 `
        -SourceModVersion 1.16.4 `
        -Operation ImportLane `
        -ExpectedOwnerConfigSha256 $ownerConfigHash
    $tagMaterializationArguments = @(
        "-ModName", "SmartPower",
        "-ReleaseRoot", $fixtureRoot,
        "-LocalModRoot", (Join-Path $fixtureRoot "package/SmartPower"),
        "-AuthoritativeWorkshopDataPath", $authoritativeWorkshopDataPath,
        "-ExpectedAuthoritativeWorkshopDataSha256", $authoritativeWorkshopDataHash,
        "-MaterializeLocalOnly")
    $firstTagMaterializationOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File (Join-Path $fixtureTools "update-platform-tags.ps1") @tagMaterializationArguments 2>&1)
    Assert-Equal 0 $LASTEXITCODE "First authoritative taxonomy materialization exit code"
    $materializedWorkshopDataPath = Join-Path $fixtureRoot "package/SmartPower/workshop_data.json"
    $firstMaterializedWorkshopData = Get-Content -Raw -LiteralPath $materializedWorkshopDataPath |
        ConvertFrom-Json
    Assert-Equal "Mod|Update 1.0|Update 1.1|Quality of life" `
        (@($firstMaterializedWorkshopData.Tags) -join "|") `
        "First combined-package Steam taxonomy"
    Assert-True (($firstTagMaterializationOutput | Out-String).Contains(
            "Target Mod.IO tags: Mod, Update 1.0, Update 1.1, QoL")) `
        "Combined-package Mod.IO taxonomy did not map Quality of life to QoL."

    Write-TestFile $materializedWorkshopDataPath `
        '{"ItemId":"3305038022","Name":"SmartPower","Visibility":"Private","UpdateVisibility":false}'
    & (Join-Path $fixtureTools "materialize-release-package.ps1") `
        -ModName SmartPower `
        -PackageOwnerRoot $fixtureRoot `
        -SourceReleaseRoot $lineRoot `
        -GameVersion version-1.0 `
        -SourceModVersion 1.16.4 `
        -Operation ImportLane `
        -ExpectedOwnerConfigSha256 $ownerConfigHash
    $recoveryTagMaterializationOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File (Join-Path $fixtureTools "update-platform-tags.ps1") @tagMaterializationArguments 2>&1)
    Assert-Equal 0 $LASTEXITCODE "Recovery authoritative taxonomy materialization exit code"
    $recoveredWorkshopData = Get-Content -Raw -LiteralPath $materializedWorkshopDataPath | ConvertFrom-Json
    Assert-Equal "Mod|Update 1.0|Update 1.1|Quality of life" `
        (@($recoveredWorkshopData.Tags) -join "|") `
        "Recovered combined-package Steam taxonomy"
    Assert-True (($recoveryTagMaterializationOutput | Out-String).Contains(
            "Target Mod.IO tags: Mod, Update 1.0, Update 1.1, QoL")) `
        "Recovery Mod.IO taxonomy mapping drifted."
    & (Join-Path $fixtureTools "materialize-release-package.ps1") `
        -ModName SmartPower `
        -PackageOwnerRoot $fixtureRoot `
        -SourceReleaseRoot $lineRoot `
        -GameVersion version-1.0 `
        -SourceModVersion 1.16.4 `
        -Operation Package `
        -ExpectedOwnerConfigSha256 $ownerConfigHash
    $combinedPackagePath = Join-Path $fixtureRoot "package/SmartPower_v1.18.0.zip"
    Assert-True (Test-Path -LiteralPath $combinedPackagePath -PathType Leaf) `
        "Combined package was not materialized at the public owner's artifact path."
    $combinedLanes = @(Get-PackageLaneFingerprints $combinedPackagePath)
    Assert-Equal "1.16.4" `
        (@($combinedLanes | Where-Object GameVersion -eq "version-1.0")[0].ManifestVersion) `
        "Combined package changed older lane"
    Assert-Equal "1.18.0" `
        (@($combinedLanes | Where-Object GameVersion -eq "version-1.1")[0].ManifestVersion) `
        "Combined package unchanged public lane"
    Assert-Equal $staleRegisteredHash `
        (Get-FileHash -LiteralPath (Join-Path $lineRoot "package/SmartPower_v1.17.0.zip") `
            -Algorithm SHA256).Hash `
        "Stale registered-worktree artifact must not be selected or overwritten"
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $combinedArchive = [System.IO.Compression.ZipFile]::OpenRead($combinedPackagePath)
    try {
        $workshopEntry = @($combinedArchive.Entries | Where-Object {
            $_.FullName -eq "workshop_data.json" -or $_.FullName -like "*/workshop_data.json"
        }) | Select-Object -First 1
        Assert-True ($null -ne $workshopEntry) "Combined package omitted workshop_data.json."
        $reader = [System.IO.StreamReader]::new($workshopEntry.Open())
        try {
            $packagedWorkshopData = $reader.ReadToEnd() | ConvertFrom-Json
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $combinedArchive.Dispose()
    }
    Assert-Equal "Mod|Update 1.0|Update 1.1|Quality of life" `
        (@($packagedWorkshopData.Tags) -join "|") `
        "Combined artifact taxonomy after repeated materialization recovery"
    Write-Host "PASS: registered source lane materializes once into the current public package owner"

    $tagCases = @(
        [pscustomobject]@{
            Name = "absent"
            Json = '{"ItemId":"123","Name":"Fixture","Visibility":"Private","Nested":{"Keep":7},"UpdateVisibility":false}'
            ExpectedTags = @("Mod", "Update 1.0", "Update 1.1")
        },
        [pscustomobject]@{
            Name = "empty"
            Json = '{"ItemId":"123","Name":"Fixture","Visibility":"Private","Nested":{"Keep":7},"UpdateVisibility":false,"Tags":[]}'
            ExpectedTags = @("Mod", "Update 1.0", "Update 1.1")
        },
        [pscustomobject]@{
            Name = "existing"
            Json = '{"ItemId":"123","Name":"Fixture","Visibility":"Private","Nested":{"Keep":7},"UpdateVisibility":false,"Tags":["Mod","Update 0.9","Quality of life"]}'
            ExpectedTags = @("Mod", "Update 1.0", "Update 1.1", "Quality of life")
        }
    )
    foreach ($tagCase in $tagCases) {
        $tagRoot = Join-Path $testRoot "tag-$($tagCase.Name)"
        Write-TestFile (Join-Path $tagRoot "workshop_data.json") $tagCase.Json
        [void](New-Item -ItemType Directory -Path (Join-Path $tagRoot "version-1.0") -Force)
        [void](New-Item -ItemType Directory -Path (Join-Path $tagRoot "version-1.1") -Force)
        $tagOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass `
            -File (Join-Path $fixtureTools "update-platform-tags.ps1") `
            -ModName TagFixture `
            -ReleaseRoot $fixtureRoot `
            -LocalModRoot $tagRoot `
            -MaterializeLocalOnly 2>&1)
        Assert-Equal 0 $LASTEXITCODE "Local tag materialization exit code: $($tagCase.Name)"
        $savedTagJson = Get-Content -Raw -LiteralPath (Join-Path $tagRoot "workshop_data.json")
        $savedTagData = $savedTagJson | ConvertFrom-Json
        $actualTags = [string[]]@($savedTagData.Tags | ForEach-Object { [string]$_ })
        Assert-Equal ($tagCase.ExpectedTags -join "|") ($actualTags -join "|") `
            "Exact derived compatibility tags: $($tagCase.Name)"
        Assert-Equal "123" $savedTagData.ItemId "Unrelated ItemId preservation: $($tagCase.Name)"
        Assert-Equal "Fixture" $savedTagData.Name "Unrelated Name preservation: $($tagCase.Name)"
        Assert-Equal "Private" $savedTagData.Visibility `
            "Unrelated Visibility preservation: $($tagCase.Name)"
        Assert-Equal 7 $savedTagData.Nested.Keep "Nested property preservation: $($tagCase.Name)"
        Assert-Equal $false $savedTagData.UpdateVisibility `
            "Unrelated boolean preservation: $($tagCase.Name)"
        Assert-True ($savedTagJson -match '(?m)^\s+"Tags"\s*:\s*\[') `
            "workshop_data.json formatting contract omitted structured Tags: $($tagCase.Name)"
        Assert-True (-not (($tagOutput | Out-String) -match "Publish|POST|DELETE")) `
            "Local-only tag materialization exposed a public mutation operation: $($tagCase.Name)"
    }
    Write-Host "PASS: local tag materialization handles absent, empty, and existing Tags with exact readback"

    $ambiguousTagRoot = Join-Path $testRoot "tag-ambiguous-authority"
    $ambiguousAuthorityPath = Join-Path $testRoot "tracked-workshop-data-without-tags.json"
    Write-TestFile (Join-Path $ambiguousTagRoot "workshop_data.json") `
        '{"ItemId":"123","Name":"Fixture","Tags":["Quality of life"]}'
    Write-TestFile $ambiguousAuthorityPath '{"ItemId":"123","Name":"Fixture"}'
    [void](New-Item -ItemType Directory -Path (Join-Path $ambiguousTagRoot "version-1.0") -Force)
    $expectedFailurePreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $ambiguousTagOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass `
            -File (Join-Path $fixtureTools "update-platform-tags.ps1") `
            -ModName TagFixture `
            -ReleaseRoot $fixtureRoot `
            -LocalModRoot $ambiguousTagRoot `
            -AuthoritativeWorkshopDataPath $ambiguousAuthorityPath `
            -MaterializeLocalOnly 2>&1)
        $ambiguousTagExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $expectedFailurePreference
    }
    Assert-True ($ambiguousTagExitCode -ne 0) `
        "Absent authoritative Tags must stop before taxonomy materialization."
    Assert-True (($ambiguousTagOutput | Out-String) -match "does not declare Tags") `
        "Ambiguous authoritative tag intent did not report the taxonomy-removal boundary."
    $unchangedAmbiguousData = Get-Content -Raw -LiteralPath (
        Join-Path $ambiguousTagRoot "workshop_data.json") | ConvertFrom-Json
    $unchangedAmbiguousTags = @($unchangedAmbiguousData.Tags)
    Assert-Equal "Quality of life" ($unchangedAmbiguousTags -join "|") `
        "Ambiguous authoritative metadata changed local taxonomy before stopping"
    Write-Host "PASS: absent authoritative tag intent stops before taxonomy removal"

    $inventoryScript = Join-Path $fixtureTools "find-unpublished-releases.ps1"
    $humanOutput = (& $inventoryScript -RepositoryRoot $fixtureRoot | Out-String)
    $jsonOutput = (& $inventoryScript -RepositoryRoot $fixtureRoot -AsJson | Out-String)
    $jsonCandidates = @((ConvertFrom-Json $jsonOutput) | ForEach-Object { $_ })
    $candidateKeys = @($jsonCandidates | ForEach-Object { Get-CandidateKey $_ } | Sort-Object)
    $expectedKeys = @(
        "Alpha|2.0.0|main|||CurrentLine|Alpha/CHANGELOG.md",
        "SmartPower|1.16.4|update-1.0|update-1.0|version-1.0|BackportPatchOnly|SmartPower/CHANGELOG.md",
        "XRay|1.2.0|update-1.0|update-1.0|version-1.0|BackportPatchOnly|XRay/CHANGELOG.md"
    ) | Sort-Object
    Assert-Equal ($expectedKeys -join "`n") ($candidateKeys -join "`n") "Inventory JSON candidate set"

    $humanRows = @($humanOutput -split "`r?`n" | Where-Object { $_ -match "^(Alpha|SmartPower|XRay)\s+\d" })
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
        GitHub = [pscustomobject]@{
            CliAvailable = $true
            AuthReady = $true
            AuthStatusExitCode = 0
            ApiUserExitCode = 0
            Account = "fixture"
            EvidenceScope = "CurrentProcessCredentialAccessBoundary"
            StoredCredentialValidity = "ConfirmedInExecutionBoundary"
            CredentialMutationRecommended = $false
        }
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
            AccessTokenArgument = ".tools/modio/ReadyMod.token.txt"
            AccessTokenSelection = "PreferredPerModOwnerToken"
            AccessTokenPathScope = "RepositoryRelative"
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

    $pathlessReadiness = Get-ReleaseReadinessReport `
        -ToolsRoot $fixtureRoot `
        -RepositoryRoot $fixtureRoot `
        -ModName "PathlessMod" `
        -ProbeOverride $readyProbe
    $pathlessBlockers = @($pathlessReadiness.Blockers | ForEach-Object Code)
    @("ChangelogPathMissing", "ManifestPathMissing", "PackageSourcePathMissing", "PackageOutputPathMissing") |
        ForEach-Object {
            Assert-True ($_ -in $pathlessBlockers) "Missing structured optional-path blocker: $_"
        }
    Assert-Equal "BlockedMissingSourcePath" $pathlessReadiness.Package.PreparationState `
        "Empty LocalModFolder source preparation state"

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

    $preparedBackportReadiness = Get-ReleaseReadinessReport `
        -ToolsRoot $fixtureRoot `
        -RepositoryRoot $fixtureRoot `
        -ModName "Automation" `
        -ReleaseLine "update-1.0" `
        -ProbeOverride $readyProbe
    Assert-Equal "4.0.11" $preparedBackportReadiness.Candidate.SourceModVersion `
        "Prepared backport source version"
    Assert-Equal "4.0.10" $preparedBackportReadiness.Candidate.PreviousSourceModVersion `
        "Prepared backport previous source version"
    Assert-Equal "4.10.0" $preparedBackportReadiness.Candidate.PublicPackageVersion `
        "Prepared backport public package version"
    Assert-Equal "3324234282" $preparedBackportReadiness.Platforms.Steam.PublishedFileId `
        "Prepared backport public Steam identity"
    Assert-True (-not $preparedBackportReadiness.Gate1Plan.PreparationRequired) `
        "Already-prepared older-line metadata must not receive a false preparation plan."
    Assert-True $preparedBackportReadiness.ReadyForPreflight `
        "Already-prepared older-line metadata should be ready for immutable preflight."

    $humanReadiness = Format-ReleaseReadinessReport $backportReadiness | Out-String
    $jsonReadiness = $backportReadiness | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    @(
        $jsonReadiness.Candidate.ModName,
        $jsonReadiness.Candidate.SourceModVersion,
        $jsonReadiness.Candidate.PublicPackageVersion,
        $jsonReadiness.Candidate.ReleaseRoot,
        $jsonReadiness.Git.ReleaseCommit,
        $jsonReadiness.Git.IntendedTag,
        $jsonReadiness.Package.PreparationState
    ) | ForEach-Object {
        Assert-True $humanReadiness.Contains([string]$_) "Readiness human/JSON parity omitted: $_"
    }

    $blockedProbe = [pscustomobject]@{
        Unity = [pscustomobject]@{ ProjectVersion = "6000.5.2f1"; CanonicalEditorPath = ""; Ready = $false; Error = "missing" }
        GitHub = [pscustomobject]@{
            CliAvailable = $true
            AuthReady = $false
            AuthStatusExitCode = 1
            ApiUserExitCode = 1
            Account = ""
            EvidenceScope = "CurrentProcessCredentialAccessBoundary"
            StoredCredentialValidity = "Unknown"
            CredentialMutationRecommended = $false
        }
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
            AccessTokenArgument = ""
            AccessTokenSelection = "PreferredPerModOwnerToken"
            AccessTokenPathScope = "RepositoryRelative"
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
    Assert-Equal "CurrentProcessCredentialAccessBoundary" `
        $blockedReadiness.Platforms.GitHub.EvidenceScope "GitHub auth evidence scope"
    Assert-Equal "Unknown" $blockedReadiness.Platforms.GitHub.StoredCredentialValidity `
        "Sandbox-only GitHub failure must not classify the stored credential as invalid"
    Assert-True (-not $blockedReadiness.Platforms.GitHub.CredentialMutationRecommended) `
        "Readiness must not recommend credential mutation from boundary-local failure evidence."
    $blockedHumanReadiness = Format-ReleaseReadinessReport $blockedReadiness | Out-String
    Assert-True $blockedHumanReadiness.Contains("current credential-access boundary") `
        "Human readiness output must scope GitHub auth evidence to the current execution boundary."
    Assert-True $blockedHumanReadiness.Contains("stored credential validity=Unknown") `
        "Human readiness output must not imply that the stored GitHub credential is invalid."
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

    $recoveryOrigin = Join-Path $testRoot "recovery-origin.git"
    [void](Invoke-TestNative "git" @("init", "--bare", $recoveryOrigin))
    [void](Invoke-TestNative "git" @("-C", $fixtureRoot, "remote", "add", "origin", $recoveryOrigin))
    $recoveryTag = "SmartPower_1.16.4_update-1.0"
    $recoveryCommit = [string]$endToEndReadiness.Git.ReleaseCommit
    [void](Invoke-TestNative "git" @("-C", $lineRoot, "tag", $recoveryTag, $recoveryCommit))
    [void](Invoke-TestNative "git" @("-C", $lineRoot, "push", "origin", $recoveryTag))

    $recoveryBin = Join-Path $testRoot "recovery-bin"
    [void](New-Item -ItemType Directory -Path $recoveryBin)
    Write-TestFile (Join-Path $recoveryBin "gh.cmd") `
        '@powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0gh.ps1" %*'
    Write-TestFile (Join-Path $recoveryBin "gh.ps1") @'
param([Parameter(ValueFromRemainingArguments = $true)][string[]] $CommandArguments)
$ErrorActionPreference = "Stop"
$statePath = $env:RECOVERY_GH_STATE
$logPath = $env:RECOVERY_GH_LOG
[System.IO.File]::AppendAllText($logPath, (($CommandArguments -join "|") + [Environment]::NewLine))
if ($CommandArguments[0] -eq "api") {
    if ($CommandArguments[1] -eq "user" -or
            ($CommandArguments[1] -match "^repos/[^/]+/[^/]+$")) {
        Write-Output "{}"
        exit 0
    }
    if (Test-Path -LiteralPath $statePath) {
        Get-Content -Raw -LiteralPath $statePath
        exit 0
    }
    [Console]::Out.WriteLine("HTTP 404: Not Found")
    exit 1
}
if ($CommandArguments[0] -eq "issue" -and $CommandArguments[1] -eq "list") {
    Write-Output "[]"
    exit 0
}
if ($CommandArguments[0] -eq "release" -and $CommandArguments[1] -eq "list") {
    if (-not (Test-Path -LiteralPath $statePath)) {
        Write-Output "[]"
        exit 0
    }
    $state = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json
    @([ordered]@{ tagName = [string]$state.Tag }) | ConvertTo-Json -Compress
    exit 0
}
if ($CommandArguments[0] -eq "release" -and $CommandArguments[1] -eq "view") {
    if (-not (Test-Path -LiteralPath $statePath)) {
        [Console]::Error.WriteLine("release not found")
        exit 1
    }
    $state = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json
    [ordered]@{
        url = [string]$state.Url
        tagName = [string]$state.Tag
        name = [string]$state.Title
        body = [string]$state.Body
        assets = [object[]]@([ordered]@{
            name = [string]$state.AssetName
            digest = [string]$state.Digest
        })
    } | ConvertTo-Json -Depth 5 -Compress
    exit 0
}
if ($CommandArguments[0] -eq "release" -and $CommandArguments[1] -eq "create") {
    $tag = $CommandArguments[2]
    $packagePath = $CommandArguments[3]
    $titleIndex = [Array]::IndexOf($CommandArguments, "--title")
    $notesIndex = [Array]::IndexOf($CommandArguments, "--notes-file")
    $count = 0
    if (Test-Path -LiteralPath $statePath) {
        $count = [int](Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json).CreateCount
    }
    [ordered]@{
        CreateCount = $count + 1
        Url = "https://example.invalid/releases/$tag"
        Tag = $tag
        Title = $CommandArguments[$titleIndex + 1]
        Body = (Get-Content -Raw -LiteralPath $CommandArguments[$notesIndex + 1]).Trim()
        AssetName = [System.IO.Path]::GetFileName($packagePath)
        Digest = "sha256:$((Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant())"
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statePath -Encoding UTF8
    exit 0
}
[Console]::Error.WriteLine("Unsupported gh fixture command: $($CommandArguments -join ' ')")
exit 2
'@
    $recoveryGhState = Join-Path $testRoot "recovery-gh-state.json"
    $recoveryGhLog = Join-Path $testRoot "recovery-gh.log"
    Write-TestFile $recoveryGhLog ""

    $recoveryPackageLength = (Get-Item -LiteralPath $combinedPackagePath).Length
    $recoveryPackageHash = (
        Get-FileHash -LiteralPath $combinedPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $recoveryPreflightPath = Join-Path $testRoot "recovery-preflight.json"
    $recoverySignoffPath = Join-Path $testRoot "recovery-signoff.json"
    $recoveryReadbackPath = Join-Path $testRoot "recovery-platform-readback.json"
    $recoveryPreflight = [ordered]@{
        SchemaVersion = 5
        ReportPath = $recoveryPreflightPath
        ReadyForPublish = $true
        ModName = "SmartPower"
        SourceModVersion = "1.16.4"
        PublicPackageVersion = "1.18.0"
        PublicPackageGameVersion = "version-1.1"
        ReleaseCommit = $recoveryCommit
        ReleaseRoot = $lineRoot
        ReleaseBranch = "update-1.0"
        ReleaseLine = "update-1.0"
        VersionPolicy = "BackportPatchOnly"
        GameVersion = "version-1.0"
        TagName = $recoveryTag
        PackageOwnerRoot = $fixtureRoot
        Package = [ordered]@{
            Path = $combinedPackagePath
            Length = $recoveryPackageLength
            Sha256 = $recoveryPackageHash
        }
        Source = [ordered]@{ Path = (Join-Path $fixtureRoot "package/SmartPower"); Sha256 = "source" }
        PackageLaneFingerprints = [object[]]@($combinedLanes)
        ReleaseSources = [object[]]@([ordered]@{
            ReleaseRoot = $lineRoot
            Branch = "update-1.0"
            Commit = $recoveryCommit
            GameVersion = "version-1.0"
            SourceModVersion = "1.16.4"
            ReleaseLine = "update-1.0"
            VersionPolicy = "BackportPatchOnly"
            TagName = $recoveryTag
            ChangesPath = (Join-Path $lineRoot "SmartPower/CHANGELOG.md")
            IsPrimary = $true
        })
        PreflightOptions = [ordered]@{
            PublishSteamVisibility = $false
            PublishModIoPage = $false
            ModIoAccessTokenPath = ".tools/modio/Automation.token.txt"
        }
        Steps = [object[]]@()
        ReleaseLogDirectory = ""
    }
    Write-TestFile $recoveryPreflightPath ($recoveryPreflight | ConvertTo-Json -Depth 8)
    Write-TestFile $recoveryReadbackPath ([ordered]@{
        Steam = [ordered]@{
            PublishedFileId = "3305038022"
            Result = 1
            TimeUpdated = 1787714702
            FileSize = $recoveryPackageLength
            Visibility = 0
        }
        ModIO = [ordered]@{
            ModId = 3168318
            ActiveFileId = 8139362
            Version = "1.18.0"
            DateScanned = 1787714702
            VirusStatus = 1
            VirusPositive = 0
            FileSize = $recoveryPackageLength
        }
    } | ConvertTo-Json -Depth 5)
    $recoveryOperations = [object[]]@(
        [ordered]@{
            Name = "Steam release publish"; Status = "Succeeded"; ExitCode = 0
            Summary = [string[]]@("SteamCMD upload completed."); Warnings = [string[]]@(); LogPath = ""
            ExternalMutationPossible = $true; ExternalOutcome = "Confirmed"; AffectsReleaseCompletion = $true
        },
        [ordered]@{
            Name = "Mod.IO release publish"; Status = "Succeeded"; ExitCode = 0
            Summary = [string[]]@(
                "Final Mod.IO readback: mod_id=3168318; active_file_id=8139362; version=1.18.0; " +
                "date_scanned=1787714702; virus_status=1; virus_positive=0; file_size=$recoveryPackageLength")
            Warnings = [string[]]@(); LogPath = ""; ExternalMutationPossible = $true
            ExternalOutcome = "Confirmed"; AffectsReleaseCompletion = $true
        },
        [ordered]@{
            Name = "Platform tag publish"; Status = "Succeeded"; ExitCode = 0
            Summary = [string[]]@("Add: (none)", "Remove: (none)"); Warnings = [string[]]@(); LogPath = ""
            ExternalMutationPossible = $true; ExternalOutcome = "Confirmed"; AffectsReleaseCompletion = $true
        },
        [ordered]@{
            Name = "Git release tags"; Status = "Succeeded"; ExitCode = 0
            Summary = [string[]]@("Git tag created and pushed: $recoveryTag -> $recoveryCommit")
            Warnings = [string[]]@(); LogPath = ""; ExternalMutationPossible = $true
            ExternalOutcome = "Confirmed"; AffectsReleaseCompletion = $true
        },
        [ordered]@{
            Name = "GitHub release publish"; Status = "Failed"; ExitCode = 1
            Summary = [string[]]@("Stale compatibility asset selected before any GitHub mutation.")
            Warnings = [string[]]@(); LogPath = "github-failed.log"; ExternalMutationPossible = $true
            ExternalOutcome = "Unknown"; AffectsReleaseCompletion = $true
        })
    $recoveryPartial = [ordered]@{
        SchemaVersion = 1
        ArtifactType = "TimberbornReleaseSignoff"
        Status = "Partial"
        Identity = [ordered]@{
            ModName = "SmartPower"; SourceModVersion = "1.16.4"; PublicPackageVersion = "1.18.0"
            ReleaseCommit = $recoveryCommit; ReleaseSources = $recoveryPreflight.ReleaseSources
        }
        Artifacts = [ordered]@{
            Package = $recoveryPreflight.Package; Source = $recoveryPreflight.Source
            LaneFingerprints = $recoveryPreflight.PackageLaneFingerprints
        }
        Platforms = [ordered]@{
            Steam = [ordered]@{ PublishedFileId = "3305038022" }
            ModIO = [ordered]@{ FinalReadback = $recoveryReadbackPath | ForEach-Object {
                (Get-Content -Raw -LiteralPath $_ | ConvertFrom-Json).ModIO
            } }
        }
        Logs = [ordered]@{ PreflightReportPath = $recoveryPreflightPath; ReadinessReportPath = "" }
        Operations = $recoveryOperations
        PartialSuccess = [ordered]@{
            IsPartial = $true
            CompletedMutations = [string[]]@(
                "Steam release publish", "Mod.IO release publish", "Platform tag publish", "Git release tags")
            UnknownOutcomes = [object[]]@($recoveryOperations[-1])
        }
    }
    Write-TestFile $recoverySignoffPath ($recoveryPartial | ConvertTo-Json -Depth 12)

    $previousPath = $env:PATH
    $previousGhState = $env:RECOVERY_GH_STATE
    $previousGhLog = $env:RECOVERY_GH_LOG
    $env:PATH = "$recoveryBin;$previousPath"
    $env:RECOVERY_GH_STATE = $recoveryGhState
    $env:RECOVERY_GH_LOG = $recoveryGhLog
    try {
        $recoveryPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            $firstRecoveryOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass `
                -File (Join-Path $fixtureTools "recover-release.ps1") `
                -PreflightReportPath $recoveryPreflightPath `
                -PartialSignoffReportPath $recoverySignoffPath `
                -PlatformReadbackFixturePath $recoveryReadbackPath `
                -SkipDiscordHandoff 2>&1)
            $firstRecoveryExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $recoveryPreference
        }
        Assert-Equal 0 $firstRecoveryExitCode (
            "Bounded partial release recovery exit code. Output: " +
            (($firstRecoveryOutput | Out-String).Trim()))
        $firstRecovery = Get-Content -Raw -LiteralPath $recoverySignoffPath | ConvertFrom-Json
        $firstGhState = Get-Content -Raw -LiteralPath $recoveryGhState | ConvertFrom-Json
        Assert-Equal 1 $firstGhState.CreateCount "Exactly one missing GitHub Release mutation"
        Assert-Equal "SmartPower_v1.18.0.zip" $firstGhState.AssetName `
            "Recovery GitHub public-owner asset name"
        Assert-Equal "sha256:$recoveryPackageHash" $firstGhState.Digest `
            "Recovery GitHub public-owner asset digest"
        Assert-Equal "Published" $firstRecovery.Status "Recovered signoff status"
        Assert-Equal 8139362 $firstRecovery.Platforms.ModIO.FinalReadback.ActiveFileId `
            "Recovered Mod.IO final tuple"
        Assert-True $firstRecovery.Platforms.Steam.FinalReadbackVerified `
            "Recovered Steam final readback"
        Assert-True $firstRecovery.Platforms.Git.Tags[0].RemoteVerified `
            "Recovered remote qualified tag evidence"
        Assert-True $firstRecovery.Platforms.GitHub.Verified `
            "Recovered GitHub Release evidence"
        Assert-Equal "Pending" $firstRecovery.MentorOutcome.Status `
            "Recovered signoff MentorOutcome"
        Assert-Equal 1 @($firstRecovery.IncidentInventory.FailedOperations).Count `
            "Recovered signoff must retain the original failed GitHub child"
        Assert-Equal 0 @($firstRecovery.IncidentInventory.AmbiguousOrUnknownOutcomes).Count `
            "Recovered GitHub outcome must no longer remain ambiguous"
        Assert-Equal 1 @($firstRecovery.PartialSuccess.RecoveryHistory).Count `
            "First partial-recovery history entry"
        Assert-Equal 1 @($firstRecovery.Operations | Where-Object Name -eq "Steam release publish").Count `
            "Recovery must not rerun Steam publication"
        Assert-Equal 1 @($firstRecovery.Operations | Where-Object Name -eq "Mod.IO release publish").Count `
            "Recovery must not rerun Mod.IO publication"
        Assert-Equal 1 @($firstRecovery.Operations | Where-Object Name -eq "Platform tag publish").Count `
            "Recovery must not rerun platform tag mutation"
        Assert-Equal 1 @($firstRecovery.Operations | Where-Object Name -eq "Git release tags").Count `
            "Recovery must not recreate or repush Git tags"
        Assert-True (-not (($firstRecoveryOutput | Out-String) -match
                "Steam release publish|Mod.IO release publish|Platform tag publish|Git release tags")) `
            "Recovery output exposed a duplicate completed mutation step"

        $ErrorActionPreference = "Continue"
        try {
            $secondRecoveryOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass `
                -File (Join-Path $fixtureTools "recover-release.ps1") `
                -PreflightReportPath $recoveryPreflightPath `
                -PartialSignoffReportPath $recoverySignoffPath `
                -PlatformReadbackFixturePath $recoveryReadbackPath `
                -SkipDiscordHandoff 2>&1)
            $secondRecoveryExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $recoveryPreference
        }
        Assert-Equal 0 $secondRecoveryExitCode (
            "Idempotent recovery rerun exit code. Output: " +
            (($secondRecoveryOutput | Out-String).Trim()))
        $secondRecovery = Get-Content -Raw -LiteralPath $recoverySignoffPath | ConvertFrom-Json
        $secondGhState = Get-Content -Raw -LiteralPath $recoveryGhState | ConvertFrom-Json
        Assert-Equal 1 $secondGhState.CreateCount `
            "Idempotent recovery must not recreate an existing verified GitHub Release"
        Assert-Equal 2 @($secondRecovery.PartialSuccess.RecoveryHistory).Count `
            "Idempotent recovery history append"
        Assert-Equal "VerifiedExisting" $secondRecovery.PartialSuccess.RecoveryHistory[-1].GitHubAction `
            "Idempotent GitHub recovery action"
    }
    finally {
        $env:PATH = $previousPath
        $env:RECOVERY_GH_STATE = $previousGhState
        $env:RECOVERY_GH_LOG = $previousGhLog
    }
    Write-Host "PASS: bounded partial recovery performs one missing GitHub mutation and is idempotent"
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
$assemblyStepIndex = $verifyText.IndexOf('"Combined package lane assembly"', [System.StringComparison]::Ordinal)
$materializeStepIndex = $verifyText.IndexOf('"Combined package materialization"', [System.StringComparison]::Ordinal)
$modIoStepIndex = $verifyText.IndexOf('"Mod.IO release dry run"', [System.StringComparison]::Ordinal)
Assert-True ($unityStepIndex -ge 0 -and $unityStepIndex -lt $buildStepIndex -and
        $buildStepIndex -lt $assemblyStepIndex -and $assemblyStepIndex -lt $materializeStepIndex -and
        $materializeStepIndex -lt $steamStepIndex -and $steamStepIndex -lt $modIoStepIndex) `
    "Preflight must materialize the final combined package before either platform dry run."
Assert-True $verifyText.Contains('Add-SwitchArgument $commonPublishArgs "-SkipBuild" $true') `
    "Platform dry runs are not forced to skip the shared C# build."
Assert-True $verifyText.Contains(
    'Add-SwitchArgument $commonPublishArgs "-SkipUnityExport" $true') `
    "Platform dry runs are not forced to skip the shared Unity export."
Assert-True $verifyText.Contains(
    'Add-OptionalArgument $commonPublishArgs "-ExpectedPackageSha256" ([string]$packageSnapshot.Sha256)') `
    "Platform dry runs are not bound to the once-materialized package hash."
Assert-True $verifyText.Contains('$commonPublishArgs.Add($resolvedPackageOwnerRoot)') `
    "Platform dry runs do not use the readiness-bound public package owner."
Assert-True $verifyText.Contains('$commonPublishArgs.Add("-SourceReleaseRoot")') `
    "Platform dry runs do not retain the registered source/changelog owner."
Assert-True $verifyText.Contains("SchemaVersion = 5") `
    "Preflight report schema does not record the source/public version contract."
Assert-True $verifyText.Contains("PublicPackageVersion = `$publicPackageVersion") `
    "Preflight report is missing PublicPackageVersion."
Assert-True $verifyText.Contains('Assert-PathExists $resolvedReadinessReportPath "Release readiness report"') `
    "Preflight does not require the compact readiness artifact."
Assert-True $verifyText.Contains('$readinessReport.ReadyForPreflight -ne $true') `
    "Immutable preflight must reject a pre-preparation readiness artifact."
Assert-True $verifyText.Contains('Get-Gate1Argument "-ReadinessReportPath"') `
    "Immutable preflight does not bind the exact readiness artifact from Gate 1."
Assert-True $verifyText.Contains('Get-Gate1Argument "-ModIoAccessTokenPath"') `
    "Immutable preflight does not consume the readiness-bound Mod.IO owner token."
Assert-True $verifyText.Contains('Get-Gate1Argument "-AuthoritativeWorkshopDataPath"') `
    "Immutable preflight does not consume readiness-bound public-owner workshop metadata."
Assert-True $verifyText.Contains("AuthoritativeWorkshopData = [ordered]@{") `
    "Immutable preflight does not retain authoritative workshop metadata path/hash evidence."
Assert-True $verifyText.Contains('ModIoAccessTokenPath = $ModIoAccessTokenPath') `
    "Immutable preflight does not preserve the selected Mod.IO owner token for publication."
Assert-True $verifyText.Contains("Readiness = [ordered]@{") `
    "Preflight does not retain discovery/readiness evidence."

$publishText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "publish-release.ps1")
Assert-True $publishText.Contains('[switch] $PreserveChangelogDate') `
    "Release publication is missing the explicit changelog-date preservation confirmation."
Assert-True $publishText.Contains('Committed changelog date') `
    "Release publication does not identify the committed changelog date."
Assert-True $publishText.Contains('current local date:') `
    "Release publication does not surface committed and current dates."
Assert-True $publishText.Contains(
    'throw "Authoritative public-owner workshop metadata changed after immutable preflight."') `
    "Publication does not enforce immutable authoritative workshop metadata evidence."
Assert-True $publishText.Contains(
    'Add-OptionalArgument $tagArgs "-AuthoritativeWorkshopDataPath" $authoritativeWorkshopDataPath') `
    "Publication tag synchronization does not retain authoritative public-owner taxonomy ownership."
Assert-True $publishText.Contains("Save-ReleaseSignoffArtifact `$true") `
    "Release publication does not write a successful final signoff artifact."
Assert-True $publishText.Contains("Save-ReleaseSignoffArtifact `$false") `
    "Release publication does not preserve a partial/failed signoff artifact."
Assert-True $publishText.Contains("Get-OpenGitHubIssues") `
    "Release publication does not collect compact issue evidence candidates."
Assert-True $publishText.Contains("Get-ReleaseWikiCandidates") `
    "Release publication does not collect compact Wiki evidence candidates."
Assert-True $publishText.Contains('$preflightModIoAccessTokenPath = [string]$report.PreflightOptions.ModIoAccessTokenPath') `
    "Release publication does not reuse the preflight-bound Mod.IO owner token."
Assert-True $publishText.Contains('$commonPublishArgs.Add($packageOwnerRoot)') `
    "Release publication does not consume the preflight-bound public package owner."

$modIoText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "publish-modio.ps1")
Assert-True $modIoText.Contains('$changeNotes = Get-LatestChangeNotes $changesPath $sourceVersion') `
    "Mod.IO publication does not retain source-line changelog ownership."
$initialResponseIndex = $modIoText.IndexOf("Initial Mod.IO upload response", [System.StringComparison]::Ordinal)
$finalReadbackIndex = $modIoText.LastIndexOf("Final Mod.IO readback:", [System.StringComparison]::Ordinal)
Assert-True ($initialResponseIndex -ge 0 -and $initialResponseIndex -lt $finalReadbackIndex) `
    "The final Mod.IO readback must follow the initial upload response."
Assert-True $modIoText.Contains('[switch] $BackportPackageUpdate') `
    "Mod.IO publishing is missing explicit backport package update mode."
Assert-True $modIoText.Contains("Mod.IO backport upload did not create a distinct file id.") `
    "Mod.IO publishing does not require a distinct file id for a backport package update."

$steamText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "publish-steam.ps1")
Assert-True $steamText.Contains('$changeNotes = Get-LatestChangeNotes $changesPath $sourceVersion') `
    "Steam publication does not retain source-line changelog ownership."
Assert-True $steamText.Contains('& $SteamCmd +login $UserName +workshop_build_item $VdfPath +quit') `
    "Steam publication no longer uses the documented login, workshop upload, and SteamCMD quit sequence."
Assert-True ($steamText -notmatch "(?i)\+(logout|logoff)\b") `
    "Steam publication must not log off the configured account explicitly."
Assert-True ($publishText -notmatch "(?i)\+(logout|logoff)\b") `
    "Unified publication must not add an explicit Steam account logoff."
Write-Host "PASS: preflight ordering, platform skip contract, rollover, final readback, and Steam session boundary"

Write-Host "All release tooling tests passed."
