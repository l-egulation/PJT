namespace Deviant.Combat.Contracts
{
    // Observation of an actual compiled melee sweep, not accepted damage. Consumers
    // cannot turn a decorative contact into a combat hit or consume a projectile.
    public readonly struct EnvironmentAttackSample
    {
        public readonly CombatHitOrigin Origin;
        public readonly float X, Y, Width, Height, OriginX, OriginY;
        public readonly int Direction;
        public EnvironmentAttackSample(CombatHitOrigin origin, float x, float y, float width, float height, float originX, float originY, int direction)
        { Origin = origin; X = x; Y = y; Width = width; Height = height; OriginX = originX; OriginY = originY; Direction = direction; }
    }
    public readonly struct EnvironmentBodySweep
    {
        public readonly int ActorId;
        public readonly float FromX, FromY, ToX, ToY, Width, Height, Delta;
        public EnvironmentBodySweep(int actorId, float fromX, float fromY, float toX, float toY, float width, float height, float delta)
        { ActorId = actorId; FromX = fromX; FromY = fromY; ToX = toX; ToY = toY; Width = width; Height = height; Delta = delta; }
    }
}
