using System;
using System.IO;
using Deviant.Media;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Media.Tests
{
    public sealed class OggOpusDecoderTests
    {
        static string Fixture(string name)
        {
            string root = Environment.GetEnvironmentVariable("DEVIANT_MEDIA_FIXTURES");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Generate the synthetic media fixtures and set DEVIANT_MEDIA_FIXTURES.");
            return Path.Combine(root, name);
        }
        [TestCase("mono", 1, 6000)]
        [TestCase("stereo", 2, 9600)]
        public void PreskipAndEndTrimMatchReferenceDecoder(string name, int channels, int samples)
        {
            var pcm = OggOpusDecoder.Decode(File.ReadAllBytes(Fixture(name + ".opus")));
            Assert.That(pcm.Channels, Is.EqualTo(channels));
            Assert.That(pcm.Samples.Length, Is.EqualTo(samples));
            var expected = File.ReadAllBytes(Fixture(name + ".f32"));
            Assert.That(expected.Length, Is.EqualTo(samples * 4));
            double error = 0;
            for (int i = 0; i < samples; i++)
            {
                Assert.That(float.IsNaN(pcm.Samples[i]) || float.IsInfinity(pcm.Samples[i]), Is.False);
                double difference = pcm.Samples[i] - BitConverter.ToSingle(expected, i * 4); error += difference * difference;
            }
            Assert.That(Math.Sqrt(error / samples), Is.LessThan(.003), "managed decoder must agree with the reference Opus decoder");
        }
        [Test] public void RejectsDamagedOggPagesAndTruncatedStreams()
        {
            byte[] data = File.ReadAllBytes(Fixture("mono.opus"));
            var damaged = (byte[])data.Clone(); damaged[damaged.Length - 10] ^= 1;
            Assert.Throws<InvalidDataException>(() => OggOpusDecoder.Decode(damaged));
            Array.Resize(ref data, data.Length - 1);
            Assert.Throws<InvalidDataException>(() => OggOpusDecoder.Decode(data));
            Assert.Throws<InvalidDataException>(() => OggOpusDecoder.Decode(new byte[100]));
        }
        [Test] public void DecodedOpusCanPopulateUnityAudioClip()
        {
            var pcm = OggOpusDecoder.Decode(File.ReadAllBytes(Fixture("stereo.opus")));
            var clip = AudioClip.Create("Opus fixture", pcm.Frames, pcm.Channels, OggOpusDecoder.Pcm.Frequency, false);
            try
            {
                Assert.That(clip.SetData(pcm.Samples, 0), Is.True);
                Assert.That(clip.channels, Is.EqualTo(2));
                Assert.That(clip.samples, Is.EqualTo(4800));
                Assert.That(clip.frequency, Is.EqualTo(48000));
                Assert.That(clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
            }
            finally { UnityEngine.Object.DestroyImmediate(clip); }
        }
    }
}
