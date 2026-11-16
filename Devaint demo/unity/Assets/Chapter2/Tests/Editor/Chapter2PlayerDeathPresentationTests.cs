using System.Collections;
using Deviant.Chapter2.Editor;
using Deviant.Integration;
using Deviant.Run;
using ForestCombatV2;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    public sealed class Chapter2PlayerDeathPresentationTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }
        // The pause menu's durability toggle shares the result's menu; it must not sit over the result rows.
        static bool DurabilitySettingVisible() => System.Array.Exists(Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None),
            text => text.gameObject.activeInHierarchy && text.text.StartsWith("내구도:"));
        // Renders the scene camera with every overlay canvas (result paper and the black under it) into outputs/.
        static void CaptureResult(Chapter2World world, string name)
        {
            var camera = world.arena.ViewCamera; const int width = 1920, height = 1080;
            var canvases = System.Array.FindAll(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None),
                c => c.isRootCanvas && c.isActiveAndEnabled && c.renderMode == RenderMode.ScreenSpaceOverlay);
            var target = camera.targetTexture; var rect = camera.rect;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 1; }
                Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                System.IO.Directory.CreateDirectory("../outputs/qa2-result");
                System.IO.File.WriteAllBytes("../outputs/qa2-result/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
                camera.targetTexture = target; camera.rect = rect; RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt); Object.Destroy(image);
            }
        }
        [UnityTest] public IEnumerator ChapterTwoClearBlacksOutLikeADeathBeforeItsResult()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            var host = Chapter1RunHost.Ensure(); host.PrepareNewRun(Chapter1ProfileHost.Service.PlanRun(new[] { "sword", "blunt" }));
            host.Flow.Current.MarkBossDefeated(); Assert.That(host.Flow.TryAdvanceChapter(Chapter1RunHost.Now), Is.True);
            SceneManager.LoadScene(Chapter2Tools.ScenePath, LoadSceneMode.Single);
            Chapter2World world = null; float deadline = Time.realtimeSinceStartup + 45;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null; world = Object.FindAnyObjectByType<Chapter2World>(); if (world && world.Ready) break;
            }
            Assert.That(world && world.Ready, Is.True);
            world.WarpToRoom("M"); yield return null;
            var output = world.arena.GetComponent<PlayerDeathPresentation>(); Assert.That(output, Is.Not.Null);
            // The exit arch calls this once its portcullis is open (Chapter2BossExit); the boss fight itself is covered elsewhere.
            typeof(Chapter2World).GetMethod("CompleteThroughExit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(world, null);
            float started = Time.realtimeSinceStartup;
            Assert.That(world.Complete, Is.True); Assert.That(world.ShowingResult, Is.False, "the result waits for the black");
            Assert.That(world.arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.True, "the player holds still");
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(world.ShowingResult, Is.False); Assert.That(output.FadeOpacity, Is.GreaterThan(0), "a clear has no death pose to wait for");
            Assert.That(output.KickPixels.X, Is.Zero, "a clear has no hit kick"); Assert.That(world.arena.Player.Dead, Is.False);
            deadline = Time.realtimeSinceStartup + 8;
            while (!world.ShowingResult && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world.ShowingResult, Is.True);
            Assert.That(Time.realtimeSinceStartup - started, Is.GreaterThanOrEqualTo(Deviant.Presentation.PlayerDeathSequence.FadeSeconds + Deviant.Presentation.PlayerDeathSequence.BlackHoldSeconds - .05f));
            yield return null; yield return null;
            Assert.That(output.FadeOpacity, Is.EqualTo(1)); Assert.That(output.OverlayOrder, Is.EqualTo(90), "the result rises above the black");
            Assert.That(host.Flow.Current.Result.Value.Outcome, Is.EqualTo(RunOutcome.ChapterCleared));
            Assert.That(DurabilitySettingVisible(), Is.False, "the durability toggle does not cover the result rows");
            CaptureResult(world, "chapter2-clear-result");
        }
        [UnityTest] public IEnumerator ExistingChapterTwoResultWaitsForBlackAndUsesTheCurrentGui()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            var host = Chapter1RunHost.Ensure(); host.PrepareNewRun(Chapter1ProfileHost.Service.PlanRun(new[] { "sword", "blunt" }));
            host.Flow.Current.MarkBossDefeated(); Assert.That(host.Flow.TryAdvanceChapter(Chapter1RunHost.Now), Is.True);
            SceneManager.LoadScene(Chapter2Tools.ScenePath, LoadSceneMode.Single);
            Chapter2World world = null; float deadline = Time.realtimeSinceStartup + 45;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null; world = Object.FindAnyObjectByType<Chapter2World>(); if (world && world.Ready) break;
            }
            Assert.That(world && world.Ready, Is.True);
            world.WarpToRoom("M"); yield return null;
            var output = world.arena.GetComponent<PlayerDeathPresentation>(); Assert.That(output, Is.Not.Null);
            Assert.That(world.arena.Player.GetComponent<Chapter1PlayerHitFeedback>().enabled, Is.True);
            world.arena.Player.health = 0; world.arena.Player.Die();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(world.ShowingResult, Is.False); Assert.That(output.FadeOpacity, Is.Zero);
            deadline = Time.realtimeSinceStartup + 8;
            while (!world.ShowingResult && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null; yield return null;
            Assert.That(output.ReadyForResult, Is.True); Assert.That(output.FadeOpacity, Is.EqualTo(1));
            Assert.That(world.ShowingResult, Is.True); Assert.That(world.arena.Controls.Current.SimulationPaused, Is.True);
            Assert.That(output.OverlayOrder, Is.EqualTo(90), "The existing result GUI is above the retained black screen.");
            Assert.That(host.Flow.Current.Result.Value.Outcome, Is.EqualTo(RunOutcome.Died));
            Assert.That(DurabilitySettingVisible(), Is.False, "the durability toggle does not cover the result rows");
            CaptureResult(world, "chapter2-death-result");
            // The result button must stay inside its paper panel at the current canvas scale.
            var book = System.Array.Find(Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None),
                value => value.name == "결과" && value.gameObject.activeInHierarchy);
            Assert.That(book, Is.Not.Null);
            var button = book.parent.Find("계속하기") as RectTransform;
            Assert.That(button, Is.Not.Null);
            var corners = new Vector3[4]; button.GetWorldCorners(corners);
            foreach (var corner in corners)
                Assert.That(book.rect.Contains(book.InverseTransformPoint(corner)), Is.True, "Village return stays inside the result paper.");
        }
    }
}
