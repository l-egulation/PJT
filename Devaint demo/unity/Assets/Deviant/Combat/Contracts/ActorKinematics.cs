namespace Deviant.Combat.Contracts
{
    public readonly struct ActorKinematicsSnapshot
    {
        public float X { get; }
        public float Y { get; }
        public float VelocityX { get; }
        public float VelocityY { get; }
        public int Facing { get; }
        public bool Grounded { get; }
        public bool WallHit { get; }
        public ActorKinematicsSnapshot(float x, float y, float velocityX, float velocityY, int facing, bool grounded, bool wallHit)
        { X = x; Y = y; VelocityX = velocityX; VelocityY = velocityY; Facing = facing; Grounded = grounded; WallHit = wallHit; }
    }
    // One actor's live 2D motion facts. Geometry and native view depth are not state.
    public interface IActorKinematics
    {
        float X { get; }
        float Y { get; }
        float VelocityX { get; }
        float VelocityY { get; }
        int Facing { get; }
        bool Grounded { get; }
        bool WallHit { get; }
        ActorKinematicsSnapshot Read();
        void WritePosition(float x, float y);
        void WriteVelocity(float x, float y);
        void WriteVelocityX(float value);
        void WriteVelocityY(float value);
        void WriteFacing(int value);
        void WriteGrounded(bool value);
        void WriteWallHit(bool value);
    }
}
