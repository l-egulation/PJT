using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ForestCombatV2.Tests
{
    // Local, explicit marker only; also usable from the test menu in an already open Editor.
    [InitializeOnLoad]
    public static class ClockBossTestRunner
    {
        static readonly string Request=Path.GetFullPath("../outputs/clock-boss/tests.request");
        static readonly string Results=Path.GetFullPath("../outputs/clock-boss/health-phases.xml");
        const string Running="ClockBossTestRunner.Running";
        static TestRunnerApi api;
        static ClockBossTestRunner()
        {
            api=ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultsCallback());
            EditorApplication.update+=Poll;
        }
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty)return;
            if(!SessionState.GetBool(Running+".Refreshed",false))
            {
                SessionState.SetBool(Running+".Refreshed",true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                return;
            }
            SessionState.SetBool(Running+".Refreshed",false);
            File.Delete(Request);Run();
        }
        [MenuItem("Tools/Deviant/Chapter1/Verify Boss Health Phases")]
        public static void Run()
        {
            if(SessionState.GetBool(Running,false))return;
            Directory.CreateDirectory(Path.GetDirectoryName(Results));
            SessionState.SetBool(Running,true);
            api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{
                "ForestCombatV2.Tests.Chapter1BossInfoTests",
                "ForestCombatV2.Tests.ClockExecutionerChainImpactTests",
                "ForestCombatV2.Tests.Chapter1BossClearTests"}}));
        }
        sealed class ResultsCallback:ICallbacks
        {
            public void RunStarted(ITestAdaptor tests){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                if(!SessionState.GetBool(Running,false))return;
                SessionState.SetBool(Running,false);
                TestRunnerApi.SaveResultToFile(result,Results);
                Debug.Log($"CLOCK_BOSS_TESTS: {result.ResultState}, passed={result.PassCount}, failed={result.FailCount}");
            }
        }
    }
}
