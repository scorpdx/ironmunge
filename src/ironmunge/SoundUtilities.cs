using NAudio.Wave;
using System.Threading.Tasks;

namespace Ironmunge
{
    static class SoundUtilities
    {
        public static async Task PlayAsync(string path)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            using (var audioFile = new AudioFileReader(path))
            using (var outputDevice = new WaveOutEvent())
            {
                outputDevice.Init(audioFile);
                outputDevice.PlaybackStopped += (sender, e) =>
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
}
