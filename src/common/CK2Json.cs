using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace ironmunge.Common
{
    public static class CK2Json
    {
        private static string CK2JsonExecutable => Options.DefaultJsonConverterPath;

        public static Task<JsonDocument> ParseFileAsync(string path)
        {
            using var proc = Process.Start(new ProcessStartInfo(CK2JsonExecutable, $"\"{path}\"")
            {
                RedirectStandardOutput = true
            });

            return JsonDocument.ParseAsync(proc.StandardOutput.BaseStream);
        }
    }
}
