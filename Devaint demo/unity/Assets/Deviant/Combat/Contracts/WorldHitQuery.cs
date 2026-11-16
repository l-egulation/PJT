namespace Deviant.Combat.Contracts
{
    public readonly struct WorldHitPoint
    {
        public float X { get; }
        public float Y { get; }
        public WorldHitPoint(float x, float y) { X = x; Y = y; }
    }

    public readonly struct WorldHitBox
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public WorldHitBox(float x, float y, float width, float height)
        { X = x; Y = y; Width = width; Height = height; }
    }

    public readonly struct WorldHitBounds
    {
        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }
        public WorldHitBounds(float minX, float minY, float maxX, float maxY)
        { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }
    }

    // Each candidate and the current best remain Native references. Read live
    // facts only at the same phase as the original box/circle query.
    public interface IWorldNearestTargetHost
    {
        bool Exists { get; }
        bool IsPlayer { get; }
        bool IsDecoy { get; }
        bool Dead { get; }
        WorldHitPoint OwnerCenter { get; }
        WorldHitPoint TargetCenter { get; }
        bool Occluded();
        bool BestExists { get; }
        int ActorId { get; }
        int BestActorId { get; }
        float ApproximationEpsilon { get; }
    }

    public interface IWorldHitVisibilityHost
    {
        bool Occluded(WorldHitPoint origin, WorldHitPoint point);
    }
}
