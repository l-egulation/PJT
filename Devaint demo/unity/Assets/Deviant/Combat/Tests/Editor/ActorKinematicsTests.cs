using System;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class ActorKinematicsTests
    {
        [Test] public void KinematicsContractsAndOwnerDoNotReferenceEngineOrOtherRuntime()
        {
            foreach (var type in new[] { typeof(IActorKinematics), typeof(ActorKinematicsSnapshot), typeof(ActorKinematicsState) })
                Assert.That(type.Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name == "Deviant.Combat" || a.Name.StartsWith("Deviant.World") || a.Name.StartsWith("Deviant.UI")), Is.False);
        }
        [Test] public void IndependentActorsStartAtTheExistingDefaultMotionFacts()
        {
            IActorKinematics first = new ActorKinematicsState(), second = new ActorKinematicsState();
            first.WritePosition(2, 3); first.WriteVelocity(5, -7); first.WriteFacing(-1); first.WriteGrounded(true); first.WriteWallHit(true);
            var untouched = second.Read();
            Assert.That(untouched.X, Is.Zero); Assert.That(untouched.Y, Is.Zero); Assert.That(untouched.VelocityX, Is.Zero); Assert.That(untouched.VelocityY, Is.Zero);
            Assert.That(untouched.Facing, Is.EqualTo(1)); Assert.That(untouched.Grounded, Is.False); Assert.That(untouched.WallHit, Is.False);
        }
        [Test] public void SnapshotsDoNotExposeOwnerStorageOrChangeAfterLaterCommands()
        {
            var state = new ActorKinematicsState(); state.WritePosition(2, 3); state.WriteVelocity(5, -7); state.WriteFacing(-1); state.WriteGrounded(true); state.WriteWallHit(true);
            var before = state.Read(); state.WritePosition(11, 13); state.WriteVelocity(17, 19); state.WriteFacing(1); state.WriteGrounded(false); state.WriteWallHit(false);
            Assert.That(before.X, Is.EqualTo(2)); Assert.That(before.Y, Is.EqualTo(3)); Assert.That(before.VelocityX, Is.EqualTo(5)); Assert.That(before.VelocityY, Is.EqualTo(-7));
            Assert.That(before.Facing, Is.EqualTo(-1)); Assert.That(before.Grounded && before.WallHit, Is.True);
            Assert.That(typeof(ActorKinematicsSnapshot).GetProperties().Any(p => p.GetSetMethod(true) != null), Is.False);
            Assert.That(typeof(ActorKinematicsState).GetFields().Length, Is.Zero);
        }
        [Test] public void EachScalarCommandChangesOnlyItsOwnFactAndDoesNotNormalizeFacing()
        {
            IActorKinematics state = new ActorKinematicsState(); state.WritePosition(2, 3); state.WriteVelocity(5, -7); state.WriteFacing(int.MinValue); state.WriteGrounded(true); state.WriteWallHit(true);
            state.WriteVelocityX(11); Assert.That(state.VelocityX, Is.EqualTo(11)); Assert.That(state.VelocityY, Is.EqualTo(-7));
            state.WriteVelocityY(13); Assert.That(state.VelocityX, Is.EqualTo(11)); Assert.That(state.VelocityY, Is.EqualTo(13));
            Assert.That(state.X, Is.EqualTo(2)); Assert.That(state.Y, Is.EqualTo(3)); Assert.That(state.Facing, Is.EqualTo(int.MinValue)); Assert.That(state.Grounded && state.WallHit, Is.True);
            state.WriteGrounded(false); Assert.That(state.Grounded, Is.False); Assert.That(state.WallHit, Is.True);
            state.WriteWallHit(false); state.WriteFacing(0); Assert.That(state.WallHit, Is.False); Assert.That(state.Facing, Is.Zero);
        }
        [Test] public void ResetPhasesKeepFacingWallAndGroundFactsUntilTheirExplicitBoundary()
        {
            IActorKinematics state = new ActorKinematicsState(); state.WritePosition(2, 3); state.WriteVelocity(5, -7); state.WriteFacing(-1); state.WriteGrounded(true); state.WriteWallHit(true);
            state.WritePosition(11, 13); Assert.That(state.VelocityY, Is.EqualTo(-7)); Assert.That(state.Grounded && state.WallHit, Is.True);
            state.WriteVelocity(0, 0); Assert.That(state.X, Is.EqualTo(11)); Assert.That(state.Y, Is.EqualTo(13)); Assert.That(state.Facing, Is.EqualTo(-1)); Assert.That(state.Grounded && state.WallHit, Is.True);
            state.WriteGrounded(false); Assert.That(state.VelocityX, Is.Zero); Assert.That(state.VelocityY, Is.Zero); Assert.That(state.Grounded, Is.False); Assert.That(state.WallHit, Is.True); Assert.That(state.Facing, Is.EqualTo(-1));
        }
        [Test] public void WritesPreserveNativeFloatBitsRatherThanReintegratingOrClamping()
        {
            var state = new ActorKinematicsState(); float negativeZero = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
            state.WritePosition(negativeZero, 1000.125f); state.WriteVelocity(17.25f, negativeZero); var snapshot = state.Read();
            Assert.That(BitConverter.SingleToInt32Bits(snapshot.X), Is.EqualTo(unchecked((int)0x80000000)));
            Assert.That(BitConverter.SingleToInt32Bits(snapshot.VelocityY), Is.EqualTo(unchecked((int)0x80000000)));
            Assert.That(snapshot.Y, Is.EqualTo(1000.125f)); Assert.That(snapshot.VelocityX, Is.EqualTo(17.25f));
        }
        [Test] public void RepeatedReadsAndWritesDoNotAllocate()
        {
            IActorKinematics state = new ActorKinematicsState();
            for (int i = 0; i < 32; i++) Exercise(state, i);
            GC.GetAllocatedBytesForCurrentThread(); long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2048; i++) Exercise(state, i);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero); Assert.That(state.X, Is.EqualTo(2047)); Assert.That(state.VelocityX, Is.EqualTo(-2047));
        }
        static void Exercise(IActorKinematics state, int value)
        {
            state.WritePosition(value, -value); state.WriteVelocity(value, -value); state.WriteVelocityX(-value); state.WriteVelocityY(value);
            state.WriteFacing(value); state.WriteGrounded((value & 1) == 0); state.WriteWallHit((value & 2) == 0);
            var snapshot = state.Read(); state.WritePosition(snapshot.X, snapshot.Y);
        }
    }
}
