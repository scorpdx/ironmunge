using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chronicler;
using corgit;
using ironmunge.Common;
using System.Text.Json;

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
            var historyDir = await InitializeHistoryDirectoryAsync(filename);

            using var zipStream = File.OpenRead(savePath);
            await UnzipSaveAsync(zipStream, historyDir);

            return await AddGitSaveAsync(historyDir);
        }

        private async ValueTask UnzipSaveAsync(Stream zipStream, string outputDir)
        {
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, true, CK2Settings.SaveGameEncoding);
            foreach (var entry in zip.Entries)
            {
                var outputPath = Path.Combine(outputDir, entry.FullName);

                using var outputStream = File.Create(outputPath);
                using var entryStream = entry.Open();

                await entryStream.CopyToAsync(outputStream);
            }
        }

        private async ValueTask<(JsonDocument json, string jsonPath)> ConvertCk2JsonAsync(string filepath)
        {
            var jsonPath = Path.ChangeExtension(filepath, ".json");
            await using var jsonStream = File.Create(jsonPath);

            await using var writer = new Utf8JsonWriter(jsonStream,
                new JsonWriterOptions
                {
                    Indented = true
                });
            var json = await CK2Json.ParseFileAsync(filepath);
            json.WriteTo(writer);

            return (json, jsonPath);
        }

        private async Task<(string description, string commitId)> AddGitSaveAsync(string historyDir, bool extendedDescription = true)
        {
            var corgit = new Corgit(GitPath, historyDir);
            await corgit.AddAsync(); //stage all

            var statuses = (await corgit.StatusAsync())
                .Select(gfs => gfs.Path)
                .ToArray();
            if (!statuses.Any()) return default;

            var metaName = Path.Combine(historyDir, "meta");
            var metaJson = await ConvertCk2JsonAsync(metaName);

            var saveName = Directory.EnumerateFiles(historyDir, "*.ck2", SearchOption.TopDirectoryOnly).Single();
            var saveJson = await ConvertCk2JsonAsync(saveName);

            var gameDescription = GetGameDescription(metaJson.json);
            if (extendedDescription)
            {
                var sbDescription = new StringBuilder(gameDescription);

                var chronicleCollection = ChronicleCollection.Parse(saveJson.json);
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

        private static string GetGameDescription(JsonDocument doc)
            => $"[{doc.RootElement.GetProperty("date").GetString()}] {doc.RootElement.GetProperty("player_name").GetString()}";
    }
}
