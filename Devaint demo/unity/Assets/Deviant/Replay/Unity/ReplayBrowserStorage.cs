using System.Runtime.InteropServices;

namespace Deviant.Replay.Unity
{
    public static class ReplayBrowserStorage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void DeviantReplaySyncFS();
        [DllImport("__Internal")] static extern int DeviantReplaySyncStatus();
        public static void Sync() => DeviantReplaySyncFS();
        public static int Status => DeviantReplaySyncStatus();
#else
        public static void Sync() { }
        public static int Status => 0;
#endif
    }
}
