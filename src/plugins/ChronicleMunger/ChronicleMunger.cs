using Chronicler;
using Humanizer;
using Ironmunge.Plugins;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChronicleMunger
{
    public class ChronicleMunger : IMunger
    {
        public string Name => nameof(ChronicleMunger);
        public string Description => "Parses and displays the Chronicle";

        public ValueTask<string?> MungeAsync(string historyDir, (JsonDocument ck2json, JsonDocument metaJson) save, IProgress<string>? progress = null)
        {
            var chronicleCollection = ChronicleCollection.Parse(save.ck2json);
            progress?.Report($"Parsed {"chronicles".ToQuantity(chronicleCollection.Chronicles.Count)}, "
                + $"containing {"chapters".ToQuantity(chronicleCollection.Chronicles.SelectMany(c => c.Chapters).Count())}, "
                + $"and {"entries".ToQuantity(chronicleCollection.Chronicles.SelectMany(c => c.Chapters).SelectMany(c => c.Entries).Count())}");

            var mostRecentChapter = (from chronicle in chronicleCollection.Chronicles
                                     from chapter in chronicle.Chapters
                                     where chapter.Entries.Any()
                                     select chapter).LastOrDefault();

            // on very first save, we will have a chroniclecollection but no entries
            if (mostRecentChapter == null)
            {
                return ValueTask.FromResult<string?>(null);
            }

            progress?.Report($"Most recent chapter covers year {mostRecentChapter.Year}");

            var sb = new StringBuilder();
            foreach (var entry in mostRecentChapter.Entries.Select(entry => entry.Text).Reverse())
            {
                sb.AppendLine().AppendLine().Append(entry);
            }
            return ValueTask.FromResult<string?>(sb.ToString());
        }
    }
}
