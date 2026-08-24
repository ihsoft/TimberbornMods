param()

$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "release-native-command.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-date.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-output.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-package-provenance.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "release-versioning.psm1") -Force

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
        "repository-paths.psm1"
    ) | ForEach-Object {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $_) -Destination (Join-Path $fixtureTools $_)
    }

    Write-TestFile (Join-Path $fixtureRoot "Alpha/CHANGELOG.md") "# v2.0.0 (TBD)`n"
    Write-TestFile (Join-Path $fixtureRoot "XRay/CHANGELOG.md") "# v1.1.9 (2026-08-01)`n"
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

$publishText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "publish-release.ps1")
Assert-True $publishText.Contains('[switch] $PreserveChangelogDate') `
    "Release publication is missing the explicit changelog-date preservation confirmation."
Assert-True $publishText.Contains('Committed changelog date') `
    "Release publication does not identify the committed changelog date."
Assert-True $publishText.Contains('current local date:') `
    "Release publication does not surface committed and current dates."

$modIoText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "publish-modio.ps1")
$initialResponseIndex = $modIoText.IndexOf("Initial Mod.IO upload response", [System.StringComparison]::Ordinal)
$finalReadbackIndex = $modIoText.LastIndexOf("Final Mod.IO readback:", [System.StringComparison]::Ordinal)
Assert-True ($initialResponseIndex -ge 0 -and $initialResponseIndex -lt $finalReadbackIndex) `
    "The final Mod.IO readback must follow the initial upload response."
Assert-True $modIoText.Contains('[switch] $BackportPackageUpdate') `
    "Mod.IO publishing is missing explicit backport package update mode."
Assert-True $modIoText.Contains("Mod.IO backport upload did not create a distinct file id.") `
    "Mod.IO publishing does not require a distinct file id for a backport package update."
Write-Host "PASS: preflight ordering, platform skip contract, rollover gate, and final readback ordering"

Write-Host "All release tooling tests passed."
