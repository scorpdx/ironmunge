﻿using NAudio.Wave;
using System.Threading.Tasks;

namespace Ironmunge.Common
{
    public static class SoundUtilities
    {
        public static async Task PlayAsync(string path)
        {
            var tcs = new TaskCompletionSource<bool>();

            using var audioFile = new WaveFileReader(path);
            using var outputDevice = new WaveOutEvent();

            outputDevice.Init(audioFile);
            outputDevice.PlaybackStopped += (_, e) =>
            {
                if (e.Exception != null)
                {
                    tcs.SetException(e.Exception);
                }
                else
                {
                    tcs.SetResult(true);
                }
            };

            outputDevice.Play();
            await tcs.Task;
        }
    }
}
