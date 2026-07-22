[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$bundle=Join-Path $root 'work\windows-bundle'
$release=Join-Path $root 'release'
$zip=Join-Path $release 'HomePC-Windows-x64.zip'
$resolvedBundle=[IO.Path]::GetFullPath($bundle)
$resolvedWork=[IO.Path]::GetFullPath((Join-Path $root 'work'))
if (-not $resolvedBundle.StartsWith($resolvedWork,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolvedBundle -Leaf) -ne 'windows-bundle') { throw 'Unsafe bundle path.' }
if (Test-Path -LiteralPath $resolvedBundle) { Remove-Item -LiteralPath $resolvedBundle -Recurse -Force }
New-Item -ItemType Directory -Force -Path $resolvedBundle,$release | Out-Null
foreach ($project in 'HomePC.Agent','HomePC.Dashboard','HomePC.Tray') {
  dotnet publish (Join-Path $root "src\$project\$project.csproj") -c Release -r win-x64 --self-contained true -o $bundle
  if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed." }
}
Copy-Item -LiteralPath (Join-Path $root 'installer\install.ps1') -Destination (Join-Path $bundle 'install.ps1') -Force
Copy-Item -LiteralPath (Join-Path $root 'scripts\uninstall-user.ps1') -Destination $bundle -Force
Copy-Item -LiteralPath (Join-Path $root 'config\homepc.example.json') -Destination $bundle -Force
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $bundle '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Created $zip"
