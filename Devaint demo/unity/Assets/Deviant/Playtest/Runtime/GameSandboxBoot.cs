using UnityEngine;

namespace Deviant.Playtest
{
    // This scene is only an entry point; all terrain, actors and combat come from the actual chapter scenes.
    public sealed class GameSandboxBoot : MonoBehaviour
    {
        void Start() => GameSandbox.Begin();
    }
}
