using Ironmunge.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ironmunge
{
    public class IronmungeWorker : BackgroundService
    {
        const string IronmungeMutexName = nameof(ironmunge);

        private readonly IServiceProvider _services;
        private readonly ILogger<IronmungeWorker> _logger;
        private readonly IOptions<IronmungeOptions> _options;

        //private readonly 

        public IronmungeWorker(IServiceProvider services, ILogger<IronmungeWorker> logger, IOptions<IronmungeOptions> options)
        {
            _services = services;
            _logger = logger;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var mutex = new Mutex(true, IronmungeMutexName, out bool createdMutex);
            if (!createdMutex)
            {
                _logger.LogError("ironmunge is already running. Please close any running instances and try again.");
                return;
            }

            var o = _options.Value;

            //using (var scope = _services.CreateScope())
            {
                var selectedGame = _services.GetServices<IGame>().SingleOrDefault(g => g.Name.Equals(o.Game, StringComparison.InvariantCultureIgnoreCase));
                if (selectedGame == null)
                {
                    _logger.LogError("Game {game} was not found in the list of supported games.", o.Game);
                    return;
                }

                var sml = _services.GetRequiredService<ILogger<SaveMonitoring>>();
                using var cm = new SaveMonitoring(sml, o.GitLocation ?? Ironmunge.Common.Options.DefaultGitPath, o.SaveGameLocation,
                                                  selectedGame, o.SaveHistoryLocation ?? o.SaveGameLocation,
                                                  o.Remote)
                {
                    PlayNotifications = o.Notifications
                };

                _logger.LogInformation("ironmunge is now running.");
                await Task.Delay(-1, stoppingToken);
            }
        }
    }
}
