using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class CombatAudioPlaybackTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator SkillVolumeReachesAttackEventAndHitSourcesWithoutChangingBasicAttack()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var go = new GameObject("Skill volume fixture"); go.SetActive(false);
            var arena = go.AddComponent<CombatArena>(); arena.loadCharacterArt = false; go.SetActive(true);
            var content = CombatContentLoader.Load(null, false); content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Skill volume camera", typeof(AudioListener)).AddComponent<Camera>();
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout(); content.Enemies = Array.Empty<CombatEnemySpawn>();
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1); content.PlayerSpawn = new Vector2(0, 100);
            Assert.That(arena.Initialize(content), Is.True); yield return null;
            CheckSkillSources(arena);
            Object.Destroy(go); Object.Destroy(content.Camera.gameObject);
            yield return new ExitPlayMode();
        }
        static void CheckSkillSources(CombatArena arena)
        {
            string[] ids = { "dagger_range", "shield_follow_up", "bomb_range_special", "launcher_basic_special", "launcher_range_special" };
            var saved = ids.ToDictionary(SkillAudioSettings.Key, id => PlayerPrefs.HasKey(SkillAudioSettings.Key(id)) ? PlayerPrefs.GetFloat(SkillAudioSettings.Key(id)) : (float?)null);
            try
            {
                foreach (string id in ids) SkillAudioSettings.Set(id, .25f);
                using (var driver = arena.AcquireManualSimulation())
                {
                    foreach (string cue in new[] { "dagger_range__activated", "shield_follow_up__placed", "bomb_range_special__explosion", "launcher_charge_impact", "launcher_basic_special" })
                    {
                        arena.Audio.Reset();
                        Assert.That(arena.Audio.Emit(cue, Vector2.zero), Is.True, cue);
                        Assert.That(arena.GetComponentsInChildren<AudioCueOutputBinding>().Single().Source.volume, Is.EqualTo(.25f).Within(.001f), cue);
                    }
                    arena.Audio.Reset();
                    Assert.That(arena.Audio.EmitSelected("sfx_hit_launcher_laser", Vector2.zero, "launcher_range_special"), Is.True);
                    Assert.That(arena.GetComponentsInChildren<AudioCueOutputBinding>().Single().Source.volume, Is.EqualTo(.25f).Within(.001f));
                    arena.Audio.Reset();
                    Assert.That(arena.Audio.Emit("launcher_basic_0", Vector2.zero), Is.True);
                    Assert.That(arena.GetComponentsInChildren<AudioCueOutputBinding>().Single().Source.volume, Is.EqualTo(WeaponAudioSettings.Get("launcher")).Within(.001f));
                    SkillAudioSettings.Set("launcher_basic_special", 0);
                    arena.Audio.Reset(); arena.Audio.Emit("launcher_charge_impact", Vector2.zero);
                    Assert.That(arena.GetComponentsInChildren<AudioCueOutputBinding>().Single().Source.volume, Is.Zero);
                }
            }
            finally
            {
                arena.Audio.Reset();
                foreach (var item in saved)
                    if (item.Value.HasValue) PlayerPrefs.SetFloat(item.Key, item.Value.Value); else PlayerPrefs.DeleteKey(item.Key);
                PlayerPrefs.Save();
            }
        }

        [UnityTest] public IEnumerator NonWeaponSoundsKeepLegacyPlaybackWhileWeaponSoundsStartImmediately()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var go = new GameObject("Audio routing fixture"); go.SetActive(false);
            var arena = go.AddComponent<CombatArena>(); arena.loadCharacterArt = false; go.SetActive(true);
            var content = CombatContentLoader.Load(null, false); content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Audio routing camera", typeof(AudioListener)).AddComponent<Camera>();
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout(); content.Enemies = Array.Empty<CombatEnemySpawn>();
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1); content.PlayerSpawn = new Vector2(0, 100);
            Assert.That(arena.Initialize(content), Is.True); yield return null;
            foreach (string cue in new[] { "sfx_player_step_dirt", "sfx_player_step_grass", "sfx_player_step_stone", "sfx_player_step_wood",
                "sfx_player_heal", "sfx_player_hurt", "boar_attack", "boar_hurt", "bucket_death" })
            {
                arena.Audio.Reset();
                Assert.That(cue.StartsWith("sfx_") ? arena.Audio.EmitSelected(cue, Vector2.zero) : arena.Audio.Emit(cue, Vector2.zero), Is.True, cue);
                var output = arena.GetComponentsInChildren<AudioCueOutputBinding>().Single();
                Assert.That(output.Source.isPlaying, Is.False, cue + " must retain its held first sample.");
                Assert.That(output.SeekCount, Is.GreaterThan(1), cue + " must use the original sampled path.");
                // The heal sound plays at half gain (user: too loud, 2026-11-16); the rest keep 0.8.
                Assert.That(output.Source.volume, Is.EqualTo(cue == "sfx_player_heal" ? .4f : .8f).Within(.001f), cue);
            }
            // A bomb basic attack (blast radius 3) plays the triple-throw's explosion_bomb, not the large blast;
            // an explosion without a known action keeps the radius rule.
            arena.Audio.Reset();
            var bombBasic = new CombatHitOrigin(default, default, Guid.NewGuid(), 0, null, Guid.Empty, CombatHitKind.Blast, actionId: "bomb_basic_0");
            arena.BombExploded(Vector2.zero, 3f, false, null, bombBasic);
            Assert.That(arena.Audio.LastEventId, Is.EqualTo("explosion_bomb"), "bomb basic attack explosion");
            arena.Audio.Reset();
            arena.BombExploded(Vector2.zero, 3f);
            Assert.That(arena.Audio.LastEventId, Is.EqualTo("explosion_large"), "unknown radius-3 blast keeps the large explosion");
            // Traversal one-shots joined the continuous path (2026-11-16) but keep the fixed 80% gain.
            foreach (string cue in new[] { "sfx_player_jump", "sfx_player_double_jump", "sfx_player_dash", "sfx_player_wall_kick" })
            {
                arena.Audio.Reset();
                Assert.That(arena.Audio.EmitSelected(cue, Vector2.zero), Is.True, cue);
                var output = arena.GetComponentsInChildren<AudioCueOutputBinding>().Single();
                Assert.That(output.Source.isPlaying, Is.True, cue + " must start now on the continuous path.");
                Assert.That(output.SeekCount, Is.EqualTo(1), cue + " must not re-seek while it plays.");
                Assert.That(output.Source.volume, Is.EqualTo(.8f).Within(.001f), cue + " keeps its fixed traversal gain.");
            }
            foreach (string cue in new[] { "base_sword_basic_0", "dagger_range__activated", "launcher_charge_impact", "explosion_bomb",
                "sfx_hit_whip", "sfx_hit_launcher_laser", "sfx_combat_hit_ink" })
            {
                arena.Audio.Reset();
                Assert.That(cue.StartsWith("sfx_") ? arena.Audio.EmitSelected(cue, Vector2.zero) : arena.Audio.Emit(cue, Vector2.zero), Is.True, cue);
                var output = arena.GetComponentsInChildren<AudioCueOutputBinding>().Single();
                Assert.That(output.Source.isPlaying, Is.True, cue + " must retain the combat playback fix.");
                Assert.That(output.SeekCount, Is.EqualTo(1), cue);
            }
            arena.Audio.Reset(); Assert.That(arena.Audio.LastError, Is.Null);
            Object.Destroy(go); Object.Destroy(content.Camera.gameObject);
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator CurrentHitStartsImmediatelyAndDoesNotRestartOnFixedClockPolls()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var go = new GameObject("Continuous combat audio fixture"); go.SetActive(false);
            var arena = go.AddComponent<CombatArena>(); arena.loadCharacterArt = false; go.SetActive(true);
            var content = CombatContentLoader.Load(null, false); content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Audio playback camera", typeof(AudioListener)).AddComponent<Camera>();
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout(); content.Enemies = Array.Empty<CombatEnemySpawn>();
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1); content.PlayerSpawn = new Vector2(0, 100);
            Assert.That(arena.Initialize(content), Is.True); yield return null;
            Assert.That(arena.Audio.EmitSelected("sfx_hit_whip", Vector2.zero), Is.True);
            var output = Object.FindObjectsByType<AudioCueOutputBinding>(FindObjectsSortMode.None).Single(v => v.Source.clip == SelectedAudioClips.Find("sfx_hit_whip"));
            var source = output.Source;
            Assert.That(source.isPlaying, Is.True, "An accepted hit must start now, without waiting for the next 20 ms simulation step.");
            Assert.That(source.clip, Is.SameAs(SelectedAudioClips.Find("sfx_hit_whip")));
            arena.Audio.Tick(); arena.Audio.Tick();
            Assert.That(source.isPlaying, Is.True, "Duplicate polls must not pause audible gameplay output.");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(!source || !source.isPlaying, Is.True, "A finished short clip must never be restarted by later polls.");
            Assert.That(arena.Audio.LastError, Is.Null);
            arena.Audio.Reset(); Assert.That(arena.Audio.LiveOutputCount, Is.Zero);
            Object.Destroy(go); Object.Destroy(content.Camera.gameObject);
            yield return new ExitPlayMode();
        }
    }
}
