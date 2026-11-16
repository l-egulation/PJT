using System;
using System.IO;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Stage1VillageExperiment.Editor
{
    [InitializeOnLoad] public static class VillageTeamMapReview
    {
        const string Key="Village.TeamMapReview";
        static double deadline,quiet;
        static VillageTeamMapReview(){EditorApplication.update+=Tick;}
        public static void Apply()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Use Unity 6000.6.5f1");
            var scene=EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();
            if(!world||!world.proceduralRooms)throw new InvalidOperationException("The existing team procedural scene is required.");
            var art=world.GetComponent<VillagePresentation>();if(!art)art=world.gameObject.AddComponent<VillagePresentation>();art.teamWorld=world;
            if(!world.GetComponent<VillageArtTransition>())world.gameObject.AddComponent<VillageArtTransition>();
            var viewport=world.GetComponent<VillagePixelViewport>();if(!viewport)viewport=world.gameObject.AddComponent<VillagePixelViewport>();viewport.sourceCamera=world.sceneCamera;
            if(!world.GetComponent<VillageTeamMapSmoke>())world.gameObject.AddComponent<VillageTeamMapSmoke>();
            EditorSceneManager.SaveScene(scene);Debug.Log("VILLAGE_TEAM_ART_APPLIED");
        }
        public static void Run(){Apply();SessionState.SetBool(Key,true);deadline=EditorApplication.timeSinceStartup+300;quiet=0;}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(deadline==0)deadline=EditorApplication.timeSinceStartup+300;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Team map review startup timed out.");
                if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                if(!EditorApplication.isPlaying){if(EditorApplication.isPlayingOrWillChangePlaymode)return;if(quiet==0)quiet=EditorApplication.timeSinceStartup+2;if(EditorApplication.timeSinceStartup>=quiet)EditorApplication.EnterPlaymode();return;}
                var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();var art=world?world.GetComponent<VillagePresentation>():null;
                if(!world||!world.Ready||!art||!art.Ready)return;
                VillageTeamMapSmoke.Check(world,art,Path.GetFullPath("../outputs/village/team-map/editor"));Finish(0);
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }
        static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.Exit(code);}
        public static void BuildWindows()
        {
            string requested=Environment.GetEnvironmentVariable("STAGE1_WINDOWS_OUTPUT");
            string output=Path.GetFullPath(string.IsNullOrEmpty(requested)?"../outputs/village/team-map/Windows/Stage1Village.exe":requested);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Chapter1RunHost.TitleScene,Chapter1MapSelector.ProceduralScene},
                locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Team map build failed.");
            Debug.Log("VILLAGE_TEAM_BUILD_READY "+output);
        }
    }
}
