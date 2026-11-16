using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForestCombatV2.Editor
{
    public static class Chapter1MonsterImporter
    {
        const string Art="Assets/ForestCombatV2/Art/Chapter1MonstersV4";
        public const string CatalogPath="Assets/ForestCombatV2/Data/Chapter1MonstersV4.asset";
        [Serializable] class Source { public Entry[] clips; }
        [Serializable] class Entry
        {
            public string species,motion,displayName;
            public int columns,rows,ppu;
            public float[] pivot,seconds;
            public bool loop;
        }
        [MenuItem("Tools/Deviant/Meadow Dusk/Apply Six Monster Sprite Sheets")]
        public static void Apply()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string sourcePath=Path.GetFullPath(Path.Combine(Application.dataPath,"../../resources/authoring/workbench/source/docs/assets/manifests/chapter1-monsters-v4.json"));
            var source=JsonUtility.FromJson<Source>(File.ReadAllText(sourcePath));
            var catalog=AssetDatabase.LoadAssetAtPath<Chapter1MonsterCatalog>(CatalogPath);
            if(!catalog) { catalog=ScriptableObject.CreateInstance<Chapter1MonsterCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath); }
            catalog.clips=source.clips.Select(e=>Import(e)).ToArray();
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene(MeadowDuskBuilder.Scene);
            var world=UnityEngine.Object.FindFirstObjectByType<ForestCombatWorld>();
            world.monsterAnimations=catalog;
            var data=JsonUtility.FromJson<ForestData>(world.worldJson.text);
            world.monsterSprites=data.monsters.Select((m,i)=>catalog.Find(m.id,"move")?.frames[0] ?? (i<world.monsterSprites.Length?world.monsterSprites[i]:null)).ToArray();
            if(!world.GetComponent<Chapter1MonsterVerification>())world.gameObject.AddComponent<Chapter1MonsterVerification>();
            // No terrain, camera, player, controller or combat catalog reconstruction.
            EditorUtility.SetDirty(world);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("CHAPTER1_MONSTERS_APPLIED "+catalog.clips.Length+" clips / "+catalog.clips.Sum(c=>c.frames.Length)+" sprites");
        }
        static Chapter1MonsterClip Import(Entry e)
        {
            string path=Art+"/"+e.species+"/"+e.motion+".png";
            if(!File.Exists(path))throw new FileNotFoundException("Restore monster ZIP first",path);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit=e.ppu;importer.filterMode=FilterMode.Point;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true;importer.maxTextureSize=2048;
            var pivot=new Vector2(e.pivot[0],e.pivot[1]);
            var sprites=new SpriteMetaData[e.seconds.Length];
            for(int i=0;i<sprites.Length;i++)sprites[i]=new SpriteMetaData
            { name=e.species+"_"+e.motion+"_"+(i+1).ToString("D2"),rect=new Rect(i%e.columns*128,(e.rows-1-i/e.columns)*128,128,128),alignment=(int)SpriteAlignment.Custom,pivot=pivot };
#pragma warning disable 618
            importer.spritesheet=sprites;
#pragma warning restore 618
            importer.SaveAndReimport();
            var frames=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
            if(frames.Length!=e.seconds.Length || frames.Any(s=>s.rect.size!=new Vector2(128,128) || s.pivot!=pivot*128 || s.pixelsPerUnit!=32))throw new InvalidOperationException("Invalid sprite slicing: "+path);
            return new Chapter1MonsterClip{species=e.species,motion=e.motion,displayName=e.displayName,frames=frames,seconds=e.seconds,loop=e.loop};
        }
        public static void OpenAndPlay()
        {
            Apply();
            EditorApplication.delayCall+=()=>{EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.EnterPlaymode();};
        }
    }
}
