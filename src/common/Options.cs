using CommandLine;
using System;

namespace ironmunge.Common
{
    public class Options
    {
        [Option('s', "saveGames", HelpText = "Path of the Crusader Kings save game directory")]
        public string SaveGameLocation { get; set; }

        [Option('h', "saveHistories", HelpText = "Path of the ironmunge save history directory")]
        public string SaveHistoryLocation { get; set; }

        [Option('g', "git", HelpText = "Path of the git executable")]
        public string GitLocation { get; set; }
    }

}
