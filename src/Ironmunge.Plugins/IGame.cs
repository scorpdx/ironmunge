using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ironmunge.Plugins
{
    public interface IGame : IDisposable
    {
        string Name { get; }

        ValueTask Monitor(string savePath, Func<string> onChange);
    }
}
