using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // Loot leaves the monster's body, scatters on a parabola, bounces and only then becomes collectable.
    public sealed class Chapter1LootTossTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        { LootTossSettings.Override(null); if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test] public void SameKillRepeatsItsSpreadAndEachDropDiffers()
        {
            var settings = new LootTossSettings();
            var source = new Vector2(3, 0); var rest = new Vector2(3.8f, .5f);
            var a = LootToss.Plan(null, source, rest, .5f, 0, settings);
            var b = LootToss.Plan(null, source, rest, .5f, 0, settings);
            var c = LootToss.Plan(null, source, rest, .5f, 1, settings);
            Assert.That(b.Rest, Is.EqualTo(a.Rest)); Assert.That(b.Duration, Is.EqualTo(a.Duration)); Assert.That(b.Apex, Is.EqualTo(a.Apex));
            Assert.That(c.Rest != a.Rest || !Mathf.Approximately(c.Apex, a.Apex), Is.True, "another drop of the same kill takes its own path");
            Assert.That(a.Launch.y, Is.EqualTo(source.y + settings.launchLift).Within(1e-4f), "drops leave the body, not the floor");
            Assert.That(a.Bounces, Is.EqualTo(settings.bounces));
            Assert.That(a.Evaluate(0), Is.EqualTo(a.Launch));
            Assert.That(a.Evaluate(a.FlightSeconds).y, Is.EqualTo(a.Rest.y).Within(1e-3f), "the first touchdown is on the floor");
            float peak = 0; for (int i = 0; i <= 100; i++) peak = Mathf.Max(peak, a.Evaluate(a.FlightSeconds * i / 100f).y);
            Assert.That(peak - a.Launch.y, Is.EqualTo(a.Apex).Within(.02f));
            float bounceTop = 0; for (int i = 0; i <= 100; i++) bounceTop = Mathf.Max(bounceTop, a.Evaluate(a.FlightSeconds + (a.Duration - a.FlightSeconds) * i / 100f).y);
            Assert.That(bounceTop - a.Rest.y, Is.EqualTo(settings.bounceHeight).Within(.02f));
            Assert.That(a.Evaluate(a.Duration + 1), Is.EqualTo(a.Rest));
            var single = LootToss.Plan(null, source, new Vector2(source.x, .5f), .5f, 0, settings);
            Assert.That(Mathf.Abs(single.Rest.x - source.x), Is.GreaterThan(settings.minSpread - settings.spreadJitter - .01f), "a lone drop still scatters sideways");
        }

        [Test] public void ShippedNumbersLoadFromTheDataFile()
        {
            LootTossSettings.Override(null);
            var text = Resources.Load<TextAsset>("LootDropToss");
            Assert.That(text, Is.Not.Null);
            var shipped = JsonUtility.FromJson<LootTossSettings>(text.text);
            Assert.That(LootTossSettings.Current.apexHeight, Is.EqualTo(shipped.apexHeight));
            Assert.That(LootTossSettings.Current.bounces, Is.EqualTo(shipped.bounces));
        }

        [UnityTest] public IEnumerator EliteLootIsThrownLandsOnFloorAndWaitsUntilRest()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (int frame = 0; frame < 600; frame++)
            {
                world = Object.FindAnyObjectByType<ForestCombatWorld>();
                if (world && world.Ready && world.GetComponent<Chapter1Rewards>()) break;
                yield return null;
            }
            Assert.That(world && world.Ready, Is.True);
            var arena = world.arena;
            foreach (var enemy in arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }
            var rewards = world.GetComponent<Chapter1Rewards>();
            var elite = arena.enemies.Single(e => e && Chapter1EnemyRole.Of(e) == Chapter1EnemyRole.Elite);
            arena.Player.Position = elite.Position + Vector2.left * 4; arena.Player.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
            Directory.CreateDirectory("../outputs/loot-toss/capture");
            using (var simulation = arena.AcquireManualSimulation())
            {
                world.RefreshCamera(0, true);
                int before = rewards.Pickups.Count;
                elite.health = 0;
                for (int frame = 0; frame < 60 && rewards.Pickups.Count == before; frame++) { simulation.Step(1 / 60f); yield return null; }
                var thrown = rewards.Pickups.Where(p => p.Kind != Chapter1RewardKind.Gold && p.Toss != null).ToArray();
                Assert.That(thrown.Length, Is.GreaterThanOrEqualTo(3), "elite colored inks are thrown");
                Assert.That(thrown.Select(p => p.Toss.Rest).Distinct().Count(), Is.EqualTo(thrown.Length), "each drop lands in its own spot");
                float longest = thrown.Max(p => p.Toss.Duration), born = arena.Now - 1 / 60f;
                Assert.That(thrown.All(p => p.transform.position.y > p.Toss.Rest.y + .1f), Is.True, "drops start in the monster's body");
                for (int shot = 0; shot * (1 / 30f) < longest + .25f; shot++)
                {
                    foreach (var pickup in thrown)
                        if (arena.Now - born < pickup.Toss.Duration - .03f)
                        {
                            Assert.That(pickup.Ready, Is.False);
                            Assert.That(pickup.TryCollect(), Is.False, "nothing is collected in flight");
                        }
                    Capture(world, "../outputs/loot-toss/capture/frame_" + shot.ToString("00") + ".png");
                    simulation.Step(1 / 30f); yield return null;
                }
                foreach (var pickup in thrown)
                {
                    Assert.That(pickup.Ready, Is.True);
                    Assert.That((Vector2)pickup.transform.position, Is.EqualTo(LootToss.Snap(pickup.Toss.Rest)));
                    var floor = arena.FindFloor(pickup.Toss.Rest, 4);
                    Assert.That(floor.HasValue, Is.True, "no drop comes to rest over a pit");
                    Assert.That(pickup.Toss.Rest.y - floor.Value, Is.LessThan(.6f), "drops rest on the floor");
                    foreach (var hit in Physics2D.OverlapPointAll(pickup.Toss.Rest))
                        Assert.That(hit.GetComponent<ArenaSurface>() is ArenaSurface s && !s.oneWay, Is.False, "no drop rests inside terrain");
                }
            }
            yield return new ExitPlayMode();
        }

        static void Capture(ForestCombatWorld world, string path)
        {
            var view = world.View;
            var rt = RenderTexture.GetTemporary(960, 540, 24);
            var previousTarget = view.targetTexture; var previousRect = view.rect; var previousActive = RenderTexture.active;
            view.targetTexture = rt; view.rect = new Rect(0, 0, 1, 1); view.Render(); RenderTexture.active = rt;
            var shot = new Texture2D(960, 540, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); shot.Apply();
            File.WriteAllBytes(path, shot.EncodeToPNG());
            Object.Destroy(shot); RenderTexture.active = previousActive;
            view.targetTexture = previousTarget; view.rect = previousRect; RenderTexture.ReleaseTemporary(rt);
        }
    }
}
