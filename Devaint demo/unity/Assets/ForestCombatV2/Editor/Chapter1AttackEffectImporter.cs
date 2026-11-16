using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForestCombatV2.Editor
{
    public static class Chapter1AttackEffectImporter
    {
        public const string CatalogPath="Assets/ForestCombatV2/Data/ApprovedAttackEffects.asset";
        const string Art="Assets/ForestCombatV2/Art/ApprovedAttackEffects";
        [Serializable] sealed class Manifest { public Entry[] entries; }
        [Serializable] sealed class Entry { public string species;public float[] pivot,seconds; }
        static Sprite Import(string path,Vector2 pixels)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new FileNotFoundException("Run the pinned resources asset deployment first",path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
            importer.GetSourceTextureWidthAndHeight(out int width,out int height);
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(pixels.x/width,pixels.y/height);
            importer.SetTextureSettings(settings);importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        [MenuItem("Tools/Deviant/Meadow Dusk/Apply Approved Attack Effects and Placement")]
        public static void Apply()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/ForestCombatV2/Data/ApprovedAttackEffects.json"));
            var catalog=AssetDatabase.LoadAssetAtPath<Chapter1AttackEffectCatalog>(CatalogPath);
            if(!catalog){catalog=ScriptableObject.CreateInstance<Chapter1AttackEffectCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
            catalog.clips=manifest.entries.Select(entry=>new Chapter1AttackEffectClip
            {
                species=entry.species,seconds=entry.seconds,
                frames=Enumerable.Range(1,8).Select(i=>Import(Art+"/"+entry.species+"/"+i.ToString("D2")+".png",new Vector2(entry.pivot[0],entry.pivot[1]))).ToArray(),
                afterimages=entry.species=="05_boar" ? Enumerable.Range(1,8).Select(i=>Import(Art+"/05_boar/afterimages/"+i.ToString("D2")+".png",new Vector2(64,8))).ToArray() : new Sprite[0]
            }).ToArray();
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            // Retain all scene and combat settings; refresh the approved clean mouse attack sheet only.
            AssetDatabase.ImportAsset("Assets/ForestCombatV2/Art/Chapter1MonstersV4/01_field_mouse/attack.png",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            // Existing animation clips and idle slicing are already authored; keep them intact.
            var animations=AssetDatabase.LoadAssetAtPath<Chapter1MonsterCatalog>(Chapter1MonsterImporter.CatalogPath);
            if(!animations || animations.Find("01_field_mouse","attack").frames.Any(s=>!s))
                throw new InvalidOperationException("The scene's existing mouse attack sprite references must be restored first.");
            EditorSceneManager.OpenScene(MeadowDuskBuilder.Scene);
            var scene=EditorSceneManager.GetActiveScene();
            var world=UnityEngine.Object.FindFirstObjectByType<ForestCombatWorld>();
            world.monsterAttackEffects=catalog;
            var settings=world.GetComponent<Chapter1MonsterTestSettings>();
            if(!settings)settings=world.gameObject.AddComponent<Chapter1MonsterTestSettings>();
            settings.world=world;
            EditorUtility.SetDirty(settings);EditorUtility.SetDirty(world);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("APPROVED_MONSTER_EFFECTS_APPLIED mouse / mole / boar, F4 placement ready");
        }
    }
}
