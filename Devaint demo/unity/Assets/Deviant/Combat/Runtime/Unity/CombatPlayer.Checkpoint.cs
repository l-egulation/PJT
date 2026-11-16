using System;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public enum CheckpointPhase { None, Installing, Entering, Appearing }

    // Two linked portals (user decision 2026-11-07): C sets portal 1 and V sets portal 2 where the hero stands, and
    // pressing either again moves that portal. Once both stand, interacting on one carries the hero to the other, back
    // and forth, with no cost or cooldown.
    public partial class CombatPlayer
    {
        public const int PortalCount = 2;
        // How close the hero's feet must be to a portal's centre to step through it, and how far apart the two must be.
        public const float PortalReach = 1f, PortalSpacing = 2.5f;
        ManualCheckpointProfile checkpointProfile;
        IControlScope checkpointControls;
        readonly IActorPlacementQuery checkpointSurfaces = new UnityActorSurfaceQuery(_ => false);
        readonly Vector2[] portalPositions = new Vector2[PortalCount];
        readonly int[] portalFacings = new int[PortalCount];
        readonly bool[] portalInstalled = new bool[PortalCount];
        Vector2 checkpointCandidate;
        int checkpointCandidateFacing, actionSlot = -1, destinationSlot = -1;
        float checkpointStarted, checkpointDuration;
        public CheckpointPhase PortalPhase { get; private set; }
        public bool HasPortal(int slot) => slot >= 0 && slot < PortalCount && portalInstalled[slot];
        public Vector2 PortalPosition(int slot) => portalPositions[slot];
        public int PortalFacing(int slot) => portalFacings[slot];
        public bool HasCheckpoint => portalInstalled[0] || portalInstalled[1];
        public bool PortalsLinked => portalInstalled[0] && portalInstalled[1];
        // The portal being drawn (Installing) or stepped into (Entering); -1 otherwise.
        public int ActionPortal => PortalPhase == CheckpointPhase.Installing || PortalPhase == CheckpointPhase.Entering ? actionSlot : -1;
        public int DestinationPortal => destinationSlot;
        public bool CheckpointBusy => PortalPhase != CheckpointPhase.None;
        public float CheckpointProgress => CheckpointBusy ? Mathf.Clamp01((arena.Now - checkpointStarted) / checkpointDuration) : 0;
        public string CheckpointMessage { get; private set; }
        public event Action CheckpointArrived;
        public static string PortalName(int slot) => "포탈" + (slot + 1);
        // The art is drawn a quarter unit ahead of the feet, so the doorway centre sits there too.
        public static Vector2 PortalCentre(Vector2 position, int facing) => position + Vector2.right * (.25f * facing);

        public void ConfigureCheckpoint(ManualCheckpointProfile profile)
        {
            ClearCheckpoint(); checkpointProfile = profile ?? throw new ArgumentNullException(nameof(profile));
        }
        // The installed portal the hero stands in, or -1.
        public int PortalUnderfoot()
        {
            for (int i = 0; i < PortalCount; i++)
            {
                if (!portalInstalled[i]) continue;
                var centre = PortalCentre(portalPositions[i], portalFacings[i]);
                if (Mathf.Abs(Position.x - centre.x) <= PortalReach && Mathf.Abs(Position.y - portalPositions[i].y) <= .5f) return i;
            }
            return -1;
        }
        public bool TryInstallPortal(int slot)
        {
            if (slot < 0 || slot >= PortalCount) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!CheckpointReady()) return false;
            int other = 1 - slot;
            if (portalInstalled[other] && Mathf.Abs(Position.x - portalPositions[other].x) < PortalSpacing && Mathf.Abs(Position.y - portalPositions[other].y) < 2)
                return CheckpointReject("다른 포탈과 너무 가깝습니다. 조금 떨어져서 설치하세요.");
            if (!CheckpointSafe(Position, facing))
                return CheckpointReject("몸 전체가 올라갈 수 있는 평평한 위치에 설치하세요.");
            checkpointCandidate = Position; checkpointCandidateFacing = facing;
            actionSlot = slot; destinationSlot = -1;
            Begin(CheckpointPhase.Installing, checkpointProfile.InstallSeconds, PortalName(slot) + " 설치 중…");
            return true;
        }
        public bool TryTravelThroughPortal()
        {
            if (!CheckpointReady()) return false;
            int from = PortalUnderfoot();
            if (from < 0) return CheckpointReject("포탈 위에서 사용할 수 있습니다.");
            int to = 1 - from;
            if (!portalInstalled[to]) return CheckpointReject("반대쪽 포탈을 먼저 설치하세요.");
            if (!CheckpointSafe(portalPositions[to], portalFacings[to]))
                return CheckpointReject(PortalName(to) + " 도착 위치가 막혀 있습니다. 다시 설치하세요.");
            if (!CheckpointSafe(portalPositions[from], portalFacings[from]))
                return CheckpointReject(PortalName(from) + " 자리가 막혀 있습니다. 다시 설치하세요.");
            // Step onto the doorway itself so the ink-entry motion plays inside the standing portal.
            RelocateToCheckpoint(portalPositions[from], portalFacings[from]);
            checkpointCandidate = Position; checkpointCandidateFacing = facing;
            actionSlot = from; destinationSlot = to;
            Begin(CheckpointPhase.Entering, checkpointProfile.EnterSeconds, "포탈로 들어가는 중…");
            return true;
        }
        bool CheckpointReady()
        {
            if (checkpointProfile == null || !arena || Dead || CheckpointBusy) return false;
            if (InputBlocked || arena.Controls.Current.SimulationPaused || Time.timeScale == 0 ||
                arena.Controls.Current.Blocks(InputChannels.Interaction)) return false;
            if (currentCast || Dashing || WallClimbing || !grounded || !ProbeGround() ||
                arena.Now < knockUntil || arena.Now < moveLockUntil || arena.Now < stunUntil ||
                arena.Now < rootUntil || arena.Now < staggerUntil)
                return CheckpointReject("땅이나 발판 위에서 서 있거나 걸을 때 사용할 수 있습니다.");
            return true;
        }
        void Begin(CheckpointPhase phase, float seconds, string message)
        {
            CancelAction(); ClearInputState(); velocity = Vector2.zero;
            checkpointControls = arena.Controls.OpenScope("manual-checkpoint");
            checkpointControls.BlockInput(InputChannels.Combat | InputChannels.Interaction);
            SetCheckpointPhase(phase, seconds);
            CheckpointMessage = message;
        }
        bool CheckpointReject(string message) { CheckpointMessage = message; return false; }
        bool CheckpointSafe(Vector2 position, int direction)
        {
            var body = new ActorBodyFrame(position.x, position.y, size.x, size.y, offset.x, offset.y, direction);
            return !ActorPlacementResolver.Blocked(body, position.x, position.y, direction, checkpointSurfaces) &&
                ActorPlacementResolver.ProbeSafeFloor(body, 0, checkpointSurfaces, out _);
        }
        void SetCheckpointPhase(CheckpointPhase phase, float duration)
        {
            PortalPhase = phase; checkpointStarted = arena.Now; checkpointDuration = duration;
        }
        // Driven by the same simulation clock as combat; opening a pause UI cannot finish a portal.
        bool StepCheckpoint()
        {
            if (!CheckpointBusy) return false;
            if (Dead) { ClearCheckpoint(); return false; }
            velocity = Vector2.zero;
            if (arena.Now - checkpointStarted < checkpointDuration) return true;
            if (PortalPhase == CheckpointPhase.Installing)
            {
                if (!CheckpointSafe(checkpointCandidate, checkpointCandidateFacing))
                { EndCheckpoint("설치 위치가 바뀌어 취소했습니다."); return true; }
                portalPositions[actionSlot] = checkpointCandidate; portalFacings[actionSlot] = checkpointCandidateFacing;
                portalInstalled[actionSlot] = true;
                EndCheckpoint(PortalName(actionSlot) + " 설치 완료");
            }
            else if (PortalPhase == CheckpointPhase.Entering)
            {
                int to = destinationSlot;
                if (to < 0 || !portalInstalled[to] || !CheckpointSafe(portalPositions[to], portalFacings[to]))
                { EndCheckpoint("도착 위치가 막혀 이동을 취소했습니다."); return true; }
                // Enable protection before committing the position; no wall sweep or healing/reset.
                SetCheckpointPhase(CheckpointPhase.Appearing, checkpointProfile.AppearSeconds);
                RelocateToCheckpoint(portalPositions[to], portalFacings[to]);
                jumps.Reset(); floors.Clear(); tick.HadGround = grounded;
                floors.Record(arena.Now, Position.x, Position.y, facing);
                arena.Presentation.Reset(); CheckpointMessage = "포탈에서 나오는 중…";
                CheckpointArrived?.Invoke();
            }
            else EndCheckpoint(PortalName(destinationSlot) + "로 이동했습니다.");
            return true;
        }
        protected override void OnAcceptedHit(CombatActor source, int hitDirection)
        {
            base.OnAcceptedHit(source, hitDirection);
            if (!Dead) arena.Audio?.EmitSelected("sfx_player_hurt", Position);
            if (PortalPhase == CheckpointPhase.Installing || PortalPhase == CheckpointPhase.Entering)
                EndCheckpoint("피격으로 포탈 동작이 취소되었습니다.");
        }
        // 설치 중 대시(2026-11-06)나 점프(2026-11-16)를 누르면 설치를 취소하고 그 동작이 그대로 나간다.
        // 들어가기·나오기는 취소하지 않는다.
        void CancelCheckpointInstallByMovement(CombatButtons pressed)
        {
            if (PortalPhase != CheckpointPhase.Installing || Dead) return;
            if ((pressed & CombatButtons.Dash) != 0) EndCheckpoint("대시로 포탈 설치를 취소했습니다.");
            else if ((pressed & CombatButtons.Jump) != 0) EndCheckpoint("점프로 포탈 설치를 취소했습니다.");
        }
        void EndCheckpoint(string message)
        {
            PortalPhase = CheckpointPhase.None;
            var scope = checkpointControls; checkpointControls = null; scope?.Dispose();
            CheckpointMessage = message;
        }
        public void ClearCheckpoint()
        {
            for (int i = 0; i < PortalCount; i++) { portalInstalled[i] = false; portalPositions[i] = default; portalFacings[i] = 0; }
            actionSlot = destinationSlot = -1;
            EndCheckpoint(null);
        }
    }
}
