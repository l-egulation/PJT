using UnityEditor;
using UnityEngine;

namespace Deviant.World.Editor
{
public class InkEffectSpriteImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Deviant/World/Resources/BasicAttack/")&&!assetPath.StartsWith("Assets/Deviant/World/Resources/RedInk/"))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Center;
        settings.spritePivot=new Vector2(.5f,.5f);importer.SetTextureSettings(settings);
    }
}
}
