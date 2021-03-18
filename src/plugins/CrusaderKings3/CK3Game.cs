using Ironmunge.Plugins;
using LibCK3.Parsing;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CrusaderKings3
{
    public sealed class CK3Game : IGame
    {
        public string Name { get; } = "Crusader Kings 3";

        public IEnumerable<string> Filters { get; } = new string[] { "*.ck3" };

        public async ValueTask<(JsonDocument saveDocument, string commitMessage)> AddSaveAsync(string savePath, ILogger logger, CancellationToken cancellationToken = default)
        {
            var json = await ConvertCK3ToJsonAsync(savePath);

            var metadata = json.RootElement.GetProperty("meta_data");
            string playerName = metadata.GetProperty("meta_player_name").GetString();
            var ironmanManager = json.RootElement.GetProperty("ironman_manager");
            //string saveGame = ironmanManager.GetProperty("save_game").GetString();
            string date = ironmanManager.GetProperty("date").GetString();

            return (json, commitMessage: $"[{date}] {playerName}");
        }

        private static async Task<JsonDocument> ConvertCK3ToJsonAsync(string filepath, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            {
                await using var writer = new Utf8JsonWriter(ms);

                var ck3bin = new CK3Bin(filepath, writer);
                await ck3bin.ParseAsync(cancellationToken);
            }

            ms.Seek(0, SeekOrigin.Begin);
            return await JsonDocument.ParseAsync(ms, cancellationToken: cancellationToken);
        }

        public void Dispose()
        {
            ;
        }
    }
}
