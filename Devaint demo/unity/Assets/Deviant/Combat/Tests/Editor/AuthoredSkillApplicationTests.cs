using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class AuthoredSkillApplicationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameAllNineteenUseCapturedWholeDefinitionsAndFutureNumericVersions() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabAllNineteenUseCapturedWholeDefinitionsAndFutureNumericVersions() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        internal static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, AuthoredSkillDefinition definition)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false; foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear(); int form = Array.FindIndex(arena.catalog.forms, value => value.id == definition.FormId); int special = Array.FindIndex(arena.catalog.forms[form].specials, value => value.id == definition.Id);
            Assert.That(p.TryReconfigure(0, form, special, out var error), Is.True, error); p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 1000); arena.enemies[0].health = 1000; Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena, CombatArena.ManualSimulation driver) { driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var cast = arena.Player.currentCast; Assert.That(cast, Is.Not.Null); return cast; }
        static void Ticks(CombatArena.ManualSimulation driver, int count) { for (int tick = 0; tick < count; tick++) driver.Step(.02f); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var floor = new GameObject("AuthoredWholeFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            try { using (var driver = arena.AcquireManualSimulation()) {
                Assert.That(arena.AuthoredSkillCount, Is.EqualTo(19));
                foreach (string id in AuthoredSkillDefinition.SupportedIds) {
                    var source = arena.GetAuthoredSkill(id); Setup(arena, driver, source); var running = Start(arena, driver); var numeric = source.ToNumericData(); numeric.end += .1f;
                    if (numeric.hits.Length != 0) numeric.hits[0].damage += 1; else if (source.CanEdit("effect", "duration")) numeric.effect.duration += .1f; else numeric.effect.parryDamage += 1;
                    var candidate = source.Recompile(numeric); Assert.That(arena.TryApplyAuthoredSkills(new[] { candidate }, false, out var error), Is.True, error);
                    Assert.That(running.Definition.Authored, Is.SameAs(source)); Assert.That(running.DefinitionVersion, Is.EqualTo(source.ContentHash));
                    Ticks(driver, 100); Setup(arena, driver, candidate); var next = Start(arena, driver); Assert.That(next.Definition.Authored, Is.SameAs(candidate)); Assert.That(next.DefinitionVersion, Is.EqualTo(candidate.ContentHash));
                    float now = arena.Now; Assert.That(arena.TryApplyAuthoredSkills(new[] { candidate, candidate }, true, out error), Is.False); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.Player.currentCast, Is.SameAs(next));
                    Ticks(driver, 100); Assert.That(arena.TryApplyAuthoredSkills(new[] { source }, true, out error), Is.True, error);
                }
                Assert.That(arena.SimulationFaulted, Is.False);
            } }
            finally { Object.Destroy(floor); }
        }
        [UnityTest] public IEnumerator CapturedParryWindowAndDamageSmokeAreaDecoyGeometryRadiusAndFreezeAreRealConsumers()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var floor = new GameObject("AuthoredNumericFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            try { using (var driver = arena.AcquireManualSimulation()) {
                var parry = arena.GetAuthoredSkill("shield_default"); var data = parry.ToNumericData(); data.effect.parryWindow = .3f; data.effect.parryDamage = 45; var edited = parry.Recompile(data);
                Assert.That(arena.TryApplyAuthoredSkills(new[] { edited }, false, out var error), Is.True, error); Setup(arena, driver, edited); var cast = Start(arena, driver); Ticks(driver, 10);
                Assert.That(cast.TryParry(arena.enemies[0], false), Is.True); Assert.That(arena.enemies[0].health, Is.EqualTo(955));
                var smoke = arena.GetAuthoredSkill("dagger_range"); data = smoke.ToNumericData(); data.effect.box.width = 2; data.effect.box.height = 2; data.effect.box.y = 1; edited = smoke.Recompile(data);
                Assert.That(arena.TryApplyAuthoredSkills(new[] { edited }, false, out error), Is.True, error); Setup(arena, driver, edited); cast = Start(arena, driver); Ticks(driver, 12);
                Assert.That(cast.DebugBoxCount, Is.EqualTo(1)); Assert.That(cast.GetDebugBox(0).size, Is.EqualTo(new Vector3(2, 2, 1)));
                arena.Player.Position = new Vector2(2, 1000); Physics2D.SyncTransforms(); Ticks(driver, 2); Assert.That(arena.Player.Hidden, Is.False);
                var decoy = arena.GetAuthoredSkill("shield_follow_up"); data = decoy.ToNumericData(); data.effect.box.width = 1.2f; data.effect.box.height = 2; data.effect.box.y = 1; data.effect.radius = 2; edited = decoy.Recompile(data);
                Assert.That(arena.TryApplyAuthoredSkills(new[] { edited }, false, out error), Is.True, error); Setup(arena, driver, edited); cast = Start(arena, driver); Ticks(driver, 15);
                Assert.That(arena.decoys.Count, Is.Zero, "The existing solid-support overlap margin still rejects this exact resting placement.");
                Assert.That(arena.Player.Weapon.Cooldown.Available, Is.EqualTo(1), "Rejected solid placement remains before resource activation.");
                floor.GetComponent<ArenaSurface>().oneWay = true;
                Setup(arena, driver, edited); cast = Start(arena, driver); Ticks(driver, 15);
                Assert.That(arena.decoys.Count, Is.EqualTo(1)); var d = arena.decoys[0]; Assert.That(d.size, Is.EqualTo(new Vector2(1.2f, 2))); Assert.That(d.offset, Is.EqualTo(new Vector2(0, 1)));
                var enemy = arena.enemies[0]; enemy.Position = d.Position + new Vector2(3, 0); Physics2D.SyncTransforms(); Assert.That(arena.EnemyTarget(enemy), Is.SameAs(arena.Player));
                data = edited.ToNumericData(); data.effect.radius = 5; var nextVersion = edited.Recompile(data); Assert.That(arena.TryApplyAuthoredSkills(new[] { nextVersion }, false, out error), Is.True, error);
                Assert.That(arena.EnemyTarget(enemy), Is.SameAs(arena.Player), "An existing decoy keeps its captured radius after definition publication.");
                floor.GetComponent<ArenaSurface>().oneWay = false;
                var rapier = arena.GetAuthoredSkill("rapier_follow_up"); data = rapier.ToNumericData(); data.verticalFreeze = new[] { .2f, .4f }; edited = rapier.Recompile(data);
                Assert.That(arena.TryApplyAuthoredSkills(new[] { edited }, false, out error), Is.True, error); Setup(arena, driver, edited); cast = Start(arena, driver); Ticks(driver, 5); Assert.That(cast.FreezeVertical, Is.False);
                Ticks(driver, 6); Assert.That(cast.FreezeVertical, Is.True); Assert.That(cast.Definition.InvulStart[0], Is.EqualTo(.1f), "Independent invulnerability remains separate from edited freeze timing.");
            } }
            finally { Object.Destroy(floor); }
        }
    }
}
