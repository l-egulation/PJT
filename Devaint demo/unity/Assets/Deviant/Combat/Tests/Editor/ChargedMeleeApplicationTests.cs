using System;
using System.Collections;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ChargedMeleeApplicationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameBothChargedPoliciesPinCurrentReleaseAndUseNextNumericVersion() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabBothChargedPoliciesPinCurrentReleaseAndUseNextNumericVersion() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, ChargedMeleeDefinition value)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false; foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear(); int form = Array.FindIndex(arena.catalog.forms, item => item.id == value.FormId); int special = Array.FindIndex(arena.catalog.forms[form].specials, item => item.id == value.Id);
            Assert.That(p.TryReconfigure(0, form, special, out var error), Is.True, error); p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 1000); arena.enemies[0].health = 1000; Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena, CombatArena.ManualSimulation driver, bool held) { driver.Step(.02f, new CombatInput(0, CombatButtons.Special, held)); var value = arena.Player.currentCast; Assert.That(value, Is.Not.Null); return value; }
        static void Finish(CombatArena.ManualSimulation driver, bool held) { for (int tick = 0; tick < 150; tick++) driver.Step(.02f, new CombatInput(0, CombatButtons.None, held)); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var floor = new GameObject("ChargedNumericFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            try {
                using (var driver = arena.AcquireManualSimulation()) foreach (string id in ChargedMeleeDefinition.SupportedIds)
                {
                    var original = arena.GetChargedMelee(id); Setup(arena, driver, original); var running = Start(arena, driver, true);
                    var numeric = original.ToNumericData(); numeric.minimum += .04f; numeric.maximum += .08f; numeric.minimumDamage += 7; numeric.maximumDamage += 11; numeric.baseEnd += .12f; numeric.box.height += .25f;
                    var candidate = original.Recompile(numeric); Assert.That(arena.TryApplyChargedMeleeDefinitions(new[] { candidate }, false, out var error), Is.True, error);
                    Assert.That(running.ChargedMelee, Is.SameAs(original)); Assert.That(running.ChargePolicy.ContentHash, Is.EqualTo(original.Policy.ContentHash));
                    Finish(driver, true); Assert.That(running.ChargeRelease.Value.Damage, Is.EqualTo(original.Policy.MaximumDamage)); Assert.That(arena.enemies[0].health, Is.EqualTo(1000 - original.Policy.MaximumDamage));
                    Setup(arena, driver, candidate); var next = Start(arena, driver, false); Finish(driver, false);
                    Assert.That(next.ChargedMelee, Is.SameAs(candidate)); Assert.That(next.ChargeRelease.Value.Damage, Is.EqualTo(candidate.Policy.MinimumDamage)); Assert.That(arena.enemies[0].health, Is.EqualTo(1000 - candidate.Policy.MinimumDamage));
                    Setup(arena, driver, candidate); next = Start(arena, driver, true); Finish(driver, true);
                    Assert.That(next.ChargeRelease.Value.Damage, Is.EqualTo(candidate.Policy.MaximumDamage)); Assert.That(next.ChargeRelease.Value.MotionEnd, Is.EqualTo(candidate.Policy.Resolve(candidate.Policy.Maximum).MotionEnd));
                    Assert.That(arena.enemies[0].health, Is.EqualTo(1000 - candidate.Policy.MaximumDamage)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));
                    Setup(arena, driver, candidate); next = Start(arena, driver, true); arena.enemies[0].Position = new Vector2(40, 1000); Physics2D.SyncTransforms(); Finish(driver, true);
                    Assert.That(next.ResourceResult, Is.Null); Assert.That(arena.Player.Weapon.Cooldown.Available, Is.EqualTo(1), "Release target recheck fails before resource activation.");
                    float now = arena.Now; Assert.That(arena.TryApplyChargedMeleeDefinitions(new[] { candidate, candidate }, true, out error), Is.False); Assert.That(arena.Now, Is.EqualTo(now));
                    var changed = candidate.ToData(); changed.cooldown += 1; Assert.That(ChargedMeleeDefinition.TryCreate(candidate.FormId, changed, out var policy, out error), Is.True, error);
                    Assert.That(arena.TryApplyChargedMeleeDefinitions(new[] { policy }, true, out error), Is.False); Assert.That(arena.GetChargedMelee(id), Is.SameAs(candidate));
                    Assert.That(arena.TryApplyChargedMeleeDefinitions(new[] { original }, true, out error), Is.True, error);
                }
                Assert.That(arena.SimulationFaulted, Is.False);
            }
            finally { Object.Destroy(floor); }
        }
    }
}
