using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Deviant.Media.Tests
{
    public sealed class GpuTextureDataTests
    {
        [TestCase(TextureFormat.DXT5)]
        [TestCase(TextureFormat.BC7)]
        public void PackagedBlocksOrPngFallbackKeepCanvasAndVisibleAlpha(TextureFormat format)
        {
            var source = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var loaded = new Texture2D(2, 2);
            try
            {
                var pixels = new Color32[256];
                for (int y = 4; y < 12; y++) for (int x = 4; x < 12; x++) pixels[y * 16 + x] = new Color32(100, 160, 30, 255);
                source.SetPixels32(pixels); source.Apply();
                byte[] png = source.EncodeToPNG();
                EditorUtility.CompressTexture(source, format, TextureCompressionQuality.Normal);
                byte[] blocks = source.GetRawTextureData();
                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream);
                writer.Write(Encoding.ASCII.GetBytes("DVGPU001")); writer.Write(16); writer.Write(16);
                writer.Write((int)format); writer.Write(blocks.Length); writer.Write(png.Length); writer.Write(blocks); writer.Write(png);
                byte[] data = stream.ToArray();
                Assert.That(CompressedTextureData.LoadImage(loaded, data), Is.True);
                Assert.That(loaded.width, Is.EqualTo(16)); Assert.That(loaded.height, Is.EqualTo(16));
                Assert.That(loaded.GetPixels32()[8 * 16 + 8].a, Is.GreaterThan(240));
                Assert.That(loaded.GetPixels32()[0].a, Is.LessThan(16));
                // A truncated block must be rejected before native texture upload.
                Buffer.BlockCopy(BitConverter.GetBytes(blocks.Length - 1), 0, data, 20, 4);
                Assert.That(CompressedTextureData.LoadImage(loaded, data), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(loaded); }
        }
    }
}
