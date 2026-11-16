using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Deviant.Media.Tests
{
    public sealed class GpuTextureReadTests
    {
        [TestCase(TextureFormat.DXT5)]
        [TestCase(TextureFormat.BC7)]
        public void CpuAlphaSamplingStillWorksOnCompressedReadableTextures(TextureFormat format)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[256];
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    pixels[y * 16 + x] = new Color32(120, 60, 30, (byte)(x >= 4 && x < 12 && y >= 4 && y < 12 ? 255 : 0));
                texture.SetPixels32(pixels); texture.Apply();
                EditorUtility.CompressTexture(texture, format, TextureCompressionQuality.Normal);
                Assert.That(texture.format, Is.EqualTo(format));
                var read = texture.GetPixels32();
                Assert.That(read.Length, Is.EqualTo(256));
                Assert.That(read[8 * 16 + 8].a, Is.GreaterThan(240));
                Assert.That(read[0].a, Is.LessThan(16));
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
