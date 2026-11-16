using System;
using System.IO;
using System.Linq;
using Deviant.Combat.Contracts;
using ForestCombatV2;
using Stage1VillageExperiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Editor
{
    // Batch capture of one double jump in the current village scene, frame by frame, with the jump stretch and
    // afterimages on: outputs/jump-motion/jump-NN.png and jump.json (player height, scale, echo count per frame).
    //   Unity -batchmode -projectPath unity -executeMethod Deviant.Combat.Editor.JumpMotionPreview.Verify
    [InitializeOnLoad]
    public static class JumpMotionPreview
    {
        const string Request = "JumpMotionPreview.Request";
        const string Scene = "Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity";
        const float Step = 1f / 60;
        static double deadline, quietUntil;
        static int frame; static bool ready; // statics reset on the play-mode domain reload: false = set up first
        static CombatArena.ManualSimulation driver;
        static string log;
        static string Output => Path.GetFullPath("../outputs/jump-motion");
        static JumpMotionPreview() { EditorApplication.update += Tick; }
        public static void Verify()
        {
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Use Unity 6000.6.5f1.");
            SessionState.SetString(Request, "verify"); Directory.CreateDirectory(Output); quietUntil = 0;
        }
        static void Tick()
        {
            if (SessionState.GetString(Request, "").Length == 0) return;
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating) { quietUntil = 0; return; }
                    if (quietUntil == 0) { quietUntil = EditorApplication.timeSinceStartup + 3; return; }
                    if (EditorApplication.timeSinceStartup < quietUntil) return;
                    log = "";
                    EditorSceneManager.OpenScene(Scene);
                    deadline = EditorApplication.timeSinceStartup + 180;
                    EditorApplication.EnterPlaymode(); return;
                }
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 180;
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Jump motion scene timed out.");
                var arena = Object.FindAnyObjectByType<CombatArena>();
                if (!arena || !arena.Player) return;
                var world = Object.FindAnyObjectByType<ForestCombatWorld>();
                var art = world ? world.GetComponent<VillagePresentation>() : null;
                if (!world || !world.Ready || !art || !art.Ready) return;
                if (!ready || driver == null)
                {
                    ready = true; log = "";
                    driver = arena.AcquireManualSimulation();
                    // Keep monsters out of the shot; the jump only needs the player and the village.
                    foreach (var enemy in arena.enemies) if (enemy) { enemy.Position = arena.Player.Position + Vector2.right * 40; enemy.velocity = Vector2.zero; }
                    Physics2D.SyncTransforms();
                    for (int i = 0; i < 30; i++) { driver.Step(Step); arena.Presentation.Draw(Step); }
                    frame = 0; return;
                }
                // 0: first jump, 13 (~0.22 s): second jump in the air, then fall and land.
                var press = frame == 0 || frame == 13 ? CombatButtons.Jump : CombatButtons.None;
                driver.Step(Step, new CombatInput(0, press));
                arena.Presentation.Draw(Step);
                var visual = arena.Player.visual;
                log += $"{{\"frame\":{frame},\"y\":{arena.Player.Position.y:0.000},\"vy\":{arena.Player.velocity.y:0.00},\"scaleY\":{visual.transform.localScale.y:0.000},\"echoes\":{arena.Presentation.ActiveJumpAfterimages},\"clip\":\"{arena.Presentation.CurrentClip}\"}},\n";
                Capture(arena, art, $"jump-{frame:00}");
                if (++frame >= 72)
                {
                    File.WriteAllText(Path.Combine(Output, "jump.json"), "[\n" + log.TrimEnd(',', '\n') + "\n]\n");
                    Finish(true);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false); }
        }
        static void Capture(CombatArena arena, VillagePresentation art, string name)
        {
            art.RefreshForCapture();
            var camera = arena.ViewCamera; var previous = camera.targetTexture; var rect = camera.rect; var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1); camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; camera.rect = rect; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image);
            }
        }
        static void Finish(bool success)
        {
            driver?.Dispose(); driver = null; SessionState.SetString(Request, "");
            EditorApplication.ExitPlaymode();
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
