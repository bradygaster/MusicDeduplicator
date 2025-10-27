using System;
using System.Linq;
using Spectre.Console;

class Program
{
    static void Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : @"F:\Music";
        var files = AudioLibraryScanner.Scan(root).ToList();

        var groups = files
            .GroupBy(f => $"{f.Artist}|{f.Title}|{f.Duration?.TotalSeconds:F0}")
            .Where(g => g.Count() > 1)
            .Select((g, i) => new { Index = i + 1, Files = g.ToList() })
            .ToList();

        if (!groups.Any())
        {
            AnsiConsole.MarkupLine("[green]No duplicates found![/]");
            return;
        }

        AnsiConsole.MarkupLine($"[yellow]Found {groups.Count} duplicate groups.[/]\n");

        using var player = new Player();

        foreach (var group in groups)
        {
            while (true)
            {
                int selection = 0;
                bool isPlaying = false;
                string? playingPath = null;

                if (!group.Files.Any()) break;

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

                        table.AddRow(
                            indexCol,
                            artistCol,
                            titleCol,
                            durationCol,
                            EscapeMarkup(f.Path));
                    }

                    AnsiConsole.Write(table);
                    AnsiConsole.MarkupLine("\n[gray]↑/↓ to select, [green]P/Enter[/] to play/stop, [red]Del[/] to delete, [blue]N[/]ext, [magenta]Q[/]uit[/]");

                    if (isPlaying && !string.IsNullOrEmpty(playingPath))
                    {
                        AnsiConsole.MarkupLine($"[green]Playing:[/] {EscapeMarkup(playingPath)}");
                    }

                    var key = Console.ReadKey(true);

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
                    else if (key.Key == ConsoleKey.N)
                    {
                        // Next group - stop any playing file first
                        if (isPlaying) { player.Stop(); isPlaying = false; playingPath = null; }
                        break;
                    }
                    else if (key.Key == ConsoleKey.Q)
                    {
                        // Quit application
                        if (isPlaying) { player.Stop(); isPlaying = false; playingPath = null; }
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
                                }

                                System.IO.File.Delete(fileToDelete.Path);
                                group.Files.RemoveAt(selection);
                                AnsiConsole.MarkupLine("[red]Deleted.[/]");
                                // Adjust selection
                                if (selection >= group.Files.Count) selection = Math.Max(0, group.Files.Count - 1);
                                if (!group.Files.Any()) break; // group empty -> next group
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

                        if (isPlaying && playingPath == file.Path)
                        {
                            // Stop the currently playing file
                            player.Stop();
                            isPlaying = false;
                            playingPath = null;
                            AnsiConsole.MarkupLine("[gray]Stopped.[/]");
                        }
                        else
                        {
                            // Stop any other playing file and start this one
                            if (isPlaying) player.Stop();
                            player.Play(file.Path);
                            isPlaying = true;
                            playingPath = file.Path;
                        }

                        // After toggling, re-render
                        continue;
                    }

                    // Unhandled keys just re-render
                }

                // Exit the group's while(true) to move to next group
                break;
            }
        }

        player.Stop();
    }

    // Helper to escape Spectre.Console markup characters in strings
    static string EscapeMarkup(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return Markup.Escape(text);
    }
}
