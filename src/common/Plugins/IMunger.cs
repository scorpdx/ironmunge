using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace ironmunge.Plugins
{
    public interface IMunger
    {
        string Name { get; }

        ValueTask<JsonDocument?> MungeAsync(JsonDocument ck2json, IProgress<string>? progress = null);
    }
}
