using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    public partial class CombatPlayer
    {
        CombatCommandKey bufferedJump, dashCommand;
        Guid dashExecution;
        bool resettingCommands;
        void ClearBufferedJump(CombatCommandReason reason)
        {
            ClearWallJumpBuffer(reason);
            var key = bufferedJump; bufferedJump = default;
            if (arena) arena.RecordCommand(key, CombatCommandState.Rejected, reason);
        }
        void ExpireBufferedJump()
        {
            if (bufferedWallJump.IsValid && arena.Now > wallBufferUntil) ClearWallJumpBuffer(CombatCommandReason.BufferExpired);
            if (bufferedJump.IsValid && jumps.BufferedUntil < arena.Now) ClearBufferedJump(CombatCommandReason.BufferExpired);
        }
        void CompleteDashCommand()
        {
            if (!dashCommand.IsValid || Dashing) return;
            var key = dashCommand; dashCommand = default;
            arena.RecordCommand(key, CombatCommandState.Completed, executionId: dashExecution);
        }
        void CancelDashCommand(CombatCommandReason reason)
        {
            var key = dashCommand; dashCommand = default;
            if (arena) arena.RecordCommand(key, CombatCommandState.Cancelled, reason, dashExecution);
        }
    }
}
