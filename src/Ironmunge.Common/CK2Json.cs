using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ironmunge.Common
{
    public static class CK2Json
    {
        private static string? CK2JsonExecutable => Options.DefaultJsonConverterPath;

        public static Task<JsonDocument> ParseFileAsync(string path)
        {
            var ck2parser = CK2JsonExecutable;
            if (string.IsNullOrEmpty(ck2parser))
                throw new InvalidOperationException("No CK2 parser was provided");

            using var proc = Process.Start(new ProcessStartInfo(ck2parser, $"\"{path}\"")
            {
                RedirectStandardOutput = true
            });

            if (proc == null)
                throw new InvalidOperationException("Could not start CK2 parser process");

            return JsonDocument.ParseAsync(proc.StandardOutput.BaseStream);
        }
    }
}
