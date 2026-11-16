using Deviant.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForestCombatV2
{
    // Survives scene loads so one RunFlow owns the run from the title to the result screen.
    public sealed class Chapter1RunHost : MonoBehaviour
    {
        public const string TitleScene = "Assets/ForestCombatV2/Scenes/Chapter1Title.unity";
        public const string ChapterScene = Chapter1MapSelector.ProceduralScene;
        public const string VillageScene = "Assets/Deviant/Hub/Scenes/StartingVillage.unity";
        public static Chapter1RunHost Instance { get; private set; }
        public RunFlow Flow { get; private set; }
        public bool ChapterTwoPending { get; private set; }
        public static System.Func<bool> ContinueChapter { get; set; }
        public static System.Func<bool> EnterChapterTwoForDebug { get; set; }
        public static double Now => Time.timeAsDouble;
        RunSession bound;

        public static Chapter1RunHost Ensure()
        {
            if (Instance) return Instance;
            var go = new GameObject("Chapter1 run host");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<Chapter1RunHost>();
            var economy = Resources.Load<TextAsset>("Chapter1Economy");
            Instance.Flow = new RunFlow(Chapter1ProceduralMap.NextSeed, economy ? JsonUtility.FromJson<EconomyData>(economy.text) : null);
            return Instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        // sceneLoaded runs after Awake and before Start, so the seed is in place before the map is generated.
        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var world in root.GetComponentsInChildren<ForestCombatWorld>(true))
                {
                    if (!world.proceduralRooms || world.dummyTrainingOnly || world.GetComponent<Chapter1RunBinding>()) continue;
                    var host = Ensure();
                    var run = host.Flow.Current;
                    // A run prepared by the title is used once; any other chapter load (editor, map selector) starts its own run.
                    if (run == null || run.Ended || run == host.bound) run = host.Flow.StartNewRun(Now, world.proceduralSeed >= 0 ? world.proceduralSeed : (int?)null);
                    host.bound = run;
                    world.proceduralSeed = run.Seed;
                    world.gameObject.AddComponent<Chapter1RunBinding>().Initialize(world, host);
                }
        }

        public void StartNewRun(RunStartPlan plan = null)
        {
            PrepareNewRun(plan);
            EnterPreparedRun();
        }

        public void PrepareNewRun(RunStartPlan plan)
        {
            ChapterTwoPending = false;
            Flow.StartNewRun(Now, null, plan);
        }

        // background: loads while a loading screen keeps drawing (the departure's loading window).
        public void EnterPreparedRun() => EnterPreparedRun(false);
        public void EnterPreparedRun(bool background)
        {
            if (background) ChapterSceneFade.LoadSceneBehind(ChapterScene);
            else SceneManager.LoadScene(ChapterScene, LoadSceneMode.Single);
        }

        // The starting village is where every run begins and ends; runs start only from its departure sign.
        // loading: through the loading window (「우물 마을」), as the departure goes the other way; the title's start
        // goes straight in (the opening's own notebook follows).
        public void GoToVillage(bool loading = true)
        {
            ChapterTwoPending = false;
            if (loading) ChapterSceneFade.Load(LoadingPlace.Village, () => ChapterSceneFade.LoadSceneBehind(VillageScene));
            else SceneManager.LoadScene(VillageScene, LoadSceneMode.Single);
        }

        public void GoToTitle(bool chapterTwoPending = false)
        {
            ChapterTwoPending = chapterTwoPending;
            SceneManager.LoadScene(TitleScene, LoadSceneMode.Single);
        }

        // Drop every run-owned reference without settling an abandoned run into the fresh profile.
        public void DiscardCurrentRun()
        {
            Flow.DiscardCurrentRun();
            bound = null;
            ChapterTwoPending = false;
        }

        public void LeaveChapterTwoPending() => ChapterTwoPending = false;

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
