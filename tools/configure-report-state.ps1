[CmdletBinding()]
param([Parameter(Mandatory)][string]$ServiceAccountJson)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$keyPath=[IO.Path]::GetFullPath($ServiceAccountJson)
if (-not (Test-Path -LiteralPath $keyPath)) { throw 'Service-account JSON file was not found.' }
$key=Get-Content -Raw -LiteralPath $keyPath | ConvertFrom-Json
if ($key.type -ne 'service_account' -or -not $key.client_email -or -not $key.private_key -or -not $key.project_id) { throw 'The file is not a valid Google service-account JSON key.' }
$secrets=Get-Content -Raw -LiteralPath (Join-Path $root 'generated\worker-secrets.json') | ConvertFrom-Json
if ($secrets.GOOGLE_REDIRECT_URI -notmatch "/r/$([regex]::Escape($key.project_id))$") { throw 'Service-account project_id does not match the Google Home project used by bootstrap.' }
Push-Location (Join-Path $root 'cloud')
try {
  $key.client_email | npx wrangler secret put HOMEGRAPH_CLIENT_EMAIL
  if ($LASTEXITCODE -ne 0) { throw 'Uploading the HomeGraph client email failed.' }
  $key.private_key | npx wrangler secret put HOMEGRAPH_PRIVATE_KEY
  if ($LASTEXITCODE -ne 0) { throw 'Uploading the HomeGraph private key failed.' }
  npx wrangler deploy --minify
  if ($LASTEXITCODE -ne 0) { throw 'Worker deployment failed.' }
} finally { Pop-Location }
Write-Host 'Report State credentials are configured. Relink or reconnect the agent, then verify with Google Home Graph Viewer.'
