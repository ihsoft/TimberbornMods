param(
    [Parameter(Mandatory = $true)]
    [string] $PlanPath,

    [Parameter(Mandatory = $true)]
    [string] $AccessTokenPath,

    [switch] $Create
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-RequiredPath([string] $Path, [string] $Description) {
    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $resolved) {
        throw "$Description not found: $Path"
    }
    return $resolved.Path
}

function Add-StringPart(
        [System.Net.Http.MultipartFormDataContent] $Content,
        [string] $Name,
        [string] $Value) {
    $part = [System.Net.Http.StringContent]::new($Value, [System.Text.Encoding]::UTF8)
    $Content.Add($part, $Name)
}

$resolvedPlanPath = Resolve-RequiredPath $PlanPath "Creation plan"
$resolvedTokenPath = Resolve-RequiredPath $AccessTokenPath "Mod.IO access token"
$plan = Get-Content -Raw -LiteralPath $resolvedPlanPath | ConvertFrom-Json

$requiredFields = @(
    "ApiBase", "GameId", "Name", "NameId", "Summary", "DescriptionPath", "LogoPath", "ResultPath"
)
foreach ($field in $requiredFields) {
    if ([string]::IsNullOrWhiteSpace([string]$plan.$field)) {
        throw "Creation plan field is required: $field"
    }
}
if ([int]$plan.Visible -ne 0) {
    throw "Identity creation supports Hidden visibility only."
}

$descriptionPath = Resolve-RequiredPath ([string]$plan.DescriptionPath) "Description source"
$logoPath = Resolve-RequiredPath ([string]$plan.LogoPath) "Logo source"
$resultPath = [System.IO.Path]::GetFullPath([string]$plan.ResultPath)
if (Test-Path -LiteralPath $resultPath) {
    throw "Result already exists; refusing duplicate creation: $resultPath"
}

$accessToken = (Get-Content -Raw -LiteralPath $resolvedTokenPath).Trim()
if ([string]::IsNullOrWhiteSpace($accessToken)) {
    throw "Mod.IO access token is empty: $resolvedTokenPath"
}
$headers = @{ Authorization = "Bearer $accessToken" }
$apiBase = ([string]$plan.ApiBase).TrimEnd("/")
$gameId = [string]$plan.GameId
$description = Get-Content -Raw -LiteralPath $descriptionPath
$tags = @($plan.Tags | ForEach-Object { [string]$_ })

$me = Invoke-RestMethod -Uri "$apiBase/me" -Headers $headers
$game = Invoke-RestMethod -Uri "$apiBase/games/$gameId" -Headers $headers
$ownedMods = Invoke-RestMethod -Uri "$apiBase/me/mods?_limit=100" -Headers $headers
$duplicate = @($ownedMods.data | Where-Object {
    [int]$_.game_id -eq [int]$gameId -and
    ([string]$_.name_id -eq [string]$plan.NameId -or [string]$_.name -eq [string]$plan.Name)
})

Write-Host "Mod.IO identity creation plan"
Write-Host "  Account: $($me.username) ($($me.id))"
Write-Host "  Game: $($game.name) ($($game.id))"
Write-Host "  Name: $($plan.Name)"
Write-Host "  Name ID: $($plan.NameId)"
Write-Host "  Visibility: Hidden"
Write-Host "  Tags: $($tags -join ', ')"
Write-Host "  Community options: $($plan.CommunityOptions)"
Write-Host "  Dependency after identity adoption: $($plan.DependencyPreview)"
Write-Host "  Description: $descriptionPath"
Write-Host "  Logo: $logoPath"
Write-Host "  Modfile upload: disabled (this tool never calls the files endpoint with POST)"
Write-Host "  Result: $resultPath"

if ($duplicate.Count -gt 0) {
    $duplicateText = $duplicate | ForEach-Object { "$($_.id):$($_.name):$($_.visible)" }
    throw "A matching owned Mod.IO page already exists: $($duplicateText -join ', ')"
}
Write-Host "  Existing matching owned page: none"

if (-not $Create) {
    Write-Host "Dry run only. No Mod.IO page was created."
    return
}

Add-Type -AssemblyName System.Net.Http
$client = [System.Net.Http.HttpClient]::new()
$client.DefaultRequestHeaders.Authorization =
    [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $accessToken)
$content = [System.Net.Http.MultipartFormDataContent]::new()
$logoStream = $null
try {
    Add-StringPart $content "name" ([string]$plan.Name)
    Add-StringPart $content "name_id" ([string]$plan.NameId)
    Add-StringPart $content "summary" ([string]$plan.Summary)
    Add-StringPart $content "description" $description
    Add-StringPart $content "visible" "0"
    Add-StringPart $content "maturity_option" "0"
    Add-StringPart $content "community_options" ([string]$plan.CommunityOptions)
    foreach ($tag in $tags) {
        Add-StringPart $content "tags[]" $tag
    }

    $logoStream = [System.IO.File]::OpenRead($logoPath)
    $logoContent = [System.Net.Http.StreamContent]::new($logoStream)
    $logoContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new("image/jpeg")
    $content.Add($logoContent, "logo", [System.IO.Path]::GetFileName($logoPath))

    $response = $client.PostAsync("$apiBase/games/$gameId/mods", $content).GetAwaiter().GetResult()
    $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) {
        throw "Mod.IO identity creation failed: HTTP $([int]$response.StatusCode) $($response.ReasonPhrase)`n$responseBody"
    }
    $created = $responseBody | ConvertFrom-Json
} finally {
    if ($null -ne $logoStream) {
        $logoStream.Dispose()
    }
    $content.Dispose()
    $client.Dispose()
}

try {
    $verified = Invoke-RestMethod -Uri "$apiBase/games/$gameId/mods/$($created.id)" -Headers $headers
    $files = Invoke-RestMethod -Uri "$apiBase/games/$gameId/mods/$($created.id)/files" -Headers $headers
    if ([int]$verified.game_id -ne [int]$gameId -or
            [int]$verified.submitted_by.id -ne [int]$me.id -or
            [string]$verified.name -ne [string]$plan.Name -or
            [string]$verified.name_id -ne [string]$plan.NameId -or
            [int]$verified.visible -ne 0) {
        throw "Live Mod.IO identity verification mismatch."
    }
    if ([int]$files.result_count -ne 0) {
        throw "Identity-only creation unexpectedly found $($files.result_count) modfile(s)."
    }

    $resultDirectory = Split-Path -Parent $resultPath
    New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
    [pscustomobject]@{
        ModId = [int]$verified.id
        GameId = [int]$verified.game_id
        UserId = [int]$me.id
        Username = [string]$me.username
        Name = [string]$verified.name
        NameId = [string]$verified.name_id
        Visible = [int]$verified.visible
        Status = [int]$verified.status
        ModfileCount = [int]$files.result_count
        CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    } | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
    Write-Host "Verified Mod.IO identity: $($verified.id)"
    Write-Host "CREATED_MOD_ID=$($verified.id)"
} catch {
    Write-Error "Mod.IO identity $($created.id) was created, but live verification failed. PARTIAL_MOD_ID=$($created.id)`n$_"
}
