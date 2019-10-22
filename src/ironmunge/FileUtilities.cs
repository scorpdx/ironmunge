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

            TimeSpan expWait = TimeSpan.FromMilliseconds(100);
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
                    // reduced from exponential backoff because it's easier to hear nice ticking sounds :)
                    expWait *= 2;
                }

                var wait = expWait;
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
