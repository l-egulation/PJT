using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class MeleeRearCoverageTests
    {
        [Test] public void EveryBasicHitKeepsItsFrontReachAndCoversTheBodyRear()
        {
            var source = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var expected = new[] {
                (form: "base_sword", fronts: new[] { 2.65625f, 2.65625f }),
                (form: "whip", fronts: new[] { 4.15625f, 4.15625f }),
                (form: "rapier", fronts: new[] { 3.15625f, 3.15625f, 3.15625f }),
                (form: "dagger", fronts: new[] { 2.25625f, 2.25625f, 2.25625f, 2.25625f }),
                (form: "hammer", fronts: new[] { 2.85625f, 2.85625f }),
                (form: "shield", fronts: new[] { 1.95625f }),
                (form: "greatsword", fronts: new[] { 3.45625f, 3.45625f, 3.45625f })
            };
            Assert.That(BasicMeleeCatalog.TryCreate(source, out var catalog, out var error), Is.True, error);
            foreach (var entry in expected) for (int index = 0; index < entry.fronts.Length; index++)
            {
                var hit = catalog.Find(entry.form, index);
                Assert.That(hit.OffsetX + hit.Width / 2, Is.EqualTo(entry.fronts[index]).Within(.00001f), hit.Id);
                if (entry.form == "greatsword" && index < 2)
                    Assert.That(hit.OffsetX, Is.Zero, "Existing attacks on both sides remain symmetric.");
                else
                    Assert.That(hit.OffsetX - hit.Width / 2, Is.EqualTo(-.53125f).Within(.00001f), hit.Id);
            }
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator ActualBasicAndSpecialHitsCoverOverlapAndRearWithFiniteLimitsBothWays()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var arena = new GameObject("Rear coverage arena").AddComponent<CombatArena>();
            arena.loadCharacterArt = false;
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Rear coverage camera").AddComponent<Camera>();
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.PlayerSize = new Vector2(.9375f, 2);
            content.PlayerOffset = new Vector2(.1875f, 1);
            content.PlayerSpawn = new Vector2(0, 100);
            // A narrow receiver exposes the dead zone that large targets can hide.
            content.Enemies = new[] { new CombatEnemySpawn("Rear target", 0, new Vector2(0, 100),
                new Vector2(.1f, 2), Vector2.up, 10000, false, false, 1) };
            Assert.That(arena.Initialize(content), Is.True);
            arena.Player.UseTestInput = true;
            var forms = new[] { "base_sword", "whip", "rapier", "dagger", "hammer", "shield", "greatsword" };
            var specials = new[] { "base_sword_default", "base_sword_movement", "base_sword_follow_up", "whip_default", "whip_range",
                "whip_movement", "whip_follow_up", "rapier_default", "rapier_range", "dagger_movement", "dagger_follow_up",
                "hammer_default", "hammer_movement", "greatsword_default" };
            using (var driver = arena.AcquireManualSimulation())
            {
                // Shield push is the documented exception: a root-symmetric box reaching 2 unit behind.
                foreach (int facing in new[] { 1, -1 })
                {
                    Check(arena, driver, "shield", "shield_movement", facing, 0, true);
                    Check(arena, driver, "shield", "shield_movement", facing, -1.9f, true);
                    Check(arena, driver, "shield", "shield_movement", facing, -2.2f, false);
                }
                foreach (int facing in new[] { 1, -1 })
                {
                    foreach (string form in forms)
                    {
                        Check(arena, driver, form, null, facing, 0, true);
                        Check(arena, driver, form, null, facing, -.45f, true);
                        if (form != "greatsword") Check(arena, driver, form, null, facing, -.65f, false);
                    }
                    foreach (string id in specials)
                    {
                        string form = arena.catalog.forms.Single(value => value.specials.Any(skill => skill.id == id)).id;
                        Check(arena, driver, form, id, facing, 0, true);
                        Check(arena, driver, form, id, facing, -.45f, true);
                        Check(arena, driver, form, id, facing, -.65f, false);
                    }
                }
                Assert.That(arena.SimulationFaulted, Is.False);
            }
        }

        static void Check(CombatArena arena, CombatArena.ManualSimulation driver, string formId, string specialId, int facing, float x, bool expectedHit)
        {
            driver.Reset();
            var player = arena.Player; var target = arena.enemies[0];
            int form = Array.FindIndex(arena.catalog.forms, value => value.id == formId);
            int special = specialId == null ? 0 : Array.FindIndex(arena.catalog.forms[form].specials, value => value.id == specialId);
            Assert.That(player.TryReconfigure(0, form, special, out var error), Is.True, error);
            player.selected = 0; player.InputBlocked = false;
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            player.Position = new Vector2(0, 100); player.velocity = Vector2.zero; player.grounded = false; player.SyncFacing(facing);
            target.Position = player.Position + Vector2.right * (x * facing); target.velocity = Vector2.zero; target.grounded = false;
            target.aggressive = false; target.maxHealth = target.health = 10000;
            Physics2D.SyncTransforms();
            driver.Step(.005f, new CombatInput(0, specialId == null ? CombatButtons.Basic : CombatButtons.Special));
            // Airborne placement isolates the attack box from ground-only forward motion.
            for (int tick = 0; tick < 180; tick++) driver.Step(.005f);
            string context = (specialId ?? formId + " basic") + " facing=" + facing + " x=" + x;
            Assert.That(target.ReceivedHitCount > 0, Is.EqualTo(expectedHit), context);
            if (specialId == null) Assert.That(target.ReceivedHitCount, Is.EqualTo(expectedHit ? 1 : 0), context + " duplicate hit");
        }
    }
}
