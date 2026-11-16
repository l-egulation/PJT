namespace Deviant.Combat.Contracts
{
    public readonly struct MissileSnapshot
    {
        public bool Ended { get; }
        public bool EndPending { get; }
        public bool RecoilStopped { get; }
        public float LaunchedAt { get; }
        public float Travel { get; }
        public float Move { get; }
        public float RecoilStartedAt { get; }
        public float RecoilDuration { get; }
        public MissileSnapshot(bool ended, bool endPending, bool recoilStopped, float launchedAt, float travel, float move, float recoilStartedAt, float recoilDuration)
        {
            Ended = ended; EndPending = endPending; RecoilStopped = recoilStopped;
            LaunchedAt = launchedAt; Travel = travel; Move = move;
            RecoilStartedAt = recoilStartedAt; RecoilDuration = recoilDuration;
        }
    }

    // These facts remain live in the cast. No second activation, cancellation,
    // actor-life, motion-end, simulation clock or resource owner is introduced.
    public interface IMissileExecutionHost
    {
        bool Activated { get; }
        bool Cancelled { get; }
        bool MotionDone { get; }
        bool OwnerAlive { get; }
        float Now { get; }
        float StartedAt { get; }
        float StepDuration { get; }
        float MovedX { get; }
        bool WallHit { get; }
        bool Grounded { get; }
        void UpdateVisual();
        void Explode();
        void StartHop(float verticalSpeed);
        void SetMotionEnd(float elapsed);
        void RecordEnd();
    }
}
