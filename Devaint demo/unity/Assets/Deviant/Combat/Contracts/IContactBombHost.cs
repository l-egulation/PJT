namespace Deviant.Combat.Contracts
{
    // All observations stay live on the existing Native owner. Stopped is the
    // same terminal claim used by timed bombs, contacts, release and teardown.
    public interface IContactBombHost
    {
        float Now { get; }
        bool Stopped { get; }
        bool ExplodeOnExpiry { get; }
        float Gravity { get; }
        float VelocityX { get; }
        float VelocityY { get; }
        float PositionX { get; }
        float PositionY { get; }
        void SetVelocityY(float velocity);
        void SweepTo(float x, float y);
        void Explode();
        void Remove();
    }
}
