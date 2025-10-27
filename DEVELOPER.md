# Developer Guide

## Quick Commands Reference

### Build and Install (for testing)

**Windows:**
```powershell
.\build-and-install.ps1
```

**Linux/macOS:**
```bash
./build-and-install.sh
```

### Uninstall

**Windows:**
```powershell
.\uninstall.ps1
```

**Linux/macOS:**
```bash
./uninstall.sh
```

### Test the Tool

After installation, test with:

```bash
# Show help
dedupe --help

# Test with current directory
dedupe

# Test with specific path
dedupe --path "C:\TestMusic"
```

## Development Workflow

1. **Make your changes** to the code
2. **Build and install** using the script
3. **Test** the tool with sample data
4. **Iterate** as needed

## Manual Commands

If you prefer manual control:

```bash
# Build
dotnet build ./MusicDeduplicator/MusicDeduplicator.csproj --configuration Release

# Pack
dotnet pack ./MusicDeduplicator/MusicDeduplicator.csproj --configuration Release

# Uninstall old version
dotnet tool uninstall --global dedupe

# Install new version
dotnet tool install --global --add-source ./MusicDeduplicator/bin/Release dedupe
```

## Project Structure

```
MusicDeduplicator/
??? MusicDeduplicator/
?   ??? Program.cs              # Main entry point and UI logic
?   ??? AudioLibraryScanner.cs  # File scanning and metadata extraction
?   ??? Player.cs     # Audio playback using NAudio
?   ??? MusicDeduplicator.csproj
??? build-and-install.ps1       # Windows build/install script
??? build-and-install.sh        # Linux/macOS build/install script
??? uninstall.ps1               # Windows uninstall script
??? uninstall.sh                # Linux/macOS uninstall script
??? README.md  # User documentation
??? QUICKSTART.md  # Quick reference
```

## Key Components

### Program.cs
- Command-line argument parsing with System.CommandLine
- Interactive UI with Spectre.Console
- Duplicate detection algorithm
- Keyboard navigation and controls

### AudioLibraryScanner.cs
- Recursive file scanning
- Metadata extraction with TagLibSharp
- Support for multiple audio formats

### Player.cs
- Audio playback with NAudio
- Simple play/stop interface

## Testing Tips

1. **Create test data**: Use a small directory with known duplicates
2. **Test edge cases**: Empty directories, missing metadata, corrupted files
3. **Verify playback**: Test with different audio formats
4. **Check deletion**: Ensure files are properly deleted and groups update correctly

## Publishing to NuGet

When ready to publish:

```bash
# Increment version in MusicDeduplicator.csproj
# Build and pack
dotnet pack ./MusicDeduplicator/MusicDeduplicator.csproj --configuration Release

# Push to NuGet
dotnet nuget push ./MusicDeduplicator/bin/Release/dedupe.*.nupkg --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY
```

## Troubleshooting

### Tool doesn't update after rebuild
Make sure to uninstall first:
```bash
dotnet tool uninstall --global dedupe
```

### "Command not found" after installation
Ensure your PATH includes the .NET tools directory:
- Windows: `%USERPROFILE%\.dotnet\tools`
- Linux/macOS: `~/.dotnet/tools`

### Build errors
Make sure you have .NET 9 SDK installed:
```bash
dotnet --version
```

## Dependencies

- **NAudio** (2.2.1): Audio playback
- **TagLibSharp** (2.3.0): Metadata extraction
- **Spectre.Console** (0.53.0): Rich console UI
- **System.CommandLine** (2.0.0-beta4): CLI argument parsing
