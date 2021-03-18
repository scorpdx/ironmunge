using CommandLine;
using System.IO;
using System.Linq;

namespace Ironmunge.Common
{
    public class Options
    {
        public static string? DefaultGitPath
           => Directory.EnumerateFiles("./Resources/git/cmd/", "git*", SearchOption.TopDirectoryOnly).SingleOrDefault();

        [Option('g', "game", HelpText = "Name of the game to monitor")]
        public string Game { get; set; }

        [Option('s', "saveGames", HelpText = "Path of the save game directory")]
        public string SaveGameLocation { get; set; }

        [Option('h', "saveHistories", HelpText = "Path of the ironmunge save history directory")]
        public string SaveHistoryLocation { get; set; }

        [Option('g', "git", HelpText = "Path of the git executable")]
        public string GitLocation { get; set; }
    }

}
