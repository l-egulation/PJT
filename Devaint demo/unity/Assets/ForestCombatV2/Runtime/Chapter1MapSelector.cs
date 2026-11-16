using Deviant.Combat;
using Deviant.Game.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ForestCombatV2
{
    // Scene-local selector: loading another version disposes the previous combat world.
    public sealed class Chapter1MapSelector : MonoBehaviour
    {
        public const string GameScene="Assets/Deviant/Game/Scenes/deviant.unity";
        public const string OriginalScene="Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity";
        public const string Ver2Scene="Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1ForestVer2.unity";
        public const string ProceduralScene="Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity";
        public const string BossTestScene="Assets/MeadowDuskV3/MapVer2/Scenes/ClockExecutionerTest.unity";
        bool loading;
        public bool ExternalPresentation { get; set; }
        Font font;
        CombatDiagnostics compact;
        public static bool Supports(string path)=>path==ProceduralScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            // Map versions are a development tool; release play builds start from the title.
            if(!DevFeatures.Enabled)return;
            SceneManager.sceneLoaded-=Attach;
            SceneManager.sceneLoaded+=Attach;
            Attach(SceneManager.GetActiveScene(),LoadSceneMode.Single);
        }
        static void Attach(Scene scene,LoadSceneMode mode)
        {
            if(!Supports(scene.path))return;
            foreach(var root in scene.GetRootGameObjects())if(root.GetComponent<Chapter1MapSelector>())return;
            var go=new GameObject("Chapter1 map version selector");
            SceneManager.MoveGameObjectToScene(go,scene);go.AddComponent<Chapter1MapSelector>();
        }
        public bool Select(string path)
        {
            if(loading||!Supports(path)||path==gameObject.scene.path&&path!=ProceduralScene)return false;
            if(!Application.CanStreamedLevelBeLoaded(path)){Debug.LogError("Map scene is missing from Build Settings: "+path);return false;}
            loading=true;
            SceneManager.LoadSceneAsync(path,LoadSceneMode.Single);
            return true;
        }
        // Development shortcut: move within the current run instead of reloading a boss-only stage.
        public bool OpenBossTest()
        {
            if (loading) return false;
            ForestCombatWorld world = null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root.TryGetComponent(out ForestCombatWorld candidate)) { world = candidate; break; }
            return Chapter1DebugTravel.TryToBoss(world, out _);
        }
        public bool HandleKeyboard()=>false;
        void Update()=>HandleKeyboard();
        void OnGUI()
        {
            if(!DevFeatures.Enabled||ExternalPresentation||DebugGameMenu.Installed)return;
            if(!compact)compact=FindFirstObjectByType<CombatDiagnostics>();
            if(!font&&compact)font=compact.uiFont;
            var oldFont=GUI.skin.font;var oldDepth=GUI.depth;
            if(font)GUI.skin.font=font;GUI.depth=-100;
            float x=Mathf.Max(8,Screen.width-228),y=Screen.height-44;
            bool small=compact&&compact.CompactHud;
            if(GUI.Button(small?new Rect(Screen.width-128,Screen.height-30,118,20):new Rect(x,y,216,32),
                loading?"불러오는 중":"새 배치로 시작")&&!loading)Select(ProceduralScene);
            if(!loading&&GUI.Button(small?new Rect(Screen.width-128,Screen.height-54,118,20):new Rect(x,y-38,216,32),"보스방 이동 (F10)"))OpenBossTest();
            GUI.skin.font=oldFont;GUI.depth=oldDepth;
        }
    }
}
