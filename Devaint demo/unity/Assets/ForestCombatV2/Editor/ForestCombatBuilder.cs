using System;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using Deviant.Game.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForestCombatV2.Editor
{
    public static class ForestCombatBuilder
    {
        public const string Root="Assets/ForestCombatV2";
        public const string Scene="Assets/ForestCombatV2/Scenes/Chapter1ForestCombat.unity";
        static void ImportImage(string file,bool sprite,Vector2 pivot)
        {
            AssetDatabase.ImportAsset(file,ImportAssetOptions.ForceSynchronousImport);
            var ti=(TextureImporter)AssetImporter.GetAtPath(file);
            ti.textureType=sprite?TextureImporterType.Sprite:TextureImporterType.Default;
            ti.spriteImportMode=SpriteImportMode.Single;ti.spritePixelsPerUnit=32;
            var settings=new TextureImporterSettings();ti.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=pivot;ti.SetTextureSettings(settings);
            ti.filterMode=FilterMode.Point;ti.mipmapEnabled=false;ti.isReadable=!sprite;ti.npotScale=TextureImporterNPOTScale.None;
            ti.textureCompression=TextureImporterCompression.Uncompressed;ti.maxTextureSize=8192;ti.alphaIsTransparency=true;ti.SaveAndReimport();
        }
        public static void Create()
        {
            CreateAt(Root,Scene);
        }
        public static void CreateAt(string root,string scenePath)
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var json=AssetDatabase.LoadAssetAtPath<TextAsset>(root+"/Data/ForestWorld.json");
            var data=JsonUtility.FromJson<ForestData>(json.text);
            ImportImage(root+"/Art/Terrain32.png",false,Vector2.zero);
            foreach(var l in data.layers)ImportImage(root+"/Art/"+l.id+".png",true,Vector2.one*.5f);
            foreach(var m in data.monsters)if(File.Exists(Root+"/Art/Monsters/"+m.id+".png"))ImportImage(Root+"/Art/Monsters/"+m.id+".png",true,new Vector2(.5f,(m.height-m.baseline)/(float)m.height));
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var arena=new GameObject("Chapter1 combat runtime").AddComponent<CombatArena>();
            var diagnostics=arena.gameObject.AddComponent<CombatDiagnostics>();diagnostics.arena=arena;
            diagnostics.uiFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Deviant/UI/Fonts/Pretendard-Regular.ttf");
            diagnostics.showPanel=false;arena.showBoxes=false;
            var camera=new GameObject("Chapter1 map camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(15,-8.4375f,-10);camera.orthographic=true;camera.orthographicSize=540f/64;
            camera.backgroundColor=new Color32(85,75,58,255);camera.clearFlags=CameraClearFlags.SolidColor;
            var world=new GameObject("Chapter1 layout v2 - forest adapter").AddComponent<ForestCombatWorld>();
            world.arena=arena;world.diagnostics=diagnostics;world.sceneCamera=camera;world.fixedStepsPerSecond=120;
            world.spriteMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Deviant/World/Data/OriginalPixelUnlit.mat");
            world.playerSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Deviant/Combat/Resources/CharacterConfirmed/base_sword/F006.png");
            if(!world.playerSprite || !world.spriteMaterial)throw new InvalidOperationException("Approved character/material missing");
            world.worldJson=json;world.terrainAtlas=AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/Art/Terrain32.png");
            world.roundedTerrainJson=AssetDatabase.LoadAssetAtPath<TextAsset>(root+"/Data/RoundedTerrain.json");
            world.roundedTerrainAtlas=AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/Art/RoundedTerrain.png");
            if((bool)world.roundedTerrainJson!=(bool)world.roundedTerrainAtlas)
                throw new InvalidOperationException("Restore both the rounded Chapter1 atlas and its layout before rebuilding the scene.");
            world.backdropSprites=data.layers.Select(l=>AssetDatabase.LoadAssetAtPath<Sprite>(root+"/Art/"+l.id+".png")).ToArray();
            world.monsterSprites=data.monsters.Select(m=>AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Monsters/"+m.id+".png")).ToArray();
            world.monsterAnimations=AssetDatabase.LoadAssetAtPath<Chapter1MonsterCatalog>(Chapter1MonsterImporter.CatalogPath);
            world.monsterAttackEffects=AssetDatabase.LoadAssetAtPath<Chapter1AttackEffectCatalog>(Chapter1AttackEffectImporter.CatalogPath);
            world.gameObject.AddComponent<Chapter1MonsterTestSettings>().world=world;
            if(world.monsterAnimations)world.monsterSprites=data.monsters.Select((m,i)=>world.monsterAnimations.Find(m.id,"move")?.frames[0] ?? world.monsterSprites[i]).ToArray();
            world.gameObject.AddComponent<ForestRuntimeVerification>();
            if(world.backdropSprites.Any(s=>!s)||world.monsterSprites.Any(s=>!s))throw new InvalidOperationException("Missing forest art");
            EditorSceneManager.SaveScene(scene,scenePath);AssetDatabase.SaveAssets();Debug.Log("FOREST_COMBAT_SCENE_READY "+scenePath);
        }
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(Scene);}
        static string Output(string fallback)
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-forestBuildOutput");return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:fallback);
        }
        public static void BuildWindows()
        {
            Create();string output=Output("../ForestCombatV2-Windows/ForestCombat.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
            var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(r.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Forest Windows build: "+r.summary.result);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-result.json"),"{\"pass\":true,\"target\":\"Windows\",\"unity\":\""+Application.unityVersion+"\",\"scene\":\""+Scene+"\"}");
        }
        public static void BuildWebGL()
        {
            Create();string output=Output("../ForestCombatV2-WebGL");Directory.CreateDirectory(output);
            var old=PlayerSettings.WebGL.compressionFormat;bool oldDecompress=PlayerSettings.WebGL.decompressionFallback;
            try{PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;PlayerSettings.WebGL.decompressionFallback=false;
                var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output,target=BuildTarget.WebGL,options=BuildOptions.None});
                if(r.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Forest WebGL build: "+r.summary.result);
            }finally{PlayerSettings.WebGL.compressionFormat=old;PlayerSettings.WebGL.decompressionFallback=oldDecompress;AssetDatabase.SaveAssets();}
        }
    }
}
