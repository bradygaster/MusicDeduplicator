public class AudioFileMetadata
{
    public string Path { get; set; } = "";
    public string FileName => System.IO.Path.GetFileName(Path);
    public long SizeBytes { get; set; }
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }

    // MP3 tags
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Title { get; set; }
    public uint? Year { get; set; }
    public TimeSpan? Duration { get; set; }

    public override string ToString() =>
        $"{Artist} - {Title} [{Album}] ({SizeBytes / 1024} KB)";
}

public static class AudioLibraryScanner
{
    public static IEnumerable<AudioFileMetadata> Scan(string rootDir)
    {
        // Supported audio file extensions:
        // .mp3 - MPEG Audio Layer 3
        // .flac - Free Lossless Audio Codec
        // .wav - Waveform Audio File Format
        // .ogg - Ogg Vorbis
        // .m4a - MPEG-4 Audio (AAC or Apple Lossless)
        // .m4p - MPEG-4 Protected Audio (FairPlay DRM-protected AAC from Apple Music/iTunes)
        var supportedExts = new[] { ".mp3", ".flac", ".wav", ".ogg", ".m4a", ".m4p" };
        
        foreach (var file in Directory.EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories)
                                      .Where(f => supportedExts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
        {
            yield return GetMetadata(file);
        }
    }

    private static AudioFileMetadata GetMetadata(string path)
    {
        var info = new FileInfo(path);
        var data = new AudioFileMetadata
        {
            Path = path,
            SizeBytes = info.Length,
            Created = info.CreationTimeUtc,
            Modified = info.LastWriteTimeUtc
        };

        try
        {
            var tagFile = TagLib.File.Create(path);
            // Prefer performer (track artist). Fall back to album artist or any available artist.
            data.Artist = tagFile.Tag.FirstPerformer ?? tagFile.Tag.FirstAlbumArtist
#pragma warning disable CS0618
                ?? tagFile.Tag.FirstArtist;
#pragma warning restore CS0618
            data.Album = tagFile.Tag.Album;
            data.Title = tagFile.Tag.Title;
            data.Year = tagFile.Tag.Year;
            data.Duration = tagFile.Properties?.Duration;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read tags for {path}: {ex.Message}");
        }

        return data;
    }
}
