#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Native dimensions are intentional. All GUI sprites share one PPU.
namespace Deviant.UI.Editor
{
public sealed class DeviantUIImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Deviant/UI/Sprites/")) return;
        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Sprite;
        t.spriteImportMode = SpriteImportMode.Single;
        t.spriteMeshType = SpriteMeshType.FullRect;
        t.spritePixelsPerUnit = 100f;
        t.filterMode = FilterMode.Point;
        t.mipmapEnabled = false;
        t.alphaIsTransparency = true;
        t.textureCompression = TextureImporterCompression.Uncompressed;
        t.npotScale = TextureImporterNPOTScale.None;
        t.maxTextureSize = 2048;
        t.spriteBorder = Vector4.zero;
    }
}
}
#endif

