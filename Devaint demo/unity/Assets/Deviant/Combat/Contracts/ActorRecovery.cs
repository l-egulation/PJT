namespace Deviant.Combat.Contracts
{
    public readonly struct SafeGroundSample
    {
        public float Time { get; }
        public float X { get; }
        public float Y { get; }
        public int Facing { get; }
        public SafeGroundSample(float time, float x, float y, int facing)
        { Time = time; X = x; Y = y; Facing = facing; }
    }
    // Current terrain and hazards are queried without moving the live actor.
    public interface IActorRecoveryQuery
    {
        bool IsSafe(ActorPose pose);
    }
}
