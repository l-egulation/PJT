using System;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class TestEnemy
    {
        long commandSequence;
        CombatCommandKey attackCommand, chaseCommand;
        Guid attackExecution, chaseExecution;
        bool resettingCommands;
        protected CombatAICommandResult SubmitIntent(EnemyIntent intent)
        {
            if (!arena.TryBindActor(this, out var actor)) return new CombatAICommandResult(CombatActionAttempt.Reject(CombatCommandReason.Session));
            var command = new CombatAICommand(actor, checked(commandSequence + 1), intent, arena.Now);
            return arena.Commands.SubmitAIIntent(command);
        }
        protected virtual void BeginAttackExecution() => attack.Begin(arena.Now);
        protected virtual bool HasCommandChaseFloor() => arena.FindFloor(Position + new Vector2(facing, .2f), .5f).HasValue;
        internal CombatAICommandResult ApplyCommand(CombatAICommand command)
        {
            CombatCommandReason rejection = CombatCommandReason.None;
            if (command.Key.Sequence <= commandSequence || command.Key.Sequence == long.MaxValue) rejection = CombatCommandReason.Sequence;
            else if (command.Intent.Kind == EnemyIntentKind.None || float.IsNaN(command.ObservedAt) || float.IsInfinity(command.ObservedAt) || command.ObservedAt != arena.Now) rejection = CombatCommandReason.Metadata;
            if (rejection != CombatCommandReason.None) return RejectCommand(command.Key, rejection);
            commandSequence = command.Key.Sequence;
            try
            {
                var host = new AICommandHost(this);
                var result = CombatAICommandExecution.Apply(command, ref host);
                if (command.Intent.Kind == EnemyIntentKind.Chase)
                { EndChaseCommand(CombatCommandState.Cancelled, CombatCommandReason.Replaced); chaseCommand = command.Key; chaseExecution = result.Attempt.ExecutionId; }
                if (command.Intent.Kind == EnemyIntentKind.Chase) arena.RecordCommand(command.Key, CombatCommandState.Started, executionId: result.Attempt.ExecutionId);
                return result;
            }
            catch (Exception exception)
            {
                if (attackCommand.Equals(command.Key)) EndAttackCommand(CombatCommandState.Failed, CombatCommandReason.Exception, exception.Message);
                else arena.RecordCommand(command.Key, CombatCommandState.Failed, CombatCommandReason.Exception, detail: exception.Message);
                throw;
            }
        }
        CombatAICommandResult RejectCommand(CombatCommandKey command, CombatCommandReason reason)
        {
            arena.RecordCommand(command, CombatCommandState.Rejected, reason);
            return new CombatAICommandResult(CombatActionAttempt.Reject(reason));
        }
        internal void EndAttackCommand(CombatCommandState state, CombatCommandReason reason = CombatCommandReason.None, string detail = null)
        {
            var key = attackCommand; attackCommand = default;
            if (arena) arena.RecordCommand(key, state, reason, attackExecution, detail);
        }
        protected void EndChaseCommand(CombatCommandState state, CombatCommandReason reason = CombatCommandReason.None, string detail = null)
        {
            var key = chaseCommand; chaseCommand = default;
            if (arena) arena.RecordCommand(key, state, reason, chaseExecution, detail);
        }
        void OnDestroy()
        {
            EndAttackCommand(CombatCommandState.Cancelled, CombatCommandReason.ActorUnavailable);
            EndChaseCommand(CombatCommandState.Cancelled, CombatCommandReason.ActorUnavailable);
        }
        readonly struct AICommandHost : ICombatAICommandHost
        {
            readonly TestEnemy enemy;
            public AICommandHost(TestEnemy enemy) { this.enemy = enemy; }
            public Guid BeginAttack(CombatCommandKey command)
            {
                enemy.BeginAttackExecution();
                enemy.EndAttackCommand(CombatCommandState.Cancelled, CombatCommandReason.Replaced);
                enemy.attackCommand = command; enemy.attackExecution = Guid.NewGuid();
                enemy.emittedAttackCommand = command; enemy.emittedAttackSource = command.Actor;
                enemy.arena.RecordCommand(command, CombatCommandState.Started, executionId: enemy.attackExecution);
                return enemy.attackExecution;
            }
            public void SyncFacing(int direction) => enemy.SyncFacing(direction);
            public int Facing => enemy.facing;
            public bool Grounded => enemy.grounded;
            public bool HasChaseFloor() => enemy.HasCommandChaseFloor();
        }
    }
}
