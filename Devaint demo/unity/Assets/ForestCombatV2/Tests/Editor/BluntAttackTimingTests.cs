using System;
using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class BluntAttackTimingTests
    {
        [UnityTest] public IEnumerator LiveChapterHammerAndStampedGreatswordUseRequestedTimings()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++)
            { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            var arena = world.arena;
            var player = arena.Player;
            foreach (var enemy in arena.enemies)
                if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); }
            var offer = world.GetComponent<Chapter1StartingBlueprints>();
            player.Position = offer.OfferPosition(1);
            Assert.That(offer.TryChoose(1), Is.True);
            using (var driver = arena.AcquireManualSimulation())
            {
                Action<float, CombatInput> step = (seconds, input) => driver.Step(seconds, input);
                VerifyCombo(arena, step, "hammer", new[] {
                    new[] { .8f, .1f, .06f }, new[] { .29f, .1f, .2f }
                });
                // Exercise the actual blunt blueprint -> greatsword stamp path.
                var room = world.Data.rooms.First(r => Chapter1MaintenanceFacility.IsFacility(r.kind));
                player.Position = new Vector2(room.x + room.w / 2f, -(room.y + room.entryY));
                var shop = world.GetComponent<Chapter1MaintenanceShop>();
                Assert.That(shop.TryOpen(), Is.True);
                var stamp = shop.Service.Stash.AddWeaponStamp("blunt", "greatsword");
                Assert.That(shop.TryApplyStamp(0, stamp.Id, out var error), Is.True, error);
                shop.Close();
                VerifyCombo(arena, step, "greatsword", new[] {
                    new[] { .75f, .125f, .0625f }, new[] { .125f, .125f, .1125f },
                    new[] { .5f, .125f, .2f }
                });
            }
            yield return new ExitPlayMode();
        }

        static void VerifyCombo(CombatArena arena, Action<float, CombatInput> step, string form, float[][] expected)
        {
            const float dt = .001f, tolerance = .003f;
            var player = arena.Player;
            Assert.That(player.Form.id, Is.EqualTo(form));
            Assert.That(arena.TryBindActor(player, out var actor), Is.True);
            step(dt, new CombatInput(pressed: CombatButtons.Basic));
            float cycleStart = player.currentCast.StartedAt;
            float cycleEnd = cycleStart;
            for (int index = 0; index < expected.Length; index++)
            {
                var cast = player.currentCast;
                Assert.That(cast, Is.Not.Null);
                Assert.That(player.BasicIndex, Is.EqualTo(index));
                var want = expected[index];
                float duration = want.Sum(), started = cast.StartedAt;
                cycleEnd = cast.EndTime;
                Assert.That(cast.ImpactTime, Is.EqualTo(want[0]).Within(.00001f));
                Assert.That(cast.MotionDuration, Is.EqualTo(duration).Within(.00001f));
                float activeAt = -1, recoveryAt = -1;
                int ticks = 0;
                while (player.currentCast == cast && ticks++ < 2000)
                {
                    Assert.That(arena.ReadModel.TryReadActor(actor, out var read), Is.True);
                    if (read.Action.Phase == CombatActionReadPhase.Active && activeAt < 0)
                        activeAt = arena.Now - started;
                    if (read.Action.Phase == CombatActionReadPhase.Recovery && recoveryAt < 0)
                        recoveryAt = arena.Now - started;
                    // Queue each following hit, but let the final hit end naturally.
                    step(dt, index < expected.Length - 1
                        ? new CombatInput(pressed: CombatButtons.Basic) : default);
                }
                Assert.That(ticks, Is.LessThan(2000));
                Assert.That(activeAt, Is.EqualTo(want[0]).Within(tolerance), "observed windup");
                Assert.That(recoveryAt - activeAt, Is.EqualTo(want[1]).Within(tolerance), "observed active window");
                Assert.That(arena.Now - started - recoveryAt, Is.EqualTo(want[2]).Within(tolerance), "observed recovery");
                Assert.That(arena.Now, Is.EqualTo(cycleEnd).Within(tolerance), "observed completion");
                Debug.Log($"BLUNT_TIMING {form} hit={index + 1} windup={activeAt:F4} active={recoveryAt - activeAt:F4} recovery={arena.Now - started - recoveryAt:F4} total={arena.Now - started:F4}");
            }
            Assert.That(player.currentCast, Is.Null);
            Assert.That(cycleEnd - cycleStart, Is.EqualTo(expected.Sum(hit => hit.Sum())).Within(.0001f));
        }
    }
}
