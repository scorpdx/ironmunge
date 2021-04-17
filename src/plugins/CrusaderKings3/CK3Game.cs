using Ironmunge.Plugins;
using LibCK3.Parsing;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CrusaderKings3
{
    public sealed class CK3Game : IGame
    {
        private readonly ILogger<CK3Game> _logger;
        public CK3Game(ILogger<CK3Game> logger)
        {
            _logger = logger;
        }

        public string Name { get; } = "Crusader Kings 3";

        public IEnumerable<string> Filters { get; } = new string[] { "*.ck3" };

        private long _currentCharacterId;

        public async ValueTask<(JsonDocument saveDocument, string commitMessage)> AddSaveAsync(string savePath, CancellationToken cancellationToken = default)
        {
            var json = await ConvertCK3ToJsonAsync(savePath, cancellationToken);

            var metadata = json.RootElement.GetProperty("meta_data");
            var gamestate = json.RootElement.GetProperty("gamestate");

            var commitMessage = GetCommitMessage(metadata, gamestate);

            return (json, commitMessage);
        }

        private string GetCommitMessage(JsonElement meta, JsonElement gamestate)
        {
            var sb = new StringBuilder();

            string playerName = meta.GetProperty("meta_player_name").GetString();
            var ironmanManager = gamestate.GetProperty("ironman_manager");
            //string saveGame = ironmanManager.GetProperty("save_game").GetString();
            string date = ironmanManager.GetProperty("date").GetString();

            sb.Append($"[{date}] {playerName}");

            var characterIdElement = meta.GetProperty("meta_main_portrait").GetProperty("id");
            var characterId = characterIdElement.GetInt64();
            var lastCharacterId = Interlocked.Exchange(ref _currentCharacterId, characterId);
            if (lastCharacterId != characterId && lastCharacterId > 0)
            {
                //succession
                _logger.LogInformation("{0} succeeded {1}", GetCharacterName(gamestate, characterId), GetCharacterName(gamestate, lastCharacterId));

                if(TryFindCharacter(gamestate, lastCharacterId, out var lastCharacter))
                {
                    InvestigateDeath(gamestate, lastCharacter.GetProperty("dead_data"));
                }
            }

            return sb.ToString();
        }

        private void InvestigateDeath(JsonElement gamestate, JsonElement deadData)
        {
            var reason = deadData.GetProperty("reason");
            switch (reason.GetString())
            {
                case "death_mysterious":
                    var killer = deadData.GetProperty("killer").GetInt64();
                    _logger.LogInformation("Died under mysterious circumstances by killer {0}", GetCharacterName(gamestate, killer));
                    break;
                case "death_drinking_passive":
                    _logger.LogInformation("Drank themself to death");
                    break;
                default:
                    ;
                    break;
            }
        }

        private bool TryFindCharacter(JsonElement gamestate, long characterId, out JsonElement character)
        {
            var characterPropertyName = $"{characterId}";

            var living = gamestate.GetProperty("living");
            var deadUnprunable = gamestate.GetProperty("dead_unprunable");
            var deadPrunable = gamestate.GetProperty("characters").GetProperty("dead_prunable");
            if (!living.TryGetProperty(characterPropertyName, out character)
             && !deadUnprunable.TryGetProperty(characterPropertyName, out character)
             && !deadPrunable.TryGetProperty(characterPropertyName, out character))
            {
                return false;
            }

            return true;
        }

        private string GetCharacterName(JsonElement gamestate, long characterId)
        {
            if(!TryFindCharacter(gamestate, characterId, out var character))
            {
                return null;
            }

            return character.GetProperty("first_name").GetString();
        }

        //Must parse localized dynasty names for this

        //private string GetCharacterFullName(JsonElement gamestate, int characterId)
        //{
        //    //get first name and house id
        //    var characters = gamestate.GetProperty("characters");
        //    var character = characters.GetProperty($"{characterId}");
        //    var first_name = character.GetProperty("first_name").GetString();
        //    var dynastyHouseId = character.GetProperty("dynasty_house").GetInt32();

        //    var dynasties = gamestate.GetProperty("dynasties");
        //    var dynastyHouse = dynasties.GetProperty($"{dynastyHouseId}");
        //    var dynastyHouseName = d
        //}

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
