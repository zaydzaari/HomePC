[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location (Join-Path $root 'cloud')
try { npm install; npm run types; npm run typecheck; npm test; npx wrangler deploy --dry-run --outdir ..\work\worker-dry-run } finally { Pop-Location }
dotnet restore (Join-Path $root 'HomePC.sln')
dotnet build (Join-Path $root 'HomePC.sln') -c Release --no-restore
dotnet test (Join-Path $root 'HomePC.sln') -c Release --no-build
$validationConfig=Join-Path $root 'generated\homepc.json'
if (-not (Test-Path -LiteralPath $validationConfig)) {
  $work=Join-Path $root 'work'; New-Item -ItemType Directory -Force -Path $work | Out-Null
  $validationConfig=Join-Path $work 'verify-homepc.json'
  $temporary=Get-Content -Raw -LiteralPath (Join-Path $root 'config\homepc.example.json') | ConvertFrom-Json
  $temporary.workerUrl='https://example.workers.dev'
  $temporary.deviceToken='dddddddddddddddddddddddddddddddd'
  $temporary.adminToken='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
  $temporary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $validationConfig -Encoding utf8
}
dotnet run --project (Join-Path $root 'src\HomePC.Agent') -c Release --no-build -- --config $validationConfig --validate-config
$example=Get-Content -Raw -LiteralPath (Join-Path $root 'config\homepc.example.json') | ConvertFrom-Json
if ($example.deviceToken -ne 'GENERATED_NOT_COMMITTED' -or $example.adminToken -ne 'GENERATED_NOT_COMMITTED') { throw 'Example config contains unexpected token material.' }
Write-Host 'Verification complete.'
