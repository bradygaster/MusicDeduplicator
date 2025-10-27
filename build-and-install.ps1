#!/usr/bin/env pwsh
#
# Build and Install Script for Music Deduplicator
# This script rebuilds the tool and reinstalls it globally for testing
#

$ErrorActionPreference = "Stop"

Write-Host "?? Building and Installing Music Deduplicator..." -ForegroundColor Cyan
Write-Host ""

# Get the script directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptPath "MusicDeduplicator\MusicDeduplicator.csproj"
$packageSource = Join-Path $scriptPath "MusicDeduplicator\bin\Release"

# Check if tool is already installed
Write-Host "?? Checking for existing installation..." -ForegroundColor Yellow
$toolList = dotnet tool list --global | Select-String "dedupe"

if ($toolList) {
    Write-Host "   Found existing installation, uninstalling..." -ForegroundColor Yellow
dotnet tool uninstall --global dedupe
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to uninstall existing tool"
        exit 1
    }
    Write-Host "   ? Uninstalled" -ForegroundColor Green
} else {
    Write-Host "   No existing installation found" -ForegroundColor Gray
}

Write-Host ""
Write-Host "???  Building project..." -ForegroundColor Yellow
dotnet build $projectPath --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}
Write-Host "   ? Build successful" -ForegroundColor Green

Write-Host ""
Write-Host "?? Packing NuGet package..." -ForegroundColor Yellow
dotnet pack $projectPath --configuration Release --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Pack failed"
    exit 1
}
Write-Host "   ? Pack successful" -ForegroundColor Green

Write-Host ""
Write-Host "??  Installing global tool..." -ForegroundColor Yellow
dotnet tool install --global --add-source $packageSource dedupe
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tool installation failed"
  exit 1
}
Write-Host "   ? Installation successful" -ForegroundColor Green

Write-Host ""
Write-Host "? Done! You can now run 'dedupe' from anywhere." -ForegroundColor Green
Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Cyan
Write-Host "   dedupe --help" -ForegroundColor Gray
Write-Host "   dedupe --path `"C:\Music`"" -ForegroundColor Gray
Write-Host "   dedupe -p `"F:\MyMusicLibrary`"" -ForegroundColor Gray
Write-Host ""
