namespace Deviant.Combat.Contracts
{
    // Absolute deadlines use the caller's simulation clock. This detached value
    // does not advance or own dash resources, movement, or jump usage.
    public readonly struct PlayerDashSnapshot
    {
        public float StartedAt { get; }
        public float EndsAt { get; }
        public float ReadyAt { get; }
        public int Facing { get; }
        public float JumpCarryUntil { get; }
        public int JumpCarryFacing { get; }
        public PlayerDashSnapshot(float startedAt, float endsAt, float readyAt, int facing, float jumpCarryUntil, int jumpCarryFacing)
        {
            StartedAt = startedAt; EndsAt = endsAt; ReadyAt = readyAt; Facing = facing;
            JumpCarryUntil = jumpCarryUntil; JumpCarryFacing = jumpCarryFacing;
        }
    }
}
