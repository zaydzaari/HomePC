[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^https://')][string]$WorkerUrl)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$path=Join-Path $root 'generated\homepc.json'
if (-not (Test-Path $path)) { throw 'Run tools/bootstrap.ps1 first.' }
$config=Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
$config.workerUrl=$WorkerUrl.TrimEnd('/')
$config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding utf8
& (Join-Path $PSScriptRoot 'create-google-values.ps1') -WorkerUrl $WorkerUrl

