using System;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deviant.UI.Unity
{
    // Composition owns the action's map/enabled state and product key policy.
    // This adapter reports consumption; it never forwards the same edge again.
    public sealed class ScreenInputBinding : MonoBehaviour
    {
        ScreenRouter router;
        InputAction back;
        Action<InputAction.CallbackContext> callback;
        long generation;
        bool processing;
        public ScreenBackResult LastResult { get; private set; }
        public event Action<ScreenBackResult> BackProcessed;
        public void Configure(ScreenRouter router, InputAction back)
        {
            if (router == null || back == null || back.type != InputActionType.Button)
                throw new ArgumentException("A screen router and externally owned Button action are required.");
            Unwire(); this.router = router; this.back = back;
            if (isActiveAndEnabled) Wire();
        }
        public void Unbind()
        {
            Unwire(); router = null; back = null;
        }
        void Wire()
        {
            if (callback != null || router == null || back == null) return;
            long expected = generation;
            callback = context => {
                if (expected != generation || !isActiveAndEnabled || processing) return;
                processing = true;
                try
                {
                    // Capture before Back can close a view and change focus.
                    var target = router;
                    Guid binding = target.FocusedBindingId;
                    var result = target.Back(binding);
                    // Focus/unbind callbacks can reconfigure this adapter while
                    // Back is settling. Its old result cannot reach new owners.
                    if (expected != generation || !isActiveAndEnabled || !ReferenceEquals(router, target)) return;
                    LastResult = result;
                    BackProcessed?.Invoke(result);
                }
                finally { processing = false; }
            };
            back.performed += callback;
        }
        void Unwire()
        {
            generation++;
            if (callback != null && back != null) back.performed -= callback;
            callback = null;
        }
        void OnEnable() { Wire(); }
        void OnDisable() { Unwire(); }
        void OnDestroy() { Unbind(); }
    }
}
