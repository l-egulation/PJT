using UnityEditor;
using UnityEngine;
namespace Stage1VillageExperiment.Editor
{
 public sealed class VillageReferenceGroundImporter:AssetPostprocessor
 {
  void OnPreprocessTexture(){if(!assetPath.StartsWith("Assets/Stage1VillageExperiment/Resources/Village/ReferenceGround/"))return;var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;t.alphaSource=TextureImporterAlphaSource.FromInput;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Clamp;t.npotScale=TextureImporterNPOTScale.None;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=2048;t.sRGBTexture=true;t.isReadable=true;}
 }
}
