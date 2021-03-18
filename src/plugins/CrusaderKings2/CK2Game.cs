using Ironmunge.Plugins;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CrusaderKings2
{
    public class CK2Game : IGame
    {
        public static Encoding SaveGameEncoding { get; }
            = CodePagesEncodingProvider.Instance.GetEncoding(1252) ?? throw new InvalidOperationException("Couldn't find western-1252 code page");
        internal static string? CK2JsonConverterPath
    => Directory.EnumerateFiles("./Resources/", "ck2json*", SearchOption.TopDirectoryOnly).SingleOrDefault();

        internal static string? CK3JsonConverterPath
            => Directory.EnumerateFiles("./Resources/", "ck3json*", SearchOption.TopDirectoryOnly).SingleOrDefault();


        public string Name => throw new NotImplementedException();

        public IEnumerable<string> Filters => throw new NotImplementedException();

        public ValueTask<(JsonDocument saveDocument, string commitMessage)> AddSaveAsync(string savePath, ILogger logger, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();

            using var zipStream = File.OpenRead(savePath);
            await UnzipSaveAsync(zipStream, outputDir);

            var (gameDescription, save, meta) = await ParseCK2SaveAsync(outputDir);
            gameDescription = await RunMungersAsync(outputDir, gameDescription, save, meta);

            await AddGitSaveAsync(gameDescription, outputDir);

            return gameDescription;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private static async ValueTask UnzipSaveAsync(Stream zipStream, string outputDir)
        {
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, true, CKSettings.SaveGameEncoding);
            foreach (var entry in zip.Entries)
            {
                var outputPath = Path.Combine(outputDir, entry.FullName);

                using var outputStream = File.Create(outputPath);
                using var entryStream = entry.Open();

                await entryStream.CopyToAsync(outputStream);
            }
        }


        //private async ValueTask<(string gameDescription, JsonDocument save, JsonDocument meta)> ParseCK2SaveAsync(string outputDir)
        //{
        //    static string GetGameDescription(JsonDocument doc)
        //     => $"[{doc.RootElement.GetProperty("date").GetString()}] {doc.RootElement.GetProperty("player_name").GetString()}";

        //    var metaName = Path.Combine(outputDir, "meta");
        //    var (metaJson, _) = await ConvertCK2JsonAsync(metaName);

        //    var saveName = Directory.EnumerateFiles(outputDir, "*.ck2", SearchOption.TopDirectoryOnly).Single();
        //    var (saveJson, _) = await ConvertCK2JsonAsync(saveName);

        //    var gameDescription = GetGameDescription(metaJson);
        //    return (gameDescription, saveJson, metaJson);
        //}

        //private static async ValueTask<(JsonDocument json, string jsonPath)> ConvertCK2JsonAsync(string filepath)
        //{
        //    var jsonPath = Path.ChangeExtension(filepath, ".json");
        //    await using var jsonStream = File.Create(jsonPath);

        //    await using var writer = new Utf8JsonWriter(jsonStream,
        //        new JsonWriterOptions
        //        {
        //            Indented = true
        //        });
        //    var json = await CKJson.ParseCK2FileAsync(filepath);
        //    json.WriteTo(writer);

        //    return (json, jsonPath);
        //}


    }
}
