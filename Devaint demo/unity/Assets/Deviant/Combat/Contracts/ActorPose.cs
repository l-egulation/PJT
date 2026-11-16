using System;

namespace Deviant.Combat.Contracts
{
    public readonly struct ActorPose
    {
        public float X { get; }
        public float Y { get; }
        public int Facing { get; }
        public ActorPose(float x, float y, int facing)
        {
            if (!ActorBodyFrame.Finite(x) || !ActorBodyFrame.Finite(y) || facing != -1 && facing != 1) throw new ArgumentException("A pose requires finite coordinates and a signed facing.");
            X = x; Y = y; Facing = facing;
        }
    }
}
