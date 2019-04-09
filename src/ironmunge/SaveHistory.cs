using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using corgit;
using LibCK2;

namespace ironmunge
{
    class SaveHistory
    {
        public static string Prefix { get; } = "ironmunge_";

        public string BaseDirectory { get; }

        private string GitPath { get; }

        public string Remote { get; }

        public SaveHistory(string baseDirectory, string gitPath, string remote = null)
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

            using (var zip = new ZipArchive(File.OpenRead(savePath), ZipArchiveMode.Read, false, SaveGame.SaveGameEncoding))
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

        private async Task<(string description, string commitId)> AddGitSaveAsync(string historyDir)
        {
            var corgit = new Corgit(GitPath, historyDir);
            await corgit.AddAsync(); //stage all

            var statuses = (await corgit.StatusAsync())
                .Select(gfs => gfs.Path)
                .ToArray();
            if (!statuses.Any()) return default;

            string gameDescription;

            var metaName = Path.Combine(historyDir, "meta");
            using (var meta = File.OpenRead(metaName))
            {
                var parsedMeta = await SaveGame.ParseAsync(meta);
                gameDescription = GetGameDescription(parsedMeta);
            }

            var result = await corgit.CommitAsync(gameDescription);
            return (gameDescription, result.Output);
        }

        private static string GetGameDescription(SaveGame saveGame)
            => $"[{saveGame.Date}] {saveGame.PlayerName}";
    }
}
