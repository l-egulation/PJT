using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deviant.Chapter2.Editor
{
    public sealed class Chapter2EliteArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            const string prefix="Assets/Chapter2/Resources/Chapter2EliteArt/";if(!assetPath.StartsWith(prefix,StringComparison.Ordinal))return;
            var data=JsonUtility.FromJson<Chapter2EliteArtData>(File.ReadAllText(Path.Combine(Application.dataPath,"Chapter2/Resources/Chapter2EliteArt.json")));
            string resource="Chapter2EliteArt/"+assetPath.Substring(prefix.Length,assetPath.Length-prefix.Length-4);
            var entry=Array.Find(data.sprites,s=>s.path==resource);if(entry==null)throw new InvalidOperationException("Unbound A3 import: "+assetPath);
            var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=32;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;
            settings.spritePivot=new Vector2(entry.pivot[0]/entry.canvas[0],1-entry.pivot[1]/entry.canvas[1]);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;
            importer.isReadable=resource.StartsWith("Chapter2EliteArt/fragment/",StringComparison.Ordinal);
        }
    }
}
