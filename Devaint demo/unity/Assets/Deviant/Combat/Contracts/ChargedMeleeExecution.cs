namespace Deviant.Combat.Contracts
{
    // The execution reads the existing live simulation clock at the same native
    // callback boundaries. Selection, resources, geometry and damage stay native.
    // Entered callbacks may cancel or release the execution without unwinding it.
    public interface IChargedMeleeExecutionHost
    {
        float Now { get; }
        bool SelectTarget();
        bool CommitResources();
        void Activated();
        void ReleaseResources();
        void Failed();
        void Sweep();
        void Complete();
        void Dispose();
    }
}
