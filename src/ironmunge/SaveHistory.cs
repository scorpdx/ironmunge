using corgit;
using Ironmunge.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ironmunge
{
    public sealed class SaveHistory
    {
        public static string Prefix { get; } = "ironmunge_";

        public IGame Game { get; }

        public string BaseDirectory { get; }

        private string GitPath { get; }

        public string? Remote { get; }

        public SaveHistory(IGame game, string baseDirectory, string gitPath, string? remote = null)
        {
            Game = game;
            BaseDirectory = baseDirectory;
            GitPath = gitPath;
            Remote = remote;
        }

        private static IEnumerable<string> DefaultGitAttributes
        {
            get
            {
                yield return "* -text";
                yield return "*.json text";
            }
        }

        private async ValueTask<string> InitializeHistoryDirectoryAsync(string filename)
        {
            var saveName = Path.GetFileNameWithoutExtension(filename);
            var path = Path.Join(BaseDirectory, Prefix + saveName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);

                var corgit = new Corgit(GitPath, path);
                await corgit.InitAsync();
                await corgit.ConfigAsync("user.name", value: "ironmunge");
                await corgit.ConfigAsync("user.email", value: "@v0.1.0");
                await corgit.ConfigAsync("push.default", value: "current");
                //unset text to disable eol conversions
                var gitattributesPath = Path.Join(path, ".gitattributes");
                await File.WriteAllLinesAsync(gitattributesPath, DefaultGitAttributes);
                await corgit.AddAsync();
                await corgit.CommitAsync("Initialize save history");
            }

            return path;
        }

        public async Task<string> AddSaveAsync(string savePath, string filename, CancellationToken cancellationToken = default)
        {
            var historyDir = await InitializeHistoryDirectoryAsync(filename);
            var (json, commitMessage) = await Game.AddSaveAsync(savePath, null, cancellationToken);

            var historySavePath = Path.Join(historyDir, filename);
            File.Move(savePath, historySavePath, true);

            using (json)
            {
                var jsonPath = Path.Join(historyDir, Path.ChangeExtension(filename, ".json"));
                await using var jsonStream = File.Create(jsonPath);
                await using var jsonWriter = new Utf8JsonWriter(jsonStream);
                json.WriteTo(jsonWriter);

                //TODO: run mungers
            }

            await AddGitSaveAsync(commitMessage, historyDir);
            return commitMessage;
        }

        //private async ValueTask<string> RunMungersAsync(string historyDir, string gameDescription, JsonDocument save, JsonDocument meta)
        //{
        //    var mungerTasks = (from munger in Mungers
        //                       let mungerProgress = new Progress<string>(s => Console.WriteLine($"[{munger.Name}] {s}"))
        //                       select munger.MungeAsync(historyDir, (save, meta), mungerProgress)).ToArray();

        //    foreach (var task in mungerTasks)
        //    {
        //        var extendedDescription = await task;
        //        if (!string.IsNullOrWhiteSpace(extendedDescription))
        //        {
        //            gameDescription += extendedDescription;
        //        }
        //    }

        //    return gameDescription;
        //}

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
