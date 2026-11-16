namespace Deviant.Combat.Contracts
{
    public readonly struct PlayerJumpSnapshot
    {
        public int Used { get; }
        public float BufferedUntil { get; }
        public PlayerJumpSnapshot(int used, float bufferedUntil)
        { Used = used; BufferedUntil = bufferedUntil; }
    }
}
