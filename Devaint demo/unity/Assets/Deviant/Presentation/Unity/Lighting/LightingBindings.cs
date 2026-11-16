using UnityEngine;

namespace Deviant.Presentation.Unity
{
    public sealed class LightingBindings : ScriptableObject
    {
        public GameObject shadowTemplate;
        public LightEmitterView lanternTemplate, windowTemplate;
        public Shader litOutlineShader, litShader, unlitShader, connectorShader, windowBeamShader;
    }
}
