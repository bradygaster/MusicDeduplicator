# Music Deduplicator

An interactive CLI tool to find and manage duplicate music files in your library with built-in playback preview.

## Features

- ?? **Smart Duplicate Detection**: Uses metadata (artist, title, duration) and filename comparison
- ?? **Built-in Audio Preview**: Listen to files before deciding which to keep
- ?? **Beautiful CLI Interface**: Powered by Spectre.Console
- ?? **Keyboard-Driven Navigation**: Efficient workflow with arrow keys
- ?? **Supports Multiple Formats**: MP3, FLAC, WAV, OGG, M4A

## Installation

### From NuGet (when published)

```bash
dotnet tool install --global dedupe
```

### From Source (for development/testing)

Use the provided scripts for easy installation:

**Windows (PowerShell):**
```powershell
.\build-and-install.ps1
```

**Linux/macOS (Bash):**
```bash
./build-and-install.sh
```

Or manually:
```bash
dotnet pack ./MusicDeduplicator/MusicDeduplicator.csproj --configuration Release
dotnet tool install --global --add-source ./MusicDeduplicator/bin/Release dedupe
```

## Usage

Run the tool with a path to your music directory:

```bash
dedupe --path "C:\Music"
```

Or run without arguments to scan the current directory:

```bash
dedupe
```

Get help:

```bash
dedupe --help
```

### Keyboard Controls

| Key | Action |
|-----|--------|
| `?` / `?` | Navigate between files in a group |
| `?` / `?` | Navigate between duplicate groups |
| `P` / `Enter` | Play/Stop the selected file |
| `1-9` | Quick select file by number |
| `Del` | Delete the selected file |
| `N` | Move to next group |
| `Q` | Quit the application |

## Development

### Building from Source

Requirements:
- .NET 9 SDK

```bash
git clone https://github.com/bradygaster/MusicDeduplicator.git
cd MusicDeduplicator
dotnet build
```

### Testing Local Changes

Use the build and install script to quickly test your changes:

```powershell
# Windows
.\build-and-install.ps1

# Linux/macOS
./build-and-install.sh
```

### Uninstalling

Use the uninstall script:

```powershell
# Windows
.\uninstall.ps1

# Linux/macOS
./uninstall.sh
```

Or manually:

```bash
dotnet tool uninstall --global dedupe
```

## How It Works

1. **Scanning**: Recursively scans the provided directory for audio files
2. **Metadata Extraction**: Reads ID3 tags (artist, title, duration) from each file using TagLibSharp
3. **Duplicate Detection**: Groups files with matching metadata using intelligent normalization:
   - Removes parenthetical content
   - Normalizes "feat" variations
   - Compares durations within tolerance (1.5 seconds)
   - Checks file sizes for similarity (within 1% or 2KB)
4. **Interactive Review**: Presents duplicate groups one at a time for review
5. **Safe Deletion**: Confirms before deleting any file

## License

MIT License

## Credits

Built with:
- [NAudio](https://github.com/naudio/NAudio) - Audio playback
- [TagLibSharp](https://github.com/mono/taglib-sharp) - Metadata reading
- [Spectre.Console](https://github.com/spectreconsole/spectre.console) - CLI interface
- [System.CommandLine](https://github.com/dotnet/command-line-api) - Argument parsing
