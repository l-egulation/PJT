using Deviant.Combat;
using Deviant.Definitions;
using Deviant.Input.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    // Scene-owned input and presentation. Timing, protection and relocation belong to the player.
    [DefaultExecutionOrder(1100)]
    public sealed class Chapter1ManualCheckpoint : MonoBehaviour
    {
        ForestCombatWorld world;
        CombatArena arena;
        System.Action snapCamera;
        System.Func<bool> checkpointLocked;
        CombatPlayer player;
        readonly SpriteRenderer[] installedPortals = new SpriteRenderer[CombatPlayer.PortalCount];
        // What each portal showed last frame: moving a portal melts the old doorway away once the new one is drawn.
        readonly Vector2[] shownPositions = new Vector2[CombatPlayer.PortalCount];
        readonly int[] shownFacings = new int[CombatPlayer.PortalCount];
        readonly bool[] shown = new bool[CombatPlayer.PortalCount];
        SpriteRenderer actionPortal, departurePortal;
        KeycapMarker travelKey;
        float? keycapLift;
        Vector2 keycapAnchor = new Vector2(float.NaN, float.NaN);
        ApprovedTraversalArt art;
        float installSeconds, meltStarted = -100;
        CharacterClip install, enter, appear;
        bool alteredVisual;
        GUIStyle label;
        public void Initialize(ForestCombatWorld owner)
        {
            world = owner;
            Initialize(owner.arena, owner.GetComponent<Chapter1TraversalPresentation>().Art,
                () => owner.RefreshCamera(0, true), () => owner.GetComponent<Chapter1RunBinding>()?.Run != null &&
                    !Chapter1ProfileHost.Service.Snapshot.HasSeen(PortalTaughtStory));
        }
        public void Initialize(CombatArena owner, ApprovedTraversalArt traversalArt, System.Action onArrival, System.Func<bool> locked)
        {
            arena = owner; player = arena.Player; snapCamera = onArrival; checkpointLocked = locked;
            var asset = Resources.Load<TextAsset>("ManualCheckpoint");
            if (!asset) throw new System.InvalidOperationException("ManualCheckpoint catalog is missing.");
            var data = JsonUtility.FromJson<ManualCheckpointData>(asset.text);
            player.ConfigureCheckpoint(new ManualCheckpointProfile(data));
            install = Clip(data.installMotion, "checkpoint_install");
            enter = Clip(data.enterMotion, "checkpoint_enter");
            appear = Clip(data.appearMotion, "checkpoint_appear");
            installSeconds = data.installSeconds;
            art = traversalArt;
            for (int i = 0; i < installedPortals.Length; i++) installedPortals[i] = Portal("Installed portal " + (i + 1));
            actionPortal = Portal("Checkpoint action");
            departurePortal = Portal("Checkpoint departure");
            travelKey = KeycapMarker.Create(transform, 'F', player.Position, arena.spriteMaterial, player.visual.sortingOrder + 1, PlayerControls.For(player));
            travelKey.name = "Portal travel keycap"; travelKey.SetVisible(false);
            player.CheckpointArrived += Arrived;
            arena.Deactivated += Clear;
        }
        static CharacterClip Clip(CheckpointMotionData data, string name)
        {
            if (data?.frames == null || data.frames.Length == 0) return null;
            var clip = new CharacterClip { name = name, frames = data.frames, seconds = data.seconds };
            clip.Load(); return clip;
        }
        SpriteRenderer Portal(string name)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sharedMaterial = arena.spriteMaterial;
            sprite.sortingOrder = player.visual.sortingOrder - 1;
            sprite.enabled = false; return sprite;
        }
        void Update()
        {
            if (!player || world && !world.Ready || arena.ManualSimulationActive || player.UseTestInput) return;
            HandleKeyboard();
        }
        public void HandleKeyboard()
        {
            if (!player || PortalLocked) return;
            var input = PlayerControls.For(player);
            if (!input) return;
            if (player.CheckpointBusy && input.Take(GameAction.DevelopmentReset) && !arena.Controls.Current.SimulationPaused && Time.timeScale > 0)
            { arena.ResetTest(); return; }
            if (input.Read(GameAction.Weapon1Basic).IsPressed || input.Read(GameAction.Weapon2Basic).IsPressed ||
                input.Read(GameAction.Weapon1Special).IsPressed || input.Read(GameAction.Weapon2Special).IsPressed ||
                input.Read(GameAction.Jump).IsPressed || input.Read(GameAction.Dash).IsPressed) return;
            // C sets portal 1 and V portal 2 (pressing again moves it); F on a portal steps through to the other one.
            // Shops, chests and exits read F earlier in the frame, so they keep it when a portal stands beside them.
            bool first = PlayerControls.Pressed(player, GameAction.CheckpointInstall), second = PlayerControls.Pressed(player, GameAction.CheckpointReturn);
            if (first && second) return;
            if (first) { input.Take(GameAction.CheckpointInstall); player.TryInstallPortal(0); }
            else if (second) { input.Take(GameAction.CheckpointReturn); player.TryInstallPortal(1); }
            else if (TravelOffered) PlayerControls.TryPerform(player, GameAction.Interact, player.TryTravelThroughPortal);
        }
        public KeycapMarker TravelKey => travelKey;
        // Both portals stand and the hero is in one of them, free to move.
        public bool TravelOffered => player && !PortalLocked && !player.Dead && !player.CheckpointBusy && player.PortalsLinked && player.PortalUnderfoot() >= 0;
        void Arrived() => snapCamera?.Invoke();
        void Clear()
        {
            meltStarted = -100;
            for (int i = 0; i < shown.Length; i++) shown[i] = false;
            if (player) player.ClearCheckpoint();
        }
        static void PlacePortal(SpriteRenderer sprite, Vector2 position, int facing)
        {
            sprite.transform.position = (Vector3)position + Vector3.right * (.25f * facing);
            sprite.flipX = facing < 0;
        }
        void LateUpdate() => RefreshPresentation();
        public void RefreshPresentation()
        {
            if (!player || !actionPortal) return;
            float progress = player.CheckpointProgress;
            var standing = art.portalDraw.At(art.portalDraw.Duration, false);
            for (int i = 0; i < installedPortals.Length; i++)
            {
                var portal = installedPortals[i];
                bool has = player.HasPortal(i);
                if (has && shown[i] && (shownPositions[i] != player.PortalPosition(i) || shownFacings[i] != player.PortalFacing(i)))
                { meltStarted = arena.Now; PlacePortal(departurePortal, shownPositions[i], shownFacings[i]); }
                shown[i] = has;
                if (has) { shownPositions[i] = player.PortalPosition(i); shownFacings[i] = player.PortalFacing(i); }
                portal.enabled = has; portal.sprite = standing;
                if (has) PlacePortal(portal, player.PortalPosition(i), player.PortalFacing(i));
            }
            float meltAge = arena.Now - meltStarted;
            departurePortal.enabled = !player.Dead && meltAge >= 0 && meltAge < art.portalMelt.Duration;
            if (departurePortal.enabled) departurePortal.sprite = art.portalMelt.At(meltAge, false);
            RefreshTravelKey();
            // Only the drawing needs a portal of its own; entering and leaving happen inside the standing ones.
            actionPortal.enabled = player.PortalPhase == CheckpointPhase.Installing;
            if (!player.CheckpointBusy)
            {
                if (alteredVisual && player.visual)
                {
                    player.visual.transform.localScale = Vector3.one;
                    player.visual.transform.localPosition = Vector3.zero;
                    // The normal presenter owns sprite/color again; remove only our fade.
                    player.visual.color = player.Dead ? Color.gray : player.Hidden ? new Color(1,1,1,.35f) : Color.white;
                }
                alteredVisual = false; return;
            }
            PlacePortal(actionPortal, player.Position, player.facing);
            // The portal art follows the drawing motion's own timeline, so a shorter install plays both faster together.
            float drawingTime = progress * (install != null ? install.Duration : installSeconds) - art.portalDrawStartSeconds;
            if (player.PortalPhase == CheckpointPhase.Installing)
            {
                actionPortal.enabled = drawingTime >= 0;
                actionPortal.sprite = art.portalDraw.At(Mathf.Max(0,drawingTime), false);
            }
            var clip = player.PortalPhase == CheckpointPhase.Installing ? install : player.PortalPhase == CheckpointPhase.Entering ? enter : appear;
            if (!player.visual) return;
            alteredVisual = true;
            if (clip != null)
            {
                player.visual.sprite = clip.At(progress * clip.Duration, false);
                player.visual.transform.localPosition = Vector3.zero;
                player.visual.transform.localScale = Vector3.one;
                player.visual.flipX = player.facing < 0;
                // Keep the native body silhouette. No squash, stretch, world-space
                // reference-video translation, or change to the player's hitbox.
                float ink = 0, visibility = 1;
                if (player.PortalPhase == CheckpointPhase.Entering)
                {
                    int frame = Mathf.Min(art.entryInk.Length-1, Mathf.FloorToInt(progress*art.entryInk.Length));
                    ink = art.entryInk[frame]; visibility = 1-Mathf.InverseLerp(.72f,1,progress);
                }
                else if (player.PortalPhase == CheckpointPhase.Appearing)
                {
                    ink = 1-Mathf.InverseLerp(.15f,.7f,progress);
                    visibility = Mathf.InverseLerp(0,.25f,progress);
                }
                var tint = Color.Lerp(Color.white,Color.black,ink); tint.a = visibility;
                player.visual.color = tint;
            }
            else
            {
                float visibility = player.PortalPhase == CheckpointPhase.Entering ? 1 - progress :
                    player.PortalPhase == CheckpointPhase.Appearing ? progress : 1;
                var tint = player.visual.color; tint.a = visibility; player.visual.color = tint;
                player.visual.transform.localScale = new Vector3(Mathf.Max(.08f, visibility), 1, 1);
            }
        }
        void RefreshTravelKey()
        {
            if (!travelKey) return;
            bool available=arena&&!arena.Controls.Current.SimulationPaused&&!arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Interaction)&&!player.InputBlocked;
            int at = available&&TravelOffered ? player.PortalUnderfoot() : -1;
            travelKey.SetVisible(at >= 0);
            if (at < 0) return;
            var portal = installedPortals[at];
            if (keycapLift == null && portal.sprite) keycapLift = KeycapMarker.OpaqueTop(portal) - portal.transform.position.y;
            var centre = CombatPlayer.PortalCentre(player.PortalPosition(at), player.PortalFacing(at));
            var anchor = new Vector2(centre.x, player.PortalPosition(at).y + (keycapLift ?? 3) + .3f);
            // The marker bobs around its anchor in its own LateUpdate; re-anchoring every frame would cancel the bob.
            if (anchor != keycapAnchor) { keycapAnchor = anchor; travelKey.MoveTo(anchor); }
        }
        public bool ExternalPresentation { get; set; }
        // The portal is not taught before the forest workshop's artisan says its C, V and F lines (playtest 2026-11-05): in a
        // run C/V/F do nothing at portals until then. Scenes without a run (dummy training, boss test) keep them.
        public const string PortalTaughtStory = "portal-taught";
        public bool PortalLocked => checkpointLocked?.Invoke() == true;
        void OnGUI()
        {
            if (PortalLocked || ExternalPresentation || !DevFeatures.Enabled || !player || player.Dead || world && (!world.Ready || world.FullMap && world.FullMap.IsOpen)) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, font = arena.uiFont };
            string hint = PlayerControls.Label(player, GameAction.CheckpointInstall) + (player.HasPortal(0) ? " 포탈1 옮기기    " : " 포탈1 설치    ") +
                PlayerControls.Label(player, GameAction.CheckpointReturn) + (player.HasPortal(1) ? " 포탈2 옮기기    " : " 포탈2 설치    ") +
                PlayerControls.Label(player, GameAction.Interact) + " 포탈 위에서 반대쪽 포탈로";
            GUI.Label(new Rect(0, Screen.height - 72, Screen.width, 26), hint, label);
            if (!string.IsNullOrEmpty(player.CheckpointMessage))
                GUI.Label(new Rect(0, Screen.height - 46, Screen.width, 26), player.CheckpointMessage, label);
        }
        void OnDisable()
        {
            Clear();
            foreach (var portal in installedPortals) if (portal) portal.enabled = false;
            if (travelKey) travelKey.SetVisible(false);
            if (actionPortal) actionPortal.enabled = false;
            if (departurePortal) departurePortal.enabled = false;
            if (alteredVisual && player && player.visual)
            {
                player.visual.transform.localScale = Vector3.one; player.visual.transform.localPosition = Vector3.zero;
                player.visual.color = player.Dead ? Color.gray : Color.white;
            }
            alteredVisual = false;
        }
        void OnDestroy()
        {
            if (player) player.CheckpointArrived -= Arrived;
            if (arena) arena.Deactivated -= Clear;
            foreach (var portal in installedPortals) if (portal) Destroy(portal.gameObject);
            if (travelKey) Destroy(travelKey.gameObject);
            if (actionPortal) Destroy(actionPortal.gameObject);
            if (departurePortal) Destroy(departurePortal.gameObject);
        }
    }
}
