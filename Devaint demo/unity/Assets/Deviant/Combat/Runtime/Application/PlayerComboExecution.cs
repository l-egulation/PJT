using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Embedded privately by one player. This owns only the committed basic
    // index and grace deadline; admission, reservations and callbacks stay out.
    public struct PlayerComboExecution
    {
        int basicIndex;
        float lateUntil;
        public static PlayerComboExecution Create() => new PlayerComboExecution { basicIndex = -1 };
        public int BasicIndex => basicIndex;
        public PlayerComboSnapshot Read() => new PlayerComboSnapshot(basicIndex, lateUntil);
        public int SelectStart<T>(ref T host) where T : struct, IPlayerComboHost
            => host.Now <= lateUntil && basicIndex >= 0 ? SelectNext(ref host) : 0;
        public int SelectNext<T>(ref T host) where T : struct, IPlayerComboHost
            => (basicIndex + 1) % host.BasicCount;
        public void BeginBasic(int index) => basicIndex = index;
        public void OpenWindow(float completedAt) => lateUntil = completedAt + .3f;
        public void Clear() { basicIndex = -1; lateUntil = -1; }
    }
}
