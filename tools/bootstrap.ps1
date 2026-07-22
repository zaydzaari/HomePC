[CmdletBinding()]
param([string]$ProjectId)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$generated = Join-Path $root 'generated'
if (-not $ProjectId) { $ProjectId = Read-Host 'Google Home Developer Console project ID (not display name)' }
if ($ProjectId -notmatch '^[a-z][a-z0-9-]{4,62}$') { throw 'Project ID format is invalid. Copy the exact project ID from Google Home Developer Console project settings.' }
if ((Test-Path $generated) -and (Get-ChildItem $generated -Force -ErrorAction SilentlyContinue)) {
  $answer = Read-Host 'generated/ already contains files. Type OVERWRITE to replace generated secrets'
  if ($answer -cne 'OVERWRITE') { throw 'Cancelled without changing existing secrets.' }
}
New-Item -ItemType Directory -Force -Path $generated | Out-Null
function New-Secret([int]$bytes = 32) {
  $buffer = New-Object byte[] $bytes
  $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
  try { $generator.GetBytes($buffer) } finally { $generator.Dispose() }
  return [Convert]::ToBase64String($buffer).TrimEnd('=').Replace('+','-').Replace('/','_')
}
$clientId = 'homepc-' + (New-Secret 18)
$clientSecret = New-Secret 32
$linkPassword = New-Secret 18
$deviceToken = New-Secret 48
$adminToken = New-Secret 48
$redirect = "https://oauth-redirect.googleusercontent.com/r/$ProjectId"
$secrets = [ordered]@{ GOOGLE_CLIENT_ID=$clientId; GOOGLE_CLIENT_SECRET=$clientSecret; GOOGLE_REDIRECT_URI=$redirect; LINK_PASSWORD=$linkPassword; DEVICE_TOKEN=$deviceToken; ADMIN_TOKEN=$adminToken }
$secrets | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $generated 'worker-secrets.json') -Encoding utf8
$config = Get-Content -Raw -LiteralPath (Join-Path $root 'config\homepc.example.json') | ConvertFrom-Json
$config.deviceToken = $deviceToken
$config.adminToken = $adminToken
$config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $generated 'homepc.json') -Encoding utf8
$values = @"
HomePC Google Home Developer Console values
===========================================
Integration name: HomePC
Device types: Switch
OAuth Client ID: $clientId
OAuth Client Secret: $clientSecret
Authorization URL: WORKER_URL/oauth/authorize
Token URL: WORKER_URL/oauth/token
Cloud fulfillment URL: WORKER_URL/smarthome
Scope: homepc.control
HTTP Basic Auth: Enable (the token endpoint also accepts body credentials)
Icon filename: $ProjectId.png
OAuth redirect URI validated by HomePC: $redirect
Private test instructions: Open Google Home > Add > Device > Link app or service > search [test] HomePC; enter the link password below.
Link password: $linkPassword
Admin token (keep private): $adminToken
"@
$values | Set-Content -LiteralPath (Join-Path $generated 'google-home-values.txt') -Encoding utf8
Write-Host "Generated private files in $generated"
Write-Host 'Next: .\tools\deploy-cloudflare.ps1'
