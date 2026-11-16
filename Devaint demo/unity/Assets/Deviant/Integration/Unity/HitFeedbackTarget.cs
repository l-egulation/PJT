using UnityEngine;

namespace Deviant.Integration
{
    // Kept for existing authored scenes. This former monster-death marker no
    // longer authorizes slow motion; the time policy is player-death-only.
    public sealed class HitFeedbackTarget : MonoBehaviour
    {
        public bool importantDeath;
    }
}
