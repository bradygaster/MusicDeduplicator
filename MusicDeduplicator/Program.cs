using System;
using System.Linq;
using System.Text.RegularExpressions;
using Spectre.Console;

class Program
{
    static void Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : @"F:\Music";
        var files = AudioLibraryScanner.Scan(root).ToList();

        // Build duplicate groups using a stricter comparison algorithm
        var groups = BuildDuplicateGroups(files)
            .Select((g, i) => new { Index = i + 1, Files = g })
            .ToList();

        if (!groups.Any())
        {
            AnsiConsole.MarkupLine("[green]No duplicates found![/]");
            return;
        }

        AnsiConsole.MarkupLine($"[yellow]Found {groups.Count} duplicate groups.[/]\n");

        using var player = new Player();

        // Use an index-based loop so we can navigate left/right between groups
        int groupIndex = 0;
        while (groupIndex < groups.Count)
        {
            var group = groups[groupIndex];

            while (true)
            {
                int selection = 0;
                bool isPlaying = false;
                string? playingPath = null;
                int playingIndex = -1;
                bool userPaused = false; // when true, number keys won't start playback; they only change selection

                if (!group.Files.Any()) break;

                // Default move after leaving this group is to advance to next group
                int moveAfter = 1;

                // Interactive key-driven selector loop
                while (true)
                {
                    AnsiConsole.Clear();
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("[bold]#[/]")
                        .AddColumn("[bold]Artist[/]")
                        .AddColumn("[bold]Title[/]")
                        .AddColumn("[bold]Duration[/]")
                        .AddColumn("[bold]File Path[/]");

                    for (int i = 0; i < group.Files.Count; i++)
                    {
                        var f = group.Files[i];
                        var indexCol = (i == selection) ? $"[green]> {i + 1}[/]" : (i + 1).ToString();
                        var artistCol = (i == selection) ? $"[bold]{EscapeMarkup(f.Artist ?? "")}[/]" : EscapeMarkup(f.Artist ?? "");
                        var titleCol = (i == selection) ? $"[bold]{EscapeMarkup(f.Title ?? "")}[/]" : EscapeMarkup(f.Title ?? "");
                        var durationCol = (i == selection) ? $"[bold]{(f.Duration?.ToString(@"mm\:ss") ?? "")}[/]" : (f.Duration?.ToString(@"mm\:ss") ?? "");

                        // Indicate currently playing row
                        if (isPlaying && playingIndex == i)
                        {
                            indexCol = $"[green]> {i + 1} ▶[/]";
                        }

                        table.AddRow(
                            indexCol,
                            artistCol,
                            titleCol,
                            durationCol,
                            EscapeMarkup(f.Path));
                    }

                    AnsiConsole.Write(table);
                    AnsiConsole.MarkupLine("\n[gray]←/→ to move groups, ↑/↓ to select, [green]P/Enter[/] to play/stop, [cyan]1..9[/] to select/toggle, [red]Del[/] to delete, [blue]N[/] ext, [magenta]Q[/]uit[/]");

                    if (isPlaying && !string.IsNullOrEmpty(playingPath))
                    {
                        AnsiConsole.MarkupLine($"[green]Playing:[/] {EscapeMarkup(playingPath)}");
                    }

                    var key = Console.ReadKey(true);

                    // Arrow navigation within group
                    if (key.Key == ConsoleKey.UpArrow)
                    {
                        selection = (selection - 1 + group.Files.Count) % group.Files.Count;
                        continue;
                    }
                    else if (key.Key == ConsoleKey.DownArrow)
                    {
                        selection = (selection + 1) % group.Files.Count;
                        continue;
                    }
                    // Move groups right/left
                    else if (key.Key == ConsoleKey.RightArrow)
                    {
                        // Move to next group
                        if (isPlaying) { player.Stop(); isPlaying = false; playingPath = null; playingIndex = -1; }
                        moveAfter = 1;
                        break;
                    }
                    else if (key.Key == ConsoleKey.LeftArrow)
                    {
                        // Move to previous group
                        if (isPlaying) { player.Stop(); isPlaying = false; playingPath = null; playingIndex = -1; }
                        moveAfter = -1;
                        break;
                    }
                    else if (key.Key == ConsoleKey.N)
                    {
                        // Next group - stop any playing file first
                        if (isPlaying) { player.Stop(); isPlaying = false; playingPath = null; playingIndex = -1; }
                        moveAfter = 1;
                        break;
                    }
                    else if (key.Key == ConsoleKey.Q)
                    {
                        // Quit application
                        if (isPlaying) { player.Stop(); isPlaying = false; playingPath = null; playingIndex = -1; }
                        player.Stop();
                        return;
                    }
                    else if (key.Key == ConsoleKey.Delete)
                    {
                        var fileToDelete = group.Files[selection];
                        if (AnsiConsole.Confirm($"Delete [red]{fileToDelete.FileName}[/]?"))
                        {
                            try
                            {
                                // If the file being deleted is playing, stop it
                                if (isPlaying && playingPath == fileToDelete.Path)
                                {
                                    player.Stop();
                                    isPlaying = false;
                                    playingPath = null;
                                    playingIndex = -1;
                                    userPaused = true; // respect user's stop intent
                                }

                                System.IO.File.Delete(fileToDelete.Path);
                                group.Files.RemoveAt(selection);
                                AnsiConsole.MarkupLine("[red]Deleted.[/]");
                                // Adjust selection
                                if (selection >= group.Files.Count) selection = Math.Max(0, group.Files.Count - 1);

                                // If there is only one track left, move to next group automatically
                                if (group.Files.Count <= 1)
                                {
                                    moveAfter = 1;
                                    break; // leave inner loop to advance groups
                                }

                                if (!group.Files.Any())
                                {
                                    moveAfter = 1;
                                    break; // group empty -> next group
                                }
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine($"[red]Error deleting:[/] {ex.Message}");
                            }
                        }

                        // After delete, re-render
                        continue;
                    }
                    else if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.P)
                    {
                        // Toggle play/stop for the selected file
                        if (!group.Files.Any()) continue;
                        var file = group.Files[selection];

                        if (isPlaying && playingIndex == selection)
                        {
                            // Stop the currently playing file
                            player.Stop();
                            isPlaying = false;
                            playingPath = null;
                            playingIndex = -1;
                            userPaused = true; // user manually stopped playback
                            AnsiConsole.MarkupLine("[gray]Stopped.[/]");
                        }
                        else
                        {
                            // Start playing the selected file
                            if (isPlaying) player.Stop();
                            player.Play(file.Path);
                            isPlaying = true;
                            playingPath = file.Path;
                            playingIndex = selection;
                            userPaused = false;
                        }

                        // After toggling, re-render
                        continue;
                    }

                    // Numeric key handling: top-row and numpad
                    int num = -1;
                    if (key.Key >= ConsoleKey.D0 && key.Key <= ConsoleKey.D9)
                        num = key.Key - ConsoleKey.D0;
                    else if (key.Key >= ConsoleKey.NumPad0 && key.Key <= ConsoleKey.NumPad9)
                        num = key.Key - ConsoleKey.NumPad0;

                    if (num >= 1)
                    {
                        int idx = num - 1;
                        if (idx < group.Files.Count)
                        {
                            // Update selection to the numeric key
                            selection = idx;

                            // If currently playing
                            if (isPlaying)
                            {
                                if (playingIndex == idx)
                                {
                                    // Toggle off the currently playing track
                                    player.Stop();
                                    isPlaying = false;
                                    playingPath = null;
                                    playingIndex = -1;
                                    userPaused = true; // record manual stop
                                }
                                else
                                {
                                    // Switch to the newly requested track
                                    player.Stop();
                                    var file = group.Files[idx];
                                    player.Play(file.Path);
                                    isPlaying = true;
                                    playingPath = file.Path;
                                    playingIndex = idx;
                                    userPaused = false;
                                }
                            }
                            else
                            {
                                // Not currently playing
                                if (!userPaused)
                                {
                                    // Start playback
                                    var file = group.Files[idx];
                                    player.Play(file.Path);
                                    isPlaying = true;
                                    playingPath = file.Path;
                                    playingIndex = idx;
                                    userPaused = false;
                                }
                                else
                                {
                                    // User has manually paused; just change selection
                                }
                            }
                        }

                        // After handling numeric key, re-render
                        continue;
                    }

                    // Unhandled keys just re-render
                }

                // Leave group's inner loop and move to the requested group direction
                groupIndex = Math.Clamp(groupIndex + moveAfter, 0, groups.Count);
                break;
            }
        }

        player.Stop();
    }

    // Build duplicate groups with stricter comparison rules to reduce false positives
    static List<List<AudioFileMetadata>> BuildDuplicateGroups(List<AudioFileMetadata> files)
    {
        var groups = new List<List<AudioFileMetadata>>();
        var visited = new bool[files.Count];

        for (int i = 0; i < files.Count; i++)
        {
            if (visited[i]) continue;
            var a = files[i];
            var group = new List<AudioFileMetadata> { a };
            visited[i] = true;

            for (int j = i + 1; j < files.Count; j++)
            {
                if (visited[j]) continue;
                var b = files[j];
                if (AreLikelyDuplicates(a, b))
                {
                    group.Add(b);
                    visited[j] = true;
                }
            }

            if (group.Count > 1)
            {
                groups.Add(group);
            }
        }

        return groups;
    }

    static bool AreLikelyDuplicates(AudioFileMetadata a, AudioFileMetadata b)
    {
        // If both have artist and title, require these to match after normalization
        var artistA = NormalizeForComparison(a.Artist);
        var artistB = NormalizeForComparison(b.Artist);
        var titleA = NormalizeForComparison(a.Title);
        var titleB = NormalizeForComparison(b.Title);

        // Duration tolerance in seconds
        double durationA = a.Duration?.TotalSeconds ?? -1;
        double durationB = b.Duration?.TotalSeconds ?? -2; // ensure mismatch if missing

        // Size check (allow small differences due to tags/encoders)
        long sizeA = a.SizeBytes;
        long sizeB = b.SizeBytes;

        bool haveArtistAndTitle = !string.IsNullOrEmpty(artistA) && !string.IsNullOrEmpty(titleA)
            && !string.IsNullOrEmpty(artistB) && !string.IsNullOrEmpty(titleB);

        if (haveArtistAndTitle)
        {
            if (artistA != artistB || titleA != titleB)
                return false;

            // durations must be reasonably close (1.5s)
            if (durationA >= 0 && durationB >= 0 && Math.Abs(durationA - durationB) > 1.5)
                return false;

            // sizes must be similar (within 1% or 2 KB)
            var maxDiff = Math.Max(2048, (long)(Math.Max(sizeA, sizeB) * 0.01));
            if (Math.Abs(sizeA - sizeB) > maxDiff)
                return false;

            return true;
        }

        // If artist/title missing, be conservative: require filename (normalized) to match and duration to match closely
        var fileNameA = NormalizeForComparison(a.FileName);
        var fileNameB = NormalizeForComparison(b.FileName);

        if (!string.IsNullOrEmpty(fileNameA) && fileNameA == fileNameB)
        {
            if (durationA >= 0 && durationB >= 0 && Math.Abs(durationA - durationB) <= 1.0)
            {
                return true;
            }
        }

        return false;
    }

    static string NormalizeForComparison(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        // Ensure we never pass null into Regex.Replace
        var s = (text ?? string.Empty).ToLowerInvariant().Trim();
        try
        {
            // Remove parentheses/bracketed content (non-greedy)
            s = Regex.Replace(s, @"\([^)]*\)", "");
            s = Regex.Replace(s, @"\[[^\]]*\]", "");
            // Remove common feat markers as whole words
            s = Regex.Replace(s, @"\b(?:feat\.?|ft\.?)\b", "", RegexOptions.IgnoreCase);
            s = s.Replace("&", "and");
            s = Regex.Replace(s, "[^a-z0-9\\s]", " ");
            s = Regex.Replace(s, "\\s+", " ").Trim();
        }
        catch (RegexMatchTimeoutException)
        {
            // If regex fails for any reason, fall back to a simple cleanup
            s = new string(s.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
            s = Regex.Replace(s, "\\s+", " ").Trim();
        }

        return s;
    }

    // Helper to escape Spectre.Console markup characters in strings
    static string EscapeMarkup(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return Markup.Escape(text);
    }
}
