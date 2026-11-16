using System;

namespace Deviant.Combat.Contracts
{
    // The input session already owns reset generation and experiment epoch.
    // A handle never identifies an actor outside that exact session.
    public readonly struct CombatActorHandle : IEquatable<CombatActorHandle>
    {
        public CombatInputSession Session { get; }
        public int ActorId { get; }
        public CombatActorHandle(CombatInputSession session, int actorId)
        { Session = session; ActorId = actorId; }
        public bool Equals(CombatActorHandle other) => Session.Equals(other.Session) && ActorId == other.ActorId;
        public override bool Equals(object obj) => obj is CombatActorHandle other && Equals(other);
        public override int GetHashCode() => unchecked(Session.GetHashCode() * 397 ^ ActorId);
    }

    public enum CombatActionReadPhase
    {
        None, Preparing, Charging, Active, Recovery, Approaching, Completed, Cancelled, Failed
    }

    // A view of the current execution, not a command result or an outcome log.
    // Absent/unversioned definitions retain null version strings.
    public readonly struct ActionReadSnapshot
    {
        public Guid ExecutionId { get; }
        public string ContentId { get; }
        public string DefinitionVersion { get; }
        public string TimelineVersion { get; }
        public string FrameTimingVersion { get; }
        public CombatActionReadPhase Phase { get; }
        public float StartedAt { get; }
        public float Elapsed { get; }
        public float EndsAt { get; }
        public ActionReadSnapshot(Guid executionId, string contentId, string definitionVersion, string timelineVersion,
            string frameTimingVersion, CombatActionReadPhase phase, float startedAt, float elapsed, float endsAt)
        {
            ExecutionId = executionId; ContentId = contentId; DefinitionVersion = definitionVersion;
            TimelineVersion = timelineVersion; FrameTimingVersion = frameTimingVersion; Phase = phase;
            StartedAt = startedAt; Elapsed = elapsed; EndsAt = endsAt;
        }
    }

    // Live gameplay facts needed by body/Cue sampling, never Native media.
    // DynamicBodyMapping preserves the existing charge/general-special mapping;
    // it is fixed execution policy, not a newly interpreted content string.
    public readonly struct CharacterPresentationReadSnapshot
    {
        public string AppearanceId { get; }
        public int Axis { get; }
        public int BasicIndex { get; }
        public float DashDuration { get; }
        public bool HasAction { get; }
        public bool IsBasicAction { get; }
        public bool Approaching { get; }
        public float ApproachVisualDuration { get; }
        public float ImpactTime { get; }
        public float MotionDuration { get; }
        public bool DynamicBodyMapping { get; }
        public bool WallTraversal { get; }
        public CharacterPresentationReadSnapshot(int axis, int basicIndex, float dashDuration, bool hasAction, bool isBasic,
            bool approaching, float approachVisualDuration, float impactTime, float motionDuration, bool dynamicBodyMapping,string appearanceId=null,bool wallTraversal=false)
        {
            Axis = axis; BasicIndex = basicIndex; DashDuration = dashDuration; HasAction = hasAction; IsBasicAction = isBasic;
            Approaching = approaching; ApproachVisualDuration = approachVisualDuration; ImpactTime = impactTime;
            MotionDuration = motionDuration; DynamicBodyMapping = dynamicBodyMapping;
            AppearanceId=appearanceId; WallTraversal=wallTraversal;
        }
    }

    // Scalar projection of the existing Equipment charge owner, never a debt
    // calculator or another resource owner. Runtime reads allocate no collections.
    public readonly struct WeaponChargeReadSnapshot
    {
        public Guid InstanceId { get; }
        public string FormId { get; }
        public string SpecialId { get; }
        public int Capacity { get; }
        public int Available { get; }
        public float Period { get; }
        public float NextReady { get; }
        public WeaponChargeReadSnapshot(Guid instanceId, string formId, string specialId, int capacity, int available, float period, float nextReady)
        { InstanceId = instanceId; FormId = formId; SpecialId = specialId; Capacity = capacity; Available = available; Period = period; NextReady = nextReady; }
    }

    public readonly struct PlayerWeaponResourcesReadSnapshot
    {
        public bool HasLoadout { get; }
        public int ActiveSlot { get; }
        public WeaponChargeReadSnapshot Slot0 { get; }
        public WeaponChargeReadSnapshot Slot1 { get; }
        public PlayerWeaponResourcesReadSnapshot(int activeSlot, WeaponChargeReadSnapshot slot0, WeaponChargeReadSnapshot slot1)
        { HasLoadout = true; ActiveSlot = activeSlot; Slot0 = slot0; Slot1 = slot1; }
    }

    // Detached values from one actor's current owners. Reading neither advances
    // simulation nor copies the world, platform collections or mutable objects.
    public readonly struct ActorReadSnapshot
    {
        public CombatActorHandle Handle { get; }
        public long TickCount { get; }
        public float Now { get; }
        public bool IsPlayer { get; }
        public ActorKinematicsSnapshot Kinematics { get; }
        public ActorVitalsSnapshot Vitals { get; }
        public ActorStatusSnapshot Statuses { get; }
        public ActionReadSnapshot Action { get; }
        public PlayerDashSnapshot Dash { get; }
        public PlayerJumpSnapshot Jump { get; }
        public Guid ActiveWeaponId { get; }
        public string ActiveWeaponContentId { get; }
        public string ActiveWeaponName { get; }
        public CharacterPresentationReadSnapshot Presentation { get; }
        public PlayerWeaponResourcesReadSnapshot Resources { get; }
        public ActorReadSnapshot(CombatActorHandle handle, long tickCount, float now, bool isPlayer,
            ActorKinematicsSnapshot kinematics, ActorVitalsSnapshot vitals, ActorStatusSnapshot statuses,
            ActionReadSnapshot action, PlayerDashSnapshot dash, PlayerJumpSnapshot jump,
            Guid activeWeaponId, string activeWeaponContentId, string activeWeaponName, CharacterPresentationReadSnapshot presentation,
            PlayerWeaponResourcesReadSnapshot resources)
        {
            Handle = handle; TickCount = tickCount; Now = now; IsPlayer = isPlayer;
            Kinematics = kinematics; Vitals = vitals; Statuses = statuses; Action = action;
            Dash = dash; Jump = jump; ActiveWeaponId = activeWeaponId;
            ActiveWeaponContentId = activeWeaponContentId; ActiveWeaponName = activeWeaponName;
            Presentation = presentation;
            Resources = resources;
        }
    }

    // Consumers bind explicitly at initialization/reset. Rejected queries return
    // false/default; they never rebind, fall back to another actor or mutate state.
    public interface ICombatReadModel
    {
        bool TryGetSession(out CombatInputSession session);
        bool TryReadPlayer(CombatInputSession session, out ActorReadSnapshot snapshot);
        bool TryReadActor(CombatActorHandle actor, out ActorReadSnapshot snapshot);
    }
}
