using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ironmunge
{
    public static class HelperUtilities
    {
        public static async Task RetryWithExponentialBackoff(Func<ValueTask<bool>> func, TimeSpan maximumWait, CancellationToken token = default, IProgress<TimeSpan>? progress = default)
        {
            var rnd = new Random();

            int waitMs = 50;
            TimeSpan GetWait() => TimeSpan.FromMilliseconds(waitMs) + TimeSpan.FromMilliseconds(rnd.Next(500));

            while (!token.IsCancellationRequested && !(await func()))
            {
                //exponential backoff: 0 -> 1 -> 2 -> 4 -> 8 ...
                waitMs *= 2;

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
