using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class FieldMouseBrainTests
    {
        FieldMouseDefinition d;
        FieldMouseBrain brain;
        MouseObservation seen;
        [SetUp] public void SetUp()
        {
            d = FieldMouseDefinition.Load(); brain = new FieldMouseBrain(d);
            seen = new MouseObservation { position = Vector2.zero, spawn = Vector2.zero, targetPosition = Vector2.right * 2,
                detected = true, visible = true, pathToTarget = true, pathToLastKnown = true, returnPath = true,
                canAttack = true, insideStartDistance = true };
        }
        void Tick(double now)
        {
            brain.Tick(now, seen, true);
            if (brain.AttackRequested) brain.BeginAttack(now);
        }
        [TestCase(-1)] [TestCase(1)]
        public void TargetJitterAboveOrBelowKeepsDirectionBeforeAnAttack(int facing)
        {
            brain=new FieldMouseBrain(d,EnemyPursuitSettings.Load().targetFacingDeadzone);brain.Look(facing);
            seen.canAttack=false;seen.insideStartDistance=false;
            foreach(float y in new[]{-8f,8f})
            foreach(float pixels in new[]{-4f,-3f,0f,3f,4f})
            {
                seen.targetPosition=new Vector2(pixels/32,y);brain.Tick(0,seen,true);
                Assert.That(brain.Facing,Is.EqualTo(facing));Assert.That(brain.DesiredVelocity,Is.Zero);
            }
            seen.canAttack=true;brain.Tick(.1,seen,true);
            Assert.That(brain.AttackRequested,Is.True);Assert.That(brain.Facing,Is.EqualTo(facing));
            seen.targetPosition=new Vector2(-facing*5f/32,0);brain.Tick(.2,seen,true);
            Assert.That(brain.Facing,Is.EqualTo(-facing));
        }
        [Test] public void AttackObservationDoesNotStartBeforeCommandAdmission()
        {
            brain.Tick(0, seen, true);
            Assert.That(brain.AttackRequested, Is.True);
            Assert.That(brain.Executing, Is.False);
            Assert.That(brain.Execution, Is.Zero);
            Assert.That(double.IsNegativeInfinity(brain.StartedAt), Is.True);
            brain.BeginAttack(0);
            Assert.That(brain.AttackRequested, Is.False);
            Assert.That(brain.Executing, Is.True);
            Assert.That(brain.Execution, Is.EqualTo(1));
        }
        [Test] public void PrepareDirectionAndOriginalIntervalSurviveTargetLoss()
        {
            Tick(0); Assert.That(brain.State, Is.EqualTo(FieldMouseState.Prepare));
            seen.targetPosition = Vector2.left * 20; seen.detected = seen.visible = seen.canAttack = false;
            Tick(.2);
            Assert.That(brain.Facing, Is.EqualTo(1)); Assert.That(brain.Executing, Is.True);
            Assert.That(brain.NextAttackAt, Is.EqualTo(1.2).Within(.000001));
        }
        [Test] public void ActiveWindowIsEndExclusiveAndSurvivesShortSamplingIntervals()
        {
            Tick(0);
            double start = (double)d.prepare, end = start + d.active;
            Assert.That(brain.ActiveDuring(start - .1, start), Is.False);
            Assert.That(brain.ActiveDuring(start - .001, start + .001), Is.True);
            Assert.That(brain.ActiveDuring(end, end + .01), Is.False);
        }
        [Test] public void HitCancellationKeepsSPlusIAndRestartsFullPreparation()
        {
            Tick(0); brain.Interrupt(.15); brain.StaggerUntil = .25;
            Tick(.3); Assert.That(brain.Executing, Is.False);
            Tick(1.201);
            Assert.That(brain.State, Is.EqualTo(FieldMouseState.Prepare));
            Assert.That(brain.StartedAt, Is.EqualTo(1.201)); Assert.That(brain.Execution, Is.EqualTo(2));
        }
        [Test] public void RecoveryHitAndStunDoNotRestartOrPauseDeadlines()
        {
            Tick(0); brain.AdvanceTime(.5);
            double recovery = brain.RecoveryUntil; brain.Interrupt(.6); brain.StunUntil = 1.5;
            Tick(1);
            Assert.That(brain.RecoveryUntil, Is.EqualTo(recovery));
            Assert.That(brain.NextAttackAt, Is.EqualTo(1.2).Within(.000001));
            Assert.That(brain.Executing, Is.False);
            Tick(1.501); Assert.That(brain.State, Is.EqualTo(FieldMouseState.Prepare));
        }
        [Test] public void RootAllowsStationaryAttackButNotPursuit()
        {
            brain.RootUntil = 2; seen.canAttack = false; seen.insideStartDistance = false;
            Tick(0); Assert.That(brain.DesiredVelocity, Is.Zero);
            seen.canAttack = true; Tick(.1); Assert.That(brain.Executing, Is.True);
        }
        [Test] public void VisiblePathFailureDoesNotRestartWaitAndRecoveryOfPathEndsWait()
        {
            seen.canAttack = false; seen.pathToTarget = false;
            Tick(0); Tick(1);
            Assert.That(brain.WaitStartedAt, Is.Zero);
            seen.pathToTarget = true; seen.insideStartDistance = false; Tick(1.2);
            Assert.That(brain.State, Is.EqualTo(FieldMouseState.Chase)); Assert.That(double.IsNaN(brain.WaitStartedAt), Is.True);
        }
        [Test] public void WaitReasonChangeDoesNotExtendOnePointFiveSeconds()
        {
            seen.canAttack = false; seen.pathToTarget = false; Tick(0);
            seen.visible = seen.detected = false; seen.pathToLastKnown = false; Tick(1);
            Assert.That(brain.WaitStartedAt, Is.Zero);
            Tick(1.501); Assert.That(brain.State, Is.EqualTo(FieldMouseState.Return));
        }
        [Test] public void LeashAttackInterruptionKeepsRecoveryOnceAndReturnCommitment()
        {
            Tick(0); brain.Interrupt(.2); brain.LeashReached(.2); brain.LeashReached(.2);
            Assert.That(brain.RecoveryUntil, Is.EqualTo(.2 + d.recovery).Within(.000001));
            Assert.That(brain.ReturnPending, Is.True);
            seen.position = Vector2.right * 12; Tick(.4);
            Assert.That(brain.State, Is.EqualTo(FieldMouseState.Recovery));
            seen.canReengage = false; Tick(.7);
            Assert.That(brain.State, Is.EqualTo(FieldMouseState.Return));
            Assert.That(brain.NextAttackAt, Is.EqualTo(1.2).Within(.000001));
        }
        [Test] public void ReturningToSpawnDoesNotRestartAnUnreachableStationaryTarget()
        {
            seen.canAttack=false;seen.pathToTarget=false;seen.canReengage=false;
            Tick(0); Tick(1.501);
            Assert.That(brain.State,Is.EqualTo(FieldMouseState.Return));
            seen.atSpawn=true;Tick(1.6);
            Assert.That(brain.State,Is.EqualTo(FieldMouseState.Idle));
            Tick(1.7);
            Assert.That(brain.State,Is.EqualTo(FieldMouseState.Idle),"Returning must not reset the reachability requirement and restart the same failed pursuit.");
            seen.pathToTarget=seen.canReengage=true;Tick(1.8);
            Assert.That(brain.State,Is.EqualTo(FieldMouseState.Chase),"A reachable target must still restart pursuit without an added cooldown.");
        }
        [Test] public void CooldownStopsApproachInsideDistanceAndDoesNotBlockTurning()
        {
            Tick(0); brain.Interrupt(.2); Tick(.5);
            Assert.That(brain.DesiredVelocity, Is.Zero);
            seen.targetPosition = Vector2.left * 2; Tick(.6);
            Assert.That(brain.Facing, Is.EqualTo(-1));
        }
    }

}
