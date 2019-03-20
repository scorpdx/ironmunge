using Humanizer;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CommandLine;
using ironmunge.Common;
using corgit;
using System.Threading.Tasks;

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

        static async Task<(GitCommit commit, int index)?> SelectSavePromptAsync(Corgit git, (string path, string display) selectedDirectory)
        {
            Console.WriteLine($"| Viewing {selectedDirectory.display}");
            Console.WriteLine($"| From oldest to newest");
            Console.WriteLine();

            string selection;
            int selectedIndex;
            {
                var commits = (await git.LogAsync(new GitArguments.LogOptions(maxEntries: null, reverse: true), "*.ck2", "meta")).ToList();
                for (int i = 0; i < commits.Count; i++)
                {
                    DisplayCommit(commits[i], i+1);
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
                } while (!int.TryParse(selection, out selectedIndex) || (selectedIndex <= 0 || selectedIndex > commits.Count));

                return (commits[selectedIndex - 1], selectedIndex);
            }
        }

        static bool ConfirmSavePrompt((GitCommit commit, int index) selectedCommit)
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

        static async Task RestoreSaveAsync(Corgit git, string saveGameLocation, string historyPath, GitCommit commit)
        {
            {
                var currentTime = DateTime.UtcNow.ToString("yyyy''MM''dd'T'HH''mm''ss", CultureInfo.InvariantCulture);
                var restoreBranch = await git.CheckoutNewBranchAsync($"restore{currentTime}", startPoint: commit.Hash);
            }

            var historyContents = Directory.EnumerateFiles(historyPath, "*", SearchOption.TopDirectoryOnly);
            var saveContents = new
            {
                save = historyContents.Single(a => Path.GetExtension(a).Equals(".ck2", StringComparison.OrdinalIgnoreCase)),
                meta = historyContents.Single(a => Path.GetFileName(a).Equals("meta", StringComparison.OrdinalIgnoreCase))
            };
            var saveGameName = Path.GetFileName(saveContents.save);
            var saveGamePath = Path.Combine(saveGameLocation, saveGameName);

            //make a backup if sg already exists
            string backupPath = null;
            if (File.Exists(saveGamePath))
            {
                backupPath = Path.ChangeExtension(saveGamePath, ".ck2.backup");
                File.Copy(saveGamePath, backupPath, true);
            }

            var res = await git.ArchiveAsync(commit.Hash, saveGamePath, options: new corgit.GitArguments.ArchiveOptions(format: "zip", paths: new[] { saveGameName, "meta" }));

            try
            {
                using (var readStream = File.Open(saveGamePath, FileMode.Open, FileAccess.ReadWrite))
                using (var saveZip = new ZipArchive(readStream, ZipArchiveMode.Update, true, LibCK2.SaveGame.SaveGameEncoding))
                {
                    var saveEntry = saveZip.Entries[0];
                    var metaEntry = saveZip.Entries[1];

                    var crcs = File.ReadLines(Path.Combine(historyPath, "ironmunge_crcs.txt"))
                        .ToDictionary(l => l.Substring(0, l.LastIndexOf(' ')), l => l.Substring(l.LastIndexOf(' ') + 1))
                        .ToDictionary(a => a.Key, a => uint.Parse(a.Value, NumberStyles.HexNumber));
                    var matchingCrcs = (from kvp in crcs
                                        join zipEntries in saveZip.Entries on kvp.Key equals zipEntries.Name
                                        select new { ledgerName = kvp.Key, ledgerCrc = kvp.Value, zipCrc = zipEntries.Crc32, matchesCrc = kvp.Value == zipEntries.Crc32 }).ToList();
                    if (!(matchingCrcs.Count == 2 && matchingCrcs.All(a => a.matchesCrc)))
                        throw new InvalidOperationException("Reconstructed history save does not match original");
                }

                Console.WriteLine("Save restored! You are now on a new timeline.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                if (!string.IsNullOrEmpty(backupPath))
                {
                    File.Copy(backupPath, saveGamePath, true);
                    Console.Error.WriteLine($"SaveManager restored backup {Path.GetFileNameWithoutExtension(backupPath)}");
                }
                Console.Error.WriteLine("You are NOT on a new timeline.");
            }
        }

        static string DefaultSaveDir => LibCK2.SaveGame.SaveGameLocation;

        static void Main(string[] args)
        {
            var options = CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    var task = InteractiveRestoreAsync(o.GitLocation ?? GitHelpers.DefaultGitPath, o.SaveGameLocation ?? DefaultSaveDir, o.SaveHistoryLocation ?? DefaultSaveDir);
                    task.Wait();
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

        static async Task InteractiveRestoreAsync(string gitPath, string saveGameLocation, string saveHistoryLocation)
        {
            if (string.IsNullOrEmpty(gitPath))
                throw new ArgumentNullException(nameof(gitPath), "git was not found");

        SelectHistory:
            Console.Clear();
            var selectedDirectory = SelectHistoryPrompt(saveHistoryLocation);

            var git = new Corgit(gitPath, selectedDirectory.path);

        SelectSave:
            Console.Clear();
            var selectSaveResult = await SelectSavePromptAsync(git, selectedDirectory);

            if (!selectSaveResult.HasValue) goto SelectHistory;
            var selectedCommit = selectSaveResult.Value;

#pragma warning disable CS0164 // This label has not been referenced
        ConfirmRestore:
            Console.Clear();
            var confirmed = ConfirmSavePrompt(selectedCommit);
            if (!confirmed) goto SelectSave;

            await RestoreSaveAsync(git, saveGameLocation, selectedDirectory.path, selectedCommit.commit);

        Finished:
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
#pragma warning restore CS0164 // This label has not been referenced

        }

        private static void DisplayCommit(GitCommit c, int index, DateTimeOffset? lastDate = null)
        {
            if (c.Parents.Count() > 1)
            {
                Console.WriteLine("Merge: {0}",
                    string.Join(" ", c.Parents.Select(p => p.Substring(0, 7)).ToArray()));
            }

            const string RFC2822Format = "ddd dd MMM HH:mm:ss yyyy K";
            var timestamp = c.AuthorDate.ToString(RFC2822Format, CultureInfo.InvariantCulture);
            var timegist = c.AuthorDate.Humanize(lastDate, culture: CultureInfo.InvariantCulture);
            ConsoleWriteColored($"Date:\t%fg{timegist}%R\t{timestamp}");
            Console.WriteLine();

            ConsoleWriteColored($"%fc[{index:D2}]%R\t{c.Message}");
            Console.WriteLine();
            Console.WriteLine();
        }
    }
}
