using System;

namespace Deviant.Definitions
{
    public enum MeleeFrameFamily { Basic, Direct }
    public enum MeleeTimingMode { CombatTimeFixed, FrameTime }

    [Serializable]
    public sealed class MeleeFrameCatalogDocument
    {
        public const int SchemaVersion = 1;
        public int schemaVersion;
        public MeleeFrameSourceDocument[] actions;
    }
    [Serializable]
    public sealed class MeleeFrameSourceDocument
    {
        public string actionId, formId, motionId;
        public MeleeFrameFamily family;
        public MeleeTimingMode mode;
        public string policyVersion, mediaBindingVersion;
        public FrameTimelineDocument timeline;
        public CapturedCueMediaMappingDocument cueMapping;
    }
    [Serializable]
    public sealed class CapturedCueMediaMappingDocument
    {
        public float sourceImpact, sourceDuration;
    }
}
