namespace Deviant.Combat.Contracts
{
    // One entered axis sweep. Native retains its delta, ordered cast results and
    // current collider/bounds; position and radius remain live scalar reads.
    public interface ITimedBombSweepHost
    {
        bool Stopped { get; }
        bool Landed { get; }
        void SnapshotDelta();
        float HorizontalLength { get; }
        float VerticalLength { get; }
        float DeltaX { get; }
        float DeltaY { get; }
        int QueryHorizontal(int direction, float length);
        int QueryVertical(int direction, float length);
        bool FindSurface(int index);
        bool SurfaceIsOneWay { get; }
        void SnapshotBounds();
        float PositionX { get; }
        float PositionY { get; }
        float Radius { get; }
        float BoundsMinX { get; }
        float BoundsMaxX { get; }
        float BoundsMinY { get; }
        float BoundsMaxY { get; }
        float CastDistance { get; }
        void ApplyHorizontal(int direction, float distance);
        void ClearHorizontalVelocity();
        void ApplyVertical(int direction, float distance);
        void ClearVerticalVelocity();
        void Land(float duration, float contactDistance, float sweepLength);
        void ClearVelocity();
        void RecordLanding();
    }
}
