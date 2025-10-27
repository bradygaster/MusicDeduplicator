#!/usr/bin/env bash
#
# Uninstall Script for Music Deduplicator
# Removes the globally installed tool
#

set -e

echo "???  Uninstalling Music Deduplicator..."
echo ""

# Check if tool is installed
if dotnet tool list --global | grep -q "dedupe"; then
    echo "   Removing global tool..."
    dotnet tool uninstall --global dedupe
    echo "   ? Successfully uninstalled"
else
    echo "   Tool is not currently installed"
fi

echo ""
echo "? Done!"
echo ""
