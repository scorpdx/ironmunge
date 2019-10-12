using CommandLine;
using ironmunge.Common;
using ironmunge.Plugins;
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
        public string Remote { get; set; }
    }

    class Program
    {
        const string IronmungeMutexName = "ironmunge";

        static string DefaultSaveDir => LibCK2.SaveGame.SaveGameLocation;

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

                    LoadPlugins();

                    using (var cm = new ChangeMonitoring(o.GitLocation ?? GitHelpers.DefaultGitPath,
                                                         o.SaveGameLocation ?? DefaultSaveDir,
                                                         o.SaveHistoryLocation ?? DefaultSaveDir,
                                                         o.Remote))
                    {
                        cm.PlayNotifications = o.Notifications;
                        Console.WriteLine("ironmunge is now running.");
                        Console.WriteLine("Press ESCAPE to exit.");

                        ConsoleKeyInfo key;
                        while ((key = Console.ReadKey()).Key != ConsoleKey.Escape)
                        {
                            //wait for key to exit
                        }
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

        static void LoadPlugins()
        {
            string[] pluginPaths = new string[]
            {
                @"..\src\HelloPlugin\bin\Debug\netcoreapp3.0\HelloPlugin.dll",
                // Paths to plugins to load.
            };

            var mungers = pluginPaths
                .Select(LoadPlugin)
                .SelectMany(CreateCommands);

            foreach (var munger in mungers)
            {
                Console.WriteLine($"{munger.Name}\t - {munger.Description}");
            }


        }

        static Assembly LoadPlugin(string relativePath)
        {
            // Navigate up to the solution root
            string root = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(
                    Path.GetDirectoryName(
                        Path.GetDirectoryName(
                            Path.GetDirectoryName(
                                Path.GetDirectoryName(typeof(Program).Assembly.Location)))))));

            string pluginLocation = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
            Console.WriteLine($"Loading commands from: {pluginLocation}");
            PluginLoadContext loadContext = new PluginLoadContext(pluginLocation);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
        }

        static IEnumerable<IMunger> CreateCommands(Assembly assembly)
        {
            int count = 0;

            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(IMunger).IsAssignableFrom(type))
                {
                    IMunger result = Activator.CreateInstance(type) as IMunger;
                    if (result != null)
                    {
                        count++;
                        yield return result;
                    }
                }
            }

            if (count == 0)
            {
                string availableTypes = string.Join(",", assembly.GetTypes().Select(t => t.FullName));
                throw new ApplicationException(
                    $"Can't find any type which implements ICommand in {assembly} from {assembly.Location}.\n" +
                    $"Available types: {availableTypes}");
            }
        }
    }
}
