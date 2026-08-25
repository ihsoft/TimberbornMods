function New-ReleaseLogDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot,

        [Parameter(Mandatory = $true)]
        [string] $ModName,

        [Parameter(Mandatory = $true)]
        [string] $Version,

        [Parameter(Mandatory = $true)]
        [string] $Phase
    )

    $timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
    $directory = Join-Path $RepoRoot ".tools/release-logs/$ModName-$Version/$Phase-$timestamp-$PID"
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    return $directory
}

function Get-ReleaseOutputLines {
    param([object[]] $Output)

    return [string[]]@($Output | ForEach-Object { [string]$_ })
}

function Get-ReleaseWarningLines {
    param([string[]] $Lines)

    return [string[]]@(
        $Lines | Where-Object {
            ($_ -match "(?i)(^|\s)(warning|error)(\s|:|[A-Z]+\d+:)") -and
            ($_ -notmatch "^\s*0\s+(Warning|Error)\(s\)")
        } | Sort-Object -Unique
    )
}

function Get-ReleaseSummaryLines {
    param([string[]] $Lines)

    $patterns = @(
        "^\s*Already synchronized:",
        "^\s*Current tags:",
        "^\s*Target tags:",
        "^\s*Add:",
        "^\s*Remove:",
        "^\s*Visibility update:",
        "^Uploaded to Mod\.IO as file id ",
        "^Mod\.IO virus scan complete for file id ",
        "^Published to Mod\.IO and marked file id ",
        "^Final Mod\.IO readback:",
        "^SteamCMD upload completed\.",
        "^Unity export completed ",
        "^C# build completed ",
        "^Built package from local mod folder:",
        "^Steam tags are synchronized\.",
        "^Mod\.IO tags are synchronized\.",
        "^Local workshop_data\.json was updated for Steam\.",
        "^GitHub release asset verified ",
        "^https://github\.com/.+/releases/tag/",
        "^Discord channel opened\."
    )

    return [string[]]@(
        $Lines | Where-Object {
            $line = $_
            @($patterns | Where-Object { $line -match $_ }).Count -gt 0
        } | ForEach-Object { $_.Trim() } | Sort-Object -Unique
    )
}

function Invoke-LoggedReleaseStep {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [string] $ScriptPath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $LogDirectory,

        [Parameter(Mandatory = $true)]
        [int] $StepIndex,

        [switch] $DetailedOutput
    )

    if (-not (Test-Path -LiteralPath $ScriptPath)) {
        throw "$Name script not found: $ScriptPath"
    }

    $startedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    $safeName = $Name -replace "[^A-Za-z0-9._-]", "-"
    $logPath = Join-Path $LogDirectory ("{0:D2}-{1}.log" -f $StepIndex, $safeName)
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $ScriptPath @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    $lines = Get-ReleaseOutputLines $output
    $lines | Set-Content -LiteralPath $logPath -Encoding UTF8

    $warnings = Get-ReleaseWarningLines $lines
    $summaries = Get-ReleaseSummaryLines $lines
    $operation = [ordered]@{
        Name = $Name
        Script = $ScriptPath
        Arguments = [string[]]$Arguments
        Status = if ($exitCode -eq 0) { "Succeeded" } else { "Failed" }
        ExitCode = [int]$exitCode
        StartedAtUtc = $startedAtUtc
        CompletedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        LogPath = $logPath
        Summary = [string[]]$summaries
        Warnings = [string[]]$warnings
        ExternalMutationPossible = "-Publish" -in $Arguments
        ExternalOutcome = if ($exitCode -eq 0) { "Confirmed" } elseif ("-Publish" -in $Arguments) { "Unknown" } else { "NotApplicable" }
        AffectsReleaseCompletion = $true
    }

    if ($exitCode -ne 0) {
        Write-Host "Failed: $Name (exit code $exitCode)"
        $lines | ForEach-Object { Write-Host $_ }
        Write-Host "Full log: $logPath"
        $exception = [System.Exception]::new("$Name failed with exit code $exitCode.")
        $exception.Data["ReleaseOperation"] = $operation
        throw $exception
    }

    Write-Host "Completed: $Name"
    if ($DetailedOutput) {
        $lines | ForEach-Object { Write-Host $_ }
    }
    else {
        $summaries | ForEach-Object { Write-Host "  $_" }
        $warnings | ForEach-Object {
            $message = $_ -replace "(?i)^WARNING:\s*", ""
            Write-Warning $message
        }
    }
    Write-Host "  Full log: $logPath"

    return $operation
}

Export-ModuleMember -Function New-ReleaseLogDirectory, Invoke-LoggedReleaseStep
