#Requires -RunAsAdministrator
$ErrorActionPreference='Stop'
Stop-Service HomePCAgent
Get-Service HomePCAgent

