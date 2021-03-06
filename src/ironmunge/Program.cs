﻿using CommandLine;
using Ironmunge.Common;
using Ironmunge.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ironmunge
{
    class IronmungeOptions : Options
    {
        [Option('n', "notifications", HelpText = "Play notification sounds while saving history", Default = true)]
        public bool Notifications { get; set; }

        [Option('r', "remote", HelpText = "Remote server to push updates")]
        public string? Remote { get; set; }
    }

    class Program
    {
        const string IronmungeMutexName = "ironmunge";

        const string PluginPathsFilename = "plugins.txt";

        static string DefaultSaveDir => CK2Settings.SaveGameLocation;

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
                        Console.WriteLine("Press ENTER to exit.");
                        Console.ReadLine();
                        return;
                    }

#pragma warning disable CS8604 // Possible null reference argument.
                    using var cm = new SaveMonitoring(o.GitLocation ?? Options.DefaultGitPath,
#pragma warning restore CS8604 // Possible null reference argument.
                                                         o.SaveGameLocation ?? DefaultSaveDir,
                                                         o.SaveHistoryLocation ?? DefaultSaveDir,
                                                         o.Remote,
                                                         LoadPlugins())
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
                    Console.WriteLine("Press ENTER to exit.");
                    Console.ReadLine();
                });
        }

        static IEnumerable<IMunger> LoadPlugins()
        {
            IEnumerable<string> pluginPaths;
            try
            {
                pluginPaths = File.ReadLines(PluginPathsFilename);
            } catch(FileNotFoundException)
            { //intended
                yield break;
            }

            var mungers = pluginPaths
                .Select(LoadPlugin)
                .SelectMany(CreateMungers);

            foreach (var munger in mungers)
            {
                Console.WriteLine($"Loaded munger {munger.Name}");
                yield return munger;
            }
        }

        static Assembly LoadPlugin(string relativePath)
        {
            string pluginLocation = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));

            Console.WriteLine($"Loading mungers from: {pluginLocation}");
            PluginLoadContext loadContext = new PluginLoadContext(pluginLocation);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
        }

        //throw new ApplicationException($"Can't find any type which implements ICommand in {assembly} from {assembly.Location}.\n")
        static IEnumerable<IMunger> CreateMungers(Assembly assembly)
            => assembly.GetTypes()
                .Where(type => typeof(IMunger).IsAssignableFrom(type))
                .Select(Activator.CreateInstance)
                .Cast<IMunger>();
    }
}
