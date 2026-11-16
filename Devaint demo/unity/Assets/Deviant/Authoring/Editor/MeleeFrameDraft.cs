using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public sealed class MeleeFrameDraft : ScriptableObject
    {
        public MeleeFrameCatalogDocument source;
        public MeleeTimingMode firstMode;
        public FrameTimelineDocument firstTimeline;
    }
}
