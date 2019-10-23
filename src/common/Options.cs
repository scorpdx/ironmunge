using CommandLine;
using System.IO;
using System.Linq;

namespace ironmunge.Common
{
    public class Options
    {
        public static string DefaultGitPath
           => Directory.EnumerateFiles("./Resources/git/cmd/", "git*", SearchOption.TopDirectoryOnly).SingleOrDefault();

        public static string DefaultJsonConverterPath
            => Directory.EnumerateFiles("./Resources/", "ck2json*", SearchOption.TopDirectoryOnly).SingleOrDefault();

        [Option('s', "saveGames", HelpText = "Path of the Crusader Kings II save game directory")]
        public string? SaveGameLocation { get; set; }

        [Option('h', "saveHistories", HelpText = "Path of the ironmunge save history directory")]
        public string? SaveHistoryLocation { get; set; }

        [Option('g', "git", HelpText = "Path of the git executable")]
        public string? GitLocation { get; set; }
    }

}
