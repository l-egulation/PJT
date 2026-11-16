using System;

namespace Deviant.Definitions
{
    [Serializable] public sealed class CheckpointMotionData
    {
        public string[] frames;
        public float[] seconds;
    }
    [Serializable] public sealed class ManualCheckpointData
    {
        public int schemaVersion;
        public float installSeconds, enterSeconds, appearSeconds;
        public CheckpointMotionData installMotion, enterMotion, appearMotion;
    }
    public sealed class ManualCheckpointProfile
    {
        public float InstallSeconds { get; }
        public float EnterSeconds { get; }
        public float AppearSeconds { get; }
        public ManualCheckpointProfile(ManualCheckpointData data)
        {
            if (data == null || data.schemaVersion != 1) throw new ArgumentException("Invalid manual checkpoint schema.");
            Check(data.installSeconds); Check(data.enterSeconds); Check(data.appearSeconds);
            CheckMotion(data.installMotion); CheckMotion(data.enterMotion); CheckMotion(data.appearMotion);
            InstallSeconds = data.installSeconds; EnterSeconds = data.enterSeconds; AppearSeconds = data.appearSeconds;
        }
        static void Check(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
                throw new ArgumentException("Checkpoint phase durations must be finite and positive.");
        }
        static void CheckMotion(CheckpointMotionData motion)
        {
            if (motion == null) return;
            if (motion.frames == null || motion.seconds == null || motion.frames.Length != motion.seconds.Length)
                throw new ArgumentException("Checkpoint frames and frame durations must match.");
            for (int i = 0; i < motion.frames.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(motion.frames[i])) throw new ArgumentException("Checkpoint frame path is empty.");
                Check(motion.seconds[i]);
            }
        }
    }
}
