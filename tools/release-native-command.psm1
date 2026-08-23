function Invoke-ReleaseNativeCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Command,

        [string[]] $Arguments = @()
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $exitCode = -1
    try {
        $ErrorActionPreference = "Continue"
        $rawOutput = @(& $Command @Arguments 2>&1)
        $exitCode = [int]$LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    $standardOutput = [string[]]@(
        $rawOutput | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] } |
            ForEach-Object { [string]$_ })
    $standardError = [string[]]@(
        $rawOutput | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } |
            ForEach-Object { [string]$_ })

    return [pscustomobject]@{
        Command = $Command
        Arguments = [string[]]$Arguments
        Output = $standardOutput
        StandardError = $standardError
        Diagnostics = [string[]]@($rawOutput | ForEach-Object { [string]$_ })
        ExitCode = $exitCode
    }
}

Export-ModuleMember -Function Invoke-ReleaseNativeCommand
