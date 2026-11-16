using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game.Editor
{
    public static class Chapter1NewMonsterArtImporter
    {
        static string Hash(byte[] bytes)
        { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
        [MenuItem("Tools/deviant/Stage 1 Monsters/Import Approved Barrel And Millstone")]
        public static void Import()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Use Unity 6000.6.5f1.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var art=Chapter1NewMonsterArt.Load();int count=0;
            foreach(var clip in art.clips)
            {
                if(clip.frames.Length!=clip.ms.Length||clip.ms.Any(ms=>ms<=0))throw new InvalidDataException(clip.id+" timing");
                if(clip.role!="body"&&clip.palette.Length>3)throw new InvalidDataException(clip.id+" FX palette");
                foreach(var frame in clip.frames)
                {
                    string path="Assets/Deviant/Stage1Monsters/Resources/"+frame.resource+".png";
                    if(Hash(File.ReadAllBytes(path))!=frame.sha256)throw new InvalidDataException("Asset hash: "+path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                    importer.spritePixelsPerUnit=clip.ppu;importer.filterMode=FilterMode.Point;
                    importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
                    importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;
                    importer.isReadable=true;importer.maxTextureSize=1024;
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                    settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;
                    settings.spritePivot=new Vector2(clip.pivot[0],clip.pivot[1]);importer.SetTextureSettings(settings);importer.SaveAndReimport();
                    var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if(!sprite||sprite.rect.size!=new Vector2(clip.width,clip.height)||sprite.pixelsPerUnit!=32)
                        throw new InvalidDataException("Native import dimensions: "+path);
                    foreach(var pixel in sprite.texture.GetPixels32())
                    {
                        if(pixel.a==0)continue;
                        string rgb="#"+pixel.r.ToString("X2")+pixel.g.ToString("X2")+pixel.b.ToString("X2");
                        if(!clip.palette.Contains(rgb))throw new InvalidDataException("Unexpected pixel color "+rgb+" in "+path);
                        if(clip.alpha=="binary"&&pixel.a!=255)throw new InvalidDataException("Body alpha: "+path);
                    }
                    count++;
                }
            }
            AssetDatabase.SaveAssets();Debug.Log("NEW_MONSTER_ART_IMPORT_PASS: "+art.clips.Length+" clips / "+count+" native sprites.");
        }
    }
}
