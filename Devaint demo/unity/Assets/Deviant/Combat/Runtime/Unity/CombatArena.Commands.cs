using System;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        ArenaCombatCommands commands;
        public ICombatCommands Commands => commands ?? (commands = new ArenaCombatCommands(this));
        internal void RecordCommand(CombatCommandKey key, CombatCommandState state, CombatCommandReason reason = CombatCommandReason.None,
            Guid executionId = default, string detail = null, CombatCast action = null)
        {
            if (!key.IsValid) return;
            if (commands == null) commands = new ArenaCombatCommands(this);
            commands.Record(key, state, reason, executionId, detail, action);
        }
        void ReleaseCommands() => commands?.Dispose();

        sealed class ArenaCombatCommands : ICombatCommands, IDisposable
        {
            CombatArena arena;
            readonly CombatCommandOutcomes outcomes = new CombatCommandOutcomes();
            public ArenaCombatCommands(CombatArena arena) { this.arena = arena; }
            public CombatInputAdmission SubmitPlayerInput(CombatInputSample sample)
            {
                var result = !arena || !arena.Player || arena.CompositionProfile == CombatCompositionProfile.Unconfigured
                    ? new CombatInputAdmission(sample, CombatInputAdmissionStatus.RejectedSession)
                    : arena.Player.AdmitInput(sample);
                if (result.Status != CombatInputAdmissionStatus.Admitted)
                    Record(CombatCommandKey.For(sample), CombatCommandState.Rejected, AdmissionReason(result), default, null);
                return result;
            }
            public CombatAICommandResult SubmitAIIntent(CombatAICommand command)
            {
                var key = command.Key;
                CombatCommandReason rejection;
                if (!arena || !arena.ReadModel.TryGetSession(out var session) || !session.Equals(key.Actor.Session)) rejection = CombatCommandReason.Session;
                else if (!(arena.FindReadActor(key.Actor.ActorId) is TestEnemy enemy)) rejection = CombatCommandReason.ActorUnavailable;
                else return enemy.ApplyCommand(command);
                Record(key, CombatCommandState.Rejected, rejection, default, null);
                return new CombatAICommandResult(CombatActionAttempt.Reject(rejection));
            }
            public bool TryReadOutcomes(CombatOutcomeCursor cursor, CombatCommandOutcome[] destination, out CombatOutcomePage page)
            {
                page = default;
                return arena && arena.ReadModel.TryGetSession(out var session) && outcomes.TryRead(session, cursor, destination, out page);
            }
            public void Record(CombatCommandKey key, CombatCommandState state, CombatCommandReason reason, Guid executionId, string detail, CombatCast action = null)
            {
                if (!arena || !key.IsValid) return;
                ActorReadSnapshot snapshot = default;
                // Terminal hooks retain their admitted origin after reset has
                // invalidated public reads. Never rebind a rejected foreign key.
                if (state != CombatCommandState.Rejected && arena.FindReadActor(key.Actor.ActorId) is CombatActor actor)
                    snapshot = arena.CaptureActor(key.Actor, actor, action);
                else arena.ReadModel.TryReadActor(key.Actor, out snapshot);
                outcomes.Add(arena.SimulationTickCount, arena.Now, key, state, reason, executionId, snapshot, detail);
            }
            static CombatCommandReason AdmissionReason(CombatInputAdmission admission)
            {
                switch (admission.Status)
                {
                    case CombatInputAdmissionStatus.RejectedSession: return CombatCommandReason.Session;
                    case CombatInputAdmissionStatus.RejectedSequence: return CombatCommandReason.Sequence;
                    case CombatInputAdmissionStatus.RejectedMetadata: return CombatCommandReason.Metadata;
                    case CombatInputAdmissionStatus.RejectedCapacity: return CombatCommandReason.Capacity;
                    default: return CombatCommandReasons.For(admission.DiscardReason);
                }
            }
            public void Dispose() { arena = null; }
        }
    }
}
