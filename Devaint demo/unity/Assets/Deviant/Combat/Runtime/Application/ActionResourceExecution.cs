using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // One execution, one activation result. No global receipt ledger or second
    // cooldown state; the source owns the actual weapon resource.
    public sealed class ActionResourceExecution : IActionResources
    {
        readonly IActionResourceSource source;
        bool closed, busy;
        public ActionResourceKey Key { get; }
        public bool IsActive => !closed;
        public ActionResourceResult Current { get; private set; }
        public ActionResourceExecution(ActionResourceKey key, IActionResourceSource source)
        {
            if (key.ExecutionId == Guid.Empty || key.WeaponId == Guid.Empty) throw new ArgumentException("A live action and weapon key are required.");
            Key = key; this.source = source ?? throw new ArgumentNullException(nameof(source));
        }
        public ActionResourceResult TryActivate(Guid expectedExecution, float now)
        {
            if (closed) return new ActionResourceResult(ActionResourceStatus.Cancelled, reason: "Action resource execution ended.");
            if (busy || expectedExecution != Key.ExecutionId || float.IsNaN(now) || float.IsInfinity(now) || now < 0)
                return new ActionResourceResult(ActionResourceStatus.Rejected, reason: "Stale, reentrant or invalid action resource request.");
            if (Current != null) return Current;
            busy = true;
            try
            {
                var result = source.TryActivate(Key, now) ?? throw new InvalidOperationException("Action resource source returned no result.");
                if (result.Status == ActionResourceStatus.Succeeded && (!result.Snapshot.Key.Equals(Key) || result.Snapshot.ActivatedAt != now))
                    throw new InvalidOperationException("Action resource snapshot does not match its action/weapon/activation time.");
                return Current = result;
            }
            catch (Exception exception)
            {
                return Current = new ActionResourceResult(ActionResourceStatus.Failed, reason: string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message);
            }
            finally { busy = false; }
        }
        public void Dispose()
        {
            if (closed) return;
            if (busy) throw new InvalidOperationException("Resource callbacks cannot dispose their activation execution.");
            closed = true;
            // Current remains a completion fact. Returning this scope never
            // refunds a committed charge or invalidates independent effects.
        }
    }
}
