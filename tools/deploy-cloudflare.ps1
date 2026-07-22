[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$cloud=Join-Path $root 'cloud'
$secrets=Join-Path $root 'generated\worker-secrets.json'
if (-not (Get-Command node -ErrorAction SilentlyContinue) -or -not (Get-Command npm -ErrorAction SilentlyContinue)) { throw 'Node.js and npm are required.' }
if (-not (Test-Path $secrets)) { throw 'Run tools/bootstrap.ps1 first.' }
Push-Location $cloud
try {
  npm config set registry https://registry.npmjs.org/
  if (Test-Path 'package-lock.json') {
    $lock=Get-Content -Raw -LiteralPath 'package-lock.json'
    $fixed=$lock -replace 'https?://[^"/]*(artifactory|verdaccio|nexus)[^" ]*/','https://registry.npmjs.org/'
    if ($fixed -ne $lock) { $fixed | Set-Content -LiteralPath 'package-lock.json' -Encoding utf8; Write-Warning 'Replaced an internal registry URL in package-lock.json.' }
  }
  npm install
  npm run types
  npm run typecheck
  npm test
  npx wrangler whoami
  npx wrangler secret bulk $secrets
  $output = npx wrangler deploy --minify 2>&1 | Tee-Object -Variable deployment
  if ($LASTEXITCODE -ne 0) {
    if ($deployment -match 'workers.dev subdomain') {
      Write-Host 'Cloudflare has no workers.dev subdomain yet. Open https://dash.cloudflare.com/, select Workers & Pages, create your first Worker, and choose/confirm the workers.dev subdomain. Then rerun this script.' -ForegroundColor Yellow
    }
    throw 'Cloudflare deployment failed; no completion message was emitted.'
  }
  $url=([regex]::Match(($deployment -join "`n"),'https://[a-zA-Z0-9.-]+\.workers\.dev')).Value
  if (-not $url) { Write-Warning 'Deployment succeeded but the workers.dev URL could not be parsed. Copy it from the output and run tools/set-worker-url.ps1.' }
  else { & (Join-Path $root 'tools\set-worker-url.ps1') -WorkerUrl $url; Write-Host "Deployment complete: $url" }
} finally { Pop-Location }

