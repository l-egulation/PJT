#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deviant.Hub.Editor
{
    // Starting-village sprites keep their delivered canvas, PPU and foot pivot (data/assets/starting_hub.json,
    // compiled by tools/assets/build_hub_bindings.py into Hub/Data/hub-sprites.json). Hub art outside that index keeps
    // the pivot and PPU written in its .meta.
    public sealed class HubArtImporter : AssetPostprocessor
    {
        const string Root = "Assets/Deviant/Hub/Resources/Hub/";
        const string Index = "Assets/Deviant/Hub/Data/hub-sprites.json";
        [Serializable] sealed class Row { public string id, unity; public float ppu; public float[] pivot; }
        [Serializable] sealed class Table { public Row[] sprites; }

        // 2: sheets wider than 2048 keep their size, so their imported copies must be made again.
        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.filterMode = FilterMode.Point;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.npotScale = TextureImporterNPOTScale.None;
            // Wider source art keeps its dimensions; native animation cells are delivered as individual textures.
            t.maxTextureSize = PngSide(assetPath) > 2048 ? 16384 : 2048;
            var settings = new TextureImporterSettings(); t.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            var row = File.Exists(Index) ? Array.Find(JsonUtility.FromJson<Table>(File.ReadAllText(Index)).sprites, r => r.unity == assetPath) : null;
            // Indexed starting-hub sprites take the index values; other Hub art (needed-assets selections: colour
            // spread, story characters, blueprints) keeps the pivot/PPU its own .meta declares.
            if (row?.pivot != null && row.pivot.Length == 2) settings.spritePivot = new Vector2(row.pivot[0], row.pivot[1]);
            if (row != null && row.ppu > 0) settings.spritePixelsPerUnit = row.ppu;
            t.SetTextureSettings(settings);
        }

        // The longer side of a PNG from its IHDR header (0 when it cannot be read).
        static int PngSide(string path)
        {
            try
            {
                var header = new byte[24];
                using (var file = File.OpenRead(path)) if (file.Read(header, 0, 24) < 24) return 0;
                int width = header[16] << 24 | header[17] << 16 | header[18] << 8 | header[19];
                int height = header[20] << 24 | header[21] << 16 | header[22] << 8 | header[23];
                return Math.Max(width, height);
            }
            catch (IOException) { return 0; }
        }
    }
}
#endif
