using System.Collections;
using System.Collections.Generic;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class ApprovedMonsterAudioTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        // 2026-11-13: monsters make no sound of their own (attack, hurt, death). Hit sounds belong to the player's weapon.
        [UnityTest] public IEnumerator MonstersMakeNoAttackHurtOrDeathSound()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var texture = new Texture2D(128, 128); var pixels = new Color[128*128];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(.5f, 0), 32);
            var catalog = ScriptableObject.CreateInstance<Chapter1MonsterCatalog>();
            var clips = new List<Chapter1MonsterClip>();
            foreach (string species in new[] { "05_boar", "10_water_bucket" })
                foreach (string motion in new[] { "idle", "move", "attack", "hurt", "death" })
                    clips.Add(new Chapter1MonsterClip { species = species, motion = motion, frames = new[] { sprite }, seconds = new[] { 1f } });
            catalog.clips = clips.ToArray();
            using (var driver = arena.AcquireManualSimulation())
            {
                foreach (string species in new[] { "05_boar", "10_water_bucket" })
                {
                    driver.Reset(); foreach (var e in arena.enemies) { e.aggressive = false; e.Position = new Vector2(40, 0); }
                    arena.Player.Position = Vector2.zero; arena.Player.health = 10000;
                    var actor = arena.SpawnEnemy(0, new Vector2(2, 0)); actor.visual.sprite = sprite;
                    var view = actor.gameObject.AddComponent<Chapter1MonsterPresentation>(); view.actor = actor; view.catalog = catalog; view.species = species;
                    int events = arena.Audio.EventPlayCount;
                    actor.aggressive = true;
                    for (int i = 0; i < 140; i++) { driver.Step(.01f); view.Draw(.01f); arena.Audio.Tick(); }
                    actor.aggressive = false;
                    Assert.That(actor.Receive(1, arena.Player, 1, true, null), Is.True); view.Draw(.01f); arena.Audio.Tick();
                    Assert.That(actor.Receive(10000, arena.Player, 1, true, null), Is.True); view.Draw(.01f); arena.Audio.Tick();
                    Assert.That(arena.Audio.EventPlayCount, Is.EqualTo(events), species + " attack/hurt/death events");
                    Assert.That(arena.Audio.LastSelectedId ?? "", Does.Not.StartWith("sfx_monster"), species + " selected sounds"); // the player may still yelp
                    Assert.That(arena.Audio.LastError, Is.Null);
                }
            }
            Object.Destroy(catalog); Object.Destroy(sprite); Object.Destroy(texture);
            yield return null;
        }
    }
}
