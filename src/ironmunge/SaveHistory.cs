using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chronicler;
using corgit;
using ironmunge.Common;

namespace ironmunge
{
    class SaveHistory
    {
        public static string Prefix { get; } = "ironmunge_";

        public string BaseDirectory { get; }

        private string GitPath { get; }

        public string? Remote { get; }

        public SaveHistory(string baseDirectory, string gitPath, string? remote = null)
        {
            this.BaseDirectory = baseDirectory;
            this.GitPath = gitPath;
            this.Remote = remote;
        }

        private async ValueTask<string> InitializeHistoryDirectoryAsync(string filename)
        {
            var path = Path.Combine(BaseDirectory, Prefix + Path.GetFileNameWithoutExtension(filename));
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);

                var corgit = new Corgit(GitPath, path);
                await corgit.InitAsync();
                await corgit.ConfigAsync("user.name", value: "ironmunge");
                await corgit.ConfigAsync("user.email", value: "@v0.1");
                await corgit.ConfigAsync("push.default", value: "current");
                //unset text to disable eol conversions
                File.WriteAllText(Path.Combine(path, ".gitattributes"), "* -text");
                await corgit.AddAsync();
                await corgit.CommitAsync("Initialize save history");
            }

            return path;
        }

        public async Task<(string description, string commitId)> AddSaveAsync(string savePath, string filename)
        {
            var fi = new FileInfo(savePath);
            if (fi.Length == 0)
                throw new ArgumentException("Save is empty");

            var historyDir = await InitializeHistoryDirectoryAsync(filename);

            using (var zip = new ZipArchive(File.OpenRead(savePath), ZipArchiveMode.Read, false, CK2Settings.SaveGameEncoding))
            {
                var outputs = zip.Entries.Select(entry => new { entry, outputPath = Path.Combine(historyDir, entry.FullName) });
                foreach (var a in outputs)
                {
                    using (var outputStream = File.Create(a.outputPath))
                    using (var entryStream = a.entry.Open())
                        await entryStream.CopyToAsync(outputStream);
                }
            }

            return await AddGitSaveAsync(historyDir);
        }

        private async Task<(string description, string commitId)> AddGitSaveAsync(string historyDir, bool extendedDescription = true)
        {
            var corgit = new Corgit(GitPath, historyDir);
            await corgit.AddAsync(); //stage all

            var statuses = (await corgit.StatusAsync())
                .Select(gfs => gfs.Path)
                .ToArray();
            if (!statuses.Any()) return default;

            string gameDescription;

            var metaName = Path.Combine(historyDir, "meta");
            var saveName = Directory.EnumerateFiles(historyDir, "*.ck2", SearchOption.TopDirectoryOnly).Single();
            {
                var metaJson = await CK2Json.ParseFileAsync(metaName);
                gameDescription = GetGameDescription(metaJson);
            }

            if (extendedDescription)
            {
                var sbDescription = new StringBuilder(gameDescription);

                var ck2json = await CK2Json.ParseFileAsync(saveName);

                var chronicleCollection = ChronicleCollection.Parse(ck2json);
                var mostRecentChapter = (from chronicle in chronicleCollection.Chronicles
                                         from chapter in chronicle.Chapters
                                         where chapter.Entries.Any()
                                         select chapter).Last();
                foreach (var entry in mostRecentChapter.Entries.Select(entry => entry.Text).Reverse())
                {
                    sbDescription.AppendLine().AppendLine().Append(entry);
                }

                gameDescription = sbDescription.ToString();
            }

            var result = await corgit.CommitAsync(gameDescription);
            if (result.ExitCode == 0 && !string.IsNullOrEmpty(Remote))
            {
                var setUrl = await corgit.RunGitAsync($"remote set-url origin {Remote}");
                if (setUrl.ExitCode != 0)
                {
                    var addRemote = await corgit.RunGitAsync($"remote add origin {Remote}");
                    if (addRemote.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"Configuring remote failed: {setUrl} {addRemote}");
                    }
                }

                var push = await corgit.RunGitAsync("push");
                if (push.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Pushing remote failed: {push}");
                }
            }

            return (gameDescription, result.Output);
        }

        private static string GetGameDescription(System.Text.Json.JsonDocument doc)
            => $"[{doc.RootElement.GetProperty("date").GetString()}] {doc.RootElement.GetProperty("player_name").GetString()}";
    }
}
