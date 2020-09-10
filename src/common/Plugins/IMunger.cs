using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace ironmunge.Plugins
{
    public interface IMunger
    {
        string Name { get; }
       
        /// <summary>
        /// Performs any operation at the time of a new checkpoint
        /// </summary>
        /// <param name="historyDir">Location of the ironmunge history directory</param>
        /// <param name="save">Tuple containing parsed JSON of CK2 save contents</param>
        /// <param name="progress">Optional way to report progress messages for output</param>
        /// <returns>Text to append to checkpoint message, if any</returns>
        ValueTask<string?> MungeAsync(string historyDir, (JsonDocument ck2json, JsonDocument metaJson) save, IProgress<string>? progress = null);
    }
}
