using UnityEditor;
using UnityEngine;

namespace ForestCombatV2.Editor
{
    public sealed class Chapter1NewMonsterImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/ForestCombatV2/Resources/Chapter1NewMonsters/")||!assetPath.EndsWith("/base.png"))return;
            var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Sprite;
            t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=32;
            var settings=new TextureImporterSettings();t.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,.05f);
            settings.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(settings);
            t.filterMode=FilterMode.Point;t.textureCompression=TextureImporterCompression.Uncompressed;
            t.mipmapEnabled=false;t.alphaIsTransparency=true;t.isReadable=true;
        }
    }
}
