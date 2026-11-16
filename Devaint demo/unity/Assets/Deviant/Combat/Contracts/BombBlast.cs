namespace Deviant.Combat.Contracts
{
    // One already-stopped blast owns this host. Native overlap results, the
    // fresh ActorId set, current HurtRegion/contact and fragment stay here;
    // no candidate facts or live bomb values are captured by the execution.
    public interface IBombBlastHost
    {
        void BeginBlast();
        int QueryCandidates();
        void SelectCandidate(int index);
        bool RegionExists { get; }
        bool TargetExists { get; }
        bool TargetIsOwner { get; }
        bool TargetIsPlayer { get; }
        bool TargetIsDecoy { get; }
        bool TargetDead { get; }
        bool TryClaimTarget();
        void FindClosestPoint();
        float ContactDistance { get; }
        float BlastRadius { get; }
        bool Occluded();
        void ReceiveBasic();
        void BombExploded();
        int FragmentCount { get; }
        float FragmentAngleAt(int index);
        void SpawnFragment(float angle);
        void SweepFragmentAtCurrentPosition();
        void Remove();
    }
}
