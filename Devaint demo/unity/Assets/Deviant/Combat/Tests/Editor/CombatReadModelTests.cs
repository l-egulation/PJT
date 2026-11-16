using System;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CombatReadModelTests
    {
        [Test] public void PublicReadsContainOnlyDetachedValuesAndHaveNoNativeOrRuntimeDependency()
        {
            var assembly = typeof(ICombatReadModel).Assembly;
            Assert.That(assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") ||
                a.Name == "Deviant.Combat" || a.Name == "Deviant.Combat.Execution" || a.Name.StartsWith("Deviant.Equipment") || a.Name.StartsWith("Deviant.UI") || a.Name.StartsWith("Deviant.World")), Is.False);
            foreach (var type in new[] { typeof(CombatActorHandle), typeof(ActorReadSnapshot), typeof(ActionReadSnapshot), typeof(CharacterPresentationReadSnapshot), typeof(WeaponChargeReadSnapshot), typeof(PlayerWeaponResourcesReadSnapshot) })
            {
                Assert.That(type.IsValueType, Is.True);
                Assert.That(type.GetProperties().Any(p => p.GetSetMethod(true) != null), Is.False);
                Assert.That(type.GetProperties().All(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string)), Is.True);
                Assert.That(type.GetFields().Length, Is.Zero);
            }
            Assert.That(typeof(ICombatReadModel).GetMethods().All(m => m.ReturnType == typeof(bool)), Is.True);
        }

        [Test] public void HandlesIncludeActorGenerationAndEpochAndCannotAliasAnotherSession()
        {
            var session = new CombatInputSession(7, 3, 2); var handle = new CombatActorHandle(session, 11);
            Assert.That(handle.Equals(new CombatActorHandle(session, 11)), Is.True);
            Assert.That(handle.GetHashCode(), Is.EqualTo(new CombatActorHandle(session, 11).GetHashCode()));
            foreach (var other in new[] {
                new CombatActorHandle(session, 12),
                new CombatActorHandle(new CombatInputSession(8, 3, 2), 11),
                new CombatActorHandle(new CombatInputSession(7, 4, 2), 11),
                new CombatActorHandle(new CombatInputSession(7, 3, 3), 11)
            }) Assert.That(handle.Equals(other), Is.False);
        }

        [Test] public void AggregatedSnapshotKeepsOldOwnerValuesIncludingZeroDamageHitsAndExecutionVersions()
        {
            var kinematics = new ActorKinematicsState(); kinematics.WritePosition(1.25f, -2.5f); kinematics.WriteVelocity(3.5f, -4.5f); kinematics.WriteFacing(-1);
            var vitals = new ActorVitalsState(100); vitals.WriteShield(10); vitals.ApplyDamage(0, 0, false); vitals.ApplyDamage(4, 0, false);
            var statuses = new ActorStatusState(); statuses.RefreshRoot(1, 2);
            var id = Guid.NewGuid(); var weapon = Guid.NewGuid();
            var action = new ActionReadSnapshot(id, "base_sword_basic_0", "source", "compiled", "frames", CombatActionReadPhase.Active, .2f, .3f, .8f);
            var value = new ActorReadSnapshot(new CombatActorHandle(new CombatInputSession(7, 3, 2), 7), 12, .5f, true,
                kinematics.Read(), vitals.Read(), statuses.Read(), action, new PlayerDashSnapshot(1, 2, 3, -1, 4, 1), new PlayerJumpSnapshot(1, 5),
                weapon, "base_sword", "검", new CharacterPresentationReadSnapshot(-1, 2, .2f, true, true, false, .1f, .2f, .4f, false),
                new PlayerWeaponResourcesReadSnapshot(1, new WeaponChargeReadSnapshot(weapon, "base_sword", "base_sword_default", 1, 0, 5, 4.25f),
                    new WeaponChargeReadSnapshot(Guid.NewGuid(), "dagger", "dagger_movement", 3, 2, 4, 3.5f)));
            kinematics.WritePosition(90, 91); vitals.WriteHealth(1); statuses.Reset();
            Assert.That(value.Kinematics.X, Is.EqualTo(1.25f)); Assert.That(value.Kinematics.Y, Is.EqualTo(-2.5f)); Assert.That(value.Kinematics.Facing, Is.EqualTo(-1));
            Assert.That(value.Vitals.Health, Is.EqualTo(100)); Assert.That(value.Vitals.Shield, Is.EqualTo(6)); Assert.That(value.Vitals.ReceivedHits, Is.EqualTo(2));
            Assert.That(value.Statuses.RootUntil, Is.EqualTo(3)); Assert.That(value.TickCount, Is.EqualTo(12)); Assert.That(value.Now, Is.EqualTo(.5f));
            Assert.That(value.Action.ExecutionId, Is.EqualTo(id)); Assert.That(value.Action.Phase, Is.EqualTo(CombatActionReadPhase.Active));
            Assert.That(value.Action.DefinitionVersion, Is.EqualTo("source")); Assert.That(value.Action.TimelineVersion, Is.EqualTo("compiled")); Assert.That(value.Action.FrameTimingVersion, Is.EqualTo("frames"));
            Assert.That(value.ActiveWeaponId, Is.EqualTo(weapon)); Assert.That(value.ActiveWeaponContentId, Is.EqualTo("base_sword")); Assert.That(value.ActiveWeaponName, Is.EqualTo("검"));
            Assert.That(value.Jump.Used, Is.EqualTo(1)); Assert.That(value.Dash.ReadyAt, Is.EqualTo(3));
            Assert.That(value.Presentation.Axis, Is.EqualTo(-1)); Assert.That(value.Presentation.BasicIndex, Is.EqualTo(2));
            Assert.That(value.Presentation.HasAction && value.Presentation.IsBasicAction, Is.True);
            Assert.That(value.Presentation.ImpactTime, Is.EqualTo(.2f)); Assert.That(value.Presentation.MotionDuration, Is.EqualTo(.4f));
            Assert.That(value.Resources.HasLoadout, Is.True); Assert.That(value.Resources.ActiveSlot, Is.EqualTo(1));
            Assert.That(value.Resources.Slot0.InstanceId, Is.EqualTo(weapon)); Assert.That(value.Resources.Slot0.SpecialId, Is.EqualTo("base_sword_default"));
            Assert.That(value.Resources.Slot0.Available, Is.Zero); Assert.That(value.Resources.Slot0.NextReady, Is.EqualTo(4.25f));
            Assert.That(value.Resources.Slot1.Capacity, Is.EqualTo(3)); Assert.That(value.Resources.Slot1.Available, Is.EqualTo(2)); Assert.That(value.Resources.Slot1.Period, Is.EqualTo(4));
            Assert.That(default(PlayerWeaponResourcesReadSnapshot).HasLoadout, Is.False);
            Assert.That(default(ActionReadSnapshot).ExecutionId, Is.EqualTo(Guid.Empty)); Assert.That(default(ActionReadSnapshot).Phase, Is.EqualTo(CombatActionReadPhase.None));
        }
    }
}
