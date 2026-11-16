using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The village is always sketch (user decision 2026-11-04): unlocks, the first boss clear and departures colour no
    // building. Colour comes back only with the village palette pieces' blots (StartingVillageWorld.ColourBlots).
    public sealed class StartingVillageColouringTests
    {
        static string GuiState()
        {
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
            if (!gui) return "none";
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            return $"open={(gui.GetType().GetField("hubOpen", F).GetValue(gui) as GameObject)?.name} paused={gui.GetType().GetField("hubPaused", F).GetValue(gui)} story={gui.GetType().GetField("storyActive", F).GetValue(gui)}";
        }
        static Sprite Hub(string path) => Resources.Load<Sprite>("Hub/" + path);
        static SpriteRenderer Piece(StartingVillageWorld village, string name) =>
            village.GetComponentsInChildren<SpriteRenderer>().First(r => r.gameObject.name == name);
        // The pixel lawn's top cell under x (StartingVillageWorld.OpeningGround).
        static SpriteRenderer CapAt(StartingVillageWorld village, float x) =>
            village.GetComponentsInChildren<SpriteRenderer>().Where(r => r.gameObject.name == "Ground body").OrderBy(r => Mathf.Abs(r.bounds.center.x - x) + Mathf.Abs(r.bounds.max.y - village.WalkingLine)).First();

        [UnityTest] public IEnumerator UnlocksBossClearsAndDeparturesColourNoBuilding()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            // Boss cleared with enough pigment and the opening already seen, so the lobby is free to use.
            Chapter1ProfileHost.UseStore(new MemoryProfileStore(new ProfileData
            {
                profileId = Guid.NewGuid().ToString(), pigment = 300, chapter1BossFirstClear = true, chapter1Clears = 3, chaptersCleared = 3, seenStories = new[] { "opening", "artisan-first-run", "artisan-visit-1", "artisan-visit-2", "artisan-visit-3", "village-ink-reward-1", "village-ink-reward-2", "village-ink-reward-3", "village-executioner-note" },
            }));
            var profile = Chapter1ProfileHost.Service;
            StartingVillageWorld village = null;
            for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True);
            yield return null;
            // If the first-visit opening came up before the seeded profile was read, read it through so the lobby runs.
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
            const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            if (gui) gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
            for (int i = 0; gui && i < 30 && (bool)gui.GetType().GetField("storyActive", Private).GetValue(gui); i++) gui.GetType().GetMethod("AdvanceStory", Private).Invoke(gui, null);
            yield return null;
            var sketchCaps = new[] { Resources.Load<Texture2D>("Hub/pixel-village/ground/sketch/terrain-atlas") };

            // Boss already cleared: no building is coloured, the record farmhouse included.
            Assert.That(village.ColouredRegions, Is.Empty, "the boss clear colours nothing");
            Assert.That(Piece(village, "record_desk").sprite, Is.EqualTo(Hub("props/sketch/record-desk")));
            CollectionAssert.Contains(sketchCaps, CapAt(village, village.Find("record_desk").Position.x).sprite.texture, "the farmhouse ground stays sketch");
            Assert.That(Piece(village, "palette_atelier").sprite, Is.EqualTo(Hub("buildings/sketch/palette-atelier")));
            yield return Capture(village, "colouring-0-start", 33);

            // Buying unlocks colours nothing and plays no colour spread.
            Assert.That(profile.TryPurchase("ink_bundle_status", out var state), Is.True, state.ToString());
            Assert.That(profile.TryPurchase("starting_gold_50", out state), Is.True, state.ToString());
            for (int f = 0; f < 10; f++) yield return null;
            Assert.That(village.Spreading("palette_atelier") || village.Spreading("supply_office"), Is.False, "no colour spread on an unlock");
            Assert.That(village.ColouredRegions, Is.Empty, "an unlock colours nothing");
            Assert.That(Piece(village, "palette_atelier").sprite, Is.EqualTo(Hub("buildings/sketch/palette-atelier")));
            Assert.That(Piece(village, "palette_bench").sprite, Is.EqualTo(Hub("props/sketch/palette-bench")));
            Assert.That(village.Stations.Any(s => s.Id == "supply_counter"), Is.False);
            CollectionAssert.Contains(sketchCaps, CapAt(village, village.Find("palette_bench").Position.x).sprite.texture);
            yield return Capture(village, "colouring-1-unlocks", 33);

            // Previews override the state; clearing returns to sketch.
            village.SetSketch(false);
            Assert.That(Piece(village, "return_well").sprite, Is.EqualTo(Hub("props/color/return-well")));
            village.ClearPreview();
            Assert.That(Piece(village, "return_well").sprite, Is.EqualTo(Hub("props/sketch/return-well")));

            // The actual preparation button starts departure and colours nothing, including the scenery sign.
            var player = village.World.arena.Player;
            player.Position = village.Find(StartingVillageWorld.DepartureSign).Position;
            player.velocity = Vector2.zero; Physics2D.SyncTransforms();
            yield return null;
            Assert.That(village.TryInteract(), Is.True, GuiState());
            Assert.That(village.Departing, Is.True);
            yield return null;
            Assert.That(village.ColouredRegions, Is.Empty);
            Assert.That(Piece(village, "departure_sign").sprite, Is.EqualTo(Hub("props/sketch/departure-sign")));
            Assert.That(Piece(village, "handcart").sprite, Is.EqualTo(Hub("props/sketch/handcart")));
            yield return Capture(village, "colouring-2-lane", player.Position.x);

            Chapter1ProfileHost.UseStore(null);
            yield return new ExitPlayMode();
        }

        // Review capture of the lane at 960x540 (not a pixel test).
        static IEnumerator Capture(StartingVillageWorld village, string name, float x)
        {
            var player = village.World.arena.Player;
            player.Position = new Vector2(x, player.Position.y); Physics2D.SyncTransforms();
            village.World.RefreshCamera(0, true);
            // One frame so the sky and the far village row follow the view (LateUpdate).
            yield return null; yield return null;
            var camera = village.World.View; var rt = RenderTexture.GetTemporary(960, 540, 24);
            var target = camera.targetTexture; var rect = camera.rect;
            camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1); camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            RenderTexture.active = null; camera.targetTexture = target; camera.rect = rect; RenderTexture.ReleaseTemporary(rt);
            Directory.CreateDirectory("../outputs/hub/capture");
            File.WriteAllBytes($"../outputs/hub/capture/{name}.png", image.EncodeToPNG()); Object.Destroy(image);
        }
    }
}
