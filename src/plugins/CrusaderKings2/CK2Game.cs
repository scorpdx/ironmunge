using Ironmunge.Plugins;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CrusaderKings2
{
    public class CK2Game : IGame
    {
        public static Encoding SaveGameEncoding { get; } =
            CodePagesEncodingProvider.Instance.GetEncoding(1252)
            ?? throw new InvalidOperationException("Couldn't find western-1252 code page");
        private static string CK2JsonConverterPath { get; } =
            Directory.EnumerateFiles("./Resources/", "ck2json*", SearchOption.TopDirectoryOnly).SingleOrDefault()
            ?? throw new InvalidOperationException("Couldn't find ck2json parser executable");

        public string Name { get; } = "Crusader Kings 2";

        public IEnumerable<string> Filters { get; } = new string[] { "*.ck2" };

        public void Dispose()
        {
            ;
        }

        public async ValueTask<(JsonDocument saveDocument, string commitMessage)> AddSaveAsync(string savePath, CancellationToken cancellationToken = default)
        {
            var tmpFilepath = Path.GetTempFileName();
            {
                using var zip = ZipFile.OpenRead(savePath);

                var gamestateEntry = zip.Entries[0];// ("gamestate");
                using var gamestateStream = gamestateEntry.Open();
                using var tmpStream = File.OpenWrite(tmpFilepath);
                await gamestateStream.CopyToAsync(tmpStream, cancellationToken);
            }

            var json = await ParseFileAsync(CK2JsonConverterPath, tmpFilepath);
            return (json, GetGameDescription(json));
            //var (json, jsonPath) = await ConvertCK2JsonAsync(tmpFilepath, cancellationToken);
        }

        static string GetGameDescription(JsonDocument doc)
            => $"[{doc.RootElement.GetProperty("date").GetString()}] {doc.RootElement.GetProperty("player_name").GetString()}";

        private static Task<JsonDocument> ParseFileAsync(string jsonConverterPath, string filepath, string arguments = null)
        {
            using var proc = Process.Start(new ProcessStartInfo(jsonConverterPath, $"\"{filepath}\" {arguments}")
            {
                RedirectStandardOutput = true
            });

            if (proc == null)
                throw new InvalidOperationException("Couldn't start ck2json parser process");

            return JsonDocument.ParseAsync(proc.StandardOutput.BaseStream);
        }

    }
}
