using System.Collections;
using System.Linq;
using Deviant.Chapter2;
using Deviant.Combat;
using Deviant.Control.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class StageRecoveryStationTests
    {
        Keyboard keyboard;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        bool changedInputSettings;
        [UnityTest] public IEnumerator ActualFKeyRequiresHoldingAndCannotRepeatWhileKeptDown()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            StageRecoveryStation station = null;
            for (int i = 0; i < 900 && !(station && station.Placed); i++)
            { yield return null; station = Object.FindAnyObjectByType<StageRecoveryStation>(); }
            Assert.That(station && station.Placed, Is.True);
            var world = station.GetComponent<ForestCombatWorld>(); var player = world.arena.Player;
            foreach (var enemy in Object.FindObjectsByType<TestEnemy>(FindObjectsSortMode.None))
                if (enemy) { enemy.aggressive = false; enemy.Position += Vector2.right * 1000; }
            player.CancelAction(); player.Position = station.Position + Vector2.up * .05f; player.velocity = Vector2.zero;
            player.health = 20; player.grounded = true;
            var wallet = station.GetComponent<Chapter1RunBinding>().Run.Economy; wallet.Earn(250);
            background = InputSystem.settings.backgroundBehavior; editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            changedInputSettings = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            PlayerControls.For(player).Source.SetFocused(true);
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(station.Holding, Is.True, "held=" + PlayerControls.Held(player, Deviant.Input.Contracts.GameAction.Interact) +
                " near=" + station.PlayerNear + " grounded=" + player.grounded + " blocked=" + player.InputBlocked + " testInput=" + player.UseTestInput);
            Assert.That(wallet.Gold, Is.EqualTo(250));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            Assert.That(station.Holding, Is.False); Assert.That(player.health, Is.EqualTo(20));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            float deadline = Time.realtimeSinceStartup + 5;
            while (station.Remaining == 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(station.Remaining, Is.EqualTo(1)); Assert.That(player.health, Is.EqualTo(45)); Assert.That(wallet.Gold, Is.EqualTo(150));
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.That(station.Remaining, Is.EqualTo(1), "continued F cannot buy the second heal");
        }

        [UnityTest] public IEnumerator ChapterOneInstallsOneStationAndEnforcesItsBudget()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            yield return Exercise(Chapter1MapSelector.ProceduralScene);
        }

        [UnityTest] public IEnumerator ChapterTwoGetsItsOwnFullReservoirAndTheSameInteractionRules()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene("Assets/Chapter2/Scenes/Chapter2Procedural.unity");
            Object.FindAnyObjectByType<Chapter2World>().seed = 42;
            yield return new EnterPlayMode();
            yield return Exercise("Assets/Chapter2/Scenes/Chapter2Procedural.unity");
        }

        static IEnumerator Exercise(string scene)
        {
            StageRecoveryStation station = null;
            for (int i = 0; i < 900 && !(station && station.Placed); i++)
            { yield return null; station = Object.FindAnyObjectByType<StageRecoveryStation>(); }
            Assert.That(station && station.Placed, Is.True, scene + ": the generated stage has a safe recovery facility");
            Assert.That(Object.FindObjectsByType<StageRecoveryStation>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(station.Remaining, Is.EqualTo(2));
            bool secondChapter = scene.Contains("Chapter2");
            Assert.That(station.ArtHeight, Is.EqualTo(secondChapter ? 3.3f : 3.5f));
            var art = station.transform.Find("Ink recovery pump").GetComponent<SpriteRenderer>();
            Assert.That(art.sprite, Is.SameAs(Resources.Load<Sprite>("StageRecoveryArt/stage" + (secondChapter ? 2 : 1))));
            if (secondChapter)
            {
                Assert.That(art.sprite.texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
                Assert.That(art.sprite.texture.mipmapCount, Is.GreaterThan(1), "large source must be prefiltered when minified");
            }
            else
            {
                // Chapter 1 is the 2x2-dot pixel pump drawn at native size: no filtering, no mips.
                Assert.That(art.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(art.sprite.texture.mipmapCount, Is.EqualTo(1));
                Assert.That(art.sprite.pixelsPerUnit, Is.EqualTo(32));
            }
            foreach (var text in station.Prompt.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                Assert.That(text.font, Is.SameAs(Resources.Load<Font>("PixelInventory/Fonts/Galmuri11")));
                Assert.That(text.fontSize, Is.EqualTo(12), "use the HUD font at its native pixel size");
                if (text.name != "Rebound key") Assert.That(text.color, Is.EqualTo(secondChapter ? Color.white : Color.black));
            }
            Assert.That(station.Prompt.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length, Is.EqualTo(2),
                "shared keycap and currency icon, without a background panel");
            Assert.That(station.Prompt.KeySprite, Is.SameAs(KeycapMarker.SpriteFor('F')));
            var keyImage = station.Prompt.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                .Single(i => i.name == "Interaction keycap");
            Assert.That(keyImage.rectTransform.sizeDelta, Is.EqualTo(new Vector2(22, 24)));
            var priceLabel = station.Prompt.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                .Single(t => t.name == "Price");
            var boldDigits = priceLabel.GetComponent<UnityEngine.UI.Shadow>();
            Assert.That(boldDigits, Is.Not.Null, "the baked pixel font needs a real extra pixel for bold digits");
            Assert.That(boldDigits.effectDistance, Is.EqualTo(Vector2.right));
            Assert.That(boldDigits.effectColor, Is.EqualTo(priceLabel.color));
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var wallet = station.GetComponent<Chapter1RunBinding>()?.Run.Economy ?? station.GetComponent<Chapter2World>().Economy;
            var map = station.GetComponent<Chapter1FullMap>();
            Assert.That(map.TryGetRecoveryMapPoint(out _), Is.False, "unvisited room must not reveal the station");
            using (var manual = arena.AcquireManualSimulation())
            {
                foreach (var enemy in Object.FindObjectsByType<TestEnemy>(FindObjectsSortMode.None))
                    if (enemy) { enemy.aggressive = false; enemy.Position += Vector2.right * 1000; }
                player.CancelAction(); player.Position = station.Position + Vector2.up * .05f; player.velocity = Vector2.zero;
                player.grounded = true; player.health = 100; player.maxHealth = 100;
                Physics2D.SyncTransforms();
                yield return null; // Let the normal map exploration path discover the room.
                Assert.That(map.TryGetRecoveryMapPoint(out _), Is.True);
                Assert.That(station.TryBeginHold(), Is.False, "full health costs nothing");
                Assert.That(station.Remaining, Is.EqualTo(2));
                player.health = 20;
                Assert.That(station.TryBeginHold(), Is.False, "no gold means no purchase");
                wallet.Earn(250);
                var camera = arena.ViewCamera;
                var savedPosition = camera.transform.position;
                camera.transform.position = new Vector3(station.Position.x, station.Position.y + 2, savedPosition.z);
                station.Prompt.Refresh();
                Assert.That(station.Prompt.Visible, Is.True);
                Assert.That(station.Prompt.Details, Is.EqualTo("2회 남음 · 최대 체력 25% 회복"));
                Assert.That(station.Prompt.PriceText, Is.EqualTo("100"));
                Assert.That(station.Prompt.Coin, Is.SameAs(Resources.Load<Sprite>("ApprovedCombatHUD/sprites/gold-coin")));
                using (var paused = arena.Controls.OpenScope("recovery test pause"))
                {
                    paused.PauseSimulation(); Assert.That(station.TryBeginHold(), Is.False);
                    station.Prompt.Refresh(); Assert.That(station.Prompt.Visible, Is.False);
                }
                using (var blocked = arena.Controls.OpenScope("recovery test blocked input"))
                { blocked.BlockInput(InputChannels.Interaction); Assert.That(station.TryBeginHold(), Is.False); }
                player.grounded = false; Assert.That(station.TryBeginHold(), Is.False, "cannot use while jumping"); player.grounded = true;
                player.stunUntil = arena.Now + 1; Assert.That(station.TryBeginHold(), Is.False, "cannot bypass hit stun"); player.stunUntil = arena.Now;
                player.Position += Vector2.right * 8; Assert.That(station.TryBeginHold(), Is.False, "must stand near the pump"); player.Position = station.Position;
                int effectsBefore = station.GetComponent<Chapter1RoomClearHealFx>()?.Played ?? 0;
                Assert.That(station.TryBeginHold(), Is.True);
                Assert.That(station.AdvanceHold(true), Is.False, "a tap cannot heal or charge gold");
                for (int i = 0; i < 60; i++) manual.Step(1f / 120);
                station.Prompt.Refresh();
                Assert.That(station.Prompt.BorderProgress, Is.InRange(.49f, .51f), "key perimeter tracks the combat hold clock");
                Assert.That(station.AdvanceHold(false), Is.False, "release cancels");
                station.Prompt.Refresh();
                Assert.That(station.Prompt.BorderProgress, Is.Zero, "released key clears its border immediately");
                Assert.That(station.Holding, Is.False); Assert.That(station.Remaining, Is.EqualTo(2));
                Assert.That(wallet.Gold, Is.EqualTo(250)); Assert.That(player.health, Is.EqualTo(20));
                Assert.That(station.GetComponent<Chapter1RoomClearHealFx>()?.Played ?? 0, Is.EqualTo(effectsBefore),
                    "an incomplete hold must not show a healing glow");
                Assert.That(station.TryBeginHold(), Is.True);
                CompleteHold(station, manual);
                Assert.That(player.health, Is.EqualTo(45)); Assert.That(wallet.Gold, Is.EqualTo(150));
                Assert.That(station.Remaining, Is.EqualTo(1));
                station.Prompt.Refresh();
                Assert.That(station.Prompt.PriceText, Is.EqualTo("150"));
                Assert.That(station.Prompt.Details, Is.EqualTo("1회 남음 · 최대 체력 25% 회복"));
                Assert.That(station.PromptStatus, Is.Empty, "success feedback belongs to the healed character");
                Assert.That(station.Prompt.HealAmountVisible, Is.True);
                Assert.That(station.Prompt.HealAmountText, Is.EqualTo("+25 HP"));
                var floatingHeal = station.Prompt.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                    .Single(t => t.name == "Player heal amount");
                Assert.That(floatingHeal.transform.parent, Is.Not.SameAs(priceLabel.transform.parent));
                float textX = floatingHeal.rectTransform.anchoredPosition.x;
                var healedPosition = player.Position;
                player.Position += Vector2.right * .5f; station.Prompt.Refresh();
                Assert.That(floatingHeal.rectTransform.anchoredPosition.x, Is.GreaterThan(textX), "healing text follows the player");
                player.Position = healedPosition; station.Prompt.Refresh();
                Assert.That(station.AdvanceHold(true), Is.False, "keeping the key down cannot consume both charges");
                station.enabled = false; station.enabled = true;
                Assert.That(station.Remaining, Is.EqualTo(1), "hiding or revisiting does not refill the pump");
                Assert.That(station.GetComponent<Chapter1RoomClearHealFx>().GlowActive, Is.True);
                Assert.That(station.GetComponent<Chapter1RoomClearHealFx>().Played, Is.EqualTo(effectsBefore + 1));
                // Advance only the combat clock; enemies and terrain remain in their actual scene.
                for (int i = 0; i < 18; i++) manual.Step(1f / 120);
                station.GetComponent<Chapter1RoomClearHealFx>().Refresh();
                Assert.That(station.GetComponent<Chapter1RoomClearHealFx>().GlowVisible, Is.True, "paid healing enables a visible green UI overlay");
                for (int i = 0; i < 104; i++) manual.Step(1f / 120);
                station.GetComponent<Chapter1RoomClearHealFx>().Refresh();
                Assert.That(station.GetComponent<Chapter1RoomClearHealFx>().GlowActive, Is.False, "edge glow fades after healing");
                Assert.That(station.GetComponent<Chapter1RoomClearHealFx>().GlowVisible, Is.False);
                station.Prompt.Refresh(); Assert.That(station.Prompt.HealAmountVisible, Is.False, "floating amount expires");
                player.CancelAction(); player.Position = station.Position; player.velocity = Vector2.zero; player.grounded = true;
                player.knockUntil = player.moveLockUntil = player.stunUntil = player.rootUntil = player.staggerUntil = arena.Now;
                player.health = 98;
                Assert.That(station.TryBeginHold(), Is.True); CompleteHold(station, manual);
                Assert.That(player.health, Is.EqualTo(100)); Assert.That(wallet.Gold, Is.Zero);
                Assert.That(station.Remaining, Is.Zero); Assert.That(map.TryGetRecoveryMapPoint(out _), Is.False);
                Assert.That(station.Prompt.HealAmountText, Is.EqualTo("+2 HP"), "show actual capped healing");
                player.health = 1; Assert.That(station.TryBeginHold(), Is.False);
                station.Prompt.Refresh();
                Assert.That(station.Prompt.Details, Is.EqualTo("0회 남음 · 최대 체력 25% 회복"));
                Assert.That(station.PromptStatus, Is.EqualTo("충전 소진"));
                player.Position += Vector2.right * 10;
                station.Prompt.Refresh(); Assert.That(station.Prompt.Visible, Is.False);
                camera.transform.position = savedPosition;
                arena.ResetTest(); Assert.That(station.Remaining, Is.EqualTo(2), "development reset refills the stage budget");
                Assert.That(station.Prompt.HealAmountVisible, Is.False); Assert.That(station.Prompt.HealAmountText, Is.Empty);
                wallet.Earn(100);
                player.Position = station.Position; player.grounded = true; player.health = 0;
                Assert.That(station.TryBeginHold(), Is.False, "no resurrection");
            }
        }

        static void CompleteHold(StageRecoveryStation station, CombatArena.ManualSimulation manual)
        {
            for (int i = 0; i < 150 && station.Holding; i++)
            { manual.Step(1f / 120); station.AdvanceHold(true); }
            Assert.That(station.Holding, Is.False, "the hold should complete within 1.25 combat seconds");
        }

        [TestCase(1)] [TestCase(7)] [TestCase(42)] [TestCase(123)] [TestCase(999)]
        public void BothGeneratedChaptersHaveSafeMidRouteFloors(int seed)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Recovery placement proof");
            try
            {
                var arena = root.AddComponent<CombatArena>();
                var data = JsonUtility.FromJson<ForestData>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/MeadowDuskV3/MapVer2/Data/ForestWorld.json").text);
                var art = JsonUtility.FromJson<RoundedTerrainLayout>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/MeadowDuskV3/MapVer2/Data/RoundedTerrain.json").text);
                data = Chapter1ProceduralMap.Generate(data, art, seed);
                foreach (var r in data.rects) Surface(root, new Rect(r.x, -(r.y + r.h), r.w, r.h));
                foreach (var p in data.platforms) Surface(root, new Rect(p.x, -p.y - .2f, p.w, .2f), true);
                Physics2D.SyncTransforms();
                var route = data.rooms.Where(r => r.mainPath && r.kind == "combat").OrderBy(r => r.x).ToArray();
                Assert.That(route.Skip(route.Length / 3).Take(Mathf.CeilToInt(route.Length * .8f) - route.Length / 3)
                    .Any(r => StageRecoveryStation.FloorPositions(arena, new Rect(r.x, -(r.y + r.h), r.w, r.h), 3f, 3.5f).Count > 0), Is.True, "chapter 1 seed " + seed);
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var layout = C2Bank.Load().Generate(seed, null);
                foreach (var r in layout.surfaces) Surface(root, r.Bounds, r.oneWay);
                Physics2D.SyncTransforms();
                Assert.That(layout.rooms.Where(r => r.main && r.kind == "combat")
                    .Any(r => StageRecoveryStation.FloorPositions(arena, r.Bounds, 3.8f, 3.3f).Count > 0), Is.True, "chapter 2 seed " + seed);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void Surface(GameObject root, Rect bounds, bool oneWay = false)
        {
            var go = new GameObject("Terrain"); go.transform.SetParent(root.transform, false); go.transform.position = bounds.center;
            go.AddComponent<ArenaSurface>().oneWay = oneWay; go.AddComponent<BoxCollider2D>().size = bounds.size;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (changedInputSettings)
            {
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                changedInputSettings = false;
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
