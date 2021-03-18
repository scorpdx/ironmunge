using CommandLine;
using Ironmunge.Common;
using System;
using System.Linq;

namespace ironmunge
{
    class IronmungeOptions : Options
    {
        [Option('n', "notifications", HelpText = "Play notification sounds while saving history", Default = true)]
        public bool Notifications { get; set; }

        [Option('r', "remote", HelpText = "Remote server to push updates")]
        public string Remote { get; set; }
    }

    class Program
    {
        const string IronmungeMutexName = nameof(ironmunge);

        static void Main(string[] args)
        {
            var options = CommandLine.Parser.Default.ParseArguments<IronmungeOptions>(args)
                .WithParsed(o =>
                {
                    using var mutex = new System.Threading.Mutex(true, IronmungeMutexName, out bool createdMutex);
                    if (!createdMutex)
                    {
                        Console.WriteLine("ironmunge is already running.");
                        Console.WriteLine("Please close any running instances and try again.");
                        WriteExitMessage();
                        return;
                    }

                    var games = PluginManager.LoadPlugins();

                    var selectedGame = games.SingleOrDefault(g => g.Name.Equals(o.Game, StringComparison.InvariantCultureIgnoreCase));
                    if (selectedGame == null)
                    {
                        Console.WriteLine("Game \"{0}\" was not found in the list of supported games.", o.Game);
                        WriteExitMessage();
                        return;
                    }

                    using var cm = new SaveMonitoring(o.GitLocation ?? Options.DefaultGitPath, o.SaveGameLocation,
                                                      selectedGame, o.SaveHistoryLocation ?? o.SaveGameLocation,
                                                      o.Remote)
                    {
                        PlayNotifications = o.Notifications
                    };

                    Console.WriteLine("ironmunge is now running.");
                    Console.WriteLine("Press ESCAPE to exit.");

                    ConsoleKeyInfo key;
                    while ((key = Console.ReadKey()).Key != ConsoleKey.Escape)
                    {
                        //wait for key to exit
                    }
                })
                .WithNotParsed(o =>
                {
                    foreach (var error in o)
                    {
                        Console.WriteLine(error);
                    }

                    Console.WriteLine("Please correct the options and try again.");
                    WriteExitMessage();
                });

            static void WriteExitMessage()
            {
                Console.WriteLine("Press any key to exit.");
                Console.ReadLine();
            }
        }
    }
}
