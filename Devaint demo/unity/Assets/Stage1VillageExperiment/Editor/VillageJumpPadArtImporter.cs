using UnityEditor;
using UnityEngine;

namespace Stage1VillageExperiment.Editor
{
    public sealed class VillageJumpPadArtImporter:AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Stage1VillageExperiment/Resources/Village/JumpPadArt/"))return;
            var texture=(TextureImporter)assetImporter;
            texture.textureType=TextureImporterType.Default;texture.alphaSource=TextureImporterAlphaSource.FromInput;
            texture.alphaIsTransparency=true;texture.mipmapEnabled=false;texture.filterMode=FilterMode.Point;
            texture.wrapMode=TextureWrapMode.Clamp;texture.npotScale=TextureImporterNPOTScale.None;
            texture.textureCompression=TextureImporterCompression.Uncompressed;texture.maxTextureSize=2048;
            texture.sRGBTexture=true;texture.isReadable=true;
        }
    }
}
