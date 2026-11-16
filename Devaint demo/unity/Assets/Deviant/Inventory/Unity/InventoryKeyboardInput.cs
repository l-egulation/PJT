using Deviant.Combat;
using Deviant.Input.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deviant.Inventory.Unity
{
    // Device input belongs to the session, so replacing the GUI does not remove I/E/Escape.
    [DefaultExecutionOrder(-100)]
    public sealed class InventoryKeyboardInput : MonoBehaviour
    {
        public InventoryRuntimeHost Host { get; set; }
        public bool ExternalInput { get; set; }
        void Update()
        {
            if (ExternalInput) return;
            if (!Host || !Host.isActiveAndEnabled || Host.Presenter == null || Host.Arena.Player.Dead) return;
            var presenter = Host.Presenter;
            var input = PlayerControls.For(Host.Arena.Player);
            if (input)
            {
                if (presenter.IsOpen || presenter.Paused) input.SuppressGameplay();
                bool pause = input.Take(GameAction.Pause);
                bool cancel = (presenter.IsOpen || presenter.Paused) && input.Take(GameAction.Cancel);
                if (pause) input.Take(GameAction.Cancel);
                if (input.Take(GameAction.Inventory)) { input.SuppressGameplay(); presenter.Toggle(); }
                else if (pause || cancel)
                { input.SuppressGameplay(); if (presenter.IsOpen) presenter.Close(); else if (Host.Theme) presenter.TogglePause(); }
                else if (presenter.IsOpen && input.Take(GameAction.Equip)) presenter.EquipSelected();
            }
        }
    }
}
