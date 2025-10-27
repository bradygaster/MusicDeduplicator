#!/usr/bin/env pwsh
#
# Uninstall Script for Music Deduplicator
# Removes the globally installed tool
#

$ErrorActionPreference = "Stop"

Write-Host "???  Uninstalling Music Deduplicator..." -ForegroundColor Cyan
Write-Host ""

# Check if tool is installed
$toolList = dotnet tool list --global | Select-String "dedupe"

if ($toolList) {
    Write-Host "   Removing global tool..." -ForegroundColor Yellow
    dotnet tool uninstall --global dedupe
    
    if ($LASTEXITCODE -eq 0) {
  Write-Host "   ? Successfully uninstalled" -ForegroundColor Green
    } else {
        Write-Error "Failed to uninstall tool"
   exit 1
    }
} else {
    Write-Host "   Tool is not currently installed" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "? Done!" -ForegroundColor Green
Write-Host ""
