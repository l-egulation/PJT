using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class CombatAICommandsIntegrationTests
    {
        [UnityTest] public IEnumerator RealAIUsesActorLocalCommandKeysAndActualAttackAndMotorBoundaries()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var enemy = arena.enemies[0]; var player = arena.Player;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); foreach (var other in arena.enemies) { other.aggressive = false; other.Position = new Vector2(35, 1000); }
                enemy.aggressive = true; enemy.profile = 0; enemy.Position = new Vector2(5, 1000); enemy.velocity = Vector2.zero; enemy.grounded = false;
                player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero;
                Assert.That(arena.ReadModel.TryGetSession(out var session), Is.True); var cursor = new CombatOutcomeCursor(session);
                Poll(arena.Commands, ref cursor); long playerSequence = player.NextInputSequence;
                enemy.Step(.02f); var chase = Poll(arena.Commands, ref cursor);
                var chaseStart = chase.Single(o => o.Command.Kind == CombatCommandKind.EnemyChase && o.State == CombatCommandState.Started);
                var chaseEnd = chase.Single(o => o.Command.Equals(chaseStart.Command) && o.State == CombatCommandState.Completed);
                Assert.That(chaseStart.Command.Actor.ActorId, Is.EqualTo(enemy.ActorId)); Assert.That(chaseStart.Command.Actor.Session.Equals(session), Is.True);
                Assert.That(chaseStart.ExecutionId, Is.EqualTo(chaseEnd.ExecutionId)); Assert.That(chaseStart.ExecutionId, Is.Not.EqualTo(Guid.Empty));
                Assert.That(chaseEnd.Snapshot.Kinematics.X, Is.LessThan(chaseStart.Snapshot.Kinematics.X));
                Assert.That(player.NextInputSequence, Is.EqualTo(playerSequence), "AI must not pass through player S/D input admission.");
                Assert.That(arena.TryBindActor(enemy, out var handle), Is.True);
                var duplicate = new CombatAICommand(handle, chaseStart.Command.Sequence, new EnemyIntent(EnemyIntentKind.Chase, -1, 2.5f), arena.Now);
                float x = enemy.Position.x;
                Assert.That(arena.Commands.SubmitAIIntent(duplicate).Attempt.Reason, Is.EqualTo(CombatCommandReason.Sequence)); Assert.That(enemy.Position.x, Is.EqualTo(x));
                Assert.That(Poll(arena.Commands, ref cursor).Single().State, Is.EqualTo(CombatCommandState.Rejected));

                enemy.aggressive = false; for (int i = 0; i < 30; i++) driver.Step(.02f);
                enemy.aggressive = true; enemy.Position = new Vector2(1.5f, 1000); enemy.velocity = Vector2.zero; player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero;
                enemy.Step(.02f); var attackStart = Poll(arena.Commands, ref cursor).Single(o => o.Command.Kind == CombatCommandKind.EnemyAttack && o.State == CombatCommandState.Started);
                Assert.That(attackStart.Command.Sequence, Is.GreaterThan(chaseStart.Command.Sequence)); Assert.That(enemy.Telegraph, Is.GreaterThanOrEqualTo(0));
                var all = new List<CombatCommandOutcome>();
                for (int i = 0; i < 80; i++) { driver.Step(.02f); all.AddRange(Poll(arena.Commands, ref cursor)); }
                var completion = all.Single(o => o.Command.Equals(attackStart.Command) && o.State == CombatCommandState.Completed);
                Assert.That(completion.ExecutionId, Is.EqualTo(attackStart.ExecutionId));

                // Keep the old command key across ordinary reset but require the
                // new session token to submit/read; per-actor sequence never resets.
                var oldHandle = handle; var oldCursor = cursor; arena.ResetTest();
                Assert.That(arena.Commands.TryReadOutcomes(oldCursor, new CombatCommandOutcome[8], out _), Is.False);
                Assert.That(arena.TryBindActor(enemy, out handle), Is.True); cursor = new CombatOutcomeCursor(handle.Session, oldCursor.AfterEventSequence); Poll(arena.Commands, ref cursor);
                var stale = new CombatAICommand(oldHandle, attackStart.Command.Sequence + 100, new EnemyIntent(EnemyIntentKind.BeginAttack, 1, 0), arena.Now);
                Assert.That(arena.Commands.SubmitAIIntent(stale).Attempt.Reason, Is.EqualTo(CombatCommandReason.Session)); Poll(arena.Commands, ref cursor);
                var explicitAttack = new CombatAICommand(handle, attackStart.Command.Sequence + 100, new EnemyIntent(EnemyIntentKind.BeginAttack, 1, 0), arena.Now);
                var result = arena.Commands.SubmitAIIntent(explicitAttack); Assert.That(result.Attempt.Started, Is.True);
                var explicitStart = Poll(arena.Commands, ref cursor).Single(o => o.State == CombatCommandState.Started); Assert.That(explicitStart.Command.Equals(explicitAttack.Key), Is.True);
                enemy.CancelAttack(); var cancelled = Poll(arena.Commands, ref cursor).Single(o => o.State == CombatCommandState.Cancelled);
                Assert.That(cancelled.Command.Equals(explicitAttack.Key), Is.True); Assert.That(cancelled.ExecutionId, Is.EqualTo(explicitStart.ExecutionId));
                enemy.CancelAttack(); Assert.That(Poll(arena.Commands, ref cursor), Is.Empty);
                arena.enemies.Remove(enemy);
                try
                {
                    var missing = new CombatAICommand(handle, explicitAttack.Key.Sequence + 1, new EnemyIntent(EnemyIntentKind.Chase, 1, 2.5f), arena.Now);
                    Assert.That(arena.Commands.SubmitAIIntent(missing).Attempt.Reason, Is.EqualTo(CombatCommandReason.ActorUnavailable));
                }
                finally { arena.enemies.Add(enemy); }
            }
        }
        static List<CombatCommandOutcome> Poll(ICombatCommands commands, ref CombatOutcomeCursor cursor)
        {
            var result = new List<CombatCommandOutcome>(); var values = new CombatCommandOutcome[128];
            Assert.That(commands.TryReadOutcomes(cursor, values, out var page), Is.True); Assert.That(page.DroppedBefore, Is.Zero); cursor = page.Next;
            for (int i = 0; i < page.Count; i++) result.Add(values[i]);
            return result;
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
