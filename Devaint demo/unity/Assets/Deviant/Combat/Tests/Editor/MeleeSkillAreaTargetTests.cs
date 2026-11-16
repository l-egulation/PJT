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
    public class MeleeSkillAreaTargetTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation driver;
        float varianceMin, varianceMax;
        CombatPlayer Player => arena.Player;
        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            arena = new GameObject("AreaSkillArena").AddComponent<CombatArena>(); arena.loadCharacterArt = false;
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game; content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Camera = new GameObject("AreaSkillCamera").AddComponent<Camera>();
            content.Camera.orthographic = true; content.Camera.orthographicSize = 5;
            content.Camera.transform.position = new Vector3(0, 1002, -10);
            content.PlayerSpawn = new Vector2(0, 1000); content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.Enemies = Enumerable.Range(0, 4).Select(i => new CombatEnemySpawn("AreaTarget" + i, i,
                new Vector2(i == 3 ? 50 : 1.15f + i * .15f, 1000), new Vector2(.4f, 2), Vector2.up, 10000, true, false, 0)).ToArray();
            content.PrepareEnvironment = _ => {
                var floor = new GameObject("AreaSkillFloor"); floor.transform.position = new Vector2(0, 999.5f);
                floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            };
            Assert.That(arena.Initialize(content), Is.True);
            varianceMin = CombatDamageVariance.Settings.minimum; varianceMax = CombatDamageVariance.Settings.maximum;
            CombatDamageVariance.Settings.minimum = CombatDamageVariance.Settings.maximum = 1;
            driver = arena.AcquireManualSimulation();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            driver?.Dispose();
            CombatDamageVariance.Settings.minimum = varianceMin; CombatDamageVariance.Settings.maximum = varianceMax;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        void Configure(string id, int facing = 1)
        {
            driver.Reset(); Player.selected = 0; Player.InputBlocked = false;
            Player.SetWeaponSlotReady(0, true); Player.SetWeaponSlotReady(1, true);
            foreach (var weapon in Player.weapons) weapon.StatSlots.Clear();
            int form = Array.FindIndex(arena.catalog.forms, f => Array.Exists(f.specials, s => s.id == id));
            int special = Array.FindIndex(arena.catalog.forms[form].specials, s => s.id == id);
            Assert.That(Player.TryReconfigure(0, form, special, out var error), Is.True, error);
            Player.Position = new Vector2(0, 1000); Player.velocity = Vector2.zero; Player.grounded = true; Player.SyncFacing(facing);
            for (int i = 0; i < arena.enemies.Count; i++) {
                var enemy = arena.enemies[i]; enemy.aggressive = false; enemy.isBoss = false; enemy.facing = -facing;
                enemy.Position = new Vector2((i == 3 ? 50 : 1.15f + i * .15f) * facing, 1000); enemy.velocity = Vector2.zero;
            }
            Physics2D.SyncTransforms();
        }
        CombatCast Start()
        {
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Weapon1Special));
            Assert.That(Player.currentCast, Is.Not.Null); return Player.currentCast;
        }
        void Until(float end)
        {
            while (arena.Now < end) driver.Step(.01f);
            Assert.That(arena.SimulationFaulted, Is.False);
        }
        void Damage(int index, float amount, int count, string context)
        {
            Assert.That(arena.enemies[index].health, Is.EqualTo(10000 - amount).Within(.001f), context + " target=" + index);
            Assert.That(arena.enemies[index].ReceivedHitCount, Is.EqualTo(count), context + " duplicate/missing hit target=" + index);
        }
        [UnityTest] public IEnumerator FormerSingleTargetAndExistingAreaSkillsHitAllThreeInBothDirections()
        {
            foreach (int facing in new[] { 1, -1 })
            foreach (var spec in new[] { (id:"rapier_default",damage:51f,hits:1), (id:"greatsword_default",damage:70f,hits:1),
                (id:"whip_default",damage:40f,hits:1), (id:"whip_movement",damage:60f,hits:1),
                (id:"whip_follow_up",damage:75f,hits:2), (id:"rapier_movement",damage:55f,hits:1),
                (id:"hammer_movement",damage:82f,hits:2), (id:"rapier_follow_up",damage:110f,hits:5),
                (id:"dagger_follow_up",damage:126f,hits:6) })
            {
                Configure(spec.id, facing); Start(); Until(arena.Now + 2);
                for (int i = 0; i < 3; i++) Damage(i, spec.damage, spec.hits, spec.id + "/" + facing);
                Damage(3, 0, 0, spec.id + " out of range");
                Assert.That(Player.Weapon.Cooldown.Available, Is.Zero, spec.id);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ChargeCanHitALateEntrantAndCannotHitThroughASolidWall()
        {
            Configure("rapier_default"); arena.enemies[2].Position = new Vector2(50, 1000);
            arena.enemies[3].Position = new Vector2(3, 1000);
            var wall = new GameObject("AreaSkillWall"); wall.transform.position = new Vector2(2.4f, 1001);
            wall.AddComponent<ArenaSurface>(); wall.AddComponent<BoxCollider2D>().size = new Vector2(.15f, 4); Physics2D.SyncTransforms();
            var cast = Start(); Until(cast.StartedAt + .215f);
            arena.enemies[2].Position = new Vector2(1.5f, 1000); Physics2D.SyncTransforms(); Until(cast.StartedAt + .6f);
            for (int i = 0; i < 3; i++) Damage(i, 51, 1, "late entrant");
            Damage(3, 0, 0, "solid occlusion"); yield return null;
        }
        [UnityTest] public IEnumerator EveryMarkedEnemyKeepsItsOwnFollowUpWhenTheNearestDies()
        {
            Configure("whip_follow_up"); var cast = Start(); Until(cast.StartedAt + .4f);
            for (int i = 0; i < 3; i++) Damage(i, 30, 1, "primary marks");
            arena.enemies[0].health = 0;
            arena.enemies[1].Position = new Vector2(20, 1000); // Its admitted mark survives leaving the original area.
            arena.enemies[3].Position = new Vector2(1.4f, 1000); // Never marked by the primary.
            Physics2D.SyncTransforms(); Until(cast.StartedAt + 1);
            Damage(1, 75, 2, "living remote mark"); Damage(2, 75, 2, "living mark after leader death");
            Damage(3, 0, 0, "no transfer to unmarked entrant"); yield return null;
        }
        [UnityTest] public IEnumerator TargetlessTeleportCanHitEnemiesEnteringItsInPlaceAttack()
        {
            Configure("rapier_movement"); foreach (var enemy in arena.enemies) enemy.Position = new Vector2(50, 1000);
            Physics2D.SyncTransforms(); var cast = Start(); Until(cast.StartedAt + .12f);
            Assert.That(Player.Position.x, Is.EqualTo(0).Within(.001f));
            for (int i = 0; i < 3; i++) arena.enemies[i].Position = new Vector2(1.15f + i * .15f, 1000);
            Physics2D.SyncTransforms(); Until(cast.StartedAt + .5f);
            for (int i = 0; i < 3; i++) Damage(i, 55, 1, "in-place teleport whiff accepts late enemies");
            yield return null;
        }
        [UnityTest] public IEnumerator BossStoppingARushDoesNotDropOtherVictimsEndDamage()
        {
            Configure("hammer_movement"); arena.enemies[0].isBoss = true;
            var before = arena.enemies[0].Position;
            Start(); Until(arena.Now + 1);
            for (int i = 0; i < 3; i++) Damage(i, 82, 2, "boss stops the entered area sweep");
            Assert.That(arena.enemies[0].Position.x, Is.EqualTo(before.x).Within(.001f));
            yield return null;
        }
    }
}
