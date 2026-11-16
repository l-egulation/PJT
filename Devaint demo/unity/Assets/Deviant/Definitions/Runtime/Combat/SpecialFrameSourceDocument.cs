using System;

namespace Deviant.Definitions
{
    public enum SpecialFrameFamily { FiniteTracks, AuthoredSkill }

    [Serializable]
    public sealed class SpecialFrameCatalogDocument
    {
        public const int SchemaVersion = 1;
        public int schemaVersion;
        public SpecialFrameSourceDocument[] actions;
    }
    [Serializable]
    public sealed class SpecialFrameSourceDocument
    {
        public string actionId, formId, motionId;
        public MotionActionKind kind;
        public int variant = -1;
        public SpecialFrameFamily family;
        public MeleeTimingMode mode;
        public string policyVersion, mediaBindingVersion;
        public FrameTimelineDocument timeline;
    }
}
