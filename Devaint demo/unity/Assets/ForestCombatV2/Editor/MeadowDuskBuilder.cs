using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForestCombatV2.Editor
{
    public static class MeadowDuskBuilder
    {
        public const string Root="Assets/MeadowDuskV3";
        public const string Scene="Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity";
        public static void Create(){ForestCombatBuilder.CreateAt(Root,Scene);}
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(Scene);}
        public static void BuildWindows()
        {
            Create();var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-forestBuildOutput");
            string output=Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:"../MeadowDuskV3-Windows/MeadowCombat.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene,Chapter1MapSelector.GameScene,Chapter1MapSelector.Ver2Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Meadow Windows build: "+report.summary.result);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-result.json"),"{\"pass\":true,\"target\":\"Windows\",\"unity\":\""+Application.unityVersion+"\",\"scene\":\""+Scene+"\"}");
        }
    }
}
