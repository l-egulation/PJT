using System;
using Deviant.Stage1Monsters.Editor;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game.Editor
{
    [InitializeOnLoad] public static class Stage1VillageMonsterInstaller
    {
        const string VerifyKey="Stage1VillageMonsters.VerifyRequested";
        static Stage1VillageMonsterInstaller()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-stage1VillageCaptureRoot")>=0)
            {
                EditorApplication.update+=()=>{if(EditorApplication.isPlaying)EditorApplication.QueuePlayerLoopUpdate();};
                EditorApplication.delayCall+=()=>{if(!SessionState.GetBool(VerifyKey,false))PlayAndVerify();};
            }
        }
        [MenuItem("Tools/deviant/Stage 1 Monsters/Apply To Village Game")]
        public static void Apply() { TryApply(); }
        public static bool TryApply(Func<bool> confirmSave=null)
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Use Unity 6000.6.5f1");
            if(EditorApplication.isPlayingOrWillChangePlaymode)return false;
            bool mayOpen=confirmSave!=null ? confirmSave() :
                Application.isBatchMode || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            if(!mayOpen)return false;
            var scene=EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();
            if(!world || !world.proceduralRooms)throw new InvalidOperationException("Team village scene is required.");
            var content=world.GetComponent<Stage1VillageMonsterContent>();
            if(!content)content=world.gameObject.AddComponent<Stage1VillageMonsterContent>();
            content.motions=AssetDatabase.LoadAssetAtPath<Stage1MonsterCatalog>("Assets/Deviant/Stage1Monsters/Data/ApprovedStage1Monsters.asset");
            content.content=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Deviant/Stage1Monsters/Game/Data/VillageMonsterContent.json");
            content.effects=AssetDatabase.LoadAssetAtPath<Stage1MonsterEffectCatalog>(Stage1MonsterEffectImporter.CatalogPath);
            if(!content.motions || !content.content)throw new InvalidOperationException("Export/import the approved monster assets first.");
            if(!world.GetComponent<Stage1VillageMonsterReview>())world.gameObject.AddComponent<Stage1VillageMonsterReview>();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("STAGE1_VILLAGE_MONSTERS_APPLIED: four species, no boss");
            return true;
        }
        [MenuItem("Tools/deviant/Stage 1 Monsters/Play Village Game")]
        public static void Play() { TryPlay(); }
        public static bool TryPlay(Func<bool> confirmSave=null)
        {
            if(!TryApply(confirmSave))return false;
            EditorApplication.EnterPlaymode();return true;
        }
        public static void PlayAndVerify()
        {
            if(SessionState.GetBool(VerifyKey,false))return;
            if(!TryApply())return;
            SessionState.SetBool(VerifyKey,true);
            var asm=typeof(EditorWindow).Assembly;
            var sizesType=asm.GetType("UnityEditor.GameViewSizes");
            var singleton=sizesType.GetProperty("instance",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.FlattenHierarchy).GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(singleton,new[]{Enum.Parse(asm.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
            var size=Activator.CreateInstance(asm.GetType("UnityEditor.GameViewSize"),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,new object[]{Enum.Parse(asm.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),1920,1080,"Village monster review"},null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            var view=EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
            view.GetType().GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).SetValue(view,count-1);view.Repaint();
            EditorApplication.delayCall+=()=>EditorApplication.EnterPlaymode();
        }
    }
}
