using System;
using System.Runtime.InteropServices;
using System.Threading;
using OpenTK.Audio;

namespace DarkSpec
{
    public class AudioWorker
    {
        private bool running = true;
        private IntPtr tempBufferPtr;
        Thread recordThread;
        AudioBuffer audioBuffer;

        public AudioWorker()
        {
            audioBuffer = new AudioBuffer(SpecWorker.WINDOW_LENGTH);
            tempBufferPtr = Marshal.AllocHGlobal(8192);
            recordThread = new Thread(new ThreadStart(AudioLoop));
            recordThread.Start();
        }

        public AudioBuffer GetSamples()
        {
            return audioBuffer;
        }

        public void Stop()
        {
            running = false;
            audioBuffer.empty.Set();
            recordThread.Join();
            audioBuffer.full.Set();
            Marshal.FreeHGlobal(tempBufferPtr);
        }

        private void AudioLoop()
        {
            AudioCapture capture = new AudioCapture("", SpecWorker.AUDIO_FREQ, OpenTK.Audio.OpenAL.ALFormat.Mono16, 4096);
            capture.Start();
            while (running)
            {
                int availableSamples = capture.AvailableSamples;
                if (availableSamples > 0)
                {
                    //Take as many samples as we can, clamp if we will fill the buffer
                    int samplesLeft = SpecWorker.WINDOW_LENGTH - audioBuffer.samples;
                    int samplesToTake = samplesLeft;
                    if (samplesToTake > availableSamples)
                    {
                        samplesToTake = availableSamples;
                    }
                    capture.ReadSamples(tempBufferPtr, samplesToTake);
                    Marshal.Copy(tempBufferPtr, audioBuffer.data, audioBuffer.samples * 2, samplesToTake * 2);
                    audioBuffer.samples += samplesToTake;
                    if (audioBuffer.samples == SpecWorker.WINDOW_LENGTH)
                    {
                        audioBuffer.full.Set();
                        audioBuffer.empty.WaitOne();
                        audioBuffer.samples = 0;
                    }
                }
                Thread.Sleep(1);
            }
            capture.Stop();
        }
    }
}