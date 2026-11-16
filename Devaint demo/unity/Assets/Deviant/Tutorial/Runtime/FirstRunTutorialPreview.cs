using Deviant.Input.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Deviant.Tutorial.Unity
{
    // Standalone art-review scene only. Actual reward/profile timing belongs to the consuming game.
    public sealed class FirstRunTutorialPreview : MonoBehaviour
    {
        public FirstRunTutorialWindow window;
        public bool showAttack;
        void Start()
        {
            if (!EventSystem.current)
                new GameObject("Preview EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var input = GetComponent<LocalInput>();
            if (!input) input = gameObject.AddComponent<LocalInput>();
            if (showAttack) window.ShowAttack(input); else window.ShowRewards(input);
        }
    }
}
