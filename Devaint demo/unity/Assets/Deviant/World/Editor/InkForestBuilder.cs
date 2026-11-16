using System.IO;
using UnityEditor;
using UnityEngine;
using Deviant.World;

namespace Deviant.World.Editor
{
public static class InkForestBuilder
{
    public const string Root="Assets/Deviant/World";
    public static void Build() { Deviant.Game.Editor.GameBuilder.Open(); }
    public static void Play() { Deviant.Game.Editor.GameBuilder.Play(); }
    // Keep the authored terrain/data pipeline independent of the gameplay controller.
    [MenuItem("Tools/deviant/World/Refresh Terrain Data")]
    public static void RefreshTerrain()
    {
        var data=AssetDatabase.LoadAssetAtPath<InkForestData>(Root+"/Data/ForestIntegration.asset");
        var source=JsonUtility.FromJson<IntegrationSource>(File.ReadAllText(Root+"/Data/integration.json"));
        data.cells=source.tiles;data.platforms=source.platforms;
        foreach(var theme in data.themes)
        {
            theme.far=Sprite(theme.id+"/far");theme.mid=Sprite(theme.id+"/mid");theme.front=Sprite(theme.id+"/front");
            for(int i=0;i<theme.tiles.Length;i++)
            {
                theme.tiles[i].sprite=Sprite(theme.id+"/"+theme.tileIds[i]);
                EditorUtility.SetDirty(theme.tiles[i]);
            }
        }
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
    }
    static Sprite Sprite(string path)
    {
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/"+path+".png");
        if(!sprite)throw new System.InvalidOperationException("Restore terrain art before importing: "+path);
        return sprite;
    }
}
}
