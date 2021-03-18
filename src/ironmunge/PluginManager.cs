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

        public static IEnumerable<IMunger> LoadPlugins()
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

            var mungers = pluginPaths
                .Select(LoadPlugin)
                .SelectMany(CreateMungers);

            foreach (var munger in mungers)
            {
                Console.WriteLine($"Loaded munger {munger.Name}");
                yield return munger;
            }
        }

        private static Assembly LoadPlugin(string relativePath)
        {
            string pluginLocation = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));

            Console.WriteLine($"Loading plugins from {pluginLocation}");
            PluginLoadContext loadContext = new(pluginLocation);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
        }

        private static IEnumerable<IMunger> CreateMungers(Assembly assembly)
            => assembly.GetTypes()
                .Where(type => typeof(IMunger).IsAssignableFrom(type))
                .Select(Activator.CreateInstance)
                .Cast<IMunger>()
                .Select(m => m ?? throw new InvalidOperationException($"Munger was null in assembly {assembly}"));

    }
}
