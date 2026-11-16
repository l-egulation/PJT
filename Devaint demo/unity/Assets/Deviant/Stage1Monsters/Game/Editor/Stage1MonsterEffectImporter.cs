using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deviant.Stage1Monsters.Editor;
using UnityEditor;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game.Editor
{
    public static class Stage1MonsterEffectImporter
    {
        public const string Root="Assets/Deviant/Stage1Monsters";
        public const string CatalogPath=Root+"/Data/Stage1MonsterEffects.asset";
        [Serializable] class Source { public string sourceSHA256,polishManifest,polishSHA256;public Clip[] clips; }
        [Serializable] class Clip
        {
            public string id,role;
            public bool continuousAlpha;
            public int width,height,ppu;
            public float[] pivot;
            public int[] ms,opacity;
            public string[] palette;
            public Frame[] frames;
        }
        [Serializable] class Frame { public string path,sha256; }
        static string Hash(byte[] bytes)
        { using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
        [MenuItem("Tools/deviant/Stage 1 Monsters/Import Attack Effects And Apply To Village")]
        public static void ImportAndApply()
        { Import(true); }
        public static void ImportSelected()
        { Import(false); }
        static void Import(bool apply)
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Use Unity 6000.6.5f1");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var source=JsonUtility.FromJson<Source>(File.ReadAllText(Root+"/Data/effects-import.json"));
            Stage1MonsterImporter.ValidatePolish(source.polishManifest,source.polishSHA256);
            var original=Path.GetFullPath("../resources/authoring/workbench/source/MonsterReference/approved-effects/manifest.json");
            if(Hash(File.ReadAllBytes(original))!=source.sourceSHA256)throw new InvalidDataException("Stale native effects export.");
            var catalog=AssetDatabase.LoadAssetAtPath<Stage1MonsterEffectCatalog>(CatalogPath);
            if(!catalog) { catalog=ScriptableObject.CreateInstance<Stage1MonsterEffectCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath); }
            catalog.sourceSHA256=source.sourceSHA256;
            catalog.clips=source.clips.Select(clip=>
            {
                var frames=clip.frames.Select((frame,i)=>
                {
                    if(Hash(File.ReadAllBytes(frame.path))!=frame.sha256)throw new InvalidDataException(frame.path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(frame.path);
                    importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                    importer.spritePixelsPerUnit=clip.ppu;importer.filterMode=FilterMode.Point;
                    importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
                    importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;
                    importer.isReadable=true;importer.maxTextureSize=1024;
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                    settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;
                    settings.spritePivot=new Vector2(clip.pivot[0],clip.pivot[1]);importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                    var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(frame.path);
                    if(!sprite || sprite.rect.size!=new Vector2(clip.width,clip.height) || sprite.pixelsPerUnit!=32)
                        throw new InvalidDataException("FX import dimensions: "+frame.path);
                    int alpha=clip.opacity!=null && clip.opacity.Length>i?clip.opacity[i]:255;
                    foreach(var pixel in sprite.texture.GetPixels32())
                    {
                        if(!clip.continuousAlpha && pixel.a!=0 && pixel.a!=alpha)throw new InvalidDataException("FX opacity: "+frame.path);
                        string rgb="#"+pixel.r.ToString("X2")+pixel.g.ToString("X2")+pixel.b.ToString("X2");
                        if(pixel.a!=0 && !clip.palette.Contains(rgb))throw new InvalidDataException("FX palette: "+rgb+" "+frame.path);
                    }
                    return sprite;
                }).ToArray();
                if(clip.role!="body" && clip.palette.Length>3)throw new InvalidDataException("FX must use at most 3 RGB colors.");
                return new Stage1MonsterClip {id=clip.id,frames=frames,milliseconds=clip.ms,loop=false,
                    pivot=new Vector2(clip.pivot[0],clip.pivot[1])};
            }).ToArray();
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            if(apply)Stage1VillageMonsterInstaller.Apply();
            Debug.Log("STAGE1_EFFECT_IMPORT_PASS: "+catalog.clips.Length+" clips / "+catalog.clips.Sum(c=>c.frames.Length)+" frames; Point/PPU32; smoke fade preserved.");
        }
    }
}
