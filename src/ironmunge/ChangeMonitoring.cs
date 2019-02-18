using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ironmunge
{
    public class ChangeMonitoring : IDisposable
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

        public ChangeMonitoring(string gitPath, string savePath, string historyPath)
        {
            if (string.IsNullOrEmpty(gitPath))
                throw new ArgumentNullException(nameof(gitPath));
            if (string.IsNullOrEmpty(savePath))
                throw new ArgumentNullException(nameof(savePath));
            if (string.IsNullOrEmpty(historyPath))
                throw new ArgumentNullException(nameof(historyPath));
            if (PlayNotifications && !NotificationSoundsPresent())
                throw new InvalidOperationException("Notification sound files are missing and notifications are enabled");

            _history = new SaveHistory(historyPath, gitPath);

            _watcher = new FileSystemWatcher(savePath)
            {
                Filter = "*.ck2"
            };

            _watcher.Changed += CopyAndSave;
            _watcher.Created += CopyAndSave;
            _watcher.Renamed += CopyAndSave;

            _watcher.EnableRaisingEvents = true;
        }

        async void CopyAndSave(object sender, FileSystemEventArgs e)
        {
            bool ownsSave = false;
            string copyPath = null;

            try
            {
                ownsSave = _pendingSaves.TryAdd(e.FullPath, default);
                if (!ownsSave)
                    return;

                copyPath = await CopySaveAsync(e.FullPath);
                if (string.IsNullOrEmpty(copyPath))
                    return;

                await SaveAsync(copyPath, e.Name);
            }
            finally
            {
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

        async Task<string> CopySaveAsync(string filepath)
        {
            try
            {
                var timeout = MaximumWait * 3;
                using (var cancellationSource = new CancellationTokenSource(timeout))
                {
                    string tmpPath = Path.GetTempFileName();

                    var pendingProgress = new Progress<TimeSpan>(async t => await NotificationAsync(PendingSound));
                    await FileUtilities.CopyWithRetryAsync(filepath, tmpPath, MaximumWait, cancellationSource.Token, pendingProgress);

                    return tmpPath;
                }
            }
            catch (TaskCanceledException)
            {
                await NotificationAsync(FailureSound);
            }

            return null;
        }

        async Task SaveAsync(string path, string name)
        {
            try
            {
                var checkpointName = await _history.AddSaveAsync(path, name);
                Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] Saved {name} to history: checkpoint {checkpointName}");

                await NotificationAsync(SuccessSound);
            }
            catch (Exception e)
            {
                await NotificationAsync(FailureSound);
                Console.Error.WriteLine(e);

                //await Task.Delay((tries + 1) * 5000).ContinueWith(t => SaveAsync(path, name, tries++));
            }
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
