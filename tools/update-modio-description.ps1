param(
    [Parameter(Mandatory = $true)]
    [string] $ModName,

    [string] $ConfigPath = "",
    [string] $AccessTokenPath = "",
    [switch] $Publish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-RepoPath([string] $Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }
    return Join-Path $repoRoot $Path
}

function Assert-PathExists([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Get-VisibleHtmlText([string] $Html) {
    Add-Type -AssemblyName System.Web

    $text = [System.Web.HttpUtility]::HtmlDecode($Html)
    $text = $text -replace "<br\s*/?>", "`n"
    $text = $text -replace "</p>|</div>|</h\d>|</li>", "`n"
    $text = $text -replace "<[^>]+>", " "
    $text = $text -replace "\[github\.com\]", ""
    $text = $text -replace "\s+", " "
    return $text.Trim()
}

function Get-LocalDescriptionPath([string] $Name) {
    if ($Name -eq "CustomTools") {
        return "CustomTools/Workshop/ModIO-Description.html"
    }
    return "$Name/workshop/description-ModIO.html"
}

function Resolve-ConfigPath([string] $Path, [string] $Name) {
    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        return Resolve-RepoPath $Path
    }
    return Resolve-RepoPath ".tools/modio/$Name.local.json"
}

function Resolve-AccessTokenPath([string] $Path, [string] $Name) {
    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        return Resolve-RepoPath $Path
    }
    return Resolve-RepoPath ".tools/modio/$Name.token.txt"
}

function Get-ModIoDescription([object] $Config, [string] $AccessToken) {
    $endpoint = "$($Config.ApiBase.TrimEnd("/"))/games/$($Config.GameId)/mods/$($Config.ModId)"
    $response = Invoke-RestMethod -Uri $endpoint -Headers @{ Authorization = "Bearer $AccessToken" }
    return [string]$response.description
}

function Update-ModIoDescription([object] $Config, [string] $AccessToken, [string] $Description) {
    Add-Type -AssemblyName System.Net.Http

    $endpoint = "$($Config.ApiBase.TrimEnd("/"))/games/$($Config.GameId)/mods/$($Config.ModId)"
    $client = [System.Net.Http.HttpClient]::new()
    $content = [System.Net.Http.MultipartFormDataContent]::new()
    try {
        $client.DefaultRequestHeaders.Authorization =
            [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $AccessToken)
        $content.Add([System.Net.Http.StringContent]::new($Description), "description")

        $response = $client.PostAsync($endpoint, $content).GetAwaiter().GetResult()
        $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw "Mod.IO description update failed: HTTP $([int]$response.StatusCode) $($response.ReasonPhrase)`n$responseBody"
        }
        return $responseBody | ConvertFrom-Json
    }
    finally {
        $content.Dispose()
        $client.Dispose()
    }
}

$configPath = Resolve-ConfigPath $ConfigPath $ModName
$accessTokenPath = Resolve-AccessTokenPath $AccessTokenPath $ModName
$localPath = Resolve-RepoPath (Get-LocalDescriptionPath $ModName)

Assert-PathExists $configPath "Mod.IO config"
Assert-PathExists $accessTokenPath "Mod.IO access token"
Assert-PathExists $localPath "Local Mod.IO description"

$config = Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json
$accessToken = (Get-Content -Raw -LiteralPath $accessTokenPath).Trim()
if ([string]::IsNullOrWhiteSpace($accessToken)) {
    throw "Mod.IO access token is empty: $accessTokenPath"
}

$description = Get-Content -Raw -LiteralPath $localPath

Write-Host "Mod.IO description update plan for $ModName"
Write-Host "Config: $configPath"
Write-Host "Local description: $localPath"
Write-Host "Endpoint: $($config.ApiBase.TrimEnd("/"))/games/$($config.GameId)/mods/$($config.ModId)"

$current = Get-ModIoDescription $config $accessToken
$alreadySynced = (Get-VisibleHtmlText $description) -eq (Get-VisibleHtmlText $current)
Write-Host "Already synchronized: $alreadySynced"

if (-not $Publish) {
    Write-Host "Dry run only. Nothing was uploaded. Use -Publish only after an explicit description update request."
    exit 0
}

$updated = Update-ModIoDescription $config $accessToken $description
if ((Get-VisibleHtmlText $description) -ne (Get-VisibleHtmlText ([string]$updated.description))) {
    throw "Mod.IO description update completed, but live visible text does not match local description."
}

Write-Host "Mod.IO description is synchronized."
