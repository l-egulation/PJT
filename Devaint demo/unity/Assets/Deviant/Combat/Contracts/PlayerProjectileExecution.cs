namespace Deviant.Combat.Contracts
{
    // Geometry and velocity stay native. Each callback is synchronous, so a
    // contact can claim the same execution before Receive or removal re-enters.
    public interface IPlayerProjectileExecutionHost
    {
        void SweepByTime(float duration);
        void SweepByDistance(float distance);
        void SweepCurrent();
        void Rotate(float duration);
        void Remove();
    }
}
