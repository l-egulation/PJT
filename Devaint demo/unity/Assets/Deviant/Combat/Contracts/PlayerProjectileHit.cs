namespace Deviant.Combat.Contracts
{
    // Regions and return-player references are captured at the original native
    // lookup points. Their actor, owner and definition facts remain live.
    public interface IPlayerProjectileHitHost
    {
        bool ValidContact { get; }
        float BlastRadius { get; }
        void Detonate();
        bool CaptureHitRegion();
        bool TryClaimActor(float contactAge);
        bool Receive();
        float ReturnDelay { get; }
        bool TargetDead { get; }
        bool CaptureReturnPlayer();
        void VisitTarget();
        bool Pierce { get; }
        void Release();
    }

    // Each invocation owns its native overlap array, actor-ID set and current
    // closest point. No actor positions or eligibility are snapshotted.
    public interface IPlayerProjectileBlastHost
    {
        bool Removed { get; }
        float BlastRadius { get; }
        void Release();
        void BeginCandidates();
        bool NextCandidate();
        bool RegionExists { get; }
        bool ActorExists { get; }
        bool ActorIsOwner { get; }
        bool ActorIsPlayer { get; }
        bool ActorIsDecoy { get; }
        bool ActorDead { get; }
        bool TryClaimActor();
        void CaptureContact();
        float ContactDistance { get; }
        bool ContactOccluded { get; }
        void Receive();
        void ShowExplosion();
        void RecordExplosion();
    }
}
