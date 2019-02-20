using Humanizer;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CommandLine;

namespace SaveManager
{
    using static ConsoleHelpers;
    class Program
    {
        static (string path, string display) SelectHistoryPrompt(string saveHistoryLocation)
        {
            Console.WriteLine("Save histories found:");

            var directories = Directory.EnumerateDirectories(saveHistoryLocation, "ironmunge_*");
            var directoriesDisplay = directories.Select(dir =>
                new
                {
                    path = dir,
                    display = Path.GetFileName(dir).Substring("ironmunge_".Length)
                }).ToArray();

            for (int i = 0; i < directoriesDisplay.Length; i++)
            {
                var dir = directoriesDisplay[i];
                ConsoleWriteColored($"%fc[{i + 1:D2}]\t%R{dir.display}");
                Console.WriteLine();
            }

            string selection;
            int selectedIndex;
            do
            {
                Console.WriteLine("Please select a history to view.");
                Console.Write("> ");
                selection = Console.ReadLine();
            } while (!int.TryParse(selection, out selectedIndex) || (selectedIndex <= 0 || selectedIndex > directoriesDisplay.Length));

            var selectedDirectory = directoriesDisplay[selectedIndex - 1];
            return (selectedDirectory.path, selectedDirectory.display);
        }

        static (Commit commit, int index)? SelectSavePrompt((string path, string display) selectedDirectory)
        {
            Console.WriteLine($"| Viewing {selectedDirectory.display}");
            Console.WriteLine($"| From oldest to newest");
            Console.WriteLine();

            string selection;
            int selectedIndex;
            using (var repo = new Repository(selectedDirectory.path))
            {
                var commitList = new List<Commit>();

                var filter = new CommitFilter { SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Reverse };
                foreach (Commit c in repo.Commits.QueryBy(filter))
                {
                    commitList.Add(c);
                    DisplayCommit(c, commitList.Count);
                }

                do
                {
                    Console.WriteLine("Please select a save to restore.");
                    Console.WriteLine("Or leave blank to go back.");
                    Console.Write("> ");
                    selection = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(selection))
                    {
                        return null;
                    }
                } while (!int.TryParse(selection, out selectedIndex) || (selectedIndex <= 0 || selectedIndex > commitList.Count));

                return (commitList[selectedIndex - 1], selectedIndex);
            }
        }

        static bool ConfirmSavePrompt((Commit commit, int index) selectedCommit)
        {
            Console.WriteLine("You selected: ");

            DisplayCommit(selectedCommit.commit, selectedCommit.index);

            string selection;
            do
            {
                Console.WriteLine("Would you like to restore this save?");
                ConsoleWriteColored("Please type %fgy%fGes%R to restore.");
                Console.WriteLine();
                Console.WriteLine("Or leave blank to go back.");
                Console.Write("> ");
                selection = Console.ReadLine().Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(selection))
                    return false;
            } while (selection[0] != 'Y');

            return true;
        }

        static void RestoreSave(string saveGameLocation, string historyPath, Commit commit)
        {
            using (var repo = new Repository(historyPath))
            {
                var currentTime = DateTime.UtcNow.ToString("yyyy''MM''dd'T'HH''mm''ss", CultureInfo.InvariantCulture);
                var restoreBranch = repo.CreateBranch($"restore{currentTime}", commit);
                Commands.Checkout(repo, restoreBranch);

                //TODO: use `git archive` for this
            }

            var historyContents = Directory.GetFiles(historyPath);
            var saveContents = new
            {
                save = historyContents.Single(a => Path.GetExtension(a)?.Equals(".ck2", StringComparison.OrdinalIgnoreCase) ?? false),
                meta = historyContents.Single(a => string.Equals(Path.GetFileName(a), "meta", StringComparison.OrdinalIgnoreCase))
            };
            var saveGameName = Path.GetFileName(saveContents.save);
            var saveGamePath = Path.Combine(saveGameLocation, saveGameName);

            //make a backup if sg already exists
            if (File.Exists(saveGamePath))
            {
                var backupPath = Path.ChangeExtension(saveGamePath, ".ck2.backup");
                File.Copy(saveGamePath, backupPath, true);
            }

            using (var writeStream = File.Create(saveGamePath))
            using (var saveZip = new ZipArchive(writeStream, ZipArchiveMode.Create))
            {
                saveZip.CreateEntryFromFile(saveContents.save, saveGameName);
                saveZip.CreateEntryFromFile(saveContents.meta, "meta");
            }

            Console.WriteLine("Save restored! You are now on a new timeline.");
        }

        class Options
        {
            [Option('s', "saveGames", HelpText = "Path of the Crusader Kings save game directory")]
            public string SaveGameLocation { get; set; }

            [Option('h', "saveHistories", HelpText = "Path of the ironmunge save history directory")]
            public string SaveHistoryLocation { get; set; }
        }

        static string DefaultSaveDir => LibCK2.SaveGame.SaveGameLocation;

        static void Main(string[] args)
        {

            var options = CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    InteractiveRestore(o.SaveGameLocation ?? DefaultSaveDir,
                                       o.SaveHistoryLocation ?? DefaultSaveDir);
                })
                .WithNotParsed(o =>
                {
                    foreach (var error in o)
                    {
                        Console.WriteLine(error);
                    }

                    Console.WriteLine("Please correct the options and try again.");
                    Console.WriteLine("Press ENTER to exit.");
                    Console.ReadLine();
                });
        }

        static void InteractiveRestore(string saveGameLocation, string saveHistoryLocation)
        {

        SelectHistory:
            Console.Clear();
            var selectedDirectory = SelectHistoryPrompt(saveHistoryLocation);

        SelectSave:
            Console.Clear();
            var selectSaveResult = SelectSavePrompt(selectedDirectory);

            if (!selectSaveResult.HasValue) goto SelectHistory;
            var selectedCommit = selectSaveResult.Value;

#pragma warning disable CS0164 // This label has not been referenced
        ConfirmRestore:
            Console.Clear();
            var confirmed = ConfirmSavePrompt(selectedCommit);
            if (!confirmed) goto SelectSave;

            RestoreSave(saveGameLocation, selectedDirectory.path, selectedCommit.commit);

        Finished:
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
#pragma warning restore CS0164 // This label has not been referenced

        }

        private static void DisplayCommit(Commit c, int index, DateTimeOffset? lastDate = null)
        {
            if (c.Parents.Count() > 1)
            {
                Console.WriteLine("Merge: {0}",
                    string.Join(" ", c.Parents.Select(p => p.Id.Sha.Substring(0, 7)).ToArray()));
            }

            const string RFC2822Format = "ddd dd MMM HH:mm:ss yyyy K";
            var timestamp = c.Author.When.ToString(RFC2822Format, CultureInfo.InvariantCulture);
            var timegist = c.Author.When.Humanize(lastDate, culture: CultureInfo.InvariantCulture);
            ConsoleWriteColored($"Date:\t%fg{timegist}%R\t{timestamp}");
            Console.WriteLine();

            ConsoleWriteColored($"%fc[{index:D2}]%R\t{c.Message}");
            Console.WriteLine();
            Console.WriteLine();
        }
    }
}
