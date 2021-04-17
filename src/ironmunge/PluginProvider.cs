using Ironmunge.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ironmunge
{
    public class PluginProvider
    {
        const string PluginPathsFilename = "plugins.txt";

        private readonly ILogger<PluginProvider> _logger;
        public PluginProvider(ILogger<PluginProvider> logger)
        {
            _logger = logger;
        }

        public IServiceCollection AddPlugins()
        {
            var services = new ServiceCollection();

            IEnumerable<string> pluginPaths;
            try
            {
                pluginPaths = File.ReadLines(PluginPathsFilename);
            }
            catch (FileNotFoundException)
            { //intended
                return services;
            }

            var gameTypes = pluginPaths
                .Select(LoadPlugin)
                .SelectMany(GetImplementationTypes<IGame>);

            foreach (var game in gameTypes)
            {
                _logger.LogInformation("Loaded game {gameName}", game.Name);
                services.AddScoped(typeof(IGame), game);
            }

            return services;
        }

        private Assembly LoadPlugin(string relativePath)
        {
            string pluginLocation = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));

            _logger.LogDebug($"Loading plugins from {pluginLocation}");
            PluginLoadContext loadContext = new(pluginLocation);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
        }

        private static IEnumerable<Type> GetImplementationTypes<T>(Assembly assembly)
            => assembly.GetTypes().Where(type => typeof(T).IsAssignableFrom(type));
    }
}
