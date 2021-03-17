using Ironmunge.Plugins;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KingdomComeDeliverance
{
    public class KcdGame : IGame
    {
        public static string DefaultSaveFolder => Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games", "kingdomcome", "saves");

        public string Name { get; } = "Kingdom Come: Deliverance";

        public ValueTask Monitor(string savePath, Func<string> onChange)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
