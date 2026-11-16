using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Deviant.Presentation.Editor
{
    public static class HitFeedbackRuntimeCompile
    {
        // Runtime assembly validation, not a full packaged Player build.
        public static void CompileLinuxPlayerScripts()
            =>Compile(BuildTarget.StandaloneLinux64);
        public static void CompileRuntimeScripts()
        {
            Compile(BuildTarget.StandaloneLinux64);
            Compile(BuildTarget.StandaloneWindows64);
        }
        static void Compile(BuildTarget target)
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../runtime-script-compile",target.ToString()));Directory.CreateDirectory(output);
            var settings=new ScriptCompilationSettings{target=target,group=BuildTargetGroup.Standalone};
            var result=PlayerBuildInterface.CompilePlayerScripts(settings,output);
            if(result.assemblies==null||result.assemblies.Count==0)throw new InvalidOperationException("Player script compilation produced no assemblies.");
            foreach(string name in new[]{"Deviant.Combat.dll","Deviant.Control.dll","Deviant.Presentation.Unity.dll","Deviant.Integration.Unity.dll","Deviant.Game.Diagnostics.Unity.dll"})
                if(!result.assemblies.Any(p=>Path.GetFileName(p)==name))throw new InvalidOperationException("Player compilation omitted "+name);
            if(result.assemblies.Any(p=>Path.GetFileName(p).StartsWith("Deviant.CombatLab.",StringComparison.Ordinal)||Path.GetFileName(p).StartsWith("Deviant.Authoring.",StringComparison.Ordinal)))
                throw new InvalidOperationException("Default Player compilation unexpectedly included Lab/authoring assemblies.");
            Debug.Log("HIT_FEEDBACK_PLAYER_SCRIPTS_OK target="+target+" unity="+Application.unityVersion+" assemblies="+result.assemblies.Count);
        }
    }
}
