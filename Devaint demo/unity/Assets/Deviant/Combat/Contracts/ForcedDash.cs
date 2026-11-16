namespace Deviant.Combat.Contracts
{
    // Only translation completion and the wall's direct-hit cutoff are owned
    // here. Activation, cancellation and action completion still belong to Cast.
    public readonly struct ForcedDashSnapshot
    {
        public bool MovementDone { get; }
        public float WallStopTime { get; }
        public ForcedDashSnapshot(bool movementDone, float wallStopTime)
        { MovementDone = movementDone; WallStopTime = wallStopTime; }
    }

    // Native contacts have already been resolved. The adapter applies an end
    // override before zeroing vertical speed under the action's existing policy.
    public readonly struct ForcedDashMovementEnd
    {
        public bool Ended { get; }
        public bool OverridesMotionEnd { get; }
        public float MotionEnd { get; }
        public ForcedDashMovementEnd(bool ended, bool overridesMotionEnd, float motionEnd)
        { Ended = ended; OverridesMotionEnd = overridesMotionEnd; MotionEnd = motionEnd; }
    }
}
