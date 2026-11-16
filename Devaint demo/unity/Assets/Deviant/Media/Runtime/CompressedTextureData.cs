using System;
using System.Text;
using UnityEngine;

namespace Deviant.Media
{
    // GPU block data for the few PNG TextAssets that cannot go through TextureImporter.
    public static class CompressedTextureData
    {
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("DVGPU001");
        public static bool LoadImage(Texture2D texture, byte[] data, bool markNonReadable = false)
        {
            bool encoded = data != null && data.Length >= 28;
            if (encoded) for (int i = 0; i < Magic.Length; i++) if (data[i] != Magic[i]) { encoded = false; break; }
            if (!encoded) return ImageConversion.LoadImage(texture, data, markNonReadable);
            int w = BitConverter.ToInt32(data, 8), h = BitConverter.ToInt32(data, 12);
            var format = (TextureFormat)BitConverter.ToInt32(data, 16);
            int raw = BitConverter.ToInt32(data, 20), fallback = BitConverter.ToInt32(data, 24);
            if (w <= 0 || h <= 0 || w > 16384 || h > 16384 || raw <= 0 || fallback <= 0
                || raw > data.Length - 28 || fallback != data.Length - 28 - raw
                || format != TextureFormat.DXT1 && format != TextureFormat.DXT5 && format != TextureFormat.BC7) return false;
            long expected = ((w + 3L) / 4) * ((h + 3L) / 4) * (format == TextureFormat.DXT1 ? 8 : 16);
            if (raw != expected) return false;
            if (SystemInfo.SupportsTextureFormat(format) && texture.Reinitialize(w, h, format, false))
            {
                var blocks = new byte[raw]; Buffer.BlockCopy(data, 28, blocks, 0, raw);
                texture.LoadRawTextureData(blocks); texture.Apply(false, markNonReadable);
                return true;
            }
            var png = new byte[fallback]; Buffer.BlockCopy(data, 28 + raw, png, 0, fallback);
            return ImageConversion.LoadImage(texture, png, markNonReadable);
        }
    }
}
