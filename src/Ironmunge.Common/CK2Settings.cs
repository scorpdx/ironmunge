using System;
using System.IO;
using System.Text;

namespace Ironmunge.Common
{
    public static class CK2Settings
    {
        public static Encoding SaveGameEncoding { get; }
            = CodePagesEncodingProvider.Instance.GetEncoding(1252) ?? throw new InvalidOperationException("Couldn't find CK2txt code page (western 1252)");

        public static string SaveGameLocation
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Paradox Interactive", "Crusader Kings II", "save games");
    }
}
