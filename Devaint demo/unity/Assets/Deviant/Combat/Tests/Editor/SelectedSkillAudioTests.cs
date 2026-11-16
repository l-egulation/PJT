using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class SelectedSkillAudioTests
    {
        [Test] public void AllFortySkillsAndBothApprovedGreatswordVariantsLoad()
        {
            var data = JsonUtility.FromJson<ApprovedCombatAudio.Catalog>(Resources.Load<TextAsset>("SelectedSkillAudio/Catalog").text);
            Assert.That(data.entries.Count(e => !e.id.Contains("__") && !e.id.StartsWith("explosion_")), Is.EqualTo(40));
            foreach (var entry in data.entries)
                foreach (var path in entry.variants)
                {
                    var clip = Resources.Load<AudioClip>(path);
                    Assert.That(clip, Is.Not.Null, path);
                    Assert.That(clip.frequency, Is.EqualTo(48000), path);
                }
            var sword = data.entries.Single(e => e.id == "greatsword_default");
            Assert.That(sword.variants.Length, Is.EqualTo(2));
            Assert.That(sword.variants[0], Does.EndWith("_06"));
            Assert.That(sword.variants[1], Does.EndWith("_07"));
            foreach (var id in new[] { "dagger_range", "shield_default", "shield_follow_up", "bomb_move_special" })
                Assert.That(data.entries.Single(e => e.id == id).silent, Is.True, id + " waits for its event");
            var smoke = Resources.Load<AudioClip>(data.entries.Single(e => e.id == "dagger_range__activated").clip);
            Assert.That(smoke.samples, Is.EqualTo(37440));
        }

        [UnityTest] public IEnumerator EventOnlySkillsStaySilentUntilSuccessAndVariantsAlternate()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var go = new GameObject("Selected skill audio integration");go.SetActive(false);
            var arena = go.AddComponent<CombatArena>();arena.loadCharacterArt = false;
            go.SetActive(true);
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Audio test camera").AddComponent<Camera>();
            content.Camera.gameObject.AddComponent<AudioListener>();
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Enemies = Array.Empty<CombatEnemySpawn>();
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.PlayerSpawn = new Vector2(0, 100);
            Assert.That(arena.Initialize(content), Is.True);
            yield return null;
            Assert.That(arena.Audio.LastError, Is.Null);
            using (var driver = arena.AcquireManualSimulation())
            {
                int count = arena.Audio.EventPlayCount;
                Assert.That(arena.Audio.Emit("shield_default", Vector2.zero), Is.True);
                Assert.That(arena.Audio.EventPlayCount, Is.EqualTo(count));
                Assert.That(arena.Audio.EmitSkillEvent("shield_default", "parry", Vector2.zero), Is.True);
                Assert.That(arena.Audio.EventPlayCount, Is.EqualTo(count + 1));
                Assert.That(arena.Audio.LastEventId, Is.EqualTo("shield_default__parry"));
                arena.Audio.Emit("greatsword_default", Vector2.zero);
                Assert.That(arena.Audio.LastVariantClip, Does.EndWith("_06"));
                arena.Audio.Emit("greatsword_default", Vector2.zero);
                Assert.That(arena.Audio.LastVariantClip, Does.EndWith("_07"));
                arena.Audio.Emit("greatsword_default", Vector2.zero);
                Assert.That(arena.Audio.LastVariantClip, Does.EndWith("_06"));
                arena.Audio.Reset();Assert.That(arena.Audio.LiveOutputCount, Is.Zero);
                arena.Audio.EmitSkillEvent("dagger_range", "activated", Vector2.zero);
                Assert.That(arena.Audio.LastVariantClip, Does.Contain("dagger_range__activated"));
                arena.Audio.Reset();
                // Exercise the real accepted input and activation callback,
                // including cancellation before the smoke actually exists.
                Configure(arena, driver, "dagger", "dagger_range");
                count = arena.Audio.EventPlayCount;
                driver.Step(.01f, new CombatInput(pressed: CombatButtons.Special));
                Assert.That(arena.Player.currentCast, Is.Not.Null);
                Assert.That(arena.Audio.EventPlayCount, Is.EqualTo(count));
                arena.Player.currentCast.Cancel();
                for (int i = 0; i < 40; i++) driver.Step(.01f);
                Assert.That(arena.Audio.EventPlayCount, Is.EqualTo(count));
                Configure(arena, driver, "dagger", "dagger_range");
                driver.Step(.01f, new CombatInput(pressed: CombatButtons.Special));
                for (int i = 0; i < 40; i++) driver.Step(.01f);
                Assert.That(arena.Audio.EventPlayCount, Is.EqualTo(count + 1));
                Assert.That(arena.Audio.LastEventId, Is.EqualTo("dagger_range__activated"));
                Configure(arena, driver, "shield", "shield_follow_up");
                count = arena.Audio.EventPlayCount;
                driver.Step(.01f, new CombatInput(pressed: CombatButtons.Special));
                for (int i = 0; i < 40; i++) driver.Step(.01f);
                Assert.That(arena.Audio.EventPlayCount, Is.EqualTo(count), "No installation sound when no floor exists");
                var floor = new GameObject("Audio installation floor");
                floor.transform.position = new Vector3(0, 99.5f, 0);
                floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
                Configure(arena, driver, "shield", "shield_follow_up");
                driver.Step(.01f, new CombatInput(pressed: CombatButtons.Special));
                for (int i = 0; i < 40; i++) driver.Step(.01f);
                Assert.That(arena.Audio.EventPlayCount, Is.EqualTo(count + 1));
                Assert.That(arena.Audio.LastEventId, Is.EqualTo("shield_follow_up__placed"));
                Object.Destroy(floor);
            }
            Object.Destroy(go);
            yield return null;
        }
        static void Configure(CombatArena arena, CombatArena.ManualSimulation driver, string form, string skill)
        {
            driver.Reset(); var player = arena.Player; player.InputBlocked = false; player.selected = 0;
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            int formIndex = Array.FindIndex(arena.catalog.forms, f => f.id == form);
            int specialIndex = Array.FindIndex(arena.catalog.forms[formIndex].specials, s => s.id == skill);
            Assert.That(player.TryReconfigure(0, formIndex, specialIndex, out var error), Is.True, error);
            player.Position = new Vector2(0, 100); player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1);
            Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
