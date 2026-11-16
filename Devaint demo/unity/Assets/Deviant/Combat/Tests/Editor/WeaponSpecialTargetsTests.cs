using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public partial class WeaponBalanceReleaseTests
    {
        [UnityTest] public IEnumerator GreatswordBasicExcludesTheRear()
        {
            Ready("greatsword");
            foreach (var basic in Player.Form.basics) {
                Assert.That(basic.box.x - basic.box.width / 2, Is.EqualTo(.65625f).Within(.00001f));
                Assert.That(basic.box.x + basic.box.width / 2, Is.EqualTo(3.45625f).Within(.00001f));
            }
            foreach (int facing in new[] { -1, 1 }) {
                Ready("greatsword"); Player.SyncFacing(facing);
                Target.Position = new Vector2(-facing * 2, 1000); Physics2D.SyncTransforms();
                driver.Step(.001f, new CombatInput(pressed: CombatButtons.Basic));
                Advance(2.2f);
                Assert.That(Target.ReceivedHitCount, Is.Zero, "rear basic, facing=" + facing);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator GreatswordSpecialsRetainOriginalBoundsAndHitBothSides()
        {
            Ready("greatsword");
            float[] widths = { 4.1875f, 10, 4.8f, 6 };
            float[] heights = { 2.5f, 10, 2.2f, 7.2f };
            float[] offsets = { 1.5625f, 0, 0, 0 };
            float[][] times = { new[] { .25f }, new[] { .45f }, new[] { .20f, .45f, .70f, .95f, 1.20f, 1.45f, 1.70f, 1.95f }, new[] { .805f, 1.05f, 1.295f } };
            float[] damages = { 70, 100, 24, 30 };
            for (int special = 0; special < 4; special++) {
                var skill = Player.Form.specials[special];
                Assert.That(skill.hits.Select(h => h.time), Is.EqualTo(times[special]).Within(.00001f), skill.id + " clocks");
                foreach (var hit in skill.hits) {
                    Assert.That(hit.box.width, Is.EqualTo(widths[special]).Within(.00001f), skill.id);
                    Assert.That(hit.box.height, Is.EqualTo(heights[special]).Within(.00001f), skill.id);
                    Assert.That(hit.box.x, Is.EqualTo(offsets[special]).Within(.00001f), skill.id);
                    Assert.That(hit.damage, Is.EqualTo(damages[special]).Within(.00001f), skill.id);
                }
            }
            var hits = new List<CombatAcceptedHit>();
            arena.AcceptedHit += hit => { if (hit.Origin.EffectiveSource.ActorId == Player.ActorId) hits.Add(hit); };
            foreach (int facing in new[] { -1, 1 }) foreach (int side in new[] { -1, 1 }) {
                foreach (int special in new[] { 0, 1, 2, 3 }) {
                    Ready("greatsword"); Player.SyncFacing(facing);
                    hits.Clear();
                    Assert.That(Player.TryReconfigure(0, Player.Weapon.FormIndex, special, out var error), Is.True, error);
                    driver.Step(.001f, new CombatInput(pressed: CombatButtons.Special));
                    // Charge uses its original small rear overlap; the other skills cover both sides.
                    float end = arena.Now + 3;
                    while (arena.Now < end) {
                        Target.Position = special == 3 ? new Vector2(facing * (7 + side * 2), 1000)
                            : Player.Position + new Vector2(facing * side * (special == 0 && side < 0 ? .4f : 2), 0);
                        Target.velocity = Vector2.zero;
                        Physics2D.SyncTransforms(); driver.Step(.001f);
                    }
                    Assert.That(hits.Count, Is.EqualTo(times[special].Length), "special=" + special + ", facing=" + facing + ", side=" + side);
                    Assert.That(hits.Sum(h => h.Damage), Is.EqualTo(damages[special] * times[special].Length).Within(.01f));
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            yield return null;
        }

        [UnityTest] public IEnumerator AllTwelveRangedSkillsDamageThreeTargetsAndVisitDoesNotRepeat()
        {
            for (int i = 0; i < 2; i++) {
                var extra = new GameObject("Ranged skill extra target").AddComponent<TestEnemy>();
                extra.Configure(arena, new Vector2(1.3f, 1000), new Vector2(1, 2), Vector2.up, false);
                extra.health = extra.maxHealth = 10000; extra.aggressive = false; extra.isBoss = true; arena.enemies.Add(extra);
            }
            var hits = new List<CombatAcceptedHit>();
            arena.AcceptedHit += hit => { if (hit.Origin.EffectiveSource.ActorId == Player.ActorId) hits.Add(hit); };
            foreach (string form in new[] { "launcher", "shuriken", "bomb" }) foreach (int special in new[] { 0, 1, 2, 3 }) {
                Ready(form); hits.Clear();
                Assert.That(Player.TryReconfigure(0, Player.Weapon.FormIndex, special, out var error), Is.True, error);
                Vector2 origin = Player.Position;
                driver.Step(.001f, new CombatInput(pressed: CombatButtons.Special));
                Assert.That(Player.currentCast, Is.Not.Null, form + " " + special);
                float end = arena.Now + 4;
                while (arena.Now < end) {
                    if (form == "bomb" && special == 3 && arena.bombs.Count > 0) {
                        var at = arena.bombs[0].Position;
                        foreach (var target in arena.enemies) { target.Position = at; target.velocity = Vector2.zero; }
                        Physics2D.SyncTransforms();
                    }
                    driver.Step(.002f);
                }
                Assert.That(hits.Select(h => h.Target.ActorId).Distinct().Count(), Is.EqualTo(3), form + " " + special + " targets");
                if (form == "launcher" && special == 2) {
                    foreach (var target in arena.enemies)
                        Assert.That(hits.Where(h => h.Target.ActorId == target.ActorId).Sum(h => h.Damage), Is.EqualTo(100).Within(.01f), "five pellets each hit each actor once");
                }
                if (form == "shuriken" && special == 2) {
                    Assert.That(hits.Count, Is.EqualTo(3));
                    Assert.That(hits.All(h => Mathf.Abs(h.Damage - 65) < .01f), Is.True);
                    Assert.That(Player.ShurikenReturnPending(Player.Weapon), Is.True);
                    Advance(1.5f);
                    Assert.That(Player.ShurikenReturnPending(Player.Weapon), Is.False);
                    Assert.That(Player.Position.x, Is.EqualTo(origin.x).Within(.05f), "return origin from the single first visit");
                }
                Assert.That(arena.SimulationFaulted, Is.False);
                Debug.Log($"RANGED_MULTI {form} special={special} targets=3 damage={hits.Sum(h => h.Damage):F2}");
            }
            yield return null;
        }
    }
}
