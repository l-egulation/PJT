namespace Deviant.Combat.Contracts
{
    public readonly struct PlayerComboSnapshot
    {
        public int BasicIndex { get; }
        public float LateUntil { get; }
        public PlayerComboSnapshot(int basicIndex, float lateUntil)
        { BasicIndex = basicIndex; LateUntil = lateUntil; }
    }

    // Selection reads the caller's current clock and form count, never a form
    // reference or a cached equipment/definition snapshot.
    public interface IPlayerComboHost
    {
        float Now { get; }
        int BasicCount { get; }
    }
}
