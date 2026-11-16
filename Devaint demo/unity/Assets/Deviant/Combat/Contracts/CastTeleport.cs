namespace Deviant.Combat.Contracts
{
    // BeforeMovement admits the active, non-cancelled, unfinished cast. Target
    // identity and all position/facing/ClampMagnitude/Native sweep reads stay
    // with this invocation's Unity host, not another execution-state snapshot.
    public interface ICastTeleportHost
    {
        float Elapsed { get; }
        bool MarkedTargetAlive { get; }
        bool TargetHit { get; }
        void FaceMarkedTarget();
        void CaptureStrikeDestination();
        void TeleportToStrikeDestination();
        void CapturePreviousOwner();
        void RecordStrike();
        void TeleportToMarkedTarget();
        void TeleportForward();
    }
}
