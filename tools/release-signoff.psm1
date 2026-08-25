function Get-SignoffLogLines([object[]] $Operations) {
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($operation in @($Operations)) {
        foreach ($summary in @($operation.Summary)) {
            $lines.Add([string]$summary)
        }
        $logPath = [string]$operation.LogPath
        if (-not [string]::IsNullOrWhiteSpace($logPath) -and
                (Test-Path -LiteralPath $logPath -PathType Leaf)) {
            Get-Content -LiteralPath $logPath | ForEach-Object { $lines.Add([string]$_) }
        }
    }
    return [string[]]$lines.ToArray()
}

function Get-FinalModIoReadback([object[]] $Operations) {
    $pattern = (
        "Final Mod\.IO readback:\s*mod_id=(?<mod>\d+);\s*active_file_id=(?<file>\d+);\s*" +
        "version=(?<version>[^;]+);\s*date_scanned=(?<date>\d+);\s*virus_status=(?<status>-?\d+);\s*" +
        "virus_positive=(?<positive>-?\d+);\s*file_size=(?<size>\d+)")
    $matches = @(Get-SignoffLogLines $Operations | ForEach-Object {
        $match = [regex]::Match($_, $pattern)
        if ($match.Success) {
            [ordered]@{
                ModId = [long]$match.Groups["mod"].Value
                ActiveFileId = [long]$match.Groups["file"].Value
                Version = $match.Groups["version"].Value.Trim()
                DateScanned = [long]$match.Groups["date"].Value
                VirusStatus = [int]$match.Groups["status"].Value
                VirusPositive = [int]$match.Groups["positive"].Value
                FileSize = [long]$match.Groups["size"].Value
            }
        }
    })
    if ($matches.Count -eq 0) {
        return $null
    }
    return $matches[-1]
}

function Get-SignoffReleaseSection([object] $Source) {
    $changesPath = [string]$Source.ChangesPath
    if (-not (Test-Path -LiteralPath $changesPath -PathType Leaf)) {
        return ""
    }
    $content = Get-Content -Raw -LiteralPath $changesPath
    $version = [regex]::Escape([string]$Source.SourceModVersion)
    $match = [regex]::Match($content, "(?ms)^#\s+v?$version(?:\s|\(|:).+?(?=^#\s+v|\z)")
    if (-not $match.Success) {
        return ""
    }
    return $match.Value
}

