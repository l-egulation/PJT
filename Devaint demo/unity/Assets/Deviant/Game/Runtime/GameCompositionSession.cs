using System;
using Deviant.Combat;
using Deviant.Equipment;
using Deviant.Equipment.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Deviant.Game
{
    public partial class GameComposition
    {
        IWeaponLoadout sessionLoadout;
        CombatPlayer observedPlayer;
        Transform ownedCameraTarget, previousCameraTarget;
        bool sessionStarted, lifetimeWired;
        IDisposable movementValidationBinding;
        public GameSession Session { get; private set; }
        void OpenSession()
        {
            if (Session != null && Session.IsActive) return;
            if (Session != null && !Session.IsClosed) throw new InvalidOperationException("The previous Game session is still returning an in-flight child.");
            if (sessionLoadout == null) sessionLoadout = WeaponResources.CreateLoadout();
            Session = new GameSession(sessionLoadout, controls, forest, PrepareChunk);
            if (sessionStarted) BindSessionHud();
        }
        void ObservePlayer()
        {
            if (observedPlayer) observedPlayer.Died -= PlayerDied;
            observedPlayer = combat.Player;
            if (observedPlayer) observedPlayer.Died += PlayerDied;
        }
        void PlayerDied() => CloseSession(GameSessionEndReason.PlayerDied);
        void CombatUnavailable() => CloseSession(GameSessionEndReason.CombatUnavailable);
        void ForestLayoutEnded() { if (combat) combat.enabled = false; }
        void ResetSession()
        {
            if (!isActiveAndEnabled) return;
            CloseSession(GameSessionEndReason.DevelopmentReset);
            OpenSession(); BindHudSession(); BindFollowTargets();
        }
        void WireLifetime()
        {
            if (lifetimeWired) return;
            lifetimeWired = true; combat.ResetCompleted += ResetSession; combat.Deactivated += CombatUnavailable;
            combat.Initialized += BindHudSession; combat.ResetCompleted += BindHudSession;
            combat.Initialized += BindForestProfile;
            forest.LayoutEnded += ForestLayoutEnded;
        }
        void UnwireLifetime()
        {
            if (!lifetimeWired) return;
            lifetimeWired = false;
            movementValidationBinding?.Dispose(); movementValidationBinding = null;
            if (combat)
            {
                combat.ResetCompleted -= ResetSession; combat.Deactivated -= CombatUnavailable;
                combat.Initialized -= BindHudSession; combat.ResetCompleted -= BindHudSession;
                combat.Initialized -= BindForestProfile;
            }
            if (forest) forest.LayoutEnded -= ForestLayoutEnded;
            if (observedPlayer) observedPlayer.Died -= PlayerDied;
            observedPlayer = null;
        }
        void BindForestProfile()
        {
            if (Session == null || !Session.IsActive || !combat.Player) return;
            var profile = Deviant.Integration.UnityChapterModules.ProfileFor(combat.Player);
            if (!Session.Forest.TryBindProfile(Session.Forest.Id, profile, new Deviant.World.Contracts.LayoutPoint(playerSpawn.x, playerSpawn.y), out var profileError))
                throw new InvalidOperationException(profileError);
            if (movementValidationBinding == null) movementValidationBinding = combat.BindMovementValidation(new ForestMovementValidation(combat, forest));
        }
        void BindFollowTargets()
        {
            if (Session == null || !Session.IsActive || !combat.Player) return;
            BindForestProfile();
            if (!Session.Forest.TryFollow(Session.Forest.Id, combat.Player.transform, out var error)) throw new InvalidOperationException(error);
            previousCameraTarget = cameraRig.followTarget; ownedCameraTarget = combat.Player.transform;
            cameraRig.followTarget = ownedCameraTarget; cameraRig.SnapToTarget();
        }
        void CloseSession(GameSessionEndReason reason)
        {
            var closing = Session;
            if (closing == null) return;
            try { UnbindSessionHud(closing); }
            finally
            {
                closing.Close(reason);
                if (cameraRig && ReferenceEquals(cameraRig.followTarget, ownedCameraTarget)) cameraRig.followTarget = previousCameraTarget;
                ownedCameraTarget = null; previousCameraTarget = null;
            }
            if (closing.CleanupError != null) Debug.LogError("Game session cleanup: " + closing.CleanupError);
        }
        void OnDisable()
        {
            CloseSession(GameSessionEndReason.Disabled); UnwireLifetime();
        }
        void OnEnable()
        {
            if (controls == null) return;
            WireLifetime(); ObservePlayer();
            if (!sessionStarted) { OpenSession(); return; }
            if (combat && combat.Player && !combat.Player.Dead) { OpenSession(); BindFollowTargets(); }
        }
        void BindSessionHud()
        {
            if (!hud || Session == null || !Session.IsActive) return;
            var events = EventSystem.current;
            var input = events ? events.GetComponent<InputSystemUIInputModule>() : null;
            if (!input || input.cancel == null || input.cancel.action == null)
                throw new InvalidOperationException("Game HUD requires the existing EventSystem Cancel action.");
            hud.BindControls(Session.Controls, Session.Screens, input.cancel.action, events);
        }
        void UnbindSessionHud(GameSession expected)
        {
            if (hud) hud.UnbindControls(expected.Controls, expected.Screens);
        }
    }
}
