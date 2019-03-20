﻿using CommandLine;
using ironmunge.Common;
using System;

namespace ironmunge
{
    class IronmungeOptions : Options
    {
        [Option('n', "notifications", HelpText = "Play notification sounds while saving history", Default = true)]
        public bool Notifications { get; set; }
    }

    class Program
    {
        static string DefaultSaveDir => LibCK2.SaveGame.SaveGameLocation;

        static void Main(string[] args)
        {
            var options = CommandLine.Parser.Default.ParseArguments<IronmungeOptions>(args)
                .WithParsed(o =>
                {
                    using (var cm = new ChangeMonitoring(o.GitLocation ?? GitHelpers.DefaultGitPath,
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
