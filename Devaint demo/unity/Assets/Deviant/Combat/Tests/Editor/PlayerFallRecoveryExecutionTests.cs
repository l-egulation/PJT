using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class PlayerFallRecoveryExecutionTests
    {
        sealed class State
        {
            public readonly List<string> Calls = new List<string>();
            public float Health = 81, Max = 175, Now = 8, LastFall = -1, LastRestore = -1, Protection;
            public float X = 7, Y = -5, SpawnX = -4, SpawnY, LastX, LastY, Cutoff;
            public int Facing = 1;
            public bool Found = true;
            public SafeGroundSample Sample = new SafeGroundSample(4, 2, 3, -1);
            public Action<string> Callback;
            public void Call(string name) { Calls.Add(name); Callback?.Invoke(name); }
        }
        struct Host : IPlayerFallRecoveryHost
        {
            public State State;
            public void CancelAction() => State.Call("cancel");
            public void ClearBufferedJump() => State.Call("clear-buffer");
            public float Health { get => State.Health; set { State.Health = value; State.Call("health"); } }
            public float MaxHealth => State.Max;
            public bool Dead => State.Health <= 0;
            public void Die() => State.Call("die");
            public float SpawnX => State.SpawnX;
            public float SpawnY => State.SpawnY;
            public float Now => State.Now;
            public float LastFallTime { get => State.LastFall; set { State.LastFall = value; State.Call("last-fall"); } }
            public float LastRestoreTime { get => State.LastRestore; set { State.LastRestore = value; State.Call("last-restore"); } }
            public int Facing => State.Facing;
            public bool TrySafeBefore(float cutoff, out SafeGroundSample sample) { State.Cutoff = cutoff; State.Call("query"); sample = State.Sample; return State.Found; }
            public void RecordSample(bool found, SafeGroundSample sample) => State.Call("record-sample");
            public void Place(float x, float y) { State.X = x; State.Y = y; State.Call("place"); }
            public void SyncFacing(int facing) { State.Facing = facing; State.Call("facing"); }
            public void ClearVelocity() => State.Call("velocity");
            public void ResetJumps() => State.Call("jumps");
            public void SetProtectionUntil(float deadline) { State.Protection = deadline; State.Call("protection"); }
            public void SetLastRestorePosition(float x, float y) { State.LastX = x; State.LastY = y; State.Call("last-position"); }
            public void ClearHadGround() => State.Call("ground");
            public void ClearHitMotionLocks() => State.Call("locks");
            public void RecordRestored(float x, float y) => State.Call("record-restored");
        }
        [Test] public void SurvivingFallKeepsExactDamageHistoryPlacementAndCleanupOrder()
        {
            var state = new State(); var host = new Host { State = state }; PlayerFallRecoveryExecution.Fall(ref host);
            Assert.That(state.Calls, Is.EqualTo(new[] { "cancel", "clear-buffer", "health", "last-fall", "last-restore", "query", "record-sample", "last-restore", "place", "facing", "place", "velocity", "jumps", "protection", "last-position", "ground", "locks", "record-restored" }));
            Assert.That(BitConverter.SingleToInt32Bits(state.Health), Is.EqualTo(BitConverter.SingleToInt32Bits(Mathf.Max(0, 81 - 175 * .1f))));
            Assert.That(state.Cutoff, Is.EqualTo(5)); Assert.That(state.LastRestore, Is.EqualTo(4)); Assert.That(state.X, Is.EqualTo(2)); Assert.That(state.Y, Is.EqualTo(3)); Assert.That(state.Facing, Is.EqualTo(-1)); Assert.That(state.Protection, Is.EqualTo(8 + .6f));
        }
        [Test] public void LethalDamageStopsBeforeHistoryAndPreservesUnchangedPreviousRestoreFields()
        {
            var state = new State { Health = 1, LastFall = 6, LastRestore = 2 }; var host = new Host { State = state }; PlayerFallRecoveryExecution.Fall(ref host);
            Assert.That(state.Calls, Is.EqualTo(new[] { "cancel", "clear-buffer", "health", "die" })); Assert.That(state.Health, Is.Zero); Assert.That(state.LastFall, Is.EqualTo(6)); Assert.That(state.LastRestore, Is.EqualTo(2));
        }
        [Test] public void MissingHistoryUsesCapturedSpawnAndTwoPlacementWritesAroundFacingCallback()
        {
            var state = new State { Found = false }; state.Callback = name => { if (name == "facing") { state.X = 999; state.Now = 10; state.SpawnX = 888; } };
            var host = new Host { State = state }; PlayerFallRecoveryExecution.Fall(ref host);
            Assert.That(state.X, Is.EqualTo(-4)); Assert.That(state.LastX, Is.EqualTo(-4)); Assert.That(state.LastRestore, Is.EqualTo(-1)); Assert.That(state.Protection, Is.EqualTo(10 + .6f));
        }
        [Test] public void HistoryOrLoggingExceptionsPreserveExistingPartialDamageAndMetadataWithoutPlacement()
        {
            foreach (string failure in new[] { "query", "record-sample" })
            {
                var state = new State(); state.Callback = name => { if (name == failure) throw new InvalidOperationException(failure); };
                var host = new Host { State = state }; Assert.Throws<InvalidOperationException>(() => PlayerFallRecoveryExecution.Fall(ref host));
                Assert.That(state.Health, Is.EqualTo(63.5f)); Assert.That(state.LastFall, Is.EqualTo(8)); Assert.That(state.LastRestore, Is.EqualTo(-1)); Assert.That(state.Calls, Does.Not.Contain("place"));
            }
        }
        [Test] public void CancellationCallbackEditsAreReadBeforeDirectEnvironmentDamage()
        {
            var state = new State(); state.Callback = name => { if (name == "cancel") { state.Health = 40; state.Max = 250; state.Now = 9; } };
            var host = new Host { State = state }; PlayerFallRecoveryExecution.Fall(ref host);
            Assert.That(state.Health, Is.EqualTo(15)); Assert.That(state.LastFall, Is.EqualTo(9)); Assert.That(state.Cutoff, Is.EqualTo(6));
        }
    }
}
