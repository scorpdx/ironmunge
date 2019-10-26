using ironmunge.Plugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ironmunge
{
    public class SaveMonitoring : IDisposable
    {
        private const string FailureSound = "Resources/failure.wav";
        private const string PendingSound = "Resources/pending.wav";
        private const string SuccessSound = "Resources/success.wav";

        private static bool NotificationSoundsPresent()
            => File.Exists(FailureSound) && File.Exists(PendingSound) && File.Exists(SuccessSound);

        private Task NotificationAsync(string path)
            => PlayNotifications ? SoundUtilities.PlayAsync(path) : Task.CompletedTask;

        private readonly SaveHistory _history;
        private readonly FileSystemWatcher _watcher;

        private readonly ConcurrentDictionary<string, bool> _pendingSaves = new ConcurrentDictionary<string, bool>();

        public bool PlayNotifications { get; set; } = true;

        public TimeSpan MaximumWait { get; set; } = TimeSpan.FromSeconds(30);

        public SaveMonitoring(string gitPath, string savePath, string historyPath, string? remote = null, IEnumerable<IMunger>? plugins = null)
        {
            if (string.IsNullOrEmpty(gitPath))
                throw new ArgumentNullException(nameof(gitPath), "git was not found");
            if (string.IsNullOrEmpty(savePath))
                throw new ArgumentNullException(nameof(savePath));
            if (string.IsNullOrEmpty(historyPath))
                throw new ArgumentNullException(nameof(historyPath));
            if (PlayNotifications && !NotificationSoundsPresent())
                throw new InvalidOperationException("Notification sound files are missing and notifications are enabled");

            _history = new SaveHistory(historyPath, gitPath, remote, plugins);

            _watcher = new FileSystemWatcher(savePath)
            {
                Filter = "*.ck2"
            };

            _watcher.Changed += CopyAndSave;
            _watcher.EnableRaisingEvents = true;
        }

        async void CopyAndSave(object sender, FileSystemEventArgs e)
        {
            bool ownsSave = false;
            string? copyPath = null;
            Task pendingSound = Task.CompletedTask;

            using var cts = new CancellationTokenSource();
            try
            {
                ownsSave = _pendingSaves.TryAdd(e.FullPath, default);
                if (!ownsSave)
                    return;

                copyPath = await CopySaveAsync(e.FullPath);
                if (string.IsNullOrEmpty(copyPath))
                    return;

                pendingSound = Task.Run(() => PlayPendingNotificationJingle(cts.Token), cts.Token);
                await SaveAsync(copyPath, e.Name);

                cts.Cancel();
                await pendingSound;

                await NotificationAsync(SuccessSound);
            }
            catch (Exception ex)
            {
                cts.Cancel();
                await pendingSound;

                await NotificationAsync(FailureSound);
                Console.Error.WriteLine(ex);
            }
            finally
            {
                cts.Dispose();
                pendingSound?.Dispose();

                if (ownsSave)
                {
                    _pendingSaves.Remove(e.FullPath, out _);
                }

                if (!string.IsNullOrEmpty(copyPath) && File.Exists(copyPath))
                {
                    File.Delete(copyPath);
                }
            }
        }

        async Task PlayPendingNotificationJingle(CancellationToken token)
        {
            try
            {
                await HelperUtilities.RetryWithExponentialBackoff(async () =>
                {
                    await NotificationAsync(PendingSound);
                    return false;
                }, TimeSpan.FromSeconds(5), token);
            }
            catch (OperationCanceledException)
            {
                //expected
            }
        }

        async Task<string?> CopySaveAsync(string filepath)
        {
            static async ValueTask<bool> CopyToTempAsync(string sourcePath, string destinationPath, CancellationToken token = default)
            {
                try
                {
                    using var fsIn = File.OpenRead(sourcePath);
                    using var fsOut = File.Create(destinationPath);
                    await fsIn.CopyToAsync(fsOut, token);
                    token.ThrowIfCancellationRequested();
                    return true;
                }
                catch (IOException)
                {
                    return false;
                }
            }

            try
            {
                var timeout = MaximumWait * 3;
                string tmpPath = Path.GetTempFileName();

                using var cancellationSource = new CancellationTokenSource(timeout);
                await HelperUtilities.RetryWithExponentialBackoff(async () => await CopyToTempAsync(filepath, tmpPath, cancellationSource.Token),
                                                                  MaximumWait,
                                                                  cancellationSource.Token);

                return tmpPath;
            }
            catch (TaskCanceledException)
            {
                await NotificationAsync(FailureSound);
            }

            return null;
        }

        private async ValueTask SaveAsync(string path, string name)
        {
            var gameDescription = await _history.AddSaveAsync(path, name);
            Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] Saved {name} to history: {gameDescription}");
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _watcher.Dispose();
                }

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
