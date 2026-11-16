using System;
using System.Collections;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // Kill gauge -> paused offer -> confirm/equip -> player weapon, and durability from real accepted hits.
    public sealed class SketchRunBindingTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static Chapter1RunBinding ReadyBinding()
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            var binding = Object.FindAnyObjectByType<Chapter1RunBinding>();
            return world && world.Ready && binding && binding.Sketch ? binding : null;
        }

        [UnityTest] public IEnumerator FilledGaugeBanksARewardThatOpensPausedAndTheChosenWeaponReachesSlotA()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            SceneManager.LoadScene(Chapter1MapSelector.ProceduralScene);
            for (int frame = 0; frame < 600 && !ReadyBinding(); frame++) yield return null;
            yield return null; yield return null;
            var binding = ReadyBinding();
            Assert.That(binding, Is.Not.Null);
            var arena = binding.World.arena; var sketch = binding.Sketch; var state = sketch.State;
            foreach (var enemy in arena.enemies) if (enemy) enemy.aggressive = false;
            Assert.That(state.Gauge, Is.Zero);
            int departureForm = arena.Player.weapons[0].FormIndex;

            // Normal kills fill the gauge by size until one reward is banked: the run's first needs only 15 (2-4 monsters).
            int killed = 0;
            foreach (var enemy in arena.enemies)
            {
                if (state.Banked > 0 || killed >= 40) break;
                if (!enemy || enemy.Dead || Chapter1EnemyRole.Of(enemy) != Chapter1EnemyRole.Normal || enemy.GetComponent<Chapter1NoKillReward>()) continue;
                enemy.health = 0; killed++;
                yield return null; yield return null;
            }
            Assert.That(state.Banked, Is.EqualTo(1), "a filled gauge banks one reward");
            Assert.That(killed, Is.InRange(2, 4), "the run's first reward needs 15 (user decision 2026-11-13)");
            Assert.That(state.PendingOffer, Is.Null, "a banked reward waits for the player");
            Assert.That(state.OpenBanked(), Is.True, "G opens it");
            yield return null; yield return null;
            Assert.That(arena.Controls.Current.SimulationPaused, Is.True, "the offer pauses combat");

            Assert.That(state.PendingOffer, Has.All.Matches<SketchCandidate>(c => c.IsInk && state.IsStandaloneInk(c.Ink)));
            string ink = state.PendingOffer[0].Ink;
            state.Choose(0);
            yield return null; yield return null;
            Assert.That(state.Arsenal.RemainingInkUses(ink), Is.EqualTo(10));
            Assert.That(arena.Controls.Current.SimulationPaused, Is.True, "the blueprint choice keeps combat paused");
            Assert.That(state.PendingOffer, Has.All.Matches<SketchCandidate>(c => !c.IsInk));
            int weaponIndex = 0;
            var weapon = state.Choose(weaponIndex);
            Assert.That(weapon, Is.Not.Null, "choosing a weapon candidate returns the new weapon");
            Assert.That(weapon.Slots.Count, Is.EqualTo(4));
            yield return null; yield return null;
            Assert.That(arena.Controls.Current.SimulationPaused, Is.False, "taking the offer resumes combat");
            Assert.That(arena.Player.weapons[0].FormIndex, Is.EqualTo(departureForm), "an unconfirmed weapon is not usable and the departure loadout stays");

            state.Arsenal.Confirm(weapon.Id);
            state.Arsenal.Equip(weapon.Id, SketchArsenal.SlotA);
            yield return null; yield return null;
            // No lambda over locals here: captured locals do not survive EnterPlayMode's domain reload.
            int form = -1;
            for (int i = 0; i < arena.catalog.forms.Length; i++) if (arena.catalog.forms[i] != null && arena.catalog.forms[i].id == weapon.Form) { form = i; break; }
            Assert.That(arena.Player.weapons[0].FormIndex, Is.EqualTo(form));
            Assert.That(arena.Player.IsWeaponSlotReady(0), Is.True);

            // A real accepted hit stamped with slot A's combat weapon wears the sketch weapon by the HP removed.
            var target = (CombatActor)null;
            foreach (var enemy in arena.enemies) if (enemy && !enemy.Dead && Chapter1EnemyRole.Of(enemy) == Chapter1EnemyRole.Normal) { target = enemy; break; }
            target.health = target.maxHealth = 1000;
            arena.TryBindActor(arena.Player, out var source);
            var w = arena.Player.weapons[0];
            var origin = new CombatHitOrigin(source, default, Guid.NewGuid(), 0, "sketch_fixture", Guid.Empty, CombatHitKind.Melee,
                stamp: new CombatAttackStamp(w.InstanceId, weapon.Form, "sketch_fixture", "test", true, -1, w.InkPalette.Read()), sourceFacing: 1);
            target.Receive(40, arena.Player, 1, true, null, origin: origin);
            yield return null; yield return null;
            Assert.That(weapon.DurabilityUsed, Is.EqualTo(1000 - target.health).Within(0.01f));
            Assert.That(weapon.DurabilityUsed, Is.GreaterThan(0));
            yield return new ExitPlayMode();
        }
    }
}
