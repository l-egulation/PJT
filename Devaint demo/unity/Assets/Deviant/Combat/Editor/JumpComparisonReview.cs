using System;
using System.IO;
using System.Reflection;
using System.Text;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Deviant.Combat.Contracts;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Editor
{
    // Editor-only A/B controls and live automatic-simulation measurement. No scene or gameplay data is saved.
    [InitializeOnLoad]
    public static class JumpComparisonReview
    {
        const string Request = "JumpComparisonReview.Run";
        static double quiet;
        static CombatArena arena;
        static JumpMotionPresentation art;
        static ActorRenderInterpolation interpolation;
        static float started, nextJump, releaseAt, secondJumpAt;
        static int jumpNumber;
        static Camera camera;
        static int phase = -1;
        static IDisposable tutorialProfile;
        static readonly string[] Modes = { "original", "no-echo", "no-stretch", "plain", "plain-no-interpolation" };
        static readonly StringBuilder rows = new StringBuilder();
        static string Output => Path.GetFullPath("../outputs/jump-comparison-live");
        static JumpComparisonReview()
        {
            EditorApplication.update += Wait;
            EditorApplication.playModeStateChanged += TutorialPlayState;
        }
        [MenuItem("Deviant/Review/Play current tutorial with jump and wall art")]
        public static void OpenTutorial()
        {
            SessionState.SetBool(Request + ".Tutorial", true);
            SessionState.SetBool(Request + ".Manual", true);
            SessionState.SetBool(Request, true); quiet = 0;
        }
        static void TutorialPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Request + ".Tutorial", false))
                tutorialProfile = Chapter1ProfileHost.UseTemporaryStore(new Deviant.Run.MemoryProfileStore());
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                tutorialProfile?.Dispose(); tutorialProfile = null;
                SessionState.SetBool(Request + ".Tutorial", false);
            }
        }
        [MenuItem("Deviant/Review/Jump A-B test")]
        public static void Run()
        {
            SessionState.SetBool(Request + ".Tutorial", false);
            SessionState.SetBool(Request + ".Manual", false);
            SessionState.SetBool(Request, true); quiet = 0;
            if (EditorApplication.isPlaying) Start();
        }
        [MenuItem("Deviant/Review/Jump manual comparison")]
        public static void OpenManual()
        {
            SessionState.SetBool(Request + ".Tutorial", false);
            SessionState.SetBool(Request + ".Manual", true);
            SessionState.SetBool(Request, true); quiet = 0;
            if (EditorApplication.isPlaying) Start();
        }
        static void Wait()
        {
            if (!SessionState.GetBool(Request, false)) return;
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) { quiet = 0; return; }
                if (quiet == 0) { quiet = EditorApplication.timeSinceStartup + 3; return; }
                if (EditorApplication.timeSinceStartup < quiet) return;
                const string villageScene = "Assets/Deviant/Hub/Scenes/StartingVillage.unity";
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(villageScene);
                EditorSceneManager.OpenScene(villageScene);
                EditorApplication.EnterPlaymode(); quiet = double.MaxValue; return;
            }
            Start();
        }
        static void Start()
        {
            var village = Object.FindAnyObjectByType<StartingVillageWorld>();
            if (!village || !village.Ready) return;
            arena = village.World.arena;
            art = (JumpMotionPresentation)typeof(CharacterPresenter).GetField("jumpArt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(arena.Presentation);
            interpolation = arena.Player.GetComponent<ActorRenderInterpolation>();
            SessionState.SetBool(Request, false);
            if (SessionState.GetBool(Request + ".Manual", false))
            {
                arena.Player.UseTestInput = false;
                SetMode(3);
                if (SessionState.GetBool(Request + ".Tutorial", false))
                {
                    foreach (var window in Resources.FindObjectsOfTypeAll<JumpComparisonWindow>()) window.Close();
                    EditorApplication.ExecuteMenuItem("Window/General/Game");
                    Debug.Log("TUTORIAL_TRAVERSAL_READY " + Application.dataPath + " | temporary profile; saved progress untouched; plain jump + interpolation; approved wall art");
                }
                else JumpComparisonWindow.ShowReview();
                return;
            }
            arena.Player.UseTestInput = true;
            camera = village.World.View;
            rows.Clear(); rows.AppendLine("mode,frame,realTime,dt,simTime,fixedTime,logicalY,visualY,scaleY,echoes,grounded,clip,velocityY,renderTime,screenY,sprite,ppu,bodyHeight,form");
            phase = -1; started = Time.realtimeSinceStartup + 3;
            Application.onBeforeRender -= Sample; Application.onBeforeRender += Sample;
        }
        static void Sample()
        {
            if (!arena || !EditorApplication.isPlaying) { Application.onBeforeRender -= Sample; return; }
            float age = Time.realtimeSinceStartup - started;
            if (age < 0) return;
            // Repeat the same single/double-jump input in both mode orders after warm-up.
            int block = (int)(age / 8);
            int current = block < Modes.Length ? block : Modes.Length * 2 - block - 1;
            if (block >= Modes.Length * 2)
            {
                Application.onBeforeRender -= Sample;
                Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "frames-v2.csv"), rows.ToString());
                arena.Player.UseTestInput = false;
                SetMode(3);
                Debug.Log("JUMP_COMPARISON_COMPLETE " + Output);
                JumpComparisonWindow.ShowReview();
                return;
            }
            if (block != phase)
            {
                phase = block; SetMode(current); nextJump = age + .8f;
                releaseAt = secondJumpAt = float.PositiveInfinity; jumpNumber = 0;
            }
            if (age >= nextJump && arena.Player.grounded)
            {
                arena.Player.SubmitInput(new CombatInput(0, CombatButtons.Jump));
                jumpNumber++; nextJump = age + 1.6f; releaseAt = age + .2f;
                secondJumpAt = jumpNumber % 2 == 0 ? age + .28f : float.PositiveInfinity;
            }
            if (age >= releaseAt)
            {
                arena.Player.SubmitInput(new CombatInput(0, CombatButtons.None, released: CombatButtons.Jump));
                releaseAt = float.PositiveInfinity;
            }
            if (age >= secondJumpAt && !arena.Player.grounded)
            {
                arena.Player.SubmitInput(new CombatInput(0, CombatButtons.Jump));
                secondJumpAt = float.PositiveInfinity; releaseAt = age + .15f;
            }
            var p = arena.Player;
            var sprite = p.visual.sprite;
            rows.AppendLine(FormattableString.Invariant($"{Modes[current]},{Time.frameCount},{Time.realtimeSinceStartup:F6},{Time.unscaledDeltaTime:F6},{arena.Now:F6},{Time.fixedTimeAsDouble:F6},{p.Position.y:F6},{p.visual.transform.position.y:F6},{p.visual.transform.localScale.y:F6},{art.ActiveAfterimages},{p.grounded},{arena.Presentation.CurrentClip},{p.VelocityY:F6},{Time.timeAsDouble:F6},{camera.WorldToScreenPoint(p.visual.transform.position).y:F6},{sprite?.name},{sprite?.pixelsPerUnit:F6},{sprite?.bounds.size.y:F6},{arena.Presentation.CurrentForm}"));
        }
        public static void SetMode(int mode)
        {
            if (art == null) return;
            art.Reset();
            art.AfterimagesEnabled = mode == 0 || mode == 2;
            art.StretchEnabled = mode == 0 || mode == 1;
            if (interpolation) interpolation.enabled = mode != 4;
        }
    }
    public sealed class JumpComparisonWindow : EditorWindow
    {
        public static void ShowReview() => GetWindow<JumpComparisonWindow>("점프 표현 비교");
        void OnGUI()
        {
            GUILayout.Label("같은 점프 수치 · 표시 효과만 비교");
            GUILayout.Label(Application.dataPath);
            if (GUILayout.Button("1. 기존: 잔상 + 늘어남")) JumpComparisonReview.SetMode(0);
            if (GUILayout.Button("2. 잔상 끄기 / 늘어남 유지")) JumpComparisonReview.SetMode(1);
            if (GUILayout.Button("3. 늘어남 끄기 / 잔상 유지")) JumpComparisonReview.SetMode(2);
            if (GUILayout.Button("4. 잔상과 늘어남 모두 끄기")) JumpComparisonReview.SetMode(3);
            if (GUILayout.Button("5. 모두 끄기 + 이동 보간 끄기")) JumpComparisonReview.SetMode(4);
            GUILayout.Label("게임 화면 클릭 후 Space로 점프");
        }
    }
}
