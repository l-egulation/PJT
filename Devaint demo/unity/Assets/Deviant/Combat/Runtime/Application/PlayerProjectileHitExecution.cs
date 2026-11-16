using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class PlayerProjectileHitExecution
    {
        public static bool Resolve<T>(float contactAge, ref T host) where T : struct, IPlayerProjectileHitHost
        {
            if (!host.ValidContact) return false;
            if (host.BlastRadius > 0) { host.Detonate(); return true; }
            if (host.CaptureHitRegion())
            {
                if (!host.TryClaimActor(contactAge)) return false;
                bool accepted = host.Receive();
                if (accepted && host.ReturnDelay > 0 && !host.TargetDead && host.CaptureReturnPlayer())
                    host.VisitTarget();
                if (host.Pierce) return false;
            }
            host.Release(); return true;
        }

        public static void Detonate<T>(ref T host) where T : struct, IPlayerProjectileBlastHost
        {
            if (host.Removed || host.BlastRadius <= 0) return;
            // Release owns the terminal claim before overlap queries or Receive
            // can re-enter. Entered blasts keep their original callback prefix.
            host.Release();
            host.BeginCandidates();
            while (host.NextCandidate())
            {
                if (!host.RegionExists || !host.ActorExists || host.ActorIsOwner || host.ActorIsPlayer ||
                    host.ActorIsDecoy || host.ActorDead || !host.TryClaimActor()) continue;
                // The actor is claimed even when its first collider is rejected
                // by geometry. Later colliders do not retry that actor.
                host.CaptureContact();
                if (host.ContactDistance > host.BlastRadius + .0001f || host.ContactOccluded) continue;
                host.Receive();
            }
            host.ShowExplosion();
            host.RecordExplosion();
        }
    }
}
