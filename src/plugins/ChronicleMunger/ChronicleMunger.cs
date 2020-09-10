using Chronicler;
using Ironmunge.Plugins;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Humanizer;

namespace ChronicleMunger
{
    public class ChronicleMunger : IMunger
    {
        public string Name => nameof(ChronicleMunger);
        public string Description => "Parses and displays the Chronicle";

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async ValueTask<string> MungeAsync(string historyDir, (JsonDocument ck2json, JsonDocument metaJson) save, IProgress<string> progress = null)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var chronicleCollection = ChronicleCollection.Parse(save.ck2json);
            progress.Report($"Parsed {"chronicles".ToQuantity(chronicleCollection.Chronicles.Count)}, "
                + $"containing {"chapters".ToQuantity(chronicleCollection.Chronicles.SelectMany(c => c.Chapters).Count())}, "
                + $"and {"entries".ToQuantity(chronicleCollection.Chronicles.SelectMany(c => c.Chapters).SelectMany(c => c.Entries).Count())}");

            var mostRecentChapter = (from chronicle in chronicleCollection.Chronicles
                                     from chapter in chronicle.Chapters
                                     where chapter.Entries.Any()
                                     select chapter).LastOrDefault();
            progress.Report($"Most recent chapter covers year {mostRecentChapter.Year}");

            // on very first save, we will have a chroniclecollection but no entries
            if (mostRecentChapter != null)
            {
                var sb = new StringBuilder();
                foreach (var entry in mostRecentChapter.Entries.Select(entry => entry.Text).Reverse())
                {
                    sb.AppendLine().AppendLine().Append(entry);
                }
                return sb.ToString();
            }

            return null;
        }
    }
}
