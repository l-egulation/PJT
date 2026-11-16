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
    public class DirectMeleeSpecialApplicationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameAllThreeNumericCandidatesPinCurrentCastAndAdoptAtNextStart() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabAllThreeNumericCandidatesPinCurrentCastAndAdoptAtNextStart() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, DirectMeleeSpecialDefinition definition)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            int form = Array.FindIndex(arena.catalog.forms, value => value.id == definition.FormId); int special = Array.FindIndex(arena.catalog.forms[form].specials, value => value.id == definition.Id);
            Assert.That(p.TryReconfigure(0, form, special, out var error), Is.True, error);
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); arena.enemies[0].Position = new Vector2(2, 1000); arena.enemies[0].health = 1000; Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena, CombatArena.ManualSimulation driver) { driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var value = arena.Player.currentCast; Assert.That(value, Is.Not.Null); return value; }
        static void Finish(CombatArena.ManualSimulation driver) { for (int tick = 0; tick < 100; tick++) driver.Step(.02f); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var floor = new GameObject("DirectNumericFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            try {
                using (var driver = arena.AcquireManualSimulation()) foreach (string id in DirectMeleeSpecialDefinition.SupportedIds)
                {
                    var original = arena.GetDirectMeleeSpecial(id); Setup(arena, driver, original); var running = Start(arena, driver);
                    var data = original.ToBasicData(); data.damage += 7; data.impact += .06f; data.duration += .12f; data.box.height += .25f;
                    var candidate = original.Recompile(data); var others = DirectMeleeSpecialDefinition.SupportedIds.Where(value => value != id).Select(value => arena.GetDirectMeleeSpecial(value)).ToArray();
                    Assert.That(arena.TryApplyDirectMeleeSpecials(new[] { candidate }, false, out var error), Is.True, error);
                    Assert.That(running.DirectMeleeSpecial, Is.SameAs(original)); Assert.That(running.DefinitionVersion, Is.EqualTo(original.ContentHash));
                    Finish(driver); Assert.That(arena.enemies[0].health, Is.EqualTo(1000 - original.Damage)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));
                    Setup(arena, driver, candidate); var next = Start(arena, driver); Assert.That(next.DirectMeleeSpecial, Is.SameAs(candidate)); Assert.That(next.ImpactTime, Is.EqualTo(candidate.Impact)); Assert.That(next.MotionDuration, Is.EqualTo(candidate.Duration));
                    Finish(driver); Assert.That(arena.enemies[0].health, Is.EqualTo(1000 - candidate.Damage));
                    foreach (var other in others) Assert.That(arena.GetDirectMeleeSpecial(other.Id), Is.SameAs(other));
                    float now = arena.Now; Assert.That(arena.TryApplyDirectMeleeSpecials(new[] { candidate, candidate }, true, out error), Is.False); Assert.That(arena.Now, Is.EqualTo(now));
                    var changedPolicy = candidate.ToData(); changedPolicy.cooldown += 1; Assert.That(DirectMeleeSpecialDefinition.TryCreate(candidate.FormId, changedPolicy, out var stale, out error), Is.True, error);
                    Assert.That(arena.TryApplyDirectMeleeSpecials(new[] { stale }, true, out error), Is.False); Assert.That(arena.GetDirectMeleeSpecial(id), Is.SameAs(candidate));
                    Assert.That(arena.TryApplyDirectMeleeSpecials(new[] { original }, true, out error), Is.True, error);
                }
                Assert.That(arena.SimulationFaulted, Is.False);
            }
            finally { Object.Destroy(floor); }
        }
    }
}
