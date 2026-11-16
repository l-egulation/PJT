using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    // Only draft values participate in Undo. File hashes and live state do not.
    public sealed class LocomotionDraft : ScriptableObject
    {
        public LocomotionData data;
    }
}
