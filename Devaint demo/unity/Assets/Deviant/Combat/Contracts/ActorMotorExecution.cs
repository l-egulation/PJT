namespace Deviant.Combat.Contracts
{
    // Current actor facts and Native operations only. No pose, velocity, contact
    // flag, clock or platform binding is copied into an execution-owned state.
    public interface IActorMotorHost
    {
        float Now { get; }
        void ExpireShield(float now);
        void RefreshPlatforms(float now);
        void PruneUnusedPlatforms();
        ActorBodyFrame BodyFrame { get; }
        IActorSurfaceQuery SurfaceQuery { get; }
        float VelocityY { get; set; }
        bool Grounded { get; set; }
        bool WallHit { set; }
        // Read after the ground probe, including any synchronous query callback.
        ActorMotionInput ReadMotionInput(float now, float step, float desiredX, bool freezeVertical);
        void SetVelocity(float x, float y);
        // Add to the current pose, not the body frame captured before the query.
        void MovePosition(float deltaX, float deltaY);
        void SyncTransforms();
    }
}
