using System;
using UnityEditor;
using UnityEngine;

// Optional importer for the exported sprite delivery folder. Does not modify existing UI art.
public sealed class TemporaryGuiSpriteImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Deviant/TemporaryGUI/Resources/TemporaryGUI/";
    public override uint GetVersion() => 2;

    private void OnPostprocessTexture(Texture2D texture)
    {
        if (!assetPath.StartsWith(Folder + "Pixel/", StringComparison.Ordinal)) return;
        // Preserve the source silhouette and alpha; uGUI supplies the flat ink colour.
        var pixels = texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, pixels[i].a);
        texture.SetPixels32(pixels);
        texture.Apply(false);
    }

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
