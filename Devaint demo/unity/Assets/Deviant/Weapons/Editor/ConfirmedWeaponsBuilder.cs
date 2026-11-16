using System.IO;
using UnityEditor;
using UnityEngine;
using Deviant.Weapons;

namespace Deviant.Weapons.Editor
{
public class ConfirmedSpriteImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Deviant/Weapons/Art/"))return;
        var imp=(TextureImporter)assetImporter;
        imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
        imp.spritePixelsPerUnit=32;imp.filterMode=FilterMode.Point;imp.textureCompression=TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled=false;imp.alphaIsTransparency=true;imp.maxTextureSize=2048;imp.npotScale=TextureImporterNPOTScale.None;
        var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=Vector2.zero;
        imp.SetTextureSettings(settings);
    }
}
public static class ConfirmedWeaponsBuilder
{
    const string Root="Assets/Deviant/Weapons";
    public static void Build() { Deviant.Game.Editor.GameBuilder.Open(); }
    public static void Play() { Deviant.Game.Editor.GameBuilder.Play(); }
    [MenuItem("Tools/deviant/Weapons/Refresh Presentation Library")]
    public static void RefreshLibrary()
    {
        var source=JsonUtility.FromJson<SourceData>(File.ReadAllText(Root+"/Data/source.json"));
        foreach(var weapon in source.weapons)
        {
            foreach(var frame in weapon.frames)frame.sprite=Load(frame.path);
            foreach(var effect in weapon.effects)foreach(var frame in effect.steps)frame.sprite=Load(frame.path);
        }
        foreach(var frame in source.dummy)frame.sprite=Load(frame.path);
        var library=AssetDatabase.LoadAssetAtPath<ConfirmedWeaponLibrary>(Root+"/Data/ConfirmedWeapons.asset");
        library.weapons=source.weapons;library.dummy=source.dummy;
        library.dummyPivotX=source.dummyPivotX;library.dummyPivotY=source.dummyPivotY;
        EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
    }
    static Sprite Load(string path)
    {
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(!sprite)throw new System.InvalidOperationException("Restore confirmed art before importing: "+path);
        return sprite;
    }
}
}
