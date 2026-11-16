using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // Renders the village camera at 960x540 along the lane into outputs/hub/capture for art review (not a pixel test).
    public sealed class StartingVillageCaptureTests
    {
        [UnityTest] public IEnumerator VillageUsesItsOwnGuiWithoutLegacyCombatInventory()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            StartingVillageWorld village = null;
            for (int frame = 0; frame < 600 && !(village && village.Ready); frame++)
            { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True);
            yield return null;
            var behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var gui = behaviours.Single(b => b.GetType().Name == "TemporaryGameGui");
            Assert.That((bool)gui.GetType().GetProperty("WorldBehindGui").GetValue(gui), Is.True);
            Assert.That(village.World.diagnostics.ExternalHud, Is.True,
                "The old diagnostic HP/weapon HUD must not overlay the village GUI");
            Assert.That(behaviours.Where(b => b.GetType().Name == "InventoryGameBinding" ||
                b.GetType().Name == "InventoryRuntimeHost" || b.GetType().Name == "TemporaryInventoryView"),
                Is.Empty, "Village GUI must not compete with the legacy combat inventory/HUD");
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator CaptureTheLaneForReview()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            StartingVillageWorld village = null;
            for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True);
            Directory.CreateDirectory("../outputs/hub/capture");
            var player = village.World.arena.Player;
            foreach (bool sketch in new[] { false, true })
            {
                village.SetSketch(sketch);
                foreach (float x in new[] { 15f, 33f, 50f, 68f, 80f })
                {
                    player.Position = new Vector2(x, player.Position.y); Physics2D.SyncTransforms();
                    village.World.RefreshCamera(0, true);
                    yield return null; yield return null;
                    var camera = village.World.View;
                    var rt = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
                    var previous = camera.targetTexture; var rect = camera.rect;
                    camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1); camera.Render();
                    RenderTexture.active = rt;
                    var image = new Texture2D(960, 540, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                    RenderTexture.active = null; camera.targetTexture = previous; camera.rect = rect; RenderTexture.ReleaseTemporary(rt);
                    File.WriteAllBytes($"../outputs/hub/capture/village-{(sketch ? "sketch" : "color")}-{x:00}.png", image.EncodeToPNG());
                    Object.Destroy(image);
                }
            }
            yield return new ExitPlayMode();
        }

        // Each village screen over the lobby at 960x540, 1280x720 and 1920x1080; every visible glyph must exist in the static font.
        [UnityTest] public IEnumerator CaptureTheScreensForReview()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            StartingVillageWorld village = null;
            for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            yield return null;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
            Directory.CreateDirectory("../outputs/hub/capture");
            var player = village.World.arena.Player;
            // The first visit plays the opening (StartingVillageOpeningTests captures it); skip it to reach the screens.
            // The GUI may have checked the real editor profile before the memory store was installed: check again.
            gui.GetType().GetField("storyChecked", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(gui, false);
            yield return null; yield return null;
            gui.GetType().GetMethod("SkipOpening", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(gui, null);
            Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("opening"), Is.True);
            yield return null;
            Capture(gui, village, "screen-lobby");
            // Three chapter-1 clears: three village palette pieces open the three workbenches.
            var pieces = Chapter1ProfileHost.Service;
            for (int i = 1; i <= 5; i++) pieces.MarkStorySeen("artisan-visit-" + i);   // the visits have their own tests
            for (int i = 0; i < 3; i++) Assert.That(pieces.SettleRun(pieces.PlanRun(new[] { "sword", "blunt" }).RunId, Deviant.Run.RunOutcome.ChapterCleared, false, pieces.ChaptersCleared + 1), Is.True);
            yield return null;
            // A workbench opening: the lock cracks, breaks and falls apart, the name inks in and sparks scatter.
            player.Position = village.Find("palette_bench").Position + new Vector2(-2.5f, 0); Physics2D.SyncTransforms();
            village.World.RefreshCamera(0, true);
            yield return null;
            // Name cards only exist from the first departure on (the runs above started); wait for them to be built.
            var cardsRoot = (Component)gui.GetType().GetField("stationCards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(gui);
            for (int frame = 0; frame < 300 && !cardsRoot.gameObject.activeInHierarchy; frame++) yield return null;
            Assert.That(cardsRoot.gameObject.activeInHierarchy, Is.True, "name cards after the first departure");
            gui.GetType().GetMethod("PlayStationUnlock").Invoke(gui, new object[] { "palette_bench" });
            foreach (var (at, name) in new[] { (.2f, "crack"), (.5f, "break"), (.8f, "ink") })
            {
                for (float start = Time.realtimeSinceStartup; Time.realtimeSinceStartup - start < at - (name == "crack" ? 0 : at == .5f ? .2f : .5f);) yield return null;
                Capture(gui, village, "screen-unlock-" + name);
            }
            Assert.That((bool)gui.GetType().GetProperty("StationUnlockPlaying").GetValue(gui), Is.True);
            foreach (var id in new[] { "artisan_hub", "record_desk", "return_well" })
            {
                player.Position = village.Find(id).Position; Physics2D.SyncTransforms();
                yield return null;
                Assert.That(village.TryInteract(), Is.True, id);
                yield return null; yield return null;
                foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
                    Capture(gui, village, $"screen-{id}-{size.x}", size.x, size.y);
                gui.GetType().GetMethod("Back").Invoke(gui, null);
                yield return null;
            }
            var hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            // After the first boss clear with pigment in hand: a buyable unlock, the same unlock owned, and the record.
            var profile = Chapter1ProfileHost.Service; var run = System.Guid.NewGuid();
            Assert.That(profile.RecordChapterOneClear(run, false), Is.True);
            for (int i = 0; i < 6; i++) profile.AwardPigment(run, "boss" + i, "boss", false);
            Assert.That(profile.FlushPigment(), Is.True);
            yield return new WaitForSecondsRealtime(4.1f); // let the confirmation note fade
            player.Position = village.Find("palette_bench").Position; Physics2D.SyncTransforms();
            yield return null;
            Assert.That(village.TryInteract(), Is.True);
            yield return null; yield return null;
            Capture(gui, village, "screen-palette_bench-ready");
            gui.GetType().GetMethod("BuySelected", hidden).Invoke(gui, null);
            yield return new WaitForSecondsRealtime(.8f); yield return null;
            Capture(gui, village, "screen-palette_bench-owned");
            gui.GetType().GetMethod("Back").Invoke(gui, null);
            yield return null;
            // A workbench lock-break record (station-opened-<id>) sits in the same seen-story list as the stories.
            Chapter1ProfileHost.Service.MarkStorySeen("station-opened-supply_counter");
            player.Position = village.Find("record_desk").Position; Physics2D.SyncTransforms();
            yield return null;
            Assert.That(village.TryInteract(), Is.True);
            yield return null; yield return null;
            Capture(gui, village, "screen-record_desk-progress");
            // The workbench lock-break records share the seen-story list; the record book lists only stories.
            Assert.That(Chapter1ProfileHost.Service.Snapshot.SeenStories.Any(id => id.StartsWith("station-opened-")), Is.True);
            Assert.That(gui.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t => t.text.Contains("station-opened")), Is.Empty);
            // Visits have no title of their own: the record book names them by the visits' title and the piece.
            Assert.That(gui.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t => t.text.Contains("artisan-visit")), Is.Empty);
            Assert.That(gui.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t => t.text == "마을 장인의 방문 1"), Is.True);
            gui.GetType().GetMethod("Back").Invoke(gui, null);
            yield return null;
            // Supply counter: the attack line before and after its first tier, then the max-health line.
            player.Position = village.Find("supply_counter").Position; Physics2D.SyncTransforms();
            yield return null;
            Assert.That(village.TryInteract(), Is.True);
            yield return null;
            void Select(string id) { gui.GetType().GetField("hubSelected", hidden).SetValue(gui, id); gui.GetType().GetField("benchDirty", hidden).SetValue(gui, true); }
            Select("attack_bonus_1");
            yield return null; yield return null;
            Capture(gui, village, "screen-supply_counter-attack");
            gui.GetType().GetMethod("BuySelected", hidden).Invoke(gui, null);
            yield return new WaitForSecondsRealtime(.8f); yield return null;
            Assert.That(gui.GetType().GetField("hubSelected", hidden).GetValue(gui), Is.EqualTo("attack_bonus_2"));
            Capture(gui, village, "screen-supply_counter-attack-next");
            Select("max_health_bonus_1");
            yield return null; yield return null;
            Capture(gui, village, "screen-supply_counter-health");
            gui.GetType().GetMethod("Back").Invoke(gui, null);
            yield return null;
            Chapter1ProfileHost.UseStore(null);
            yield return new ExitPlayMode();
        }

        internal static void Capture(MonoBehaviour gui, StartingVillageWorld village, string name, int width = 960, int height = 540)
        {
            Directory.CreateDirectory("../outputs/hub/capture");
            Canvas.ForceUpdateCanvases();
            foreach (var style in gui.GetComponentsInChildren<MonoBehaviour>().Where(b => b.GetType().Name == "PaperGuiButtonStyle")) style.SendMessage("LateUpdate");
            foreach (var label in gui.GetComponentsInChildren<UnityEngine.UI.Text>())
            {
                // Rich-text tags are markup, not glyphs. A dynamic font (the village's Malgun Gothic) is asked for the
                // label's own size and style; the static Galmuri set only has its import size.
                string text = label.supportRichText ? System.Text.RegularExpressions.Regex.Replace(label.text, "<[^>]+>", "") : label.text;
                if (label.font.dynamic) label.font.RequestCharactersInTexture(text, label.fontSize, label.fontStyle);
                foreach (char glyph in text)
                    Assert.That(char.IsWhiteSpace(glyph) || (label.font.dynamic ? label.font.GetCharacterInfo(glyph, out _, label.fontSize, label.fontStyle) : label.font.GetCharacterInfo(glyph, out _, label.font.fontSize)),
                        Is.True, $"missing glyph '{glyph}' in {name}: {label.text}");
            }
            var canvas = gui.GetComponent<Canvas>(); var camera = village.World.View;
            var mode = canvas.renderMode; var target = camera.targetTexture; var rect = camera.rect;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var fit = gui.GetType().GetMethod("FitToWorld");
            // The artisan's workshop bubble is its own overlay canvas over the GUI: it joins the capture in front of it.
            var bubbles = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(b => b.GetType().Name == "MaintenanceBubbleView").ToArray();
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 1;
                foreach (var bubble in bubbles)
                {
                    var own = bubble.GetComponent<Canvas>(); own.renderMode = RenderMode.ScreenSpaceCamera; own.worldCamera = camera;
                    own.planeDistance = camera.nearClipPlane + .5f; bubble.SendMessage("Fit");
                }
                // Dynamic-font labels rasterise at the canvas scale, so rebuild them for the capture size.
                fit.Invoke(gui, null); foreach (var label in gui.GetComponentsInChildren<UnityEngine.UI.Text>()) label.SetAllDirty();
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes($"../outputs/hub/capture/{name}.png", image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = null; camera.targetTexture = target; camera.rect = rect; RenderTexture.active = null;
                foreach (var bubble in bubbles) { var own = bubble.GetComponent<Canvas>(); own.renderMode = RenderMode.ScreenSpaceOverlay; own.worldCamera = null; bubble.SendMessage("Fit"); }
                fit.Invoke(gui, null); RenderTexture.ReleaseTemporary(rt); Object.Destroy(image);
            }
        }
    }
}
