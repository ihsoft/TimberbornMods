param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$helperPath = Join-Path $PSScriptRoot "commit-repository-changes.cmd"
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) `
    ("commit-repository-changes-" + [guid]::NewGuid().ToString("N"))
$testRoot = Join-Path $fixtureRoot "target"
$callerRoot = Join-Path $fixtureRoot "caller"

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-TestGit([string[]] $Arguments, [switch] $AllowFailure) {
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = @(& git @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }

    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $exitCode.`n$($output -join [Environment]::NewLine)"
    }
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = @($output | ForEach-Object { [string]$_ })
    }
}

function Invoke-CommitHelper([string[]] $Paths, [string] $Message, [string] $ExpectedHead) {
    $previousPreference = $ErrorActionPreference
    Push-Location $callerRoot
    try {
        $ErrorActionPreference = "Continue"
        $output = @(
            & $helperPath -RepositoryRoot $testRoot -CoordinationRoot $testRoot `
                -Path ($Paths -join ',') -Message $Message -Owner "commit-helper-regression-test" `
                -ExpectedHead $ExpectedHead 2>&1
        )
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
        Pop-Location
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = @($output | ForEach-Object { [string]$_ })
    }
}

function Write-TestFile([string] $RelativePath, [string] $Content) {
    $absolutePath = Join-Path $testRoot $RelativePath
    $parent = Split-Path -Parent $absolutePath
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($absolutePath, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Assert-PathChangedBetweenCommits([string] $RelativePath) {
    $result = Invoke-TestGit @("diff", "--quiet", "HEAD^", "HEAD", "--", $RelativePath) -AllowFailure
    Assert-True ($result.ExitCode -eq 1) "Expected the commit to change '$RelativePath'."
}

New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
New-Item -ItemType Directory -Path $callerRoot -Force | Out-Null
try {
    Push-Location $testRoot
    try {
        Invoke-TestGit @("init", "--quiet") | Out-Null
        Invoke-TestGit @("config", "user.name", "Commit Helper Test") | Out-Null
        Invoke-TestGit @("config", "user.email", "commit-helper@example.invalid") | Out-Null
        Invoke-TestGit @("config", "core.quotePath", "true") | Out-Null
        Invoke-TestGit @("config", "core.autocrlf", "false") | Out-Null
        Invoke-TestGit @("config", "commit.gpgsign", "false") | Out-Null

        $localizedPath = "locale/ru-RU/Автоматизация-(скрипты).md"
        $parenthesizedPath = "Automation-(scripting).md"
        $spacedPath = "notes/Release notes (draft).md"
        $foreignPath = "locale/ru-RU/Чужой файл (staged).md"
        $allPaths = @($parenthesizedPath, $localizedPath, $spacedPath, $foreignPath)
        foreach ($path in $allPaths) {
            Write-TestFile $path "initial $path`n"
        }
        Invoke-TestGit (@("add", "--") + $allPaths) | Out-Null
        Invoke-TestGit @("commit", "--quiet", "-m", "Initial fixture") | Out-Null

        Push-Location $callerRoot
        try {
            Invoke-TestGit @("init", "--quiet") | Out-Null
            Invoke-TestGit @("config", "user.name", "Caller Repository Test") | Out-Null
            Invoke-TestGit @("config", "user.email", "caller-repository@example.invalid") | Out-Null
            $callerFile = Join-Path $callerRoot "caller-only.md"
            [System.IO.File]::WriteAllText(
                $callerFile,
                "initial caller state`n",
                [System.Text.UTF8Encoding]::new($false))
            Invoke-TestGit @("add", "--", "caller-only.md") | Out-Null
            Invoke-TestGit @("commit", "--quiet", "-m", "Initial caller fixture") | Out-Null
            $callerHead = [string](Invoke-TestGit @("rev-parse", "HEAD")).Output[0]
            [System.IO.File]::WriteAllText(
                $callerFile,
                "preserved caller working change`n",
                [System.Text.UTF8Encoding]::new($false))
        } finally {
            Pop-Location
        }

        $requestedPaths = @($parenthesizedPath, $localizedPath, $spacedPath)
        foreach ($path in $requestedPaths) {
            Write-TestFile $path "updated $path`n"
        }
        $headBefore = [string](Invoke-TestGit @("rev-parse", "HEAD")).Output[0]
        $success = Invoke-CommitHelper $requestedPaths "Commit localized exact paths" $headBefore
        Assert-True ($success.ExitCode -eq 0) "Localized exact-path commit failed:`n$($success.Output -join [Environment]::NewLine)"
        foreach ($path in $requestedPaths) {
            Assert-PathChangedBetweenCommits $path
        }
        $commitPathCount = (Invoke-TestGit @(
            "diff-tree", "--no-commit-id", "--name-only", "-r", "HEAD"
        )).Output.Count
        Assert-True ($commitPathCount -eq $requestedPaths.Count) "The helper committed an unexpected path count."
        Assert-True ((Invoke-TestGit @("diff", "--cached", "--quiet") -AllowFailure).ExitCode -eq 0) `
            "The successful helper run left staged content."
        $quotePath = [string](Invoke-TestGit @("config", "--get", "core.quotePath")).Output[0]
        Assert-True ($quotePath -eq "true") "The helper changed the repository's core.quotePath setting."

        Write-TestFile $localizedPath "second update`n"
        Write-TestFile $foreignPath "foreign staged update`n"
        Invoke-TestGit @("add", "--", $foreignPath) | Out-Null
        $headBeforeRejection = [string](Invoke-TestGit @("rev-parse", "HEAD")).Output[0]
        $rejected = Invoke-CommitHelper @($localizedPath) "Must reject foreign staging" $headBeforeRejection
        Assert-True ($rejected.ExitCode -ne 0) "The helper accepted pre-existing foreign staged content."
        $headAfterRejection = [string](Invoke-TestGit @("rev-parse", "HEAD")).Output[0]
        Assert-True ($headAfterRejection -eq $headBeforeRejection) "Foreign staged rejection created a commit."
        Assert-True ((Invoke-TestGit @("diff", "--cached", "--quiet", "--", $foreignPath) -AllowFailure).ExitCode -eq 1) `
            "Foreign staged content was altered during rejection."
        Assert-True ((Invoke-TestGit @("diff", "--cached", "--quiet", "--", $localizedPath) -AllowFailure).ExitCode -eq 0) `
            "The requested path was staged despite foreign staged content."

        Invoke-TestGit @("restore", "--staged", "--", $foreignPath) | Out-Null
        Invoke-TestGit @("restore", "--", $foreignPath) | Out-Null
        $hookPath = Join-Path $testRoot ".git/hooks/pre-commit"
        [System.IO.File]::WriteAllText($hookPath, "#!/bin/sh`nexit 9`n", [System.Text.UTF8Encoding]::new($false))
        $headBeforeHook = [string](Invoke-TestGit @("rev-parse", "HEAD")).Output[0]
        $hookFailure = Invoke-CommitHelper @($localizedPath) "Hook failure cleanup" $headBeforeHook
        Assert-True ($hookFailure.ExitCode -ne 0) "The failing pre-commit hook did not stop the helper."
        Assert-True ((Invoke-TestGit @("diff", "--cached", "--quiet") -AllowFailure).ExitCode -eq 0) `
            "The helper did not restore staging after commit failure."
        Assert-True ((Invoke-TestGit @("diff", "--quiet", "--", $localizedPath) -AllowFailure).ExitCode -eq 1) `
            "The helper cleanup discarded the requested working-tree change."
        $headAfterHook = [string](Invoke-TestGit @("rev-parse", "HEAD")).Output[0]
        Assert-True ($headAfterHook -eq $headBeforeHook) "The failed commit hook changed HEAD."

        Push-Location $callerRoot
        try {
            $callerHeadAfter = [string](Invoke-TestGit @("rev-parse", "HEAD")).Output[0]
            Assert-True ($callerHeadAfter -eq $callerHead) "The helper changed the caller repository HEAD."
            Assert-True ((Invoke-TestGit @("diff", "--cached", "--quiet") -AllowFailure).ExitCode -eq 0) `
                "The helper changed the caller repository index."
            Assert-True ((Invoke-TestGit @("diff", "--quiet", "--", "caller-only.md") -AllowFailure).ExitCode -eq 1) `
                "The helper discarded the caller repository working-tree change."
        } finally {
            Pop-Location
        }
    } finally {
        Pop-Location
    }

    Write-Host "PASS (Windows PowerShell $($PSVersionTable.PSVersion)): commit helper preserves literal Unicode exact paths, rejects foreign staging, and restores staging on failure."
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
