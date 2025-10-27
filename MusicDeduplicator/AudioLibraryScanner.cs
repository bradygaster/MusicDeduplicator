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
        var supportedExts = new[] { ".mp3", ".flac", ".wav", ".ogg", ".m4a" };
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
            data.Artist = tagFile.Tag.FirstArtist;
            data.Album = tagFile.Tag.Album;
            data.Title = tagFile.Tag.Title;
            data.Year = tagFile.Tag.Year;
            data.Duration = tagFile.Properties.Duration;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read tags for {path}: {ex.Message}");
        }

        return data;
    }
}
