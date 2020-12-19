using System;
using System.Threading;
using System.IO;
using FftSharp;

namespace DarkSpec
{
    public class SpecWorker
    {
        public const int PADDING = 2;
        public const int AUDIO_FREQ = 48000;
        public const int WINDOW_LENGTH = 1024;
        public const int FFT_LENGTH = 4096;
        public const int XSCALE = 3;
        public const int YSCALE = 3;
        private bool running = true;
        private Action UpdateFrame;
        private Thread fftThread;
        private int SIZEX;
        private int SIZEY;
        private int FPS;
        private double[] hanningWindow;
        private double[] singalWindow;
        private double[] audioData;
        private double[] audioDataShift;
        private double[] audioDataWindowed;
        private double[] audioDataPadded;
        private byte[] specData;
        private double[] markers = new double[] { 100, 175, 220 };
        private int frame;
        private AudioWorker audioWorker = new AudioWorker();
        private AutoResetEvent drawSync;

        public SpecWorker(int SIZEX, int SIZEY, int FPS, byte[] specData, Action UpdateFrame, AutoResetEvent drawSync)
        {
            this.SIZEX = SIZEX;
            this.SIZEY = SIZEY;
            this.FPS = FPS;
            this.specData = specData;
            this.UpdateFrame = UpdateFrame;
            this.drawSync = drawSync;
            singalWindow = new double[WINDOW_LENGTH * 2];
            audioData = new double[FFT_LENGTH * 2];
            audioDataShift = new double[audioData.Length];
            audioDataWindowed = new double[audioData.Length];
            audioDataPadded = new double[audioData.Length * PADDING];
            hanningWindow = Window.Hanning(audioData.Length);
            fftThread = new Thread(new ThreadStart(UpdateLoop));
            fftThread.Start();
        }

        public void Stop()
        {
            running = false;
            drawSync.Set();
            audioWorker.Stop();
            fftThread.Join();
        }

        private void UpdateLoop()
        {
            while (running)
            {
                MoveLeft();
                DrawSample();
                UpdateFrame();
                drawSync.WaitOne();
            }
        }

        private void MoveLeft()
        {
            int strideBytes = SIZEX * 4;
            byte[] stride = new byte[strideBytes - (4 * XSCALE)];
            for (int yPos = 0; yPos < SIZEY; yPos++)
            {
                int startPos = yPos * strideBytes;
                Buffer.BlockCopy(specData, startPos + (4 * XSCALE), stride, 0, stride.Length);
                Buffer.BlockCopy(stride, 0, specData, startPos, stride.Length);
            }
        }

        private void DrawSample()
        {
            int strideBytes = SIZEX * 4;
            AudioBuffer audioBuffer = audioWorker.GetSamples();
            audioBuffer.full.WaitOne();
            //Shift and swap buffers
            Array.Copy(audioData, WINDOW_LENGTH, audioDataShift, 0, audioDataShift.Length - WINDOW_LENGTH);
            double[] temp = audioData;
            audioData = audioDataShift;
            audioDataShift = temp;
            //Write new data into buffer
            for (int i = 0; i < audioBuffer.data.Length; i = i + 2)
            {
                int sampleIndex = (i / 2);
                int samples = audioBuffer.data.Length / 2;
                short s16 = BitConverter.ToInt16(audioBuffer.data, i);
                double d64 = s16 / 32768d;
                singalWindow[sampleIndex] = d64;
            }
            audioBuffer.empty.Set();
            Array.Copy(singalWindow, 0, audioData, audioData.Length - singalWindow.Length, singalWindow.Length);
            //Hanning window the data that has multiple samples, then place it in the middle of the padding array
            Array.Copy(audioData, audioDataWindowed, audioData.Length);
            Window.ApplyInPlace(hanningWindow, audioDataWindowed);
            int offset = (audioDataPadded.Length - audioData.Length) / 2;
            Array.Copy(audioDataWindowed, 0, audioDataPadded, offset, audioData.Length);
            //Run the FFT
            double[] fftData = Transform.FFTpower(audioDataPadded);
            for (int yPos = 0; yPos < SIZEY; yPos++)
            {
                int invertY = SIZEY - 1 - yPos;
                int startPos = (strideBytes * invertY) + (strideBytes - (4 * XSCALE));
                //Scale the display
                int showPos = yPos / YSCALE;
                //[-100:0] to [0:1] mapping
                double thisValue = ((fftData[showPos] + 100d) / 100d);
                if (thisValue < 0)
                {
                    thisValue = 0;
                }
                if (thisValue > 1)
                {
                    thisValue = 1;
                }
                //Test colour
                //thisValue = invertY / (double)SIZEY;

                //Old blue
                /*
                byte thisValueByte = (byte)(thisValue * 255);
                for (int i = 0; i < XSCALE; i++)
                {
                    specData[startPos] = (byte)(thisValueByte / 2);
                    specData[startPos + 1] = thisValueByte;
                    specData[startPos + 2] = thisValueByte;
                    specData[startPos + 3] = 255;
                    startPos = startPos + 4;
                }
                */
                double rDistance = Math.Abs(1 - thisValue);
                double gDistance = Math.Abs(0.7 - thisValue);
                double bDistance = Math.Abs(0.4 - thisValue);
                double rVal = Math.Clamp(1 - rDistance * 3d, 0, 1);
                double gVal = Math.Clamp(1 - gDistance * 3d, 0, 1);
                double bVal = Math.Clamp(1 - bDistance * 3d, 0, 1);
                byte rByte = (byte)(rVal * 255);
                byte gByte = (byte)(gVal * 255);
                byte bByte = (byte)(bVal * 255);
                for (int i = 0; i < XSCALE; i++)
                {
                    specData[startPos] = rByte;
                    specData[startPos + 1] = gByte;
                    specData[startPos + 2] = bByte;
                    specData[startPos + 3] = 255;
                    startPos = startPos + 4;
                }
            }

            if (frame == 0)
            {
                double binWidth = Transform.FFTfreqPeriod(AUDIO_FREQ, fftData.Length);
                foreach (double marker in markers)
                {
                    int dotY = 1 + (int)(marker / binWidth) * YSCALE;
                    int invertY = SIZEY - 1 - dotY;
                    if (invertY < 0)
                    {
                        continue;
                    }
                    int startPos = (strideBytes * invertY) + (strideBytes - 4);
                    specData[startPos] = 255;
                    specData[startPos + 1] = 0;
                    specData[startPos + 2] = 0;
                    specData[startPos + 3] = 255;
                }
            }
            frame = (frame + 1) % 8;
        }
    }
}