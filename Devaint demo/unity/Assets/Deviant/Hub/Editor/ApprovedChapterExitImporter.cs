#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace Deviant.Hub.Editor
{
    public sealed class ApprovedChapterExitImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/ForestCombatV2/Resources/Chapter1Art/Exit/"))return;
            if(!assetPath.EndsWith("/exit-closed.png")&&!assetPath.EndsWith("/exit-open.png")&&!assetPath.Contains("/approved-open/"))return;
            var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;
            t.filterMode=FilterMode.Point;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;
            t.alphaIsTransparency=true;t.npotScale=TextureImporterNPOTScale.None;t.maxTextureSize=2048;
            var s=new TextureImporterSettings();t.ReadTextureSettings(s);s.spriteAlignment=(int)SpriteAlignment.Custom;s.spritePivot=new Vector2(.5f,0);s.spritePixelsPerUnit=32;s.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(s);
        }
    }
}
#endif
