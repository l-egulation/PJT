using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Deviant.World;

namespace Deviant.World.Editor
{
public static class InkMonsterBuilder
{
    public const string Root="Assets/Deviant/World/Monsters";
    public static void Build() { Deviant.Game.Editor.GameBuilder.Open(); }
    public static void Play() { Deviant.Game.Editor.GameBuilder.Play(); }
    [MenuItem("Tools/deviant/World/Monsters/Refresh Approved Art")]
    public static void RefreshApprovedArt()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var source=JsonUtility.FromJson<MonsterImportSource>(File.ReadAllText(Root+"/Data/monster-import.json"));
        foreach(var entry in source.motions)
        for(int n=1;n<=12;n++)
        {
            string path=Root+"/Art/"+entry.species+"/"+entry.motion+"/"+n.ToString("00")+".png";
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            if(!imp) throw new InvalidOperationException("Restore monster art before importing: "+path);
            imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
            imp.spritePixelsPerUnit=32;imp.filterMode=FilterMode.Point;imp.textureCompression=TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled=false;imp.alphaIsTransparency=true;imp.isReadable=true;imp.npotScale=TextureImporterNPOTScale.None;
            var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(entry.pivot[0],entry.pivot[1]);imp.SetTextureSettings(settings);imp.SaveAndReimport();
        }
        foreach(string species in new[]{"eye","boar"})
        {
            var data=AssetDatabase.LoadAssetAtPath<InkMonsterData>(Root+"/Data/"+species+".asset");
            data.move=Clip(Array.Find(source.motions,e=>e.species==species&&e.motion=="move"),out data.moveHurt);
            data.attack=Clip(Array.Find(source.motions,e=>e.species==species&&e.motion=="attack"),out data.attackHurt);
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
    }
    static MotionClip Clip(MonsterMotionSource entry,out Vector4[] hurt)
    {
        var clip=new MotionClip{id=entry.motion,seconds=entry.seconds,loop=entry.loop,frames=new Sprite[12]};hurt=new Vector4[12];
        for(int i=0;i<12;i++)
        {
            clip.frames[i]=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/"+entry.species+"/"+entry.motion+"/"+(i+1).ToString("00")+".png");
            var f=entry.frames[i];var b=f.bodyBounds;
            hurt[i]=new Vector4(((b[0]+b[2])*.5f-entry.pivot[0]*f.size[0])/32f,(f.size[1]-(b[1]+b[3])*.5f-entry.pivot[1]*f.size[1])/32f,(b[2]-b[0])*.85f/32f,(b[3]-b[1])*.85f/32f);
        }
        return clip;
    }
}
}
