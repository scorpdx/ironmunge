using CommandLine;
using System;
using System.IO;
using System.Linq;

namespace ironmunge
{
    class Options
    {
        [Option('s', "saveGames", HelpText = "Path of the Crusader Kings save game directory")]
        public string SaveGameLocation { get; set; }

        [Option('h', "saveHistories", HelpText = "Path of the ironmunge save history directory")]
        public string SaveHistoryLocation { get; set; }

        [Option('n', "notifications", HelpText = "Play notification sounds while saving history", Default = true)]
        public bool Notifications { get; set; }

        [Option('g', "git", HelpText = "Path of the git executable")]
        public string GitLocation { get; set; }
    }

    class Program
    {
        static string DefaultGitPath
        {
            get
            {
                try
                {
                    var defaultGitPath = Directory.EnumerateFiles("./Resources/git/cmd/", "git*", SearchOption.TopDirectoryOnly).SingleOrDefault();
                    return defaultGitPath;
                }
                catch (IOException)
                {
                    throw new InvalidOperationException("git could not be found in resources folder");
                }
            }
        }

        static string DefaultSaveDir => LibCK2.SaveGame.SaveGameLocation;

        static void Main(string[] args)
        {
            var options = CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    using (var cm = new ChangeMonitoring(o.GitLocation ?? DefaultGitPath,
                                                         o.SaveGameLocation ?? DefaultSaveDir,
                                                         o.SaveHistoryLocation ?? DefaultSaveDir))
                    {
                        cm.PlayNotifications = o.Notifications;
                        Console.WriteLine("ironmunge is now running.");
                        Console.WriteLine("Press ENTER to exit.");
                        Console.ReadLine();
                    }
                })
                .WithNotParsed(o =>
                {
                    foreach (var error in o)
                    {
                        Console.WriteLine(error);
                    }

                    Console.WriteLine("Please correct the options and try again.");
                    Console.WriteLine("Press ENTER to exit.");
                    Console.ReadLine();
                });
        }
    }
}
