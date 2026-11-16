namespace Deviant.Combat.Contracts
{
    // Called at the common before-movement / after-movement boundaries.
    // The host owns stat capture and Unity queries; the runner owns timing,
    // lifecycle and target claims. Callbacks may cancel, but must not advance,
    // this runner.
    public interface IMeleeActionHost
    {
        bool TryActivate();
        void Sweep();
        void Complete();
    }
}
