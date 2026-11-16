namespace Deviant.Combat
{
    // Playtest comparison of how long village monsters hold their wind-up pose, cycled with F12 in the Editor and
    // Development Builds only. Index 0 follows each monster's data (prepareHoldExtra); the others override it.
    public static class PrepareHoldCompare
    {
        static readonly float[] Seconds = { 0, 0, .2f, .4f, .6f };
        static readonly string[] Labels = { "데이터 기본값", "+0초 (원래 길이)", "+0.2초", "+0.4초", "+0.6초" };
        public static int Index { get; private set; }
        public static void Set(int index) { Index = index < 0 || index >= Seconds.Length ? 0 : index; }
        public static void Cycle() { Set((Index + 1) % Seconds.Length); }
        public static string Name => Labels[Index];
        public static float Extra(float data) => Index == 0 ? data : Seconds[Index];
    }
}
