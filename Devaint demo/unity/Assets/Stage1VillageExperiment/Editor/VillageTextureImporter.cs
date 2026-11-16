using UnityEditor;
using UnityEngine;
namespace Stage1VillageExperiment.Editor
{
    // Import only the isolated experiment; approved shared assets retain their settings.
    public sealed class VillageTextureImporter:AssetPostprocessor
    {
        public override uint GetVersion() => 1;
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Stage1VillageExperiment/Resources/Village/"))return;
            var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Sprite;
            t.textureShape=TextureImporterShape.Texture2D;
            t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=32;
            t.filterMode=FilterMode.Point;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;
            t.isReadable=true;t.alphaIsTransparency=true;t.wrapMode=TextureWrapMode.Clamp;t.maxTextureSize=4096;
        }
    }
}
