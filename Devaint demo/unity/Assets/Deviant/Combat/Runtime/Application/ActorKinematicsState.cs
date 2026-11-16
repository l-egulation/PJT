using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public sealed class ActorKinematicsState : IActorKinematics
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float VelocityX { get; private set; }
        public float VelocityY { get; private set; }
        public int Facing { get; private set; } = 1;
        public bool Grounded { get; private set; }
        public bool WallHit { get; private set; }
        public ActorKinematicsSnapshot Read() => new ActorKinematicsSnapshot(X, Y, VelocityX, VelocityY, Facing, Grounded, WallHit);
        public void WritePosition(float x, float y) { X = x; Y = y; }
        public void WriteVelocity(float x, float y) { VelocityX = x; VelocityY = y; }
        public void WriteVelocityX(float value) => VelocityX = value;
        public void WriteVelocityY(float value) => VelocityY = value;
        // Facing normalization belongs to SyncFacing; direct caller writes retain
        // their original value and timing, including reentrant native queries.
        public void WriteFacing(int value) => Facing = value;
        public void WriteGrounded(bool value) => Grounded = value;
        public void WriteWallHit(bool value) => WallHit = value;
    }
}
