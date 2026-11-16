using UnityEngine;

namespace Deviant.Presentation.Unity
{
    public sealed class HitFeedbackBindings : ScriptableObject
    {
        public HitPrefabBinding[] prefabs;
        public string sourceHash;
        public Shader distortionShader;
    }
}
