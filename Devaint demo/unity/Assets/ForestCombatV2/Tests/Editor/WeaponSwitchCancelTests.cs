using System;
using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // Real cast timing for the other weapon: its basic waits for the running action and
    // chains at its end, skills still replace at once, and the ink roles that speed up
    // alternating attacks (switch windup reduction, landed-hit recovery cancel).
    public sealed class WeaponSwitchCancelTests
    {
        const float Dt = 1f / 120;
        CombatArena arena;
        CombatArena.ManualSimulation simulation;
        WeaponGrowth growth;
        CombatPlayer P => arena.Player;

        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world = Object.FindFirstObjectByType<ForestCombatWorld>();
            for (int i = 0; i < 600 && (!world || !world.Ready); i++) { yield return null; world = Object.FindFirstObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            arena = world.arena; P.UseTestInput = true;
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
            var floor = new GameObject("Weapon switch test floor"); floor.AddComponent<ArenaSurface>();
            floor.transform.position = new Vector3(-500, -.5f, 0); floor.AddComponent<BoxCollider2D>().size = new Vector2(50, 1);
            simulation = arena.AcquireManualSimulation();
            P.SetWeaponSlotReady(0, true); P.SetWeaponSlotReady(1, true);
            P.MaintenanceSite = () => true;
            Place();
            // The run binding reassigns its own growth every frame, so equip inks into that one.
            growth = arena.MvpGrowth ?? new WeaponGrowth(JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text),
                new RunEconomy(JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text)));
            arena.MvpGrowth = growth;
        }
        [UnityTearDown] public IEnumerator Teardown() { simulation?.Dispose(); yield return new ExitPlayMode(); }

        void Place()
        {
            int sword = Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword");
            P.spawn = new Vector2(-500, .003f); simulation.Reset();
            Assert.That(P.TryReconfigure(0, sword, 0, out var error), Is.True, error);
            Assert.That(P.TryReconfigure(1, sword, 0, out error), Is.True, error);
            P.selected = 0; P.SyncFacing(1); Physics2D.SyncTransforms();
            for (int i = 0; i < 12; i++) simulation.Step(Dt);
            Assert.That(P.currentCast, Is.Null); Assert.That(P.grounded, Is.True);
        }
        void Equip(int weapon, params string[] parts)
        {
            var palette = growth.GrantPalette(Enumerable.Repeat("yellow", Math.Max(3, parts.Length)).ToArray());
            growth.Bind(palette.Id, P.weapons[weapon].InstanceId);
            for (int i = 0; i < parts.Length; i++) growth.Insert(P.weapons[weapon].InstanceId, i, growth.GrantInk(parts[i]).Id);
        }
        void SpawnTarget()
        {
            var target = arena.SpawnEnemy(0, P.Position + Vector2.right * 1.1f);
            target.aggressive = false; target.maxHealth = target.health = 100000;
            Physics2D.SyncTransforms();
        }
        GrowthValues Level0(string part) => growth.Data.parts.Single(p => p.id == part).levels[0];
        void Press(CombatButtons button) => simulation.Step(Dt, new CombatInput(pressed: button));
        static bool Activated(CombatCast cast) => cast.ResourceResult?.Status == ActionResourceStatus.Succeeded;
        void StepUntilActivated(CombatCast cast)
        {
            for (int i = 0; i < 240 && !Activated(cast); i++) simulation.Step(Dt);
            Assert.That(Activated(cast), Is.True, "The action never committed its first hit/effect.");
        }
        CombatCast StepUntilReplaced(CombatCast cast)
        {
            float limit = cast.EndTime + 1;
            while (P.currentCast == cast && arena.Now < limit) simulation.Step(Dt);
            Assert.That(P.currentCast, Is.Not.Null.And.Not.SameAs(cast), "The reserved attack never started.");
            return P.currentCast;
        }
        // Press the first weapon, let it land its first hit, then press the second weapon's basic.
        CombatCast AlternateAfterHit()
        {
            Press(CombatButtons.Weapon1Basic); var first = P.currentCast;
            Assert.That(first, Is.Not.Null); StepUntilActivated(first);
            Assert.That(first.EndTime, Is.GreaterThan(arena.Now), "The press happens before the first basic would end.");
            Press(CombatButtons.Weapon2Basic);
            return first;
        }

        [UnityTest] public IEnumerator OtherWeaponBasicWaitsForTheRunningBasicWithoutReduction()
        {
            Assert.That(arena.WeaponSwitch.AfterHitWindupReduction, Is.Zero, "No switch windup reduction without inks.");
            var first = AlternateAfterHit();
            Assert.That(P.currentCast, Is.SameAs(first)); Assert.That(P.selected, Is.Zero, "The other basic is reserved, not started.");
            float end = first.EndTime;
            var second = StepUntilReplaced(first);
            Assert.That(first.PresentationCancelled, Is.False, "The first basic finishes its recovery.");
            Assert.That(P.selected, Is.EqualTo(1)); Assert.That(second.isBasic, Is.True);
            Assert.That(second.StartedAt, Is.EqualTo(end).Within(1e-4f));
            Assert.That(second.EndTime - second.StartedAt, Is.EqualTo(second.MotionDuration).Within(1e-4f));
            yield return null;
        }

        [UnityTest] public IEnumerator OtherWeaponSkillStillCancelsBasicAtOnce()
        {
            Press(CombatButtons.Weapon1Basic); var first = P.currentCast;
            Press(CombatButtons.Weapon2Special); var skill = P.currentCast;
            Assert.That(Activated(first), Is.False);
            Assert.That(P.selected, Is.EqualTo(1)); Assert.That(skill, Is.Not.SameAs(first)); Assert.That(skill.isBasic, Is.False);
            Assert.That(first.PresentationCancelled, Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator ActivatedSkillReservesOtherWeaponBasicUntilItEnds()
        {
            Press(CombatButtons.Weapon1Special); var skill = P.currentCast;
            Assert.That(skill, Is.Not.Null); Assert.That(skill.isBasic, Is.False);
            Press(CombatButtons.Weapon2Basic);
            Assert.That(P.currentCast, Is.SameAs(skill)); Assert.That(P.selected, Is.Zero, "No switch before activation.");
            StepUntilActivated(skill);
            Press(CombatButtons.Weapon2Basic);
            Assert.That(P.currentCast, Is.SameAs(skill), "The activated skill keeps its recovery.");
            float end = skill.EndTime;
            var basic = StepUntilReplaced(skill);
            Assert.That(P.selected, Is.EqualTo(1)); Assert.That(basic.isBasic, Is.True);
            Assert.That(basic.StartedAt, Is.EqualTo(end).Within(1e-4f));
            Assert.That(P.weapons[0].Cooldown.Available, Is.Zero, "Switching never refunds the spent cooldown.");
            yield return null;
        }

        [UnityTest] public IEnumerator SameWeaponBasicStillWaitsForComboSuccessor()
        {
            Press(CombatButtons.Weapon1Basic); var first = P.currentCast;
            StepUntilActivated(first);
            Press(CombatButtons.Weapon1Basic);
            Assert.That(P.currentCast, Is.SameAs(first), "The same weapon queues its next hit.");
            float end = first.EndTime;
            var next = StepUntilReplaced(first);
            Assert.That(P.selected, Is.Zero); Assert.That(next.StartedAt, Is.EqualTo(end).Within(1e-4f));
            Assert.That(next.EndTime - next.StartedAt, Is.EqualTo(next.MotionDuration).Within(1e-4f));
            yield return null;
        }

        [UnityTest] public IEnumerator WhirlwindInkShortensTheChainedFirstWindup()
        {
            Equip(0, "ink3_yellow_04");
            var whirl = Level0("ink3_yellow_04");
            Assert.That(whirl.switchWindupReduction, Is.EqualTo(.5f));
            var first = AlternateAfterHit();
            var second = StepUntilReplaced(first);
            Assert.That(first.PresentationCancelled, Is.False, "Role B alone does not cut recovery.");
            float started = second.StartedAt;
            StepUntilActivated(second);
            // The switch also grants the incoming weapon the ink's attack speed buff.
            float expected = second.ImpactTime * (1 - whirl.switchWindupReduction) / (1 + whirl.attackSpeed);
            Assert.That(arena.Now - started, Is.EqualTo(expected).Within(Dt + 1e-4f));
            yield return null;
        }

        [UnityTest] public IEnumerator RecoveryCancelInkCutsALandedBasic()
        {
            Equip(0, "ink3_yellow_07"); SpawnTarget();
            var first = AlternateAfterHit();
            float end = first.EndTime;
            var second = StepUntilReplaced(first);
            Assert.That(first.PresentationCancelled, Is.True, "The landed hit unlocked the recovery cancel.");
            Assert.That(P.selected, Is.EqualTo(1)); Assert.That(second.isBasic, Is.True);
            Assert.That(second.StartedAt, Is.LessThan(end - Dt));
            Assert.That(second.EndTime - second.StartedAt, Is.EqualTo(second.MotionDuration).Within(1e-4f), "Role C alone does not shorten the next windup.");
            yield return null;
        }

        [UnityTest] public IEnumerator RecoveryCancelInkIgnoresAirSwings()
        {
            Equip(0, "ink3_yellow_07");
            var first = AlternateAfterHit();
            float end = first.EndTime;
            var second = StepUntilReplaced(first);
            Assert.That(first.PresentationCancelled, Is.False, "A swing that hit nothing keeps its recovery.");
            Assert.That(second.StartedAt, Is.EqualTo(end).Within(1e-4f));
            yield return null;
        }
    }
}
