namespace Deviant.Combat.Contracts
{
    // One invocation owns the native cast/list enumerator and fresh ActorId set.
    // Vector2 geometry, current actor references and presentation stay native.
    public interface ILaserHitHost
    {
        float MuzzleGap { get; }
        float ProjectileDistance { get; }
        bool IgnoresTerrain { get; }
        int QueryCast(float length);
        bool FindSurface(int index);
        bool SurfaceIsOneWay { get; }
        float SurfaceDistance { get; }
        void BeginTargets();
        void QueryTargets(float length, float width);
        bool NextTarget();
        bool TargetIsOwner { get; }
        bool TargetIsPlayer { get; }
        bool TargetIsDecoy { get; }
        bool TargetDead { get; }
        bool TryClaimTarget();
        bool TargetOccluded();
        void ReceiveBasic();
        void ShowVisual(float visible);
        int ClaimedTargetCount { get; }
        void Record(int targetCount);
    }
}
