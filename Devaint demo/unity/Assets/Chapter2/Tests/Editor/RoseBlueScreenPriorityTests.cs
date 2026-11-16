using System.Collections;
using Deviant.Chapter2.RoseBlue;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    public sealed partial class RoseBlueIntegrationTests
    {
        [UnityTest] public IEnumerator PhasePresentationsOwnTheScreenThroughFloorChangeAndAbsorption()
        {
            var world = Object.FindAnyObjectByType<Chapter2World>();
            var arena = world.arena; var boss = world.Boss;
            using (var simulation = arena.AcquireManualSimulation()) {
                world.WarpToRoom("B"); Step(simulation, .02f);
                Assert.That(boss.Model.Phase, Is.EqualTo(Phase.Introduction));
                Assert.That(arena.CombatScreenEffectsAllowed, Is.False);
                Step(simulation, RoseBlueEncounter.IntroSeconds + .05f);
                Assert.That(arena.CombatScreenEffectsAllowed, Is.True);
                boss.Machine.Receive(boss.Machine.health * 2, arena.Player, 1, false, null);
                Step(simulation, .02f);
                Assert.That(boss.Model.Phase, Is.EqualTo(Phase.FloorTransition));
                Assert.That(arena.CombatScreenEffectsAllowed, Is.False);
                Step(simulation, RoseBlueEncounter.PhaseShiftSeconds + .05f);
                Assert.That(boss.Model.Phase, Is.EqualTo(Phase.Duo));
                Assert.That(arena.CombatScreenEffectsAllowed, Is.True);
                boss.Rose.Receive(boss.Rose.health * 2, arena.Player, 1, false, null);
                Step(simulation, .02f);
                Assert.That(boss.Model.Phase, Is.EqualTo(Phase.AbsorptionPush));
                Assert.That(arena.CombatScreenEffectsAllowed, Is.False);
                Step(simulation, (float)Tuning.PushSeconds + .05f);
                Assert.That(boss.Model.Phase, Is.EqualTo(Phase.Absorption));
                Assert.That(arena.CombatScreenEffectsAllowed, Is.False);
                boss.enabled = false;
                Assert.That(arena.CombatScreenEffectsAllowed, Is.True);
                boss.enabled = true;
                Assert.That(arena.CombatScreenEffectsAllowed, Is.False, "re-enabling the active cutscene restores its presentation ownership");
                boss.CompleteCutscene(boss.Model.Generation);
                Assert.That(boss.Model.Phase, Is.EqualTo(Phase.FusedBlue));
                Assert.That(arena.CombatScreenEffectsAllowed, Is.True);
            }
            yield return null;
        }
    }
}
