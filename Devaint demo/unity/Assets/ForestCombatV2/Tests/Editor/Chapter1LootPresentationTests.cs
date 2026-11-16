using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1LootPresentationTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator GeneratedArtRendersWithoutTintAndCurrencyKeepsItsPaymentRules()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons = false; // legacy loot/bag path
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
            var run = rewards.Run;
            Assert.That(run.Growth, Is.Not.Null);
            using (var simulation = arena.AcquireManualSimulation())
            {
                float x = 2.5f;
                foreach (var color in new[] { "red", "yellow", "green", "blue", "purple" })
                {
                    var drop = new GameObject("Review ink " + color).AddComponent<Chapter1RewardPickup>();
                    drop.Initialize(world, run.Stash, Chapter1RewardKind.ColoredInk, null, arena.DropPoint(arena.Player.Position, x));
                    var art = drop.GetComponent<SpriteRenderer>();
                    for (int grade = 1; grade <= 4; grade++)
                    {
                        var part = run.Growth.Data.parts.First(p => p.color == color && p.grade == grade);
                        drop.ConfigureGrowth(run.Growth, part.id);
                        Assert.That(art.sprite.name, Is.EqualTo($"ink-{color}-grade-{grade}"));
                        Assert.That(art.color, Is.EqualTo(Color.white));
                        Assert.That(art.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                        Assert.That(art.sprite.rect.size, Is.EqualTo(new Vector2(16, 16)));
                        Assert.That(art.sprite.pixelsPerUnit, Is.EqualTo(16), "native pixels render at integer 2x on the 32 PPU camera");
                    }
                    x += 1.1f;
                }
                foreach (var kind in new[] { Chapter1RewardKind.WeaponStamp, Chapter1RewardKind.SkillStamp })
                {
                    var drop = new GameObject("Review " + kind).AddComponent<Chapter1RewardPickup>();
                    drop.Initialize(world, run.Stash, kind, "sword", arena.DropPoint(arena.Player.Position, x));
                    Assert.That(drop.GetComponent<SpriteRenderer>().sprite.name,
                        Is.EqualTo(kind == Chapter1RewardKind.WeaponStamp ? "weapon-stamp-refined" : "skill-stamp-refined"));
                    x += 1.1f;
                }
                var coin = new GameObject("Review gold").AddComponent<Chapter1RewardPickup>();
                coin.Initialize(world, run.Stash, Chapter1RewardKind.Gold, null, arena.DropPoint(arena.Player.Position, 1.5f));
                int before = run.Economy.Gold;
                coin.SetGold(run, 8);
                Assert.That(run.Economy.Gold, Is.EqualTo(before + 8));
                Assert.That(coin.TryCollect(), Is.False);
                Assert.That(coin.GetComponent<SpriteRenderer>().sprite.bounds.size.y, Is.EqualTo(.75f));
                simulation.Step(.4f);
                yield return null;
                var pigment = world.GetComponent<Chapter1PigmentPickupEffect>();
                pigment.Show(arena.DropPoint(arena.Player.Position, .8f), 1);
                var palette = pigment.transform.GetChild(pigment.transform.childCount - 1).GetComponent<SpriteRenderer>();
                Assert.That(palette.sprite.name, Is.EqualTo("pigment-palette-flat"));
                Assert.That(palette.sprite.rect.size, Is.EqualTo(new Vector2(16, 16)));
                Assert.That(palette.sprite.pixelsPerUnit, Is.EqualTo(16));
                Assert.That(run.Economy.Gold, Is.EqualTo(before + 8), "the pigment view never grants gold");
                world.RefreshCamera(0, true);
                var view = world.View;
                var rt = RenderTexture.GetTemporary(960, 540, 24);
                var previousTarget = view.targetTexture; var previousRect = view.rect;
                var previousActive = RenderTexture.active;
                view.targetTexture = rt; view.rect = new Rect(0, 0, 1, 1); view.Render(); RenderTexture.active = rt;
                var shot = new Texture2D(960, 540, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); shot.Apply();
                Directory.CreateDirectory("../outputs/monster-loot");
                File.WriteAllBytes("../outputs/monster-loot/in-game-drops.png", shot.EncodeToPNG());
                Object.Destroy(shot); RenderTexture.active = previousActive;
                view.targetTexture = previousTarget; view.rect = previousRect; RenderTexture.ReleaseTemporary(rt);
            }
            yield return new ExitPlayMode();
        }
    }
}
