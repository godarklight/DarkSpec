using System.Threading;

namespace DarkSpec
{
    public class AudioBuffer
    {
        public byte[] data;
        public int samples;
        public AutoResetEvent empty;
        public AutoResetEvent full;

        public AudioBuffer(int sampleSize)
        {
            data = new byte[sampleSize * 2];
            samples = 0;
            empty = new AutoResetEvent(true);
            full = new AutoResetEvent(false);
        }
    }
}