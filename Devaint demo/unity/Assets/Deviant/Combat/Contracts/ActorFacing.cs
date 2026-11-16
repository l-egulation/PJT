namespace Deviant.Combat.Contracts
{
    // The actor remains the sole owner of its live pose, facing and Native objects.
    public interface IActorFacingHost
    {
        int Facing { get; set; }
        float OffsetX { get; }
        float OffsetY { get; }
        void ReadPosition(out float x, out float y);
        ActorBodyFrame BodyFrame { get; }
        IActorPlacementQuery SurfaceQuery { get; }
        void SetPosition(float x, float y);
        bool HasBody { get; }
        void SetBodyOffset(float x, float y);
        bool HasHurt { get; }
        void SetHurtOffset(float x, float y);
        bool HasVisual { get; }
        void SetVisualFlip(bool flip);
        bool IsPlayer { get; }
        void SetVisualPosition(float x, float y);
    }
}
