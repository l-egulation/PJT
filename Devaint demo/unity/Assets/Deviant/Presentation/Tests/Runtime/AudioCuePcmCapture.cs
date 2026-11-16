using UnityEngine;

namespace Deviant.Presentation.Tests
{
    public sealed class AudioCuePcmCapture : MonoBehaviour
    {
        readonly float[] samples = new float[48000 * 4];
        int written;
        public float[] Snapshot()
        {
            int count = System.Threading.Volatile.Read(ref written);
            var result = new float[count]; System.Array.Copy(samples, result, count); return result;
        }
        void OnAudioFilterRead(float[] data, int channels)
        {
            int at = written;
            for (int i = 0; i < data.Length && at < samples.Length; i += channels) samples[at++] = data[i];
            System.Threading.Volatile.Write(ref written, at);
        }
    }
}
