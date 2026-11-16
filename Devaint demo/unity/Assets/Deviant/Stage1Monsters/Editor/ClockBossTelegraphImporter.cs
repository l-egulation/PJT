using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Deviant.Stage1Monsters.Editor
{
    // Native clock boss preview art for the stationary boss test room: ink warnings
    // (resources/tools/export_clock_boss_telegraphs.py) and assembled parts / attack sprites / FX
    // (resources/tools/export_clock_boss_preview.py). Preview parts, not approved motion clips.
    public static class ClockBossTelegraphImporter
    {
        public const string Folder="Assets/Deviant/Stage1Monsters/Resources/ClockExecutionerTelegraph";
        public const string PreviewFolder="Assets/Deviant/Stage1Monsters/Resources/ClockExecutionerPreview";
        const string Preview="../resources/monsters/stage-01/bosses/clock-executioner/source/battle-preview/asset-manifest.json";
        [Serializable] class Source { public string sourceSHA256;public int ppu;public Clip[] clips; }
        [Serializable] class Clip { public string id,role;public int width,height;public float[] pivot;public int[] opacity;public string[] palette;public Frame[] frames; }
        [Serializable] class Frame { public string path,sha256; }
        static string Hash(byte[] bytes)
        { using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
        [MenuItem("Tools/deviant/Stage 1 Monsters/Import Clock Boss Preview Art")]
        public static void Import()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Use Unity 6000.6.5f1");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string preview=Hash(File.ReadAllBytes(Path.GetFullPath(Preview)));
            int clips=0,count=0;
            foreach(var json in new[]{Folder+"/telegraphs.json",PreviewFolder+"/preview.json"})
            {
                var source=JsonUtility.FromJson<Source>(File.ReadAllText(json));
                if(source.sourceSHA256!=preview)throw new InvalidDataException("Stale clock boss export: "+json);
                foreach(var clip in source.clips)
                {
                    clips++;
                    if((string.IsNullOrEmpty(clip.role)||clip.role=="fx")&&clip.palette.Length>3)throw new InvalidDataException("FX must use at most 3 RGB colors: "+clip.id);
                    for(int i=0;i<clip.frames.Length;i++)
                    {
                        var frame=clip.frames[i];
                        if(Hash(File.ReadAllBytes(frame.path))!=frame.sha256)throw new InvalidDataException(frame.path);
                        var importer=(TextureImporter)AssetImporter.GetAtPath(frame.path);
                        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                        importer.spritePixelsPerUnit=source.ppu;importer.filterMode=FilterMode.Point;
                        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
                        importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;
                        importer.isReadable=true;importer.maxTextureSize=1024;
                        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                        settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;
                        settings.spritePivot=new Vector2(clip.pivot[0],clip.pivot[1]);importer.SetTextureSettings(settings);
                        importer.SaveAndReimport();
                        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(frame.path);
                        if(!sprite||sprite.rect.size!=new Vector2(clip.width,clip.height)||sprite.pixelsPerUnit!=source.ppu)
                            throw new InvalidDataException("Clock boss import dimensions: "+frame.path);
                        int alpha=clip.opacity!=null&&clip.opacity.Length>i?clip.opacity[i]:255;
                        foreach(var pixel in sprite.texture.GetPixels32())
                        {
                            if(pixel.a!=0&&pixel.a!=alpha)throw new InvalidDataException("Clock boss alpha: "+frame.path);
                            string rgb="#"+pixel.r.ToString("X2")+pixel.g.ToString("X2")+pixel.b.ToString("X2");
                            if(pixel.a!=0&&!clip.palette.Contains(rgb,StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("Clock boss palette: "+rgb+" "+frame.path);
                        }
                        count++;
                    }
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("CLOCK_BOSS_PREVIEW_IMPORT_PASS: "+clips+" clips / "+count+" frames; Point/PPU32/FullRect.");
        }
    }
}
