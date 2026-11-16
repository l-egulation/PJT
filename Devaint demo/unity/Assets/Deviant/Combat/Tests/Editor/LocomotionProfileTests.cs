using System.Collections;
using System.Globalization;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class LocomotionProfileTests
    {
        [Test] public void JumpApexDurationIsBoundedImmutableHashedAndExported()
        {
            var source = Source();
            Assert.That(LocomotionProfile.TryCreate(source, out var original, out var error), Is.True, error);
            Assert.That(original.JumpApexSeconds, Is.EqualTo(.06f));
            var edited = original.ToData(); edited.jumpApexSeconds = .04f;
            Assert.That(LocomotionProfile.TryCreate(edited, out var changed, out error), Is.True, error);
            Assert.That(changed.ContentHash, Is.Not.EqualTo(original.ContentHash));
            source.jumpApexSeconds = 0;
            Assert.That(original.ToData().jumpApexSeconds, Is.EqualTo(.06f));
            foreach (var invalid in new[] { -.01f, .061f, float.NaN, float.PositiveInfinity })
            {
                edited.jumpApexSeconds = invalid;
                Assert.That(LocomotionProfile.TryCreate(edited, out _, out _), Is.False);
            }
        }
        [Test] public void MovementAssistUsesCanonicalTrialValuesAndLegacyProfilesKeepOriginalControl()
        {
            Assert.That(LocomotionProfile.TryCreate(Source(), out var profile, out var error), Is.True, error);
            Assert.That(new[] { profile.JumpBufferDuration, profile.WallCoyoteDuration, profile.JumpReleaseVelocityScale,
                profile.GroundAccelerationSeconds, profile.GroundDecelerationSeconds, profile.AirAccelerationSeconds, profile.AirDecelerationSeconds },
                Is.EqualTo(new[] { .12f, .1f, 1f, .05f, .03f, .1f, .08f }));
            var legacy = profile.ToData(); legacy.jumpAssist = null; legacy.horizontalControl = null;
            Assert.That(LocomotionProfile.TryCreate(legacy, out var original, out error), Is.True, error);
            Assert.That(original.JumpBufferDuration, Is.EqualTo(LocomotionProfile.LegacyJumpBufferDuration));
            Assert.That(original.JumpReleaseVelocityScale, Is.EqualTo(1)); Assert.That(original.WallCoyoteDuration, Is.Zero);
            Assert.That(original.GroundAccelerationSeconds + original.AirDecelerationSeconds, Is.Zero);
            Assert.That(original.ContentHash, Is.Not.EqualTo(profile.ContentHash));
            foreach (System.Action<LocomotionData> invalid in new System.Action<LocomotionData>[] {
                d => d.jumpAssist.groundBufferDuration = .51f, d => d.jumpAssist.wallCoyoteDuration = -.01f,
                d => d.jumpAssist.releaseVelocityScale = 0, d => d.jumpAssist.releaseVelocityScale = 1.01f,
                d => d.horizontalControl.airAccelerationSeconds = float.NaN, d => d.horizontalControl.groundDecelerationSeconds = 1.01f })
            {
                var edited = profile.ToData(); invalid(edited);
                Assert.That(LocomotionProfile.TryCreate(edited, out _, out _), Is.False);
            }
        }
        [Test] public void WallFeelValuesDisableStationaryClingAndKeepLegacyProfileRoundtrip()
        {
            Assert.That(LocomotionProfile.TryCreate(Source(), out var profile, out var error), Is.True, error);
            Assert.That(new[] { profile.WallDetachDuration, profile.WallAwayKickSpeed,
                profile.WallReleaseKickCarryDuration, profile.WallKickControlRecoveryDuration, profile.WallSlideClingDuration,
                profile.WallSlideStartSpeed, profile.WallSlideMaxSpeed, profile.WallSlideAcceleration,
                profile.WallArrivalClingDuration, profile.WallLedgeHopHeight, profile.WallStaminaDuration,
                profile.WallJumpCheckDistance, profile.WallLedgeClimbSeconds, profile.WallLedgeClimbForward },
                Is.EqualTo(new[] { 0f, 18f, .15f, .2f, 0f, 2f, 6f, 8f, 0f, 1.6f, 1f, .15f, .48f, .67f }));
            Assert.That(LocomotionProfile.TryCreate(profile.ToData(), out var copy, out error), Is.True, error);
            Assert.That(copy.ContentHash, Is.EqualTo(profile.ContentHash));
            var legacy = profile.ToData(); legacy.wallSlide = null;
            legacy.wallJump.detachDuration = legacy.wallJump.releaseCarryDuration = legacy.wallJump.jumpCheckDistance = 0;
            Assert.That(LocomotionProfile.TryCreate(legacy, out var original, out error), Is.True, error);
            Assert.That(original.WallSlideMaxSpeed + original.WallDetachDuration + original.WallReleaseKickCarryDuration +
                original.WallStaminaDuration + original.WallJumpCheckDistance, Is.Zero);
            Assert.That(original.ContentHash, Is.Not.EqualTo(profile.ContentHash));
            foreach (System.Action<LocomotionData> invalid in new System.Action<LocomotionData>[] {
                d => d.wallJump.detachDuration = .51f, d => d.wallJump.awayHorizontalSpeed = -1,
                d => d.wallJump.awayHorizontalSpeed = float.NaN, d => d.wallJump.releaseCarryDuration = 1.01f,
                d => d.wallJump.controlRecoveryDuration = -.01f, d => d.wallSlide.clingDuration = float.PositiveInfinity,
                d => d.wallSlide.startSpeed = 7, d => d.wallSlide.acceleration = -1,
                d => d.wallJump.arrivalClingDuration = .51f, d => d.wallJump.ledgeHopHeight = 2.01f,
                d => d.wallSlide.staminaDuration = 10.01f, d => d.wallJump.jumpCheckDistance = .51f,
                d => d.wallJump.ledgeClimbSeconds = 2.01f, d => d.wallJump.ledgeClimbSeconds = float.NaN,
                d => d.wallJump.ledgeClimbForward = -.01f, d => d.wallJump.ledgeClimbForward = 3.01f })
            {
                var edited = profile.ToData(); invalid(edited);
                Assert.That(LocomotionProfile.TryCreate(edited, out _, out _), Is.False);
            }
        }
        static LocomotionData Source() => JsonUtility.FromJson<LocomotionData>(Resources.Load<TextAsset>("PlayerLocomotion").text);

        [Test] public void GeneratedProfilePreservesApprovedMovementAndIsAnImmutableSnapshot()
        {
            var data = Source();
            Assert.That(LocomotionProfile.TryCreate(data, out var profile, out var error), Is.True, error);
            Assert.That(profile.RunSpeed, Is.EqualTo(7.5f));
            Assert.That(profile.DashSpeed, Is.EqualTo(22.5f));
            Assert.That(profile.ExpectedDashDistance, Is.EqualTo(4.5f));
            Assert.That(profile.DashDuration, Is.EqualTo(.2f));
            Assert.That(profile.DashRecovery, Is.EqualTo(1));
            Assert.That(profile.DashInvulnerability, Is.EqualTo(.15f));
            Assert.That(profile.DashJumpCarrySpeed, Is.EqualTo(5));
            Assert.That(profile.DashJumpCarryDuration, Is.EqualTo(.1f));
            Assert.That(data.schemaVersion, Is.EqualTo(2));
            Assert.That(profile.FirstJumpVelocity, Is.EqualTo(20)); Assert.That(profile.SecondJumpVelocity, Is.EqualTo(20));
            Assert.That(profile.RiseGravity, Is.EqualTo(66.6666667f)); Assert.That(profile.FallGravity, Is.EqualTo(104.1666667f));
            data.runSpeed = 999;
            profile.ToData().dashSpeed = 999;
            Assert.That(profile.RunSpeed, Is.EqualTo(7.5f));
            Assert.That(profile.DashSpeed, Is.EqualTo(22.5f));
            data.firstJumpVelocity = 999; profile.ToData().riseGravity = 999;
            Assert.That(profile.FirstJumpVelocity, Is.EqualTo(20)); Assert.That(profile.RiseGravity, Is.EqualTo(66.6666667f));
        }

        [TestCase("speed")] [TestCase("carry")] [TestCase("buffer")]
        public void WallJumpSettingsAreValidatedHashedAndCopied(string field)
        {
            var data=Source();
            Assert.That(LocomotionProfile.TryCreate(data,out var original,out var error),Is.True,error);
            Assert.That(original.WallJumpHorizontalSpeed,Is.EqualTo(15));
            Assert.That(original.WallJumpCarryDuration,Is.EqualTo(.35f));
            Assert.That(original.WallJumpBufferDuration,Is.EqualTo(.1f));
            void Set(WallJumpData w,float value) {
                if(field=="speed")w.horizontalSpeed=value;else if(field=="carry")w.carryDuration=value;else w.bufferDuration=value;
            }
            foreach(var value in new[]{-1f,float.NaN,float.PositiveInfinity}) {
                var invalid=original.ToData();Set(invalid.wallJump,value);
                Assert.That(LocomotionProfile.TryCreate(invalid,out _,out _),Is.False);
            }
            var edited=original.ToData();Set(edited.wallJump,2);
            Assert.That(LocomotionProfile.TryCreate(edited,out var changed,out error),Is.True,error);
            Assert.That(changed.ContentHash,Is.Not.EqualTo(original.ContentHash));
            Assert.That(original.WallJumpHorizontalSpeed,Is.EqualTo(15));
            data.wallJump=null;
            Assert.That(LocomotionProfile.TryCreate(data,out var legacy,out error),Is.True,error);
            Assert.That(legacy.WallJumpHorizontalSpeed,Is.EqualTo(data.runSpeed));
            Assert.That(legacy.WallJumpCarryDuration,Is.EqualTo(data.dashJumpCarryDuration));
            Assert.That(legacy.WallJumpBufferDuration,Is.Zero);
            Assert.That(LocomotionProfile.TryCreate(legacy.ToData(),out var copy,out error),Is.True,error);
            Assert.That(copy.ContentHash,Is.EqualTo(legacy.ContentHash));
        }

        [Test] public void ContentHashSurvivesJsonRoundTripAndCultureChange()
        {
            LocomotionProfile.TryCreate(Source(), out var original, out _);
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var restored = JsonUtility.FromJson<LocomotionData>(JsonUtility.ToJson(original.ToData()));
                Assert.That(LocomotionProfile.TryCreate(restored, out var copy, out var error), Is.True, error);
                Assert.That(copy.ContentHash, Is.EqualTo(original.ContentHash));
                restored.dashSpeed = 10;
                Assert.That(LocomotionProfile.TryCreate(restored, out var changed, out error), Is.True, error);
                Assert.That(changed.ContentHash, Is.Not.EqualTo(original.ContentHash));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidSpeedIsRejected(float speed)
        {
            var data = Source(); data.dashSpeed = speed;
            Assert.That(LocomotionProfile.TryCreate(data, out var profile, out var error), Is.False);
            Assert.That(profile, Is.Null); Assert.That(error, Is.Not.Empty);
        }

        [Test] public void InvalidSchemaAndImpossibleDefenseWindowAreRejected()
        {
            var data = Source(); data.schemaVersion = 1;
            Assert.That(LocomotionProfile.TryCreate(data, out _, out _), Is.False);
            data = Source(); data.dashDuration = 0;
            Assert.That(LocomotionProfile.TryCreate(data, out _, out _), Is.False);
            data = Source(); data.dashInvulnerability = data.dashDuration + .01f;
            Assert.That(LocomotionProfile.TryCreate(data, out _, out _), Is.False);
        }
        [TestCase("first")] [TestCase("second")] [TestCase("rise")] [TestCase("fall")]
        public void AllJumpGravityFieldsParticipateInHashAndRejectMissingOrNonpositiveValues(string field)
        {
            LocomotionProfile.TryCreate(Source(), out var original, out _);
            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                var data = Source(); Set(data, field, invalid); Assert.That(LocomotionProfile.TryCreate(data, out _, out _), Is.False);
            }
            var changed = Source(); Set(changed, field, field == "first" || field == "second" ? 18 : 80);
            Assert.That(LocomotionProfile.TryCreate(changed, out var next, out var error), Is.True, error); Assert.That(next.ContentHash, Is.Not.EqualTo(original.ContentHash));
        }
        static void Set(LocomotionData data, string field, float value)
        {
            switch (field) { case "first": data.firstJumpVelocity = value; break; case "second": data.secondJumpVelocity = value; break;
                case "rise": data.riseGravity = value; break; case "fall": data.fallGravity = value; break; }
        }

        [UnityTest] public IEnumerator TestArenaUsesResetAppliedMovement() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTest] public IEnumerator CommonGameUsesTheSameResetAppliedMovement() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene);
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var player = arena.Player; player.UseTestInput = true;
            foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.CancelAttack();}
            yield return new WaitForFixedUpdate();
            var source = arena.Movement;
            var edited = source.ToData(); edited.runSpeed = 8; edited.dashSpeed = 10;
            player.health = 42; player.QueueTestInput(basic: true);
            Assert.That(arena.TryApplyMovementAndReset(edited, out var error), Is.True, error);
            Assert.That(player.health, Is.EqualTo(100));
            Assert.That(arena.Movement.ContentHash, Is.Not.EqualTo(source.ContentHash));
            edited.runSpeed = 999;
            player.axis = 1;
            yield return Advance(arena,.2f); // Measure configured steady speed after the ground/air acceleration window.
            float at = arena.Now, x = player.Position.x;
            yield return Advance(arena, .06f);
            Assert.That(player.currentCast, Is.Null, "Reset must discard queued attack input.");
            Assert.That(player.Position.x - x, Is.EqualTo((arena.Now - at) * 8).Within(.025f));
            player.axis = 0; player.QueueTestInput(dash: true);
            at = arena.Now; x = player.Position.x;
            yield return Advance(arena, .06f);
            Assert.That(player.Dashing, Is.True);
            Assert.That(player.Position.x - x, Is.EqualTo((arena.Now - at) * 10).Within(.025f));
            var active = arena.Movement;
            var invalid = active.ToData(); invalid.dashDuration = -1;
            Assert.That(arena.TryApplyMovementAndReset(invalid, out error), Is.False);
            Assert.That(arena.Movement, Is.SameAs(active));
            Assert.That(player.Dashing, Is.True, "Rejected edit must not reset the scenario.");
            Assert.That(arena.TryApplyMovementAndReset(source.ToData(), out error), Is.True, error);
            Assert.That(player.Dashing, Is.False);
            Assert.That(player.DashRemaining, Is.EqualTo(0));
            Assert.That(arena.Movement.ContentHash, Is.EqualTo(source.ContentHash));
        }

        static IEnumerator Advance(CombatArena arena, float seconds)
        {
            float target = arena.Now + seconds, limit = Time.realtimeSinceStartup + 5;
            while (arena.Now < target && Time.realtimeSinceStartup < limit) yield return new WaitForFixedUpdate();
            Assert.That(arena.Now, Is.GreaterThanOrEqualTo(target));
        }
    }
}
