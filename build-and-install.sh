#!/usr/bin/env bash
#
# Build and Install Script for Music Deduplicator
# This script rebuilds the tool and reinstalls it globally for testing
#

set -e

echo "?? Building and Installing Music Deduplicator..."
echo ""

# Get the script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_PATH="$SCRIPT_DIR/MusicDeduplicator/MusicDeduplicator.csproj"
PACKAGE_SOURCE="$SCRIPT_DIR/MusicDeduplicator/bin/Release"

# Check if tool is already installed
echo "?? Checking for existing installation..."
if dotnet tool list --global | grep -q "dedupe"; then
  echo "   Found existing installation, uninstalling..."
    dotnet tool uninstall --global dedupe
    echo "   ? Uninstalled"
else
    echo "   No existing installation found"
fi

echo ""
echo "???  Building project..."
dotnet build "$PROJECT_PATH" --configuration Release
echo "   ? Build successful"

echo ""
echo "?? Packing NuGet package..."
dotnet pack "$PROJECT_PATH" --configuration Release --no-build
echo "   ? Pack successful"

echo ""
echo "??  Installing global tool..."
dotnet tool install --global --add-source "$PACKAGE_SOURCE" dedupe
echo "   ? Installation successful"

echo ""
echo "? Done! You can now run 'dedupe' from anywhere."
echo ""
echo "Usage examples:"
echo "   dedupe --help"
echo "   dedupe --path \"/home/user/Music\""
echo "   dedupe -p \"/mnt/music\""
echo ""
