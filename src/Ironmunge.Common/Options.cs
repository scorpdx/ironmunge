using CommandLine;
using System;
using System.IO;
using System.Linq;

namespace Ironmunge.Common
{
    public class Options
    {
        public static string DefaultGitPath
           => Directory.EnumerateFiles("./Resources/git/cmd/", "git*", SearchOption.TopDirectoryOnly).SingleOrDefault()
            ?? throw new InvalidOperationException("Git was not found");

        [Option('g', "game", HelpText = "Name of the game to monitor", Required = true)]
        public string Game { get; set; }

        [Option('s', "save-games", HelpText = "Path of the save game directory", Required = true)]
        public string SaveGameLocation { get; set; }

        [Option('h', "save-histories", HelpText = "Path of the ironmunge save history directory")]
        public string? SaveHistoryLocation { get; set; }

        [Option("git", HelpText = "Path of the git executable")]
        public string? GitLocation { get; set; }
    }

}
