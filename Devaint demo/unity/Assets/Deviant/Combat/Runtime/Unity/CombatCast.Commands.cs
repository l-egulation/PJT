using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        CombatCommandKey command;
        bool commandEnded;
        internal CombatCommandReason CommandCancellationReason { get; set; } = CombatCommandReason.Interrupted;
        internal void BindCommand(CombatCommandKey origin)
        {
            command = origin; commandEnded = false; CommandCancellationReason = CombatCommandReason.Interrupted;
        }
        internal void StartCommand(CombatCommandKey origin)
        {
            BindCommand(origin);
            if (!hitSourceCaptured)
            {
                hitCommand = origin;
                if (origin.IsValid) hitSource = origin.Actor;
                else arena.TryBindActor(owner, out hitSource);
                hitSourceCaptured = true;
            }
            if (arena) arena.RecordCommand(origin, CombatCommandState.Started, executionId: ExecutionId, action: this);
        }
        internal void EndCommand(CombatCommandState state, CombatCommandReason reason = CombatCommandReason.None, string detail = null)
        {
            if (commandEnded || !command.IsValid) return;
            commandEnded = true;
            if (arena) arena.RecordCommand(command, state, reason, ExecutionId, detail, this);
        }
        void FailCommand()
        {
            var result = ResourceResult;
            EndCommand(CombatCommandState.Failed, result != null && result.Status != ActionResourceStatus.Succeeded
                ? CombatCommandReason.Resources : CombatCommandReason.Activation, result?.Reason);
        }
        void CommandException(System.Exception exception)
            => EndCommand(CombatCommandState.Failed, CombatCommandReason.Exception, exception.Message);
    }
}
