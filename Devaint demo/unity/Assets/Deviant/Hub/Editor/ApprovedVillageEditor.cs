#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deviant.Hub.Editor
{
    public static class ApprovedVillageEditor
    {
        public static void OpenAndPlay()
        {
            Open();
            EditorApplication.delayCall += EditorApplication.EnterPlaymode;
        }

        [MenuItem("Tools/deviant/Hub/Open Approved Starting Village")]
        public static void Open()
        {
            const string scene="Assets/Deviant/Hub/Scenes/StartingVillage.unity";
            EditorSceneManager.OpenScene(scene);
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            var game=Type.GetType("UnityEditor.GameView,UnityEditor");
            if(game!=null){EditorWindow.GetWindow(game).Show();UseFullHdGameView();}
            Debug.Log("APPROVED_VILLAGE_OPEN compact ground / 32 PPU; Play to enter. K cycles colour preview; F interacts.");
        }

        // The GUI redraws its text at the screen's whole-number multiple of 960x540, so a 960x540 Game view zoomed up
        // shows the low-resolution text blurred (playtest 2026-11-03). Opening the village picks a 1920x1080 Game view
        // (added once as "Deviant 1920x1080"), where the text is drawn at twice the size. Editor-only, best effort.
        public static void UseFullHdGameView()
        {
            const string Label="Deviant 1920x1080";
            try
            {
                var asm=typeof(EditorWindow).Assembly;
                var sizesType=asm.GetType("UnityEditor.GameViewSizes");
                var singleton=sizesType.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
                var group=sizesType.GetMethod("GetGroup").Invoke(singleton,new[]{Enum.Parse(asm.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
                var groupType=group.GetType();
                int count=(int)groupType.GetMethod("GetTotalCount").Invoke(group,null),index=-1;
                for(int i=0;i<count&&index<0;i++)
                {
                    var existing=groupType.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});
                    if((string)existing.GetType().GetProperty("baseText").GetValue(existing)==Label)index=i;
                }
                if(index<0)
                {
                    var size=Activator.CreateInstance(asm.GetType("UnityEditor.GameViewSize"),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,
                        new object[]{Enum.Parse(asm.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),1920,1080,Label},null);
                    groupType.GetMethod("AddCustomSize").Invoke(group,new[]{size});
                    index=(int)groupType.GetMethod("GetTotalCount").Invoke(group,null)-1;
                }
                var view=EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
                view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,index);
                view.Repaint();
            }
            catch(Exception error){Debug.LogWarning("Could not set the 1920x1080 Game view: "+error.Message);}
        }
    }
}
#endif
