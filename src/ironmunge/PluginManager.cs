using Ironmunge.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ironmunge
{
    class PluginManager
    {
        const string PluginPathsFilename = "plugins.txt";

        static IEnumerable<IMunger> LoadPlugins()
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
                .Cast<IMunger>()
                .Select(m => m ?? throw new InvalidOperationException($"Munger was null in assembly {assembly}"));

    }
}
