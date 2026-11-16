using System;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorStatusTests
    {
        [Test] public void StatusContractAndOwnerHaveNoEngineSceneOrUiReferences()
        {
            foreach (var type in new[] { typeof(IActorStatuses), typeof(ActorStatusSnapshot), typeof(ActorStatusState) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name.StartsWith("Deviant.World") || a.Name.StartsWith("Deviant.UI")), Is.False);
        }
        [Test] public void PlayerHitsRefreshInputLockButPreserveActiveKnockDirectionAndDeadline()
        {
            var state = new ActorStatusState(); state.PlayerHit(1, 1, false); var initial = state.Read(); state.PlayerHit(1.05f, -1, false); var repeated = state.Read();
            Assert.That(initial.InputLockUntil, Is.EqualTo(1 + .12f)); Assert.That(initial.KnockUntil, Is.EqualTo(1 + .1f)); Assert.That(initial.KnockSpeed, Is.EqualTo(5));
            Assert.That(repeated.InputLockUntil, Is.EqualTo(1.05f + .12f)); Assert.That(repeated.KnockUntil, Is.EqualTo(initial.KnockUntil)); Assert.That(repeated.KnockSpeed, Is.EqualTo(5));
            state.PlayerHit(initial.KnockUntil, -1, false); Assert.That(state.Read().KnockSpeed, Is.EqualTo(-5));
        }
        [Test] public void ForcedHitDoesNotReserveDelayedKnockback()
        {
            var state = new ActorStatusState(); state.PlayerHit(1, -1, true); var forced = state.Read();
            Assert.That(forced.InputLockUntil, Is.EqualTo(1 + .12f)); Assert.That(forced.KnockUntil, Is.Zero); Assert.That(forced.KnockSpeed, Is.Zero);
            Assert.That(state.Read().KnockUntil, Is.Zero); state.PlayerHit(2, 1, false); Assert.That(state.Read().KnockUntil, Is.EqualTo(2 + .1f));
        }
        [Test] public void RootStunAndConfiguredBasicStaggerRefreshByMaximum()
        {
            var state = new ActorStatusState(); state.WriteRootUntil(3); state.WriteStunUntil(3); state.RefreshRoot(1, .5f); state.RefreshStun(1, .5f);
            Assert.That(state.Read().RootUntil, Is.EqualTo(3)); Assert.That(state.Read().StunUntil, Is.EqualTo(3));
            state.RefreshRoot(1, 5); state.RefreshStun(1, 5); Assert.That(state.Read().RootUntil, Is.EqualTo(6)); Assert.That(state.Read().StunUntil, Is.EqualTo(6));
            state.BasicStagger(1, .3f); Assert.That(state.Read().StaggerUntil, Is.EqualTo(1 + .3f));
            state.WriteStaggerUntil(3); state.BasicStagger(1, .3f); Assert.That(state.Read().StaggerUntil, Is.EqualTo(3));
            state.BasicStagger(3, .3f); Assert.That(state.Read().StaggerUntil, Is.EqualTo(3 + .3f));
        }
        [Test] public void PushAndExplicitKnockUseOneValidatedPair()
        {
            var state = new ActorStatusState(); state.Push(1, -1, .5f, .1f); Assert.That(state.Read().KnockUntil, Is.EqualTo(1 + .1f)); Assert.That(state.Read().KnockSpeed, Is.EqualTo(-5));
            state.Push(2, 1, .5f, 0); Assert.That(state.Read().KnockUntil, Is.EqualTo(2)); Assert.That(state.Read().KnockSpeed, Is.EqualTo(50));
            state.SetKnockMotion(8, -3); Assert.That(state.Read().KnockUntil, Is.EqualTo(8)); Assert.That(state.Read().KnockSpeed, Is.EqualTo(-3));
        }
        [Test] public void HiddenAndEnemyControlQueriesUseStrictDeadlinesAndDoNotExpireStoredValues()
        {
            var state = new ActorStatusState(); state.WriteRootUntil(9); Assert.That(state.Read().EnemyControlledAt(1), Is.False, "Root affects movement, not enemy attack admission.");
            state.WriteStunUntil(2); state.WriteStealthUntil(2); state.WriteSmokeUntil(3); var snapshot = state.Read();
            Assert.That(snapshot.EnemyControlledAt(1.999f), Is.True); Assert.That(snapshot.EnemyControlledAt(2), Is.False); Assert.That(snapshot.HiddenAt(2), Is.True); Assert.That(snapshot.HiddenAt(3), Is.False);
            Assert.That(state.Read().StunUntil, Is.EqualTo(2)); Assert.That(state.Read().SmokeUntil, Is.EqualTo(3));
        }
        [Test] public void PartialClearAndResetKeepTheirSpecificScopeAndLegacyInactiveSpeed()
        {
            var state = new ActorStatusState(); state.WriteStunUntil(2); state.WriteRootUntil(3); state.WriteStaggerUntil(4); state.WriteInputLockUntil(5); state.SetKnockMotion(6, -7); state.WriteStealthUntil(8); state.WriteSmokeUntil(9);
            state.ClearHitMotionLocks(); var cleared = state.Read(); Assert.That(cleared.InputLockUntil, Is.Zero); Assert.That(cleared.KnockUntil, Is.Zero); Assert.That(cleared.RootUntil, Is.EqualTo(3)); Assert.That(cleared.KnockSpeed, Is.EqualTo(-7)); Assert.That(cleared.HiddenAt(1), Is.True);
            state.ClearConcealment(); Assert.That(state.Read().HiddenAt(1), Is.False); Assert.That(state.Read().StunUntil, Is.EqualTo(2)); state.Reset(); var reset = state.Read();
            Assert.That(reset.StunUntil + reset.RootUntil + reset.StaggerUntil + reset.InputLockUntil + reset.KnockUntil + reset.StealthUntil + reset.SmokeUntil, Is.Zero); Assert.That(reset.KnockSpeed, Is.EqualTo(-7));
        }
        [Test] public void OldSnapshotsAndOtherActorsStayIndependentAndReadOnly()
        {
            var a = new ActorStatusState(); var b = new ActorStatusState(); a.RefreshRoot(1, 3); var before = a.Read(); a.Reset(); b.RefreshStun(1, 5);
            Assert.That(before.RootUntil, Is.EqualTo(4)); Assert.That(a.Read().RootUntil, Is.Zero); Assert.That(a.Read().StunUntil, Is.Zero); Assert.That(b.Read().StunUntil, Is.EqualTo(6)); Assert.That(typeof(ActorStatusSnapshot).GetProperties().Any(p => p.GetSetMethod(true) != null), Is.False);
        }
        [Test] public void InvalidOrOverflowingStatusCommandsDoNotPublishPartialValues()
        {
            var state = new ActorStatusState(); state.SetKnockMotion(6, -7); state.WriteInputLockUntil(5); state.WriteRootUntil(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.WriteStunUntil(float.NaN)); Assert.Throws<ArgumentOutOfRangeException>(() => state.WriteRootUntil(float.PositiveInfinity)); Assert.Throws<ArgumentOutOfRangeException>(() => state.WriteStealthUntil(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SetKnockMotion(8, float.NaN)); Assert.Throws<ArgumentOutOfRangeException>(() => state.PlayerHit(-1, 1, false)); Assert.Throws<ArgumentOutOfRangeException>(() => state.RefreshRoot(float.MaxValue, float.MaxValue)); Assert.Throws<ArgumentOutOfRangeException>(() => state.Push(1, 1, float.MaxValue, .01f));
            Assert.That(state.Read().KnockUntil, Is.EqualTo(6)); Assert.That(state.Read().KnockSpeed, Is.EqualTo(-7)); Assert.That(state.Read().InputLockUntil, Is.EqualTo(5)); Assert.That(state.Read().RootUntil, Is.EqualTo(3));
        }
    }
}
