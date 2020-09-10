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

            int waitMs = 50;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var fsIn = File.OpenRead(sourcePath);
                    using var fsOut = File.Create(destinationPath);

                    await fsIn.CopyToAsync(fsOut, token);
                    break;
                }
                catch (IOException)
                {
                    //exponential backoff: 0 -> 1 -> 2 -> 4 -> 8 ...
                    waitMs *= 2;
                }

                var wait = TimeSpan.FromMilliseconds(waitMs + rnd.Next(500));
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
