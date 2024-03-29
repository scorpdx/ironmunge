﻿using Ironmunge.Common;
using Ironmunge.Plugins;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ironmunge
{
    public sealed class SaveMonitoring : IDisposable
    {
        private const string FailureSound = "Resources/failure.wav";
        private const string PendingSound = "Resources/pending.wav";
        private const string SuccessSound = "Resources/success.wav";

        private static bool NotificationSoundsPresent()
            => File.Exists(FailureSound) && File.Exists(PendingSound) && File.Exists(SuccessSound);

        private Task NotificationAsync(string path)
            => PlayNotifications ? SoundUtilities.PlayAsync(path) : Task.CompletedTask;

        private readonly ILogger<SaveMonitoring> _logger;
        private readonly SaveHistory _history;
        private readonly FileSystemWatcher _watcher;

        private readonly ConcurrentDictionary<string, bool> _pendingSaves = new();

        public bool PlayNotifications { get; set; } = true;

        public TimeSpan MaximumWait { get; set; } = TimeSpan.FromSeconds(30);

        public SaveMonitoring(ILogger<SaveMonitoring> logger, string gitPath, string savePath, IGame game, string historyPath, string? remote = null)
        {
            if (string.IsNullOrEmpty(gitPath))
                throw new ArgumentNullException(nameof(gitPath), "git was not found");
            if (string.IsNullOrEmpty(savePath))
                throw new ArgumentNullException(nameof(savePath));
            if (string.IsNullOrEmpty(historyPath))
                throw new ArgumentNullException(nameof(historyPath));
            if (PlayNotifications && !NotificationSoundsPresent())
                throw new InvalidOperationException("Notification sound files are missing and notifications are enabled");

            _logger = logger;

            _history = new SaveHistory(game, historyPath, gitPath, remote);

            _watcher = new FileSystemWatcher(savePath);
            foreach (var filter in game.Filters)
            {
                _watcher.Filters.Add(filter);
            }

            _watcher.Changed += CopyAndSave;
            _watcher.Created += CopyAndSave;
            _watcher.Renamed += CopyAndSave;

            _watcher.EnableRaisingEvents = true;
        }

        async void CopyAndSave(object sender, FileSystemEventArgs e)
        {
            bool ownsSave = false;
            string? copyPath = null;

            try
            {
                ownsSave = _pendingSaves.TryAdd(e.FullPath, default);
                if (!ownsSave)
                    return;

                copyPath = await CopySaveAsync(e.FullPath);
                if (string.IsNullOrEmpty(copyPath) || string.IsNullOrEmpty(e.Name))
                    return;

                //Sanity-check to avoid crash on new game creation
                // When CK2 first creates a new game, the initial save is a 1-byte dummy file with a single nul char.
                // We could avoid the crash by not watching Created types at all, but this would also stop saving games when they are dropped into the save folder.
                if (e.ChangeType == WatcherChangeTypes.Created)
                {
                    var copyInfo = new FileInfo(copyPath);
                    if (copyInfo.Length <= 1) return;
                }

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

        async Task<string?> CopySaveAsync(string filepath)
        {
            try
            {
                var timeout = MaximumWait * 3;
                string tmpPath = Path.GetTempFileName();

                var pendingProgress = new Progress<TimeSpan>(async t => await NotificationAsync(PendingSound));

                using var cancellationSource = new CancellationTokenSource(timeout);
                await FileUtilities.CopyWithRetryAsync(filepath, tmpPath, MaximumWait, cancellationSource.Token, pendingProgress);

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
            try
            {
                var gameDescription = await _history.AddSaveAsync(path, name);
                _logger.LogInformation("[{timestamp}] Saved {name} to history: {gameDescription}", DateTime.Now.ToShortTimeString(), name, gameDescription);

                await NotificationAsync(SuccessSound);
            }
            catch (Exception e)
            {
                await NotificationAsync(FailureSound);
                _logger.LogError(e, "Failed saving to history");
            }
        }

        #region IDisposable Support
        private bool disposedValue;

        public void Dispose()
        {
            if (!disposedValue)
            {
                _watcher?.Dispose();
                disposedValue = true;
            }
        }
        #endregion
    }
}
