using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // What stays readable in front in the village (user 2026-11-03): characters are never covered by a station name card
    // or the F keycap, speech bubbles are never covered by a name card, and the bubble font holds every letter the
    // dialogue uses (a missing glyph silently drops the letter: 「부탁해도 될까」 read 「부해도 까」).
    public sealed class StartingVillageFrontTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Fonts = "Assets/Deviant/Inventory/Resources/PixelInventory/Fonts/";

        [Test] public void VillageScenerySpansItsAuthoredBackgroundSegments()
        {
            foreach (var (name, width) in new[] { ("mountains", 888f / 32), ("far-village", 444f / 32), ("near-village", 666f / 32) })
            {
                var sprite = Resources.Load<Sprite>("Hub/pixel-village/background/color/" + name);
                Assert.That(sprite, Is.Not.Null, name);
                Assert.That(sprite.bounds.size.x, Is.EqualTo(width).Within(.001f),
                    name + " must cover its full parallax segment, without gaps from a regenerated importer");
            }
        }

        [Test] public void TheBubbleFontHoldsEveryLetterOfTheVillageDialogue()
        {
            foreach (var face in new[] { "Galmuri11.ttf", "Galmuri11-Bold.ttf" })
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(Fonts + face);
                Assert.That(font && !font.dynamic, Is.True, face + " is the static pixel font");
                var glyphs = new HashSet<int>(font.characterInfo.Select(c => c.index));
                foreach (var data in new[] { "HubOpening", "HubStory", "HubVisits" })
                {
                    var text = Resources.Load<TextAsset>(data).text;
                    var missing = text.Where(c => c > '~' && !char.IsWhiteSpace(c) && !glyphs.Contains(c)).Distinct().ToArray();
                    Assert.That(missing, Is.Empty, $"{face} lacks letters of {data}: {new string(missing)}");
                }
            }
        }

        [UnityTest] public IEnumerator CharactersAndBubblesStayInFrontOfTheNameCards()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            var profile = Chapter1ProfileHost.Service;
            profile.MarkStorySeen("opening");
            StartingVillageWorld village = null;
            for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True);
            // As in the game, the camera draws into the native 960x540 buffer (batch runs disable it unless asked).
            var viewport = Object.FindAnyObjectByType<Stage1VillageExperiment.VillagePixelViewport>();
            viewport.enableInBatchMode = true; viewport.enabled = true;
            for (int frame = 0; frame < 30 && !viewport.Ready; frame++) yield return null;
            Assert.That(viewport.Ready, Is.True);
            yield return null; yield return null;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
            if ((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui)) gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
            yield return null;

            // Before the first departure the stations have no names, and before a scene tells of magic pigment there is
            // no pigment chip (playtest 2026-11-03).
            var cards = (Canvas)gui.GetType().GetField("cardCanvas", Private).GetValue(gui);
            var stationCards = (RectTransform)gui.GetType().GetField("stationCards", Private).GetValue(gui);
            var currency = (GameObject)gui.GetType().GetField("hubCurrency", Private).GetValue(gui);
            var prompt = (Component)gui.GetType().GetField("hubPromptPlate", Private).GetValue(gui);
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.That(stationCards.gameObject.activeInHierarchy, Is.False, "no name cards before the first departure");
            Assert.That(prompt.gameObject.activeSelf, Is.False, "nor the bottom name prompt");
            Assert.That(currency.activeSelf, Is.False, "no pigment chip before pigment is told");
            CaptureWithGui(gui, village.World.View, "front-before-departure");
            // The optional archive opens its story list and preview without starting a cutscene.
            var hero = village.World.arena.Player; var stood = hero.Position;
            hero.Position = village.Find("record_desk").Position; Physics2D.SyncTransforms(); yield return null;
            Assert.That(village.TryInteract(), Is.True); yield return null; yield return null;
            var heading = (Text)gui.GetType().GetField("recordTitle", Private).GetValue(gui);
            Assert.That(heading.text, Is.EqualTo("기록"), "the sign and the screen share one name (playtest 2026-11-13)");
            Assert.That((bool)gui.GetType().GetProperty("RecordDetailShowing").GetValue(gui), Is.False);
            Assert.That(gui.GetComponentsInChildren<Button>().Any(b=>b.GetComponentInChildren<Text>()?.text=="기록 초기화"), Is.False, "the farmhouse is a read-only story book");
            Assert.That(gui.GetComponentsInChildren<Text>().Any(t => t.text == "스토리 1"), Is.True);
            Assert.That(((RawImage)gui.GetType().GetField("recordPreviewArt", Private).GetValue(gui)).texture, Is.Not.Null);
            CaptureWithGui(gui, village.World.View, "front-record-note");
            gui.GetType().GetMethod("CloseHubPanel", Private).Invoke(gui, null); yield return null;
            hero.Position = stood; Physics2D.SyncTransforms(); yield return null;
            profile.PlanRun(profile.Catalog.Data.startLoadout.availableBlueprints.Take(profile.Catalog.Data.startLoadout.selectedCount).ToArray());
            profile.MarkStorySeen("artisan-visit-1");
            yield return null;
            Assert.That(currency.activeSelf, Is.True, "the pigment chip after the artisan's first visit");

            // The name cards are drawn by the village camera between the scenery and the characters.
            for (int frame = 0; frame < 60 && !stationCards.gameObject.activeInHierarchy; frame++) yield return null;
            Assert.That(stationCards.gameObject.activeInHierarchy, Is.True, "the lobby shows its name cards");
            CaptureWithGui(gui, village.World.View, "front-after-departure");
            Assert.That(cards.renderMode, Is.EqualTo(RenderMode.WorldSpace), "the cards are part of the world picture");
            // Each card hangs over its own station in world units, whatever size the camera draws at.
            foreach (var station in village.Stations)
            {
                var card = stationCards.Find("이름표 " + station.Label);
                Assert.That(card, Is.Not.Null, station.Label);
                Assert.That(card.position.x, Is.EqualTo(station.Position.x).Within(1 / 32f), station.Label + " card over its station");
                Assert.That(card.position.y, Is.EqualTo(station.Top + 27 / 32f).Within(1 / 32f), station.Label + " card above its art");
            }
            var player = village.World.arena.Player;
            Assert.That(cards.sortingOrder, Is.LessThan(player.visual.sortingOrder), "the player is in front of the cards");
            Assert.That(cards.sortingOrder, Is.LessThan(-10), "and so are the opening residents");
            Assert.That(village.StationKey.GetComponent<SpriteRenderer>().sortingOrder, Is.LessThan(-10), "the F keycap is behind every character");
            var stationArt = village.Stations.Where(s => s.Art).Max(s => s.Art.sortingOrder);
            Assert.That(cards.sortingOrder, Is.GreaterThan(stationArt), "the cards stay over the stations they label");
            // Speech bubbles are an overlay canvas over the world picture (so no card covers one) and under the GUI's.
            int bubbleOrder = (int)gui.GetType().GetField("BubbleCanvasOrder", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.That(bubbleOrder, Is.LessThan(gui.GetComponent<Canvas>().sortingOrder));
            // (A loop, not a lambda: a lambda here would capture this test's locals from before EnterPlayMode.)
            int worldOutputOrder = int.MinValue;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.sortingOrder < 0) worldOutputOrder = Mathf.Max(worldOutputOrder, canvas.sortingOrder);
            Assert.That(bubbleOrder, Is.GreaterThan(worldOutputOrder), "over the world output");
            Assert.That(stationCards.GetComponentInParent<Canvas>().rootCanvas, Is.SameAs(cards));

            // The player lifted onto the well's card: the capture shows the player over it.
            var well = village.Find(StartingVillageWorld.ReturnWell);
            using (village.World.arena.AcquireManualSimulation())
            {
                player.Position = new Vector2(well.Position.x, well.Top + .2f); player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                village.World.RefreshCamera(0, true);
                yield return null; yield return null;
                // The capture draws into a 960x540 texture of its own, the same size as the buffer the cards were laid out for.
                ApprovedVillageIntegrationTests.Capture(village.World.View, "front-player-over-card");
            }
            Assert.That(File.Exists("../outputs/approved-hub/captures/front-player-over-card.png"), Is.True);
            yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        // The village is built from the profile it loaded with, so a record reset only fully takes on a fresh load:
        // Reading the continued story must preserve the player's permanent progress.
        [UnityTest] public IEnumerator RecordFarmKeepsTheStoryWithoutResettingTheProfile()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            var profile = Chapter1ProfileHost.Service;
            profile.MarkStorySeen("opening");
            profile.PlanRun(profile.Catalog.Data.startLoadout.availableBlueprints.Take(profile.Catalog.Data.startLoadout.selectedCount).ToArray());
            StartingVillageWorld village = null;
            for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True);
            yield return null; yield return null;
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
            if ((bool)gui.GetType().GetProperty("OpeningActive").GetValue(gui)) gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
            yield return null;

            var hero = village.World.arena.Player;
            hero.Position = village.Find("record_desk").Position; Physics2D.SyncTransforms(); yield return null;
            Assert.That(village.TryInteract(), Is.True); yield return null; yield return null;
            Assert.That(gui.GetComponentsInChildren<Button>().Any(b=>b.GetComponentInChildren<Text>()?.text=="기록 초기화"),Is.False);
            Assert.That(profile.Snapshot.RunsStarted,Is.EqualTo(1),"reading the story never resets permanent progress");
            Assert.That((bool)gui.GetType().GetProperty("RecordDetailShowing").GetValue(gui),Is.False);
            Assert.That(gui.GetComponentsInChildren<Text>().Any(t=>t.text=="스토리 1"),Is.True);
            gui.GetType().GetMethod("CloseHubPanel",Private).Invoke(gui,null);
            yield return null;
            Assert.That(Time.timeScale,Is.EqualTo(1f));
            yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        // Review capture (not a pixel test): the village camera with the GUI canvas drawn over it at 960x540.
        public static void CaptureWithGui(MonoBehaviour gui, Camera camera, string name)
        {
            var canvas = gui.GetComponent<Canvas>();
            var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
            var target = camera.targetTexture; var rect = camera.rect; var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(960, 540, 24); var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 1;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                Directory.CreateDirectory("../outputs/approved-hub/captures");
                File.WriteAllBytes("../outputs/approved-hub/captures/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
                camera.targetTexture = target; camera.rect = rect; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt); Object.Destroy(image); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
