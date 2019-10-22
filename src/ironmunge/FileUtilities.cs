using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ironmunge
{
    static class FileUtilities
    {
        public static async Task CopyWithRetryAsync(string sourcePath, string destinationPath, TimeSpan maximumWait, CancellationToken token = default, IProgress<TimeSpan>? progress = default)
        {
            var rnd = new Random();

            int waitSeconds = 0;
            TimeSpan GetWait() => TimeSpan.FromSeconds(waitSeconds) + TimeSpan.FromMilliseconds(rnd.Next(500));

            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var fsIn = File.OpenRead(sourcePath))
                    using (var fsOut = File.Create(destinationPath))
                    {
                        await fsIn.CopyToAsync(fsOut);
                        return;
                    }
                }
                catch (IOException)
                {
                    //exponential backoff: 0 -> 1 -> 2 -> 4 -> 8 ...
                    waitSeconds = waitSeconds == 0 ? 1 : waitSeconds * 2;
                }

                var wait = GetWait();
                //if wait is higher than maximumWait, wait for maximumWait
                wait = wait > maximumWait ? maximumWait : wait;

                //report how long we're waiting
                progress?.Report(wait);

                //wait and retry
                await Task.Delay(wait, token);
            }

            token.ThrowIfCancellationRequested();
        }
    }
}
