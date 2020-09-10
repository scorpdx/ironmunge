using Humanizer;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CommandLine;
using Ironmunge.Common;
using corgit;
using System.Threading.Tasks;
using System.Text;

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
                    DisplayCommit(commits[i], i + 1);
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

            var savePath = Directory.EnumerateFiles(historyPath, "*.ck2", SearchOption.TopDirectoryOnly).Single();
            var metaPath = Directory.EnumerateFiles(historyPath, "meta", SearchOption.TopDirectoryOnly).Single();

            var saveGameName = Path.GetFileName(savePath);
            var saveGamePath = Path.Combine(saveGameLocation, saveGameName);

            //make a backup if sg already exists
            string? backupPath = null;
            if (File.Exists(saveGamePath))
            {
                backupPath = Path.ChangeExtension(saveGamePath, ".ck2.backup");
                File.Copy(saveGamePath, backupPath, true);
            }

            try
            {
                await using (var writeStream = File.Create(saveGamePath))
                using (var saveZip = new ZipArchive(writeStream, ZipArchiveMode.Create, true, CK2Settings.SaveGameEncoding))
                {

                    saveZip.CreateEntryFromFile(savePath, saveGameName);
                    saveZip.CreateEntryFromFile(metaPath, "meta");
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

        static string DefaultSaveDir => CK2Settings.SaveGameLocation;

        static void Main(string[] args)
        {
            var options = CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    var task = InteractiveRestoreAsync(o.GitLocation ?? Options.DefaultGitPath, o.SaveGameLocation ?? DefaultSaveDir, o.SaveHistoryLocation ?? DefaultSaveDir);
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
