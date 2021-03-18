using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ironmunge.Plugins
{
    public interface IGame : IDisposable
    {
        string Name { get; }

        IEnumerable<string> Filters { get; }

        ValueTask<(JsonDocument saveDocument, string commitMessage)> AddSaveAsync(string savePath, ILogger logger, CancellationToken cancellationToken = default);
    }
}
