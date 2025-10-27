using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Spectre.Console;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootOption = new Option<DirectoryInfo>(
            name: "--path",
     description: "The root directory to scan for music files",
    getDefaultValue: () => new DirectoryInfo(Environment.CurrentDirectory));

        rootOption.AddAlias("-p");

        var rootCommand = new RootCommand("Music Deduplicator - Find and manage duplicate music files in your library")
        {
 rootOption
        };

        // Set the command name to match the tool name
        rootCommand.Name = "dedupe";

        rootCommand.SetHandler((DirectoryInfo directory) =>
        {
            if (!directory.Exists)
            {
                AnsiConsole.MarkupLine($"[red]Error: Directory '{directory.FullName}' does not exist.[/]");
                Environment.Exit(1);
            }

            RunDeduplicator(directory.FullName);
        }, rootOption);

        return await rootCommand.InvokeAsync(args);
    }

    static int RunDeduplicator(string root)
    {
        AnsiConsole.MarkupLine($"[cyan]Scanning directory:[/] {EscapeMarkup(root)}");

        var files = AudioLibraryScanner.Scan(root).ToList();

        AnsiConsole.MarkupLine($"[cyan]Found {files.Count} audio files.[/]");

        // Build duplicate groups using a stricter comparison algorithm
        var groups = BuildDuplicateGroups(files)
    .Select((g, i) => new { Index = i + 1, Files = g })
       .ToList();

        if (!groups.Any())
        {
            AnsiConsole.MarkupLine("[green]No duplicates found![/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[yellow]Found {groups.Count} duplicate groups.[/]\n");

        using var player = new Player();

        // Persist playback state across groups
        bool globalIsPlaying = false;
        string? globalPlayingPath = null;
        int globalPlayingIndex = -1; // index within current group
        bool userPaused = false; // when true, number keys won't start playback; they only change selection

        // Use an index-based loop so we can navigate left/right between groups
        int groupIndex = 0;
        bool continuePlayNextGroup = false; // if true, when moving to next/prev group start track1
        int? pendingGroupIndex = null;
        while (groupIndex < groups.Count)
        {
            var group = groups[groupIndex];

            while (true)
            {
                int selection = 0;

                if (!group.Files.Any()) break;

                // If arriving to this group with a flag to continue playback, start the first track
                if (continuePlayNextGroup && group.Files.Count > 0)
                {
                    try
                    {
                        var first = group.Files[0];
                        // Start playback for first track in this group (Player.Play stops any previous playback)
                        player.Play(first.Path);
                        globalIsPlaying = true;
                        globalPlayingPath = first.Path;
                        globalPlayingIndex = 0;
                        userPaused = false;
                    }
                    catch
                    {
                        // ignore playback errors here
                    }
                    finally
                    {
                        continuePlayNextGroup = false;
                        pendingGroupIndex = null;
                    }
                }

                // Default move after leaving this group is to advance to next group
                int moveAfter = 1;

                // Interactive key-driven selector loop
                while (true)
                {
                    AnsiConsole.Clear();

                    AnsiConsole.MarkupLine($"[bold cyan]Group {groupIndex + 1} of {groups.Count}[/]\n");

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
                        var durationCol = (i == selection) ? $"[bold]{EscapeMarkup(FormatDuration(f.Duration))}[/]" : EscapeMarkup(FormatDuration(f.Duration));

                        // Indicate currently playing row
                        if (globalIsPlaying && globalPlayingIndex == i)
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
                    AnsiConsole.MarkupLine("\n[gray]←/→ to move groups, ↑/↓ to select, [green]P/Enter[/] to play/stop, [cyan]1..9[/] to select/toggle, [red]Del[/] to delete, [blue]N[/]ext, [magenta]Q[/]uit[/]");

                    if (globalIsPlaying && !string.IsNullOrEmpty(globalPlayingPath))
                    {
                        AnsiConsole.MarkupLine($"[green]Playing:[/] {EscapeMarkup(globalPlayingPath)}");
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
                        // remember whether we should continue playback in the next group
                        continuePlayNextGroup = globalIsPlaying && !userPaused;
                        pendingGroupIndex = groupIndex + 1;
                        // do not stop playback here; destination group's startup (or immediate restart) will call Play which stops previous
                        moveAfter = 1;
                        break;
                    }
                    else if (key.Key == ConsoleKey.LeftArrow)
                    {
                        // Move to previous group
                        continuePlayNextGroup = globalIsPlaying && !userPaused;
                        pendingGroupIndex = groupIndex - 1;
                        moveAfter = -1;
                        break;
                    }
                    else if (key.Key == ConsoleKey.N)
                    {
                        // Next group - remember to continue if appropriate
                        continuePlayNextGroup = globalIsPlaying && !userPaused;
                        pendingGroupIndex = groupIndex + 1;
                        moveAfter = 1;
                        break;
                    }
                    else if (key.Key == ConsoleKey.Q)
                    {
                        // Quit application
                        if (globalIsPlaying) { player.Stop(); globalIsPlaying = false; globalPlayingPath = null; globalPlayingIndex = -1; }
                        player.Stop();
                        return 0;
                    }
                    else if (key.Key == ConsoleKey.Delete)
                    {
                        var fileToDelete = group.Files[selection];
                        if (AnsiConsole.Confirm($"Delete [red]{EscapeMarkup(fileToDelete.FileName)}[/]?"))
                        {
                            try
                            {
                                // If the file being deleted is playing, stop it to release the handle
                                // but schedule continuation in the next group so playback starts immediately there.
                                if (globalIsPlaying && globalPlayingPath == fileToDelete.Path)
                                {
                                    player.Stop();
                                    // Clear current playing info; we'll restart in the next group
                                    globalIsPlaying = false;
                                    globalPlayingPath = null;
                                    globalPlayingIndex = -1;
                                    // Do NOT mark userPaused here - user intends to continue
                                    continuePlayNextGroup = true;
                                    pendingGroupIndex = groupIndex + 1;
                                    // Immediately move to next group so playback continues there
                                    moveAfter = 1;
                                }

                                System.IO.File.Delete(fileToDelete.Path);
                                group.Files.RemoveAt(selection);
                                AnsiConsole.MarkupLine("[red]Deleted.[/]");
                                // Adjust selection
                                if (selection >= group.Files.Count) selection = Math.Max(0, group.Files.Count - 1);

                                // If we've scheduled a continuation (we deleted the playing file), advance now
                                if (continuePlayNextGroup)
                                {
                                    break; // leave inner loop to advance groups and continue playback
                                }

                                // If there is only one track left, move to next group automatically
                                if (group.Files.Count <= 1)
                                {
                                    // if we were playing, remember to continue in next group
                                    if (!continuePlayNextGroup)
                                    {
                                        continuePlayNextGroup = globalIsPlaying && !userPaused;
                                        pendingGroupIndex = groupIndex + 1;
                                    }
                                    moveAfter = 1;
                                    break; // leave inner loop to advance groups
                                }

                                if (!group.Files.Any())
                                {
                                    if (!continuePlayNextGroup)
                                    {
                                        continuePlayNextGroup = globalIsPlaying && !userPaused;
                                        pendingGroupIndex = groupIndex + 1;
                                    }
                                    moveAfter = 1;
                                    break; // group empty -> next group
                                }
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine($"[red]Error deleting:[/] {EscapeMarkup(ex.Message)}");
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

                        if (globalIsPlaying && globalPlayingIndex == selection)
                        {
                            // Stop the currently playing file
                            player.Stop();
                            globalIsPlaying = false;
                            globalPlayingPath = null;
                            globalPlayingIndex = -1;
                            userPaused = true; // user manually stopped playback
                            AnsiConsole.MarkupLine("[gray]Stopped.[/]");
                        }
                        else
                        {
                            // Start playing the selected file
                            if (globalIsPlaying) player.Stop();
                            player.Play(file.Path);
                            globalIsPlaying = true;
                            globalPlayingPath = file.Path;
                            globalPlayingIndex = selection;
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
                            if (globalIsPlaying)
                            {
                                if (globalPlayingIndex == idx)
                                {
                                    // Toggle off the currently playing track
                                    player.Stop();
                                    globalIsPlaying = false;
                                    globalPlayingPath = null;
                                    globalPlayingIndex = -1;
                                    userPaused = true; // record manual stop
                                }
                                else
                                {
                                    // Switch to the newly requested track
                                    if (globalIsPlaying) player.Stop();
                                    var file = group.Files[idx];
                                    player.Play(file.Path);
                                    globalIsPlaying = true;
                                    globalPlayingPath = file.Path;
                                    globalPlayingIndex = idx;
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
                                    globalIsPlaying = true;
                                    globalPlayingPath = file.Path;
                                    globalPlayingIndex = idx;
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
                var newIndex = Math.Clamp(groupIndex + moveAfter, 0, Math.Max(0, groups.Count - 1));
                groupIndex = newIndex;

                // If we have a pending continue flag and valid pendingGroupIndex matches new index, start playback immediately
                if (continuePlayNextGroup && pendingGroupIndex.HasValue && pendingGroupIndex.Value == groupIndex)
                {
                    var nextGroup = groups[groupIndex];
                    if (nextGroup.Files.Count > 0)
                    {
                        try
                        {
                            player.Play(nextGroup.Files[0].Path);
                            globalIsPlaying = true;
                            globalPlayingPath = nextGroup.Files[0].Path;
                            globalPlayingIndex = 0;
                            userPaused = false;
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                    continuePlayNextGroup = false;
                    pendingGroupIndex = null;
                }

                break;
            }
        }

        player.Stop();
        return 0;
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
            s = Regex.Replace(s, @"\b(?:feat\.?|ft\.? )\b", "", RegexOptions.IgnoreCase);
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

    static string FormatDuration(TimeSpan? ts)
    {
        if (ts == null) return string.Empty;
        var t = ts.Value;
        // Use total minutes so durations >59 min display properly
        int minutes = (int)Math.Floor(t.TotalMinutes);
        int seconds = t.Seconds;
        return $"{minutes:D2}:{seconds:D2}";
    }
}
