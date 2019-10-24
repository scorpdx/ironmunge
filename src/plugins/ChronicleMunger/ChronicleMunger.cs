using Chronicler;
using ironmunge.Plugins;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChronicleMunger
{
    public class ChronicleMunger : IMunger
    {
        public string Name => nameof(ChronicleMunger);
        public string Description => "Parses and displays the Chronicle";

        public ValueTask<JsonDocument> MungeAsync(JsonDocument saveJson, IProgress<string> progress = null)
        {
            var chronicleCollection = ChronicleCollection.Parse(saveJson);
            var mostRecentChapter = (from chronicle in chronicleCollection.Chronicles
                                     from chapter in chronicle.Chapters
                                     where chapter.Entries.Any()
                                     select chapter).LastOrDefault();

            // on very first save, we will have a chroniclecollection but no entries
            if (mostRecentChapter != null)
            {
                foreach (var entry in mostRecentChapter.Entries.Select(entry => entry.Text).Reverse())
                {
                    progress.Report(entry);
                }
            }

            return default;
        }
    }
}
