using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public sealed class MotionRegistryDraft : ScriptableObject
    {
        public MotionRegistryDocument registry;
        public AttackMotionDocument firstMotion;
    }
}