function Get-ReleaseEvidenceTerms([object] $Preflight, [string[]] $AdditionalTerms) {
    $terms = New-Object System.Collections.Generic.List[string]
    $terms.Add([string]$Preflight.ModName)
    foreach ($source in @($Preflight.ReleaseSources)) {
        $section = Get-SignoffReleaseSection $source
        if (-not [string]::IsNullOrWhiteSpace($section)) {
            [regex]::Matches($section, '`(?<term>[A-Za-z][A-Za-z0-9_.-]{2,})`') | ForEach-Object {
                $terms.Add($_.Groups["term"].Value)
            }
        }
    }
    foreach ($term in @($AdditionalTerms)) {
        if (-not [string]::IsNullOrWhiteSpace($term)) {
            $terms.Add($term.Trim())
        }
    }
    return [string[]]@($terms | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
}

function Get-ReleaseIssueCandidates(
    [object] $Preflight,
    [object[]] $OpenIssues,
    [string[]] $EvidenceTerms = @()
) {
    $issueNumbers = New-Object System.Collections.Generic.HashSet[int]
    foreach ($source in @($Preflight.ReleaseSources)) {
        $section = Get-SignoffReleaseSection $source
        if (-not [string]::IsNullOrWhiteSpace($section)) {
            [regex]::Matches($section, "#(?<number>\d+)") | ForEach-Object {
                [void]$issueNumbers.Add([int]$_.Groups["number"].Value)
            }
        }
    }
    $terms = Get-ReleaseEvidenceTerms $Preflight $EvidenceTerms
    $modPattern = "(?<![A-Za-z0-9_])" + [regex]::Escape([string]$Preflight.ModName) + "(?![A-Za-z0-9_])"
    $candidates = New-Object System.Collections.Generic.List[object]
    foreach ($issue in @($OpenIssues)) {
        $labels = @($issue.labels | ForEach-Object {
            if ($null -ne $_.name) { [string]$_.name } else { [string]$_ }
        })
        $haystack = "$($issue.title)`n$($issue.body)`n$($labels -join ' ')"
        $matchedTerms = @($terms | Where-Object { $haystack -match [regex]::Escape($_) })
        $explicitReference = $issueNumbers.Contains([int]$issue.number)
        $modScoped = $haystack -match "(?i)$modPattern"
        if (-not $explicitReference -and -not $modScoped) {
            continue
        }
        $candidates.Add([ordered]@{
            Number = [int]$issue.number
            Title = [string]$issue.title
            Url = [string]$issue.url
            Labels = [string[]]$labels
            Evidence = [ordered]@{
                ChangelogReference = $explicitReference
                ModScoped = $modScoped
                MatchedTerms = [string[]]$matchedTerms
            }
            SemanticDecision = "PendingHumanReview"
        })
    }
    return [ordered]@{
        Boundary = "EvidenceCandidatesOnly"
        MutationsPerformed = $false
        Query = "Open issues filtered by explicit changelog issue numbers or exact mod scope."
        Terms = [string[]]$terms
        Candidates = [object[]]$candidates.ToArray()
        Decision = "PendingHumanReview"
    }
}

function Get-ReleaseWikiCandidates(
    [object] $Preflight,
    [string] $WikiRoot,
    [string[]] $EvidenceTerms = @()
) {
    $terms = Get-ReleaseEvidenceTerms $Preflight $EvidenceTerms
    $references = New-Object System.Collections.Generic.List[object]
    $available = -not [string]::IsNullOrWhiteSpace($WikiRoot) -and
        (Test-Path -LiteralPath $WikiRoot -PathType Container)
    if ($available) {
        foreach ($page in Get-ChildItem -LiteralPath $WikiRoot -Filter "*.md" -File) {
            $content = Get-Content -Raw -LiteralPath $page.FullName
            $matched = @($terms | Where-Object { $content -match [regex]::Escape($_) })
            if ($matched.Count -gt 0) {
                $references.Add([ordered]@{
                    Page = $page.Name
                    Path = $page.FullName
                    MatchedTerms = [string[]]$matched
                    SemanticDecision = "PendingWikiEditorReview"
                })
            }
        }
    }
    return [ordered]@{
        Boundary = "EvidenceCandidatesOnly"
        MutationsPerformed = $false
        WikiRoot = $WikiRoot
        Available = [bool]$available
        Query = "Markdown pages containing exact mod name, changelog identifiers, or explicit evidence terms."
        Terms = [string[]]$terms
        References = [object[]]$references.ToArray()
        Decision = "PendingWikiEditorReview"
    }
}

function Get-OpenGitHubIssues([string] $Repository) {
    $result = Invoke-ReleaseNativeCommand -Command "gh" -Arguments @(
        "issue", "list", "--repo", $Repository, "--state", "open", "--limit", "1000",
        "--json", "number,title,url,labels,body")
    if ($result.ExitCode -ne 0) {
        throw "Open-issue evidence collection failed.`n$($result.Diagnostics -join [Environment]::NewLine)"
    }
    $json = $result.Output -join [Environment]::NewLine
    if ([string]::IsNullOrWhiteSpace($json)) {
        return [object[]]@()
    }
    return [object[]]@((ConvertFrom-Json $json) | ForEach-Object { $_ })
}

function Get-ReleaseIncidentInventory(
    [object[]] $Operations,
    [string[]] $Warnings,
    [object] $Overrides,
    [object] $DiscoveryComparison,
    [string] $PrerequisiteInterruption
) {
    $failed = @($Operations | Where-Object { [string]$_.Status -eq "Failed" })
    $retries = @($Operations | Group-Object Name | Where-Object Count -gt 1 | ForEach-Object {
        [ordered]@{ Operation = $_.Name; Attempts = $_.Count }
    })
    $operationWarnings = @($Operations | ForEach-Object { @($_.Warnings) })
    $ambiguous = @($failed | Where-Object { [string]$_.ExternalOutcome -eq "Unknown" } | ForEach-Object {
        [ordered]@{ Operation = $_.Name; Outcome = "Unknown"; LogPath = $_.LogPath }
    })
    $recovery = @($Operations | Where-Object {
        [string]$_.Name -match "(?i)recover|replace|backport" -or
            (@($_.Summary) -join "`n") -match "(?i)recover|replace|backport"
    } | ForEach-Object {
        [ordered]@{ Operation = $_.Name; Evidence = [string[]]$_.Summary; LogPath = $_.LogPath }
    })
    $interruptions = if ([string]::IsNullOrWhiteSpace($PrerequisiteInterruption)) {
        [string[]]@()
    }
    else {
        [string[]]@($PrerequisiteInterruption)
    }
    return [ordered]@{
        FailedOperations = [object[]]$failed
        RetriedOperations = [object[]]$retries
        Warnings = [string[]]@($Warnings + $operationWarnings | Where-Object {
            -not [string]::IsNullOrWhiteSpace([string]$_)
        } | Sort-Object -Unique)
        Overrides = $Overrides
        RecoveryPaths = [object[]]$recovery
        AmbiguousOrUnknownOutcomes = [object[]]$ambiguous
        PrerequisiteInterruptions = $interruptions
        DiscoveryPreflightPublishDifferences = $DiscoveryComparison
    }
}

function Compare-ReleaseDiscovery([object] $Readiness, [object] $Preflight, [object[]] $Operations) {
    if ($null -eq $Readiness) {
        return [ordered]@{
            DiscoveryArtifactAvailable = $false
            Differences = [object[]]@([ordered]@{
                Field = "DiscoveryArtifact"
                Discovery = "Missing"
                Preflight = "Available"
                Publish = "Available"
            })
        }
    }
    $checks = @(
        @("ModName", [string]$Readiness.Candidate.ModName, [string]$Preflight.ModName),
        @("SourceModVersion", [string]$Readiness.Candidate.SourceModVersion, [string]$Preflight.SourceModVersion),
        @("PublicPackageVersion", [string]$Readiness.Candidate.PublicPackageVersion, [string]$Preflight.PublicPackageVersion),
        @("ReleaseRoot", [string]$Readiness.Candidate.ReleaseRoot, [string]$Preflight.ReleaseRoot),
        @("VersionPolicy", [string]$Readiness.Candidate.VersionPolicy, [string]$Preflight.VersionPolicy),
        @("ReleaseCommit", [string]$Readiness.Git.ReleaseCommit, [string]$Preflight.ReleaseCommit))
    $differences = @($checks | Where-Object { $_[1] -ne $_[2] } | ForEach-Object {
        [ordered]@{ Field = $_[0]; Discovery = $_[1]; Preflight = $_[2]; Publish = $_[2] }
    })
    return [ordered]@{
        DiscoveryArtifactAvailable = $true
        Differences = [object[]]$differences
        OperationCount = @($Operations).Count
    }
}

function New-ReleaseSignoffReport {
    param(
        [object] $Preflight,
        [object] $Readiness,
        [object[]] $Operations,
        [object] $IssueEvidence,
        [object] $WikiEvidence,
        [object] $ExternalEvidence,
        [object] $Overrides,
        [string[]] $Warnings = @(),
        [string] $PrerequisiteInterruption = ""
    )

    $operations = [object[]]@($Operations)
    $failed = @($operations | Where-Object {
        $_.Status -eq "Failed" -and $_.AffectsReleaseCompletion -ne $false
    })
    $unknown = @($failed | Where-Object ExternalOutcome -eq "Unknown")
    $completedMutations = @($operations | Where-Object {
        $_.Status -eq "Succeeded" -and $_.ExternalMutationPossible -eq $true
    } | ForEach-Object { [string]$_.Name })
    $discoveryComparison = Compare-ReleaseDiscovery $Readiness $Preflight $operations
    $allWarnings = [string[]]@($Warnings + @($Readiness.Warnings))
    $incidents = Get-ReleaseIncidentInventory `
        $operations $allWarnings $Overrides $discoveryComparison $PrerequisiteInterruption
    $modIoReadback = Get-FinalModIoReadback $operations
    $discordOperation = @($operations | Where-Object Name -eq "Discord release handoff" | Select-Object -Last 1)
    $status = if ($failed.Count -gt 0 -or -not [string]::IsNullOrWhiteSpace($PrerequisiteInterruption)) {
        if ($completedMutations.Count -gt 0 -or $unknown.Count -gt 0) { "Partial" } else { "Failed" }
    }
    else {
        "Published"
    }
    return [ordered]@{
        SchemaVersion = 1
        ArtifactType = "TimberbornReleaseSignoff"
        CreatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        Status = $status
        Identity = [ordered]@{
            ModName = [string]$Preflight.ModName
            SourceModVersion = [string]$Preflight.SourceModVersion
            PublicPackageVersion = [string]$Preflight.PublicPackageVersion
            PublicPackageGameVersion = [string]$Preflight.PublicPackageGameVersion
            ReleaseCommit = [string]$Preflight.ReleaseCommit
            ReleaseSources = [object[]]@($Preflight.ReleaseSources)
        }
        Artifacts = [ordered]@{
            Package = $Preflight.Package
            Source = $Preflight.Source
            LaneFingerprints = [object[]]@($Preflight.PackageLaneFingerprints)
        }
        Platforms = [ordered]@{
            Steam = $ExternalEvidence.Steam
            ModIO = [ordered]@{
                Publication = $ExternalEvidence.ModIO
                FinalReadback = $modIoReadback
            }
            Git = $ExternalEvidence.Git
            GitHub = $ExternalEvidence.GitHub
        }
        Visibility = [ordered]@{
            SteamIntent = if ($Preflight.PreflightOptions.PublishSteamVisibility) { "Publish" } else { "Unchanged" }
            SteamResult = $ExternalEvidence.Steam.VisibilityResult
            ModIoIntent = if ($Preflight.PreflightOptions.PublishModIoPage) { "Publish" } else { "Unchanged" }
            ModIoResult = $ExternalEvidence.ModIO.VisibilityResult
        }
        IssueAudit = $IssueEvidence
        WikiImpact = $WikiEvidence
        Discord = [ordered]@{
            Status = if ($discordOperation.Count -eq 0) { "SkippedOrNotReached" } else { $discordOperation[0].Status }
            MessageSent = $false
            Operation = if ($discordOperation.Count -eq 0) { $null } else { $discordOperation[0] }
        }
        Logs = [ordered]@{
            PreflightReportPath = [string]$Preflight.ReportPath
            ReadinessReportPath = if ($null -eq $Readiness) { "" } else { [string]$Readiness.ReportPath }
            PreflightLogDirectory = [string]$Preflight.ReleaseLogDirectory
            OperationLogDirectories = [string[]]@($operations | ForEach-Object {
                $operationLogPath = [string]$_.LogPath
                if (-not [string]::IsNullOrWhiteSpace($operationLogPath)) {
                    Split-Path -Parent $operationLogPath
                }
            } | Where-Object { $_ } | Sort-Object -Unique)
            OperationLogs = [string[]]@($operations | ForEach-Object { [string]$_.LogPath })
        }
        Operations = $operations
        IncidentInventory = $incidents
        PartialSuccess = [ordered]@{
            IsPartial = $status -eq "Partial"
            CompletedMutations = [string[]]$completedMutations
            UnknownOutcomes = [object[]]$unknown
            NextAction = if ($unknown.Count -gt 0) {
                "Read back every external boundary before retrying."
            }
            elseif ($status -eq "Partial") {
                "Resume only incomplete operations from durable evidence."
            }
            else {
                "None"
            }
        }
        MentorOutcome = [ordered]@{
            Status = if ($status -eq "Published") { "Pending" } else { "Unresolved" }
            RequiredHumanDecision = $true
            Decision = ""
            Rationale = ""
        }
        FullySignedOff = $false
    }
}

function Write-ReleaseSignoffReport([object] $Report, [string] $Path) {
    $parent = Split-Path -Parent $Path
    [void](New-Item -ItemType Directory -Path $parent -Force)
    $Report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Format-ReleaseSignoffReport([object] $Report) {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("Release signoff artifact: $($Report.Status)")
    $lines.Add(
        "  Identity: $($Report.Identity.ModName) source v$($Report.Identity.SourceModVersion); " +
        "public v$($Report.Identity.PublicPackageVersion)")
    $lines.Add("  Package SHA256: $($Report.Artifacts.Package.Sha256)")
    if ($null -ne $Report.Platforms.ModIO.FinalReadback) {
        $readback = $Report.Platforms.ModIO.FinalReadback
        $lines.Add(
            "  Mod.IO final: mod=$($readback.ModId); file=$($readback.ActiveFileId); " +
            "version=$($readback.Version); scan=$($readback.VirusStatus)/$($readback.VirusPositive)")
    }
    $lines.Add("  Issues: $(@($Report.IssueAudit.Candidates).Count) evidence candidates; no mutations")
    $lines.Add("  Wiki: $(@($Report.WikiImpact.References).Count) evidence references; no mutations")
    $lines.Add(
        "  Incidents: failed=$(@($Report.IncidentInventory.FailedOperations).Count); " +
        "unknown=$(@($Report.IncidentInventory.AmbiguousOrUnknownOutcomes).Count)")
    $lines.Add("  MentorOutcome: $($Report.MentorOutcome.Status) (human decision required)")
    return [string[]]$lines.ToArray()
}

Export-ModuleMember -Function Get-FinalModIoReadback, Get-ReleaseIssueCandidates, Get-ReleaseWikiCandidates, `
    Get-OpenGitHubIssues, Get-ReleaseIncidentInventory, Compare-ReleaseDiscovery, New-ReleaseSignoffReport, `
    Write-ReleaseSignoffReport, Format-ReleaseSignoffReport
