using System;
using UnityEditor;
using UnityEngine;

// Optional importer for the exported sprite delivery folder. Does not modify existing UI art.
public sealed class TemporaryGuiSpriteImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Deviant/TemporaryGUI/Sprites/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder, StringComparison.Ordinal) ||
            !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;

        string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        bool sliced = !name.StartsWith("icon-", StringComparison.Ordinal) &&
                      !name.StartsWith("ink-", StringComparison.Ordinal) &&
                      name.IndexOf("fill", StringComparison.Ordinal) < 0;
        importer.spriteBorder = sliced ? new Vector4(4, 4, 4, 4) : Vector4.zero;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
