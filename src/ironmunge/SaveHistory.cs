using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using corgit;

namespace ironmunge
{
    class SaveHistory
    {
        public static string Prefix { get; } = "ironmunge_";

        public string BaseDirectory { get; }

        private string GitPath { get; }

        public SaveHistory(string baseDirectory, string gitPath)
        {
            this.BaseDirectory = baseDirectory;
            this.GitPath = gitPath;
        }

        private async Task<string> HistoryDirFromSavePathAsync(string savePath, string filename)
        {
            var historyDir = Path.Combine(BaseDirectory, Prefix + Path.GetFileNameWithoutExtension(filename));
            if (!Directory.Exists(historyDir))
            {
                await InitializeHistoryDirectoryAsync(historyDir);
            }

            return historyDir;
        }

        private async Task InitializeHistoryDirectoryAsync(string path)
        {
            Directory.CreateDirectory(path);

            var corgit = new Corgit(GitPath, path);
            await corgit.InitAsync();
            await corgit.ConfigAsync("user.name", value: "ironmunge");
            await corgit.ConfigAsync("user.email", value: "@v0.1");
            //unset text to disable eol conversions
            File.WriteAllText(Path.Combine(path, ".gitattributes"), "* -text");
            await corgit.AddAsync();
            await corgit.CommitAsync("Initialize save history");
        }

        public async Task<(string description, string commitId)> AddSaveAsync(string savePath, string filename)
        {
            var fi = new FileInfo(savePath);
            if (fi.Length == 0)
                throw new ArgumentException("Save is empty");

            var historyDir = await HistoryDirFromSavePathAsync(savePath, filename);
            ZipFile.ExtractToDirectory(savePath, historyDir, true);

            string ReadSingleEntry(IReadOnlyCollection<object> col)
                => col.Cast<string>().Single().Trim('"');

            var saveMeta = Path.Combine(historyDir, "meta");
            var sg = new LibCK2.SaveGame(File.ReadAllText(saveMeta)).GameState;
            string gameDescription = $"[{ReadSingleEntry(sg["date"])}] {ReadSingleEntry(sg["player_name"])}";

            var corgit = new Corgit(GitPath, historyDir);
            await corgit.AddAsync(); //stage all
            var result = await corgit.CommitAsync(gameDescription);
            return (gameDescription, result.Output);
        }
    }
}
