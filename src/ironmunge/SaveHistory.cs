using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using corgit;
using ironmunge.Common;
using System.Text.Json;
using ironmunge.Plugins;
using System.Collections.Generic;

namespace ironmunge
{
    class SaveHistory
    {
        public static string Prefix { get; } = "ironmunge_";

        public string BaseDirectory { get; }

        private string GitPath { get; }

        public string? Remote { get; }

        public System.Collections.Concurrent.ConcurrentBag<IMunger> Mungers { get; }

        public SaveHistory(string baseDirectory, string gitPath, string? remote = null, IEnumerable<IMunger>? plugins = null)
        {
            this.BaseDirectory = baseDirectory;
            this.GitPath = gitPath;
            this.Remote = remote;
            this.Mungers = new System.Collections.Concurrent.ConcurrentBag<IMunger>(plugins ?? Enumerable.Empty<IMunger>());
        }

        private async ValueTask<string> InitializeHistoryDirectoryAsync(string filename)
        {
            var saveName = Path.GetFileNameWithoutExtension(filename);
            var path = Path.Combine(BaseDirectory, Prefix + saveName);
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

        public async ValueTask<string> AddSaveAsync(string savePath, string filename)
        {
            var outputDir = await InitializeHistoryDirectoryAsync(filename);

            using var zipStream = File.OpenRead(savePath);
            await UnzipSaveAsync(zipStream, outputDir);

            var (gameDescription, save, meta) = await ParseSaveAsync(outputDir);
            gameDescription = await RunMungersAsync(outputDir, gameDescription, save, meta);

            await AddGitSaveAsync(gameDescription, outputDir);

            return gameDescription;
        }

        private async ValueTask<string> RunMungersAsync(string historyDir, string gameDescription, JsonDocument save, JsonDocument meta)
        {
            var mungerTasks = (from munger in Mungers
                               let mungerProgress = new Progress<string>(s => Console.WriteLine($"[{munger.Name}] {s}"))
                               select munger.MungeAsync(historyDir, (save, meta), mungerProgress)).ToArray();

            foreach (var task in mungerTasks)
            {
                var extendedDescription = await task;
                if (!string.IsNullOrWhiteSpace(extendedDescription))
                {
                    gameDescription += extendedDescription;
                }
            }

            return gameDescription;
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

        private async ValueTask<(string gameDescription, JsonDocument save, JsonDocument meta)> ParseSaveAsync(string outputDir)
        {
            static string GetGameDescription(JsonDocument doc)
             => $"[{doc.RootElement.GetProperty("date").GetString()}] {doc.RootElement.GetProperty("player_name").GetString()}";

            var metaName = Path.Combine(outputDir, "meta");
            var (metaJson, _) = await ConvertCk2JsonAsync(metaName);

            var saveName = Directory.EnumerateFiles(outputDir, "*.ck2", SearchOption.TopDirectoryOnly).Single();
            var (saveJson, _) = await ConvertCk2JsonAsync(saveName);

            var gameDescription = GetGameDescription(metaJson);
            return (gameDescription, saveJson, metaJson);
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

        private async ValueTask GitPushToRemoteAsync(Corgit corgit)
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

        private async ValueTask AddGitSaveAsync(string gameDescription, string historyDir)
        {
            var corgit = new Corgit(GitPath, historyDir);
            await corgit.AddAsync(); //stage all

            var statuses = (await corgit.StatusAsync())
                .Select(gfs => gfs.Path);
            if (statuses.Any())
            {
                var result = await corgit.CommitAsync(gameDescription);
                if (result.ExitCode == 0 && !string.IsNullOrEmpty(Remote))
                {
                    await GitPushToRemoteAsync(corgit);
                }
            }
        }
    }
}
