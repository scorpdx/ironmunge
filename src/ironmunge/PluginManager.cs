using Ironmunge.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ironmunge
{
    internal static class PluginManager
    {
        const string PluginPathsFilename = "plugins.txt";

        public static IEnumerable<IGame> LoadPlugins()
        {
            IEnumerable<string> pluginPaths;
            try
            {
                pluginPaths = File.ReadLines(PluginPathsFilename);
            }
            catch (FileNotFoundException)
            { //intended
                yield break;
            }

            var games = pluginPaths
                .Select(LoadPlugin)
                .SelectMany(CreatePlugins<IGame>);

            foreach (var game in games)
            {
                Console.WriteLine($"Loaded game {game.Name}");
                yield return game;
            }
        }

        private static Assembly LoadPlugin(string relativePath)
        {
            string pluginLocation = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));

            Console.WriteLine($"Loading plugins from {pluginLocation}");
            PluginLoadContext loadContext = new(pluginLocation);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
        }

        private static IEnumerable<T> CreatePlugins<T>(Assembly assembly)
            => assembly.GetTypes()
                .Where(type => typeof(T).IsAssignableFrom(type))
                .Select(Activator.CreateInstance)
                .Cast<T>();
                //.Select(p => p ?? throw new InvalidOperationException($"Plugin was null in assembly {assembly}"));

    }
}
