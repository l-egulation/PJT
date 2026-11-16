using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Inventory.Unity;
using Deviant.Run;
using NUnit.Framework;
using Stage1VillageExperiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class CurrencyPowderPresentationTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        [UnityTest] public IEnumerator LatestVillageShowsAllLootAndBrighterLargerGoldPowder()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            StartingVillageWorld village = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(village && village.Ready);)
            { village = Object.FindAnyObjectByType<StartingVillageWorld>(); yield return null; }
            Assert.That(village && village.Ready, Is.True);
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(b => b.GetType().Name == "TemporaryGameGui");
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            gui.GetType().GetField("storyChecked", hidden).SetValue(gui, false);
            yield return null; yield return null;
            gui.GetType().GetMethod("SkipOpening", hidden).Invoke(gui, null);
            yield return null; yield return null;
            var viewport = Object.FindAnyObjectByType<VillagePixelViewport>();
            if (viewport) { viewport.enableInBatchMode = true; viewport.enabled = true; }
            var world = village.World;
            var arena = world.arena;
            arena.Player.Position = village.Find(StartingVillageWorld.ReturnWell).Position + Vector2.right * 1.5f;
            Physics2D.SyncTransforms(); world.RefreshCamera(0, true);
            yield return null; yield return null;
            Directory.CreateDirectory("../outputs/hub/capture");
            Directory.CreateDirectory("../outputs/hub/capture/refined-loot-drop-frames");
            StartingVillageCaptureTests.Capture(gui, village, "latest-village");
            Assert.That(arena.Controls.Current.SimulationPaused, Is.False);

            var economy = new RunEconomy(JsonUtility.FromJson<EconomyData>(File.ReadAllText("../data/combat/chapter1_economy.json")));
            using (var simulation = arena.AcquireManualSimulation())
            {
                // Arrange the existing runtime pickups for art review; this is not a new monster reward table.
                var stash = new RunStash();
                var growth = new WeaponGrowth(JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text), economy);
                var items = new List<Chapter1RewardPickup>();
                string[] colors = { "red", "yellow", "green", "blue", "purple" };
                for (int i = 0; i < colors.Length; i++)
                {
                    var item = new GameObject("Review drop " + colors[i]).AddComponent<Chapter1RewardPickup>();
                    item.Initialize(arena, world.View, null, stash, Chapter1RewardKind.ColoredInk, null,
                        arena.DropPoint(arena.Player.Position, -8 + i * 1.2f, 1));
                    int grade = i % 4 + 1;
                    item.ConfigureGrowth(growth, growth.Data.parts.First(p => p.color == colors[i] && p.grade == grade).id);
                    Assert.That(item.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo($"ink-{colors[i]}-grade-{grade}"));
                    items.Add(item);
                }
                for (int i = 0; i < 2; i++)
                {
                    var item = new GameObject("Review stamp " + i).AddComponent<Chapter1RewardPickup>();
                    item.Initialize(arena, world.View, null, stash, i == 0 ? Chapter1RewardKind.WeaponStamp : Chapter1RewardKind.SkillStamp,
                        i == 0 ? "sword" : null, arena.DropPoint(arena.Player.Position, 7 + i * 1.4f, 1));
                    items.Add(item);
                }
                var landed = items.Select(p => p.transform.position).ToArray();
                var at = arena.Player.Position + Vector2.right * 3;
                var coin = new GameObject("Review local gold").AddComponent<Chapter1RewardPickup>();
                coin.Initialize(arena, world.View, null, null, Chapter1RewardKind.Gold, null, at);
                coin.SetGold(economy, 8);
                var gold = coin.CurrencyEffect;
                var goldOrigin = at;
                Assert.That(economy.Gold, Is.EqualTo(8));
                var pigment = new GameObject("Review local palette").AddComponent<Chapter1PigmentPickupEffect>();
                pigment.Initialize(arena);
                var profile = Chapter1ProfileHost.Service;
                var plan = profile.PlanRun(new[] { "sword", "blunt" });
                int amount = profile.AwardPigment(plan.RunId, "powder-review", "normal", false);
                pigment.Show(at + Vector2.right * 2, amount);
                var palette = pigment.transform.GetChild(0).GetComponent<SpriteRenderer>();
                Assert.That(palette, Is.Not.Null);
                Vector2 paletteOrigin = palette.transform.position;
                var amountLabel = palette.transform.GetChild(0).GetComponent<SpriteRenderer>();
                Assert.That(amountLabel.sprite.name, Is.EqualTo("+" + amount));
                Assert.That(amountLabel.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(amountLabel.enabled, Is.False, "no amount is shown while the palette falls");
                Assert.That(coin.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(1));
                for (int frame = 0; frame < 60; frame++)
                {
                    simulation.Step(.03f);
                    yield return null; yield return null;
                    if (frame == 3)
                    {
                        Assert.That(gold.HasBurst, Is.False);
                        Assert.That(palette.transform.position.y, Is.GreaterThan(paletteOrigin.y));
                        Assert.That(palette.enabled, Is.True);
                        Assert.That(amountLabel.enabled, Is.False);
                        Assert.That(items.All(p => p.transform.position.y > landed[items.IndexOf(p)].y), Is.True, "inks and stamps rise before landing");
                        // Moving the player must not redirect a paid currency's effect.
                        arena.Player.Position += Vector2.left * .25f;
                    }
                    if (frame == 8)
                    {
                        Assert.That(palette.enabled, Is.True, "the palette rests on the floor before disappearing");
                        Assert.That(palette.transform.position.y, Is.EqualTo(paletteOrigin.y).Within(1f / 32));
                        Assert.That(amountLabel.enabled, Is.False);
                    }
                    if (frame == 12)
                    {
                        Assert.That(gold.HasBurst, Is.True);
                        Assert.That(gold.ActiveGrains, Is.GreaterThanOrEqualTo(70), "a denser gold burst");
                        var visibleGrains = gold.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == "Currency powder").ToArray();
                        Assert.That(visibleGrains.All(r => r.transform.lossyScale.x >= 2), Is.True, "larger whole-pixel grains");
                        Assert.That(visibleGrains.Any(r => r.transform.lossyScale.x == 3), Is.True);
                        Assert.That(visibleGrains.All(r => r.color.r > .99f && r.color.g > .8f), Is.True, "bright yellow and cream");
                        Assert.That(palette.enabled, Is.False, "the amount replaces the palette where it landed");
                        Assert.That(amountLabel.enabled, Is.True);
                        Assert.That(palette.transform.position.y, Is.EqualTo(paletteOrigin.y).Within(1f / 32));
                        Assert.That(amountLabel.transform.position.y, Is.GreaterThan(paletteOrigin.y));
                        Assert.That(amountLabel.transform.position.x, Is.EqualTo(paletteOrigin.x).Within(1f / 32));
                        Assert.That(amountLabel.color.a, Is.GreaterThan(.8f));
                        using (var pause = arena.Controls.OpenScope("pigment rise pause test"))
                        {
                            pause.PauseSimulation();
                            var previous = amountLabel.transform.position;
                            var alpha = amountLabel.color.a;
                            yield return null; yield return null;
                            Assert.That(amountLabel.transform.position, Is.EqualTo(previous));
                            Assert.That(amountLabel.color.a, Is.EqualTo(alpha));
                        }
                        Assert.That(gold.BurstNotifications, Is.EqualTo(1));
                        Assert.That(Vector2.Distance(gold.transform.position, goldOrigin), Is.LessThan(1));
                        foreach (var grain in gold.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == "Currency powder"))
                        {
                            var p = grain.transform.position * 32;
                            Assert.That(p.x, Is.EqualTo(Mathf.Round(p.x)).Within(.001f));
                            Assert.That(p.y, Is.EqualTo(Mathf.Round(p.y)).Within(.001f));
                        }
                    }
                    if (frame == 24)
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            Assert.That(items[i].Collected, Is.False);
                            Assert.That(items[i].transform.position.y, Is.EqualTo(landed[i].y).Within(.001f), "inks and stamps remain on their landing spots");
                        }
                        Assert.That(gold.FloorBounces, Is.GreaterThan(0), "the gold grains collide with the real village floor");
                        var grainX = gold.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == "Currency powder").Select(r => r.transform.position.x).ToArray();
                        Assert.That(grainX.Max() - grainX.Min(), Is.GreaterThan(.8f), "the powder spreads sideways over the floor");
                        Assert.That(palette.enabled, Is.False);
                        Assert.That(amountLabel.color.a, Is.InRange(.01f, .7f));
                    }
                    StartingVillageCaptureTests.Capture(gui, village, "refined-loot-drop-frames/frame-" + frame.ToString("000"));
                }
                Assert.That(coin == null, Is.True);
                Assert.That(pigment.ActiveCount, Is.Zero);
                Assert.That(economy.Gold, Is.EqualTo(8));
                Assert.That(profile.Snapshot.Pigment, Is.EqualTo(amount));
                // Elite/boss amounts must use their actual award, not a fixed +1.
                pigment.Show(at, 5);
                Assert.That(pigment.GetComponentsInChildren<SpriteRenderer>().Any(r => r.sprite && r.sprite.name == "+5"), Is.True);
            }
            yield return new ExitPlayMode();
        }
    }
}
