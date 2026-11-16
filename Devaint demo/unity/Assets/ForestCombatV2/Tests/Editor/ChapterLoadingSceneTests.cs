using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The loading screen in the real village (docs/design/story/opening/opening-playable.md 「로딩」): leaving by the departure
    // sign shows chapter 1 with the hero as the village drew it, and a hero in a weapon form runs in that form. Each screen
    // is captured to outputs/chapter-loading-tests/ for a look.
    public sealed class ChapterLoadingSceneTests
    {
        const string Captures = "../outputs/chapter-loading-tests";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        [UnityTest]
        public IEnumerator TheLoadingRunsTheHeroAsItIsDrawnThroughThePlace()
        {
            EditorSceneManager.playModeStartScene = null; EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode(); Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            StartingVillageWorld v = null;
            for (int i = 0; i < 1200 && !(v && v.Ready); i++) { v = Object.FindAnyObjectByType<StartingVillageWorld>(); yield return null; }
            Assert.That(v && v.Ready, Is.True);
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
            gui.GetType().GetMethod("SkipOpening", F).Invoke(gui, null);
            for (int i = 0; i < 100 && (bool)gui.GetType().GetField("storyActive", F).GetValue(gui); i++) gui.GetType().GetMethod("AdvanceStory", F).Invoke(gui, null);
            yield return null; yield return null;
            Directory.CreateDirectory(Captures);

            // The departure: the hero walks out bare-handed and the loading goes on with that body.
            Assert.That(v.StartDeparture(() => { }), Is.True);
            for (float from = Time.realtimeSinceStartup; !ChapterSceneFade.Busy && Time.realtimeSinceStartup - from < 15;) yield return null;
            Assert.That(ChapterSceneFade.Busy, Is.True, "the departure opens the loading");
            Assert.That(ChapterSceneFade.ShowingPlace, Is.EqualTo(LoadingPlace.Chapter1));
            Assert.That(ChapterSceneFade.ShowingHeroForm, Is.EqualTo(v.VillageBody));
            Assert.That(ChapterSceneFade.ShowingLeaving, Is.EqualTo(LoadingPlace.Village), "the window opens on the village and sweeps to chapter 1");
            // Fade-in .25 s, then the seam is halfway across about .77 s into the window.
            for (float from = Time.realtimeSinceStartup; Time.realtimeSinceStartup - from < 1f;) yield return null;
            Capture("departure-sweep-village-to-chapter1");
            yield return Settle();
            Capture("departure-chapter1-village-body");
            Close();
            yield return null;

            // In a weapon form (outside the village body) the same screen runs that form's run clip.
            v.SetHeroBody(null);
            for (int i = 0; i < 5; i++) yield return null;
            string form = v.World.arena.Presentation.CurrentForm;
            Assert.That(form, Is.Not.Null.And.Not.EqualTo(v.VillageBody));
            foreach (var place in new[] { LoadingPlace.Chapter1, LoadingPlace.Chapter2, LoadingPlace.Village })
            {
                ChapterSceneFade.Load(place, () => { });
                Assert.That(ChapterSceneFade.ShowingPlace, Is.EqualTo(place));
                Assert.That(ChapterSceneFade.ShowingLeaving, Is.EqualTo(place == LoadingPlace.Village ? (LoadingPlace?)null : LoadingPlace.Village));
                Assert.That(ChapterSceneFade.ShowingHeroForm, Is.EqualTo(form));
                yield return Settle();
                Capture(place.ToString().ToLowerInvariant() + "-" + form);
                Close();
                yield return null;
            }
        }

        // Leaving a chapter (opening-playable.md 「장소 전환」): in the real chapter scenes the window opens on that chapter and
        // sweeps to the next place; captured halfway and after. Entering play mode reloads the domain, so each scene has
        // its own test and the places are passed only after it.
        [UnityTest]
        public IEnumerator TheLoadingSweepsFromChapter1ToChapter2()
        {
            EditorSceneManager.playModeStartScene = null; EditorSceneManager.OpenScene(Chapter1RunHost.ChapterScene);
            yield return new EnterPlayMode();
            yield return SweepOut(LoadingPlace.Chapter1, LoadingPlace.Chapter2);
        }

        [UnityTest]
        public IEnumerator TheLoadingSweepsFromChapter2ToTheVillage()
        {
            EditorSceneManager.playModeStartScene = null; EditorSceneManager.OpenScene(ChapterSceneFade.Chapter2Scene);
            yield return new EnterPlayMode();
            yield return SweepOut(LoadingPlace.Chapter2, LoadingPlace.Village);
        }

        static IEnumerator SweepOut(LoadingPlace leaving, LoadingPlace place)
        {
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            Directory.CreateDirectory(Captures);
            for (float from = Time.realtimeSinceStartup; !HasPlayer() && Time.realtimeSinceStartup - from < 30;) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(HasPlayer(), Is.True, leaving + " has a player to draw");
            Assert.That(ChapterSceneFade.Busy, Is.False);
            ChapterSceneFade.Load(place, () => { });
            Assert.That(ChapterSceneFade.ShowingPlace, Is.EqualTo(place));
            Assert.That(ChapterSceneFade.ShowingLeaving, Is.EqualTo(leaving), "the window opens on the chapter being left");
            Assert.That(ChapterSceneFade.ShowingHeroForm, Is.Not.Null);
            string name = leaving.ToString().ToLowerInvariant() + "-to-" + place.ToString().ToLowerInvariant();
            // Frames can be slow in a chapter scene, so wait for the seam itself (active only while it crosses the window)
            // and catch it in the left part of the window, where the row checked below is inside the circle.
            RectTransform seam = null;
            for (float from = Time.realtimeSinceStartup; Time.realtimeSinceStartup - from < 30; )
            {
                var found = GameObject.Find("Seam");
                if (found) { seam = (RectTransform)found.transform; if (seam.anchoredPosition.x <= 120) break; }
                else if (seam) break;
                yield return null;
            }
            Assert.That(seam, Is.Not.Null, "the seam crosses the window");
            int seamX = Mathf.RoundToInt(seam.anchoredPosition.x);
            var pixels = Capture("sweep-" + name);
            // The paper line stands where the cut is: on screen row 360 (inside the circle and its shade for window x 60..120) the place
            // being left shows just left of it and the place entered just right of it (chapter 2 dark, the others pale).
            int lineX = 288 + seamX * 2;
            if (seamX >= 60 && seamX <= 120)
            {
                float Light(int x) { var c = pixels[(539 - 360) * 960 + x]; return (c.r + c.g + c.b) / 3f; }
                Assert.That(Light(lineX + 1), Is.GreaterThan(230), $"the paper line at window x {seamX}");
                bool leftDark = leaving == LoadingPlace.Chapter2, rightDark = place == LoadingPlace.Chapter2;
                Assert.That(Light(lineX - 6) < 120, Is.EqualTo(leftDark), $"{leaving} just left of the line (brightness {Light(lineX - 6)})");
                Assert.That(Light(lineX + 10) < 120, Is.EqualTo(rightDark), $"{place} just right of the line (brightness {Light(lineX + 10)})");
            }
            else Debug.LogWarning($"Loading check: the seam was caught at window x {seamX}; the line/cut check needs 60..120.");
            for (float from = Time.realtimeSinceStartup; GameObject.Find("Seam") && Time.realtimeSinceStartup - from < 30;) yield return null;
            Assert.That(GameObject.Find("Seam"), Is.Null, "the place entered fills the window");
            for (int i = 0; i < 3; i++) yield return null;
            Capture("after-" + name);
            Close();
            yield return null;
        }

        static bool HasPlayer() => Object.FindObjectsByType<Deviant.Combat.CombatArena>(FindObjectsSortMode.None).Any(a => a && a.Player);

        // Past the fade-in, the window's opening and the name.
        static IEnumerator Settle() { for (float from = Time.realtimeSinceStartup; Time.realtimeSinceStartup - from < 1.5f;) yield return null; }

        static void Close() { var fade = GameObject.Find("Chapter scene fade"); if (fade) Object.Destroy(fade); }

        // Renders the overlay canvas through a camera at 960x540, writes it as PNG and returns its pixels (bottom row first).
        static Color32[] Capture(string name)
        {
            var canvas = GameObject.Find("Chapter scene fade").GetComponent<Canvas>();
            var camera = new GameObject("Loading capture camera").AddComponent<Camera>();
            var rt = RenderTexture.GetTemporary(960, 540, 24);
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.targetTexture = rt;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(Captures, name + ".png"), image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt); Object.Destroy(image); Object.Destroy(camera.gameObject);
            }
        }
    }
}
