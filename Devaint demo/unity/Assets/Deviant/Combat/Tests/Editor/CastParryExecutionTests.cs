using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    public class CastParryExecutionTests
    {
        sealed class Attempt
        {
            public bool Activated = true, Cancelled, SourceExists = true, AcceptDamage = true;
            public bool EffectExists = true;
            public string EffectType = "parry";
            public float Elapsed, Origin = 2, OwnerCenter, Multiplier = 1, Health = 100;
            public int Direction = 1, Received, Recorded;
            public float LastDamage;
            public bool LastProjectile;
            public Action<string> OnRead;
            public Action DuringReceive, DuringRecord;
            public readonly List<string> Events = new List<string>();
            public bool Try(bool projectile = false)
            {
                var host = new Host(this); return CastParryExecution.TryParry(projectile, ref host);
            }
            public T Read<T>(string name, T value) { Events.Add(name); OnRead?.Invoke(name); return value; }
        }
        readonly struct Host : ICastParryHost
        {
            readonly Attempt attempt;
            public Host(Attempt attempt) { this.attempt = attempt; }
            public bool Activated => attempt.Read("activated", attempt.Activated);
            public bool Cancelled => attempt.Read("cancelled", attempt.Cancelled);
            public bool EffectExists => attempt.Read("effect-exists", attempt.EffectExists);
            public string EffectType => attempt.Read("effect-type", attempt.EffectType);
            public float ParryWindow => .15f;
            public float ParryDamage => 30;
            public float Elapsed => attempt.Read("elapsed", attempt.Elapsed);
            public bool SourceExists => attempt.Read("source-exists", attempt.SourceExists);
            public float AttackOriginX => attempt.Read("attack-origin", attempt.Origin);
            public float OwnerCenterX => attempt.Read("owner-center", attempt.OwnerCenter);
            public int Direction => attempt.Read("direction", attempt.Direction);
            public float DamageMultiplier => attempt.Read("multiplier", attempt.Multiplier);
            public bool Receive(float damage)
            {
                attempt.Events.Add("receive"); attempt.LastDamage = damage;
                if (attempt.AcceptDamage) { attempt.Health -= damage; attempt.Received++; }
                attempt.DuringReceive?.Invoke(); return attempt.AcceptDamage;
            }
            public void Record(bool projectileAttack)
            {
                attempt.Events.Add("record"); attempt.Recorded++; attempt.LastProjectile = projectileAttack;
                attempt.DuringRecord?.Invoke();
            }
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static float Before(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) - 1);
        static float After(float positive) => BitConverter.Int32BitsToSingle(Bits(positive) + 1);
        static string[] ReadsThroughSource => new[] { "activated", "cancelled", "effect-exists", "effect-type", "elapsed", "elapsed", "source-exists" };

        [TestCase("inactive")]
        [TestCase("cancelled")]
        [TestCase("missing-effect")]
        [TestCase("different-effect")]
        [TestCase("negative-age")]
        [TestCase("closed-window")]
        [TestCase("missing-source")]
        public void IneligibleAttemptsStopAtTheOriginalGateWithoutGeometryOrResponses(string scenario)
        {
            var attempt = new Attempt(); string[] expected;
            switch (scenario)
            {
                case "inactive": attempt.Activated = false; expected = new[] { "activated" }; break;
                case "cancelled": attempt.Cancelled = true; expected = new[] { "activated", "cancelled" }; break;
                case "missing-effect": attempt.EffectExists = false; expected = new[] { "activated", "cancelled", "effect-exists" }; break;
                case "different-effect":
                    attempt.EffectType = "owner_guard";
                    expected = new[] { "activated", "cancelled", "effect-exists", "effect-type" }; break;
                case "negative-age":
                    attempt.Elapsed = -.03125f; expected = new[] { "activated", "cancelled", "effect-exists", "effect-type", "elapsed" }; break;
                case "closed-window":
                    attempt.Elapsed = .15f; expected = new[] { "activated", "cancelled", "effect-exists", "effect-type", "elapsed", "elapsed" }; break;
                default: attempt.SourceExists = false; expected = ReadsThroughSource; break;
            }
            Assert.That(attempt.Try(), Is.False); Assert.That(attempt.Events, Is.EqualTo(expected));
            Assert.That(attempt.Health, Is.EqualTo(100)); Assert.That(attempt.Received, Is.Zero); Assert.That(attempt.Recorded, Is.Zero);
        }
        [Test] public void WindowIncludesZeroAndTheLastFloatBeforePointFifteenButNotItsEndOrEarlierTime()
        {
            var attempt = new Attempt { Elapsed = -float.Epsilon };
            Assert.That(attempt.Try(), Is.False);
            attempt.Elapsed = 0; Assert.That(attempt.Try(), Is.True);
            attempt.Elapsed = Before(.15f); Assert.That(attempt.Try(), Is.True);
            attempt.Elapsed = .15f; Assert.That(attempt.Try(), Is.False);
            attempt.Elapsed = After(.15f); Assert.That(attempt.Try(), Is.False);
            Assert.That(attempt.Received, Is.EqualTo(2)); Assert.That(attempt.Recorded, Is.EqualTo(2)); Assert.That(attempt.Health, Is.EqualTo(40));
        }
        [TestCase(1)]
        [TestCase(-1)]
        public void FrontEdgeIsIncludedAndEveryAttemptReadsCurrentOriginCenterAndCastDirection(int direction)
        {
            var attempt = new Attempt { OwnerCenter = 3, Direction = direction, Origin = 3 + direction };
            Assert.That(attempt.Try(true), Is.True);
            attempt.Origin = 3; Assert.That(attempt.Try(true), Is.True);
            attempt.Origin = 3 - direction * .000244140625f; Assert.That(attempt.Try(true), Is.False);
            attempt.OwnerCenter = 3 - 2 * direction; Assert.That(attempt.Try(true), Is.True);
            attempt.Direction = -direction; Assert.That(attempt.Try(true), Is.False);
            Assert.That(attempt.Received, Is.Zero); Assert.That(attempt.Recorded, Is.EqualTo(3));
        }
        [Test] public void GeometryAndMultiplierAreReadOnlyAtTheirExistingResponseBoundaries()
        {
            var attempt = new Attempt { OwnerCenter = 4, Origin = 5, Multiplier = 3 };
            attempt.OnRead = name => {
                if (name == "attack-origin") attempt.OwnerCenter = 5;
                if (name == "owner-center") attempt.Direction = -1;
                if (name == "direction") attempt.Multiplier = 1.5f;
            };
            Assert.That(attempt.Try(), Is.True); Assert.That(attempt.LastDamage, Is.EqualTo(45));
            Assert.That(attempt.Events, Is.EqualTo(new[] { "activated", "cancelled", "effect-exists", "effect-type", "elapsed", "elapsed", "source-exists",
                "attack-origin", "owner-center", "direction", "multiplier", "receive", "record" }));
            attempt.Events.Clear(); attempt.OnRead = null; attempt.Origin = 6; attempt.OwnerCenter = 5;
            Assert.That(attempt.Try(), Is.False);
            Assert.That(attempt.Events, Is.EqualTo(new[] { "activated", "cancelled", "effect-exists", "effect-type", "elapsed", "elapsed", "source-exists",
                "attack-origin", "owner-center", "direction" }));
        }
        [Test] public void MeleeRespondsBeforeRecordingAndRepeatedAttacksDoNotSpendAWindowClaim()
        {
            var attempt = new Attempt { Multiplier = 1.5f };
            attempt.DuringReceive = () => Assert.That(attempt.Recorded, Is.EqualTo(attempt.Received - 1));
            attempt.DuringRecord = () => Assert.That(attempt.Received, Is.EqualTo(attempt.Recorded));
            Assert.That(attempt.Try(), Is.True); Assert.That(attempt.Try(), Is.True);
            Assert.That(attempt.Received, Is.EqualTo(2)); Assert.That(attempt.Recorded, Is.EqualTo(2));
            Assert.That(attempt.Health, Is.EqualTo(10)); Assert.That(attempt.LastProjectile, Is.False);
        }
        [Test] public void ProjectileSuccessRecordsWithoutReadingDamageOrDamagingTheSource()
        {
            var attempt = new Attempt();
            attempt.OnRead = name => { if (name == "multiplier") throw new InvalidOperationException("projectile damage must stay with its projectile"); };
            attempt.DuringReceive = () => throw new InvalidOperationException("projectile parry does not call Receive");
            Assert.That(attempt.Try(true), Is.True); Assert.That(attempt.Try(true), Is.True);
            Assert.That(attempt.Health, Is.EqualTo(100)); Assert.That(attempt.Received, Is.Zero);
            Assert.That(attempt.Recorded, Is.EqualTo(2)); Assert.That(attempt.LastProjectile, Is.True);
        }
        [Test] public void ReceiveRejectionStillBlocksAndRecordsTheIncomingAttack()
        {
            var attempt = new Attempt { AcceptDamage = false };
            Assert.That(attempt.Try(), Is.True); Assert.That(attempt.Try(), Is.True);
            Assert.That(attempt.Health, Is.EqualTo(100)); Assert.That(attempt.Received, Is.Zero);
            Assert.That(attempt.Recorded, Is.EqualTo(2)); Assert.That(attempt.LastDamage, Is.EqualTo(30));
        }
        [Test] public void ReceiveCallbackCanCloseTheCastWithoutSuppressingTheEnteredSuccessRecord()
        {
            var attempt = new Attempt();
            attempt.DuringReceive = () => {
                attempt.Activated = false; attempt.Cancelled = true; attempt.Elapsed = 4;
                attempt.SourceExists = false; attempt.Origin = -2;
                Assert.That(attempt.Try(), Is.False);
            };
            Assert.That(attempt.Try(), Is.True); Assert.That(attempt.Recorded, Is.EqualTo(1));
            Assert.That(attempt.Received, Is.EqualTo(1)); Assert.That(attempt.Health, Is.EqualTo(70));
            Assert.That(attempt.Try(), Is.False); Assert.That(attempt.Recorded, Is.EqualTo(1));
        }
        [Test] public void ResponseReentryRemainsAnotherAttackWithoutAOnePerWindowLedger()
        {
            var attempt = new Attempt(); bool nested = false;
            attempt.DuringReceive = () => {
                if (nested) return; nested = true; Assert.That(attempt.Recorded, Is.Zero);
                Assert.That(attempt.Try(), Is.True); Assert.That(attempt.Recorded, Is.EqualTo(1));
            };
            Assert.That(attempt.Try(), Is.True); Assert.That(attempt.Received, Is.EqualTo(2));
            Assert.That(attempt.Recorded, Is.EqualTo(2)); Assert.That(attempt.Health, Is.EqualTo(40));
        }
        [Test] public void ReceiveExceptionKeepsItsPartialEffectsAndDoesNotInventASuccessRecordOrClaim()
        {
            var failure = new InvalidOperationException("native Receive failure"); var attempt = new Attempt();
            attempt.DuringReceive = () => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => attempt.Try()), Is.SameAs(failure));
            Assert.That(attempt.Health, Is.EqualTo(70)); Assert.That(attempt.Received, Is.EqualTo(1)); Assert.That(attempt.Recorded, Is.Zero);
            attempt.DuringReceive = null; Assert.That(attempt.Try(), Is.True);
            Assert.That(attempt.Health, Is.EqualTo(40)); Assert.That(attempt.Received, Is.EqualTo(2)); Assert.That(attempt.Recorded, Is.EqualTo(1));
        }
        [Test] public void RecordExceptionPropagatesAfterDamageAndDoesNotPreventLaterAttempts()
        {
            var failure = new InvalidOperationException("native record failure"); var attempt = new Attempt();
            attempt.DuringRecord = () => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => attempt.Try()), Is.SameAs(failure));
            Assert.That(attempt.Health, Is.EqualTo(70)); Assert.That(attempt.Received, Is.EqualTo(1)); Assert.That(attempt.Recorded, Is.EqualTo(1));
            attempt.DuringRecord = null; Assert.That(attempt.Try(), Is.True);
            Assert.That(attempt.Health, Is.EqualTo(40)); Assert.That(attempt.Recorded, Is.EqualTo(2));
        }
        [Test] public void AStructHostIsPassedByReferenceRatherThanBoxedOrCopied()
        {
            var host = new ValueHost(); Assert.That(CastParryExecution.TryParry(false, ref host), Is.True);
            Assert.That(host.Received, Is.EqualTo(1)); Assert.That(host.Recorded, Is.EqualTo(1));
            Assert.That(CastParryExecution.TryParry(true, ref host), Is.True);
            Assert.That(host.Received, Is.EqualTo(1)); Assert.That(host.Recorded, Is.EqualTo(2));
        }
        struct ValueHost : ICastParryHost
        {
            public int Received, Recorded;
            public bool Activated => true;
            public bool Cancelled => false;
            public bool EffectExists => true;
            public string EffectType => "parry";
            public float ParryWindow => .15f;
            public float ParryDamage => 30;
            public float Elapsed => 0;
            public bool SourceExists => true;
            public float AttackOriginX => 0;
            public float OwnerCenterX => 0;
            public int Direction => 1;
            public float DamageMultiplier => 1;
            public bool Receive(float damage) { Received++; return true; }
            public void Record(bool projectileAttack) { Recorded++; }
        }
    }
}
