using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ironmunge.Common
{
    public static class CKJson
    {
        private static Task<JsonDocument> ParseFileAsync(string jsonConverterPath, string filepath)
        {
            using var proc = Process.Start(new ProcessStartInfo(jsonConverterPath, $"\"{filepath}\"")
            {
                RedirectStandardOutput = true
            });

            if (proc == null)
                throw new InvalidOperationException("Could not start CK2 parser process");

            return JsonDocument.ParseAsync(proc.StandardOutput.BaseStream);
        }

        public static Task<JsonDocument> ParseCK2FileAsync(string filepath)
            => ParseFileAsync(Options.CK2JsonConverterPath ?? throw new InvalidOperationException("No CK2 JSON parser was found"), filepath);
        public static Task<JsonDocument> ParseCK3FileAsync(string filepath)
            => ParseFileAsync(Options.CK3JsonConverterPath ?? throw new InvalidOperationException("No CK3 JSON parser was found"), filepath);
    }
}
