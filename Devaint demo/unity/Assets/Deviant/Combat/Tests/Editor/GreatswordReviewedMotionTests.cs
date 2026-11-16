using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using Deviant.Integration;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class GreatswordReviewedMotionTests
    {
        [Test]
        public void SevenActionsLoadDistinctApprovedMotionsAndWorldEffects()
        {
            var combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var media = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var registry = new MotionSourceRegistry(media.motionRegistry, combat);
            var body = media.sets.Single(set => set.form == "greatsword");
            foreach (var name in new[] { "attack1", "attack2", "attack3", "attack_charge_slam",
                "attack_summon_sword", "attack_spin_advance", "attack_throw_sword" })
            {
                var motion = registry.Motion("greatsword/" + name);
                Assert.That(motion, Is.Not.Null, name);
                foreach (string frame in body.Find(name).frames.Distinct())
                    Assert.That(Resources.Load<Sprite>(frame), Is.Not.Null, frame);
            }
            foreach (var group in new[] { (Name:"fall", Count:18), (Name:"flight", Count:12), (Name:"wave", Count:14) })
                for (int i = 0; i < group.Count; i++)
                    Assert.That(Resources.Load<Sprite>($"CharacterGreatswordReviewed/{group.Name}-{i:00}"), Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator ExitPlay() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest]
        public IEnumerator ReleasedSwordHitsThreeTimesWhileOwnerStartsABasicAttack()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var arena = new GameObject("Greatsword test arena").AddComponent<CombatArena>();
            var setup = CombatContentLoader.Load(null, false);
            setup.Profile = CombatCompositionProfile.Game;
            setup.Camera = new GameObject("Camera").AddComponent<Camera>();
            setup.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            setup.PlayerSpawn = new Vector2(0, 100);
            setup.PlayerSize = new Vector2(.9375f, 2);
            setup.PlayerOffset = new Vector2(.1875f, 1);
            setup.Enemies = new[] { new CombatEnemySpawn("Spin receiver", 0, new Vector2(3, 100),
                new Vector2(.6f, 2), Vector2.up, 1000, false, false, 1) };
            setup.PrepareEnvironment = _ =>
            {
                var floor = new GameObject("Ground");
                floor.transform.position = new Vector2(0, 99.5f);
                floor.AddComponent<ArenaSurface>();
                floor.AddComponent<BoxCollider2D>().size = new Vector2(30, 1);
            };
            Assert.That(arena.Initialize(setup), Is.True);
            var player = arena.Player;
            player.MaintenanceSite = () => true;
            player.SetWeaponSlotReady(0, true);
            Assert.That(player.TrySetChargeStamp(0, false, out var stampError), Is.True, stampError);
            int form = Array.FindIndex(arena.catalog.forms, value => value.id == "greatsword");
            Assert.That(player.TryReconfigure(0, form, 3, out var configureError), Is.True, configureError);
            player.selected = 0;
            player.InputBlocked = false;
            player.UseTestInput = true;
            player.SyncFacing(1);
            Physics2D.SyncTransforms();

            using (var simulation = arena.AcquireManualSimulation())
            {
                Assert.That(arena.HitEvents.TryGetCursor(out var cursor), Is.True);
                simulation.Step(.01f, new CombatInput(0, CombatButtons.Special));
                var sword = player.currentCast;
                Assert.That(sword, Is.Not.Null);
                var execution = sword.ExecutionId;
                for (int i = 0; i < 44; i++) simulation.Step(.01f);
                Assert.That(sword.PresentationActivated, Is.True, $"feet {player.Position}; ground {player.grounded}");
                Assert.That(player.currentCast, Is.Null);
                Assert.That(arena.casts.Contains(sword), Is.True);
                player.SyncFacing(-1);
                simulation.Step(.01f, new CombatInput(0, CombatButtons.Basic));
                Assert.That(player.currentCast, Is.Not.Null);
                Assert.That(player.currentCast.isBasic, Is.True);
                for (int i = 0; i < 79; i++) simulation.Step(.01f);
                var hits = new CombatAcceptedHit[32];
                Assert.That(arena.HitEvents.TryRead(cursor, hits, out var page), Is.True);
                var independent = hits.Take(page.Count).Where(hit => hit.Origin.ExecutionId == execution).ToArray();
                Assert.That(independent, Has.Length.EqualTo(3));
                Assert.That(independent.All(hit => hit.Origin.Kind == CombatHitKind.Independent), Is.True);
                Assert.That(arena.SimulationFaulted, Is.False);
            }
        }

        [Test]
        public void GreatswordChargeHoldsPastDamageCapAndKeepsItsReleaseSwing()
        {
            var data = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var skill = data.forms.Single(form => form.id == "greatsword").specials[0];
            Assert.That(ChargedMeleeDefinition.TryCreate("greatsword", skill, out var charge, out var error), Is.True, error);
            var execution = new ChargeExecution(Guid.NewGuid(), charge.Policy, 0);
            Assert.That(execution.TryRelease(2.5f, .02f, true), Is.False);
            Assert.That(execution.TryRelease(2.6f, .02f, false), Is.True);
            Assert.That(execution.Release.Value.ReleaseTime, Is.EqualTo(2.6f).Within(.001f));
            Assert.That(execution.Release.Value.Damage, Is.EqualTo(75));
            Assert.That(execution.Release.Value.MotionEnd, Is.EqualTo(3.23f).Within(.001f));
            Assert.That(charge.Policy.ReleaseWindup, Is.EqualTo(.18f).Within(.001f));
        }
    }
}
