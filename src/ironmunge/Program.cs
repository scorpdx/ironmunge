using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace ironmunge
{
    class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostContext, builder) =>
                {

                })
                .ConfigureServices((hostContext, services) =>
                {
                    var options = EnsureCommandLine(args);
                    services.AddSingleton<IOptions<IronmungeOptions>>(new OptionsWrapper<IronmungeOptions>(options));

                    services.AddSingleton<PluginProvider>();
                    {
                        using var sp = services.BuildServiceProvider();
                        var provider = sp.GetRequiredService<PluginProvider>();
                        var pluginServices = provider.AddPlugins();
                        services.Add(pluginServices);
                    }

                    services.AddHostedService<IronmungeWorker>();
                });

        static IronmungeOptions EnsureCommandLine(string[] args)
        {
            var options = CommandLine.Parser.Default.ParseArguments<IronmungeOptions>(args)
                .WithParsed(o =>
                {
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
                Environment.Exit(1);
            }

            return options.Value;
        }
    }
}
