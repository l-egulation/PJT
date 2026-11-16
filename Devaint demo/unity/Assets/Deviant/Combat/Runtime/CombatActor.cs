using Deviant.Definitions;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using UnityEngine;

namespace Deviant.Combat
{
    public enum FootstepMaterial { Dirt, Grass, Stone, Wood }
    public class ArenaSurface : MonoBehaviour
    {
        public const int TerrainLayer = 8;
        void Awake() => ArenaSurfaceQueries.Register(this);
        void OnEnable() => ArenaSurfaceQueries.Register(this);
        // Keep custom runtime layers eligible without requiring reactivation.
        internal static int QueryMask => Application.isPlaying
            ? Physics2D.DefaultRaycastLayers & ~1 : Physics2D.DefaultRaycastLayers;
        internal static ContactFilter2D QueryFilter => new ContactFilter2D
            { useTriggers = Physics2D.queriesHitTriggers, useLayerMask = true, layerMask = QueryMask };
        public bool oneWay, blockDropThrough;
        public FootstepMaterial footstepMaterial;
    }
    public class ArenaPit : MonoBehaviour { }
    public class HurtRegion : MonoBehaviour { public CombatActor actor; }

    public partial class CombatActor : MonoBehaviour
    {
        static int nextActorId;
        public int ActorId { get; private set; }
        public int LifeVersion { get; private set; }
        // Accepted hits since reset, including zero or fully absorbed damage.
        readonly IActorVitals vitals = new ActorVitalsState(100);
        public ActorVitalsSnapshot Vitals => InkVitals.Read();
        public int ReceivedHitCount => vitals.Read().ReceivedHits;
        public CombatArena arena;
        public bool isPlayer, isBoss, isDecoy, isElite;
        internal SkillEffectDefinition DecoyDefinition { get; set; }
        internal string DecoyDefinitionVersion { get; set; }
        public float maxHealth { get => vitals.Read().Maximum; set => vitals.WriteMaximum(value); }
        public float health { get => vitals.Read().Health; set => vitals.WriteHealth(value); }
        public float shield { get => vitals.Read().Shield; set => vitals.WriteShield(value); }
        public float guardUntil { get => vitals.Read().GuardUntil; set => vitals.WriteGuardUntil(value); }
        public float guardReduction { get => vitals.Read().GuardReduction; set => vitals.WriteGuardReduction(value); }
        readonly IActorStatuses statuses = new ActorStatusState();
        public ActorStatusSnapshot Statuses => statuses.Read();
        public float stunUntil { get => statuses.Read().StunUntil; set => statuses.WriteStunUntil(value); }
        public float rootUntil { get => statuses.Read().RootUntil; set => statuses.WriteRootUntil(value); }
        public float staggerUntil { get => statuses.Read().StaggerUntil; set => statuses.WriteStaggerUntil(value); }
        public float moveLockUntil { get => statuses.Read().InputLockUntil; set => statuses.WriteInputLockUntil(value); }
        public float knockUntil { get => statuses.Read().KnockUntil; set => statuses.WriteKnockUntil(value); }
        public float knockSpeed { get => statuses.Read().KnockSpeed; set => statuses.WriteKnockSpeed(value); }
        public float stealthUntil { get => statuses.Read().StealthUntil; set => statuses.WriteStealthUntil(value); }
        public float smokeUntil { get => statuses.Read().SmokeUntil; set => statuses.WriteSmokeUntil(value); }
        // 숨죽이기 은신을 공격으로 직접 깼을 때의 공속 보너스 종료 시각(자연 만료는 stealthUntil 로 계산).
        public float stealthExitHasteUntil;
        public float expiredAt;
        readonly IActorKinematics kinematics = new ActorKinematicsState();
        ActorJumpApexState jumpApex;
        int jumpApexRevision;
        public void BeginJumpApex() { jumpApexRevision++; jumpApex = isPlayer ? new ActorJumpApexState(true, 0) : default; }
        public void CancelJumpApex() { jumpApexRevision++; jumpApex = default; }
        public bool JumpApexArmed => jumpApex.Armed;
        // Shortens a voluntary rise while keeping its natural-apex hang armed.
        public void ScaleJumpRise(float scale) { if (scale < 1 && kinematics.VelocityY > 0) kinematics.WriteVelocityY(kinematics.VelocityY * scale); }
        public ActorKinematicsSnapshot Kinematics => kinematics.Read();
        public Vector2 velocity { get => new Vector2(kinematics.VelocityX, kinematics.VelocityY); set { CancelJumpApex(); kinematics.WriteVelocity(value.x, value.y); } }
        public float VelocityX { get => kinematics.VelocityX; set => kinematics.WriteVelocityX(value); }
        public float VelocityY { get => kinematics.VelocityY; set { CancelJumpApex(); kinematics.WriteVelocityY(value); } }
        public int facing { get => kinematics.Facing; set => kinematics.WriteFacing(value); }
        public bool grounded { get => kinematics.Grounded; set => kinematics.WriteGrounded(value); }
        public bool wallHit { get => kinematics.WallHit; set => kinematics.WriteWallHit(value); }
        public Vector2 size = new Vector2(.9375f, 2), offset = new Vector2(.1875f, 1);
        public SpriteRenderer visual;
        public BoxCollider2D bodyCollider, hurtCollider;
        Vector2? independentHurtOffset;
        public void ConfigureHurtbox(Vector2 hurtSize, Vector2 hurtOffset)
        {
            independentHurtOffset = hurtOffset;
            hurtCollider.size = hurtSize;
            hurtCollider.offset = new Vector2(hurtOffset.x * facing, hurtOffset.y);
        }
        // Area ink measures to the nearest point of the enabled hurt box, not to the body centre, so a tall
        // body (the clock boss) is reachable from a feet-level field. No enabled hurt box falls back to Center.
        // Where homing shots steer: the enabled hurt box centre, else Center.
        public Vector2 AimPoint => HurtBoxActive(hurtCollider) ? (Vector2)hurtCollider.bounds.center : Center;
        public virtual Vector2 ClosestHurtPoint(Vector2 point)
            => HurtBoxActive(hurtCollider) ? ClampToBox(HurtBox(hurtCollider), point) : Center;
        // Box sweeps of area ink (slide, wave, dash) test the enabled hurt box; without one, the body box at Center.
        public virtual bool HurtOverlaps(Bounds area)
            => Overlap2D(HurtBoxActive(hurtCollider) ? HurtBox(hurtCollider) : new Bounds(Center, size), area);
        protected static bool HurtBoxActive(BoxCollider2D box) => box && box.enabled && box.gameObject.activeInHierarchy;
        // Computed from the transform instead of Collider2D.bounds so it does not wait for a physics transform sync.
        protected static Bounds HurtBox(BoxCollider2D box)
        {
            var t = box.transform; Vector3 scale = t.lossyScale; Vector2 at = t.TransformPoint(box.offset);
            return new Bounds(at, new Vector3(box.size.x * Mathf.Abs(scale.x), box.size.y * Mathf.Abs(scale.y), 0));
        }
        protected static Vector2 ClampToBox(Bounds box, Vector2 point)
            => new Vector2(Mathf.Clamp(point.x, box.min.x, box.max.x), Mathf.Clamp(point.y, box.min.y, box.max.y));
        protected static bool Overlap2D(Bounds a, Bounds b)
            => a.min.x <= b.max.x && b.min.x <= a.max.x && a.min.y <= b.max.y && b.min.y <= a.max.y;
        public CombatCast currentCast;
        public bool Dead => vitals.Read().Dead;
        public bool Hidden => statuses.Read().HiddenAt(arena.Now);
        public void SetKnockMotion(float until, float speed) => statuses.SetKnockMotion(until, speed);
        public void ClearHitMotionLocks() => statuses.ClearHitMotionLocks();
        public void ClearConcealment() => statuses.ClearConcealment();
        public Vector2 Position
        {
            get => new Vector2(kinematics.X, kinematics.Y);
            set { CancelJumpApex(); ActorKinematicsBinding.SetPosition(kinematics, transform, value.x, value.y, 0); }
        }
        // Explicit native depth setup; Z is view-only and never enters the owner.
        public void SetPosition(Vector3 value) { CancelJumpApex(); ActorKinematicsBinding.SetPosition(kinematics, transform, value.x, value.y, value.z); }
        public Vector2 Center { get { return Position + new Vector2(offset.x * facing, offset.y); } }
        readonly IActorPlatforms platforms = new ActorPlatformState();
        public ActorPlatformSnapshot Platforms => platforms.Read();
        UnityActorPlatforms platformBindings;
        UnityActorPlatforms PlatformBindings => platformBindings ?? (platformBindings = new UnityActorPlatforms(platforms));
        IActorPlacementQuery surfaces;
        protected IActorPlacementQuery SurfaceQuery => surfaces ?? (surfaces = UnityActorSurfaceQuery.ForActor(PlatformBindings.Contains));
        public ActorBodyFrame BodyFrame
        {
            get { return new ActorBodyFrame(kinematics.X, kinematics.Y, size.x, size.y, offset.x, offset.y, facing); }
        }

        public void Configure(CombatArena owner, Vector2 at, Vector2 bodySize, Vector2 bodyOffset, bool player)
        {
            ActorId = ++nextActorId; arena = owner; Position = at; size = bodySize; offset = bodyOffset; isPlayer = player;
            independentHurtOffset = null;
            var rb = gameObject.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0; rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            bodyCollider = gameObject.AddComponent<BoxCollider2D>(); bodyCollider.size = size;
            var hurt = new GameObject("Hurtbox"); hurt.transform.SetParent(transform, false);
            hurtCollider = hurt.AddComponent<BoxCollider2D>(); hurtCollider.size = size; hurtCollider.isTrigger = true;
            hurt.AddComponent<HurtRegion>().actor = this;
            SyncFacing(facing);
        }

        public virtual bool SyncFacing(int direction)
        {
            var host = new FacingHost(this);
            return ActorFacingExecution.Sync(direction, ref host);
        }
        struct FacingHost : IActorFacingHost
        {
            readonly CombatActor actor;
            public FacingHost(CombatActor actor) { this.actor = actor; }
            public int Facing { get => actor.facing; set => actor.facing = value; }
            public float OffsetX => actor.offset.x;
            public float OffsetY => actor.offset.y;
            public void ReadPosition(out float x, out float y) { x = actor.kinematics.X; y = actor.kinematics.Y; }
            public ActorBodyFrame BodyFrame => actor.BodyFrame;
            public IActorPlacementQuery SurfaceQuery => actor.SurfaceQuery;
            public void SetPosition(float x, float y) => ActorKinematicsBinding.SetPosition(actor.kinematics, actor.transform, x, y, 0);
            public bool HasBody => actor.bodyCollider;
            public void SetBodyOffset(float x, float y)
            {
                var at = actor.bodyCollider.offset;
                if (at.x != x || at.y != y) actor.bodyCollider.offset = new Vector2(x, y);
            }
            public bool HasHurt => actor.hurtCollider;
            public void SetHurtOffset(float x, float y)
            {
                var offset = actor.independentHurtOffset;
                var value = offset.HasValue ? new Vector2(offset.Value.x * actor.facing, offset.Value.y) : new Vector2(x, y);
                var at = actor.hurtCollider.offset;
                if (at.x != value.x || at.y != value.y) actor.hurtCollider.offset = value;
            }
            public bool HasVisual => actor.visual;
            public void SetVisualFlip(bool flip) => actor.visual.flipX = flip;
            public bool IsPlayer => actor.isPlayer;
            public void SetVisualPosition(float x, float y) => ActorKinematicsBinding.SetVisualPosition(actor.kinematics, actor.transform, actor.visual.transform, x, y);
        }
        public bool Blocked(Vector2 pos, int direction) => ActorPlacementResolver.Blocked(BodyFrame, pos.x, pos.y, direction, SurfaceQuery);
        public bool IsForcedMoving
        {
            get { return !HorizontalKnockback && (currentCast && currentCast.Moving || isPlayer && arena.Player.Dashing); }
        }
        // Opt-in horizontal external knockback. Its owner must clear this on reset/cancel.
        public bool HorizontalKnockback {get;set;}

        public bool Receive(float damage, CombatActor source, int hitDirection, bool basic, HitData control, bool bypass = false, bool projectileHit = false, CombatHitOrigin origin = default, CombatHitContact contact = default)
            => ReceiveResolved(damage, source, hitDirection, basic, control, null, bypass, projectileHit, origin, contact);
        public bool ReceiveControl(float damage, CombatActor source, int hitDirection, bool basic, SkillHitDefinition control, bool bypass = false, bool projectileHit = false, CombatHitOrigin origin = default, CombatHitContact contact = default)
            => ReceiveResolved(damage, source, hitDirection, basic, null, control, bypass, projectileHit, origin, contact);
        // A maintenance augment's extra damage: already final, so it is not scaled, counted or turned into lifesteal again.
        internal bool ReceiveAugmentExtra(float damage, CombatActor source, int direction)
            => ReceiveResolved(damage, source, direction, false, null, null, false, false, default, default, true);
        internal bool ReceiveRunInkDamage(float damage, CombatActor source, CombatHitOrigin origin)
            => ReceiveResolved(damage, source, 0, false, null, null, false, false, origin, default, true);
        // Successful parries by this actor's casts. A single-hit attack checks it after Receive so a
        // parried hit is consumed once instead of being parried (and countered) again every frame.
        public int ParryCount { get; private set; }
        internal void NoteParry() => ParryCount++;
        bool suppressMeleeParry;
        // An enemy hit whose parry rule is the caller's: parryOrigin decides the facing check (the target's own centre
        // accepts either facing), parryCounter deals the shield's melee counter to the source. Neither path reflects.
        public bool ReceiveWithParry(float damage, CombatActor source, int hitDirection, Vector2? parryOrigin, bool parryCounter, out bool parried, bool bypass = false, bool projectileHit = false)
        {
            parried = false;
            if (isPlayer && !bypass && !Dead && !arena.Player.Invulnerable && currentCast &&
                currentCast.TryParry(source, !parryCounter, parryOrigin, parryCounter ? null : "attack parried"))
            { parried = true; return false; }
            suppressMeleeParry = true;
            try { return Receive(damage, source, hitDirection, false, null, bypass, projectileHit); }
            finally { suppressMeleeParry = false; }
        }
        internal bool ReceiveShieldRetaliation(float damage, CombatActor source, int direction)
            => ReceiveResolved(damage, source, direction, false, null, null, true, false, default, default, true);
        bool ReceiveResolved(float damage, CombatActor source, int hitDirection, bool basic, HitData sourceControl, SkillHitDefinition control, bool bypass, bool projectileHit, CombatHitOrigin origin, CombatHitContact contact, bool fixedRetaliation = false)
        {
            if (!AcceptsHitOrigin(origin)) return false;
            var owner = arena;
            bool hadSession = owner.ReadModel.TryGetSession(out var session);
            if (!contact.HasPoint)
            {
                var at = hurtCollider ? (Vector2)hurtCollider.bounds.center : Center;
                contact = new CombatHitContact(at.x, at.y, hitDirection, 0, HitAnchorKind.TargetFallback);
            }
            if (!bypass && isPlayer && arena.Player.Dashing && arena.Player.Invulnerable)
                arena.RecordInkDodge(source, origin.IsSpecified ? origin : arena.DirectHitOrigin(source));
            float healthBefore = health;
            bool critical = false;
            if (!fixedRetaliation && source && source.isPlayer)
            { damage = arena.ScalePlayerDamage(arena.InkOriginalDamage(origin, damage, this), origin, this); critical = arena.LastHitWasCritical; }
            else if (isPlayer) damage *= arena.PlayerIncomingScale * (fixedRetaliation ? 1 : arena.AugmentIncomingScale(source, origin, projectileHit));
            var host = new DamageHost(this, source, origin.IsSpecified ? origin : arena.DirectHitOrigin(source), contact, hitDirection, critical);
            if (!ActorDamageExecution.Receive(damage, hitDirection, basic, sourceControl, control, bypass, projectileHit, ref host)) return false;
            // Existing callbacks may reset the arena. The committed old hit
            // remains accepted, but cannot postprocess the fresh generation.
            if (this && arena == owner && owner.ReadModel.TryGetSession(out var current) == hadSession && (!hadSession || current.Equals(session)))
            {
                OnAcceptedHit(source, hitDirection);
                if (source && source.isPlayer && (control != null ? control.Root > 0 || control.Stun > 0 : sourceControl != null && (sourceControl.root > 0 || sourceControl.stun > 0)))
                    owner.SketchWeaponStatus(origin, this);
                if (isDecoy && Dead) GetComponent<ShieldDecoyRuntime>()?.Evaluate(false);
                // Run modifiers: invulnerability after a hit, and thorns back to the monster that struck.
                if (isPlayer && source && !source.isPlayer && !source.Dead)
                {
                    if (owner.PlayerHitProtectSeconds > 0) owner.PlayerHitProtectUntil = owner.Now + owner.PlayerHitProtectSeconds;
                    if (owner.PlayerThornsDamage > 0 && source is TestEnemy) source.Receive(owner.PlayerThornsDamage, this, -hitDirection, false, null);
                }
                OnAcceptedHitOrigin(origin);
                // Maintenance augments: the player's hit counters and extras, and the hurt reset of 무사고 장인.
                if (source && source.isPlayer && !isPlayer) owner.AfterAugmentHit(this, origin, healthBefore, !fixedRetaliation);
                if (isPlayer && health < healthBefore) owner.AugmentPlayerHurt();
                owner.FlushInkHits();
            }
            return true;
        }
        protected virtual void OnAcceptedHit(CombatActor source, int hitDirection) { }
        protected virtual bool AcceptsHitOrigin(CombatHitOrigin origin) => true;
        protected virtual void OnAcceptedHitOrigin(CombatHitOrigin origin) { }
        protected virtual bool AcceptsIncomingDamage => true;
        protected virtual bool AcceptsIncomingControl => true;
        public bool AllowsControlEffects => AcceptsIncomingControl;
        // Boss-only stun/root internal cooldowns; every player control path asks here before writing the status.
        BossControlCooldowns bossControl;
        public BossControlCooldowns BossControl => bossControl ??= new BossControlCooldowns(BossControlCooldownSettings.Settings.stunSeconds, BossControlCooldownSettings.Settings.rootSeconds);
        public bool TryAcceptBossStun() => !isBoss || BossControl.TryStun(arena.Now);
        public bool TryAcceptBossRoot() => !isBoss || BossControl.TryRoot(arena.Now);
        public bool BossStunOpen => !isBoss || BossControl.CanStun(arena.Now);
        public bool BossRootOpen => !isBoss || BossControl.CanRoot(arena.Now);
        // 캐릭터 버프칸의 원본. 잉크 런타임이 장착본별 내부 쿨타임을 넣는다(없으면 칸을 그리지 않는다).
        public IBuffSlotSource BuffSlotSource { get; set; }
        protected virtual float MinimumHealthAfterDamage => 0;
        protected virtual bool ReceivesAutomaticHitReaction => true;
        // Fixed in place: hits, pushes and walking never change its horizontal position. Gravity still applies.
        public virtual bool Immovable => false;
        public bool AutomaticHitReactionEnabled => ReceivesAutomaticHitReaction;
        protected virtual float HitStaggerSeconds => arena.EnemyHitReaction.BasicStaggerSeconds;
        protected virtual bool CancelsAttackOnIncomingHit => true;
        struct DamageHost : IActorDamageHost, IActorDamagePolicy, IActorControlGate, IActorDeathInterceptor
        {
            public bool TryAcceptStun(float now) => actor.TryAcceptBossStun();
            public bool TryAcceptRoot(float now) => actor.TryAcceptBossRoot();
            readonly CombatActor actor, source;
            readonly CombatHitOrigin origin;
            readonly CombatActorHandle target;
            readonly CombatHitContact contact;
            readonly bool critical;
            readonly float automaticPushLimit;
            public DamageHost(CombatActor actor, CombatActor source, CombatHitOrigin origin, CombatHitContact contact, int direction, bool critical)
            {
                this.actor = actor; this.source = source; this.origin = origin;
                this.contact = contact; this.critical = critical;
                automaticPushLimit = actor.arena.MeleePushDistanceLimit(source, actor, origin, direction);
                actor.arena.TryBindActor(actor, out target);
            }
            public IActorVitals Vitals => actor.InkVitals;
            public bool TryPreventDeath() => actor is CombatPlayer player && player.TryReviveFromRun();
            public bool AcceptsIncomingDamage => actor.AcceptsIncomingDamage;
            public bool AcceptsIncomingControl => actor.AcceptsIncomingControl;
            public float MinimumHealthAfterDamage => actor.MinimumHealthAfterDamage;
            public IActorStatuses Statuses => actor.statuses;
            public float Now => actor.arena.Now;
            public bool IsPlayer => actor.isPlayer;
            public bool IsBoss => actor.isBoss;
            public bool IsDecoy => actor.isDecoy;
            public bool SourceIsPlayer => source && source.isPlayer;
            public float BasicStaggerSeconds => actor.ReceivesAutomaticHitReaction ? actor.HitStaggerSeconds : 0;
            public float BasicPushDistance => actor.ReceivesAutomaticHitReaction ? actor.arena.EnemyHitReaction.BasicPushDistance * PushScale : 0;
            float PushScale => source && source.isPlayer ? actor.arena.PlayerPushScale : 1;
            public float BasicPushSeconds => actor.arena.EnemyHitReaction.BasicPushSeconds;
            // Damage-only ink retains action attribution without replaying its native push/stagger.
            public float ActionPushDistance => actor.ReceivesAutomaticHitReaction && origin.Kind != CombatHitKind.InkPart && actor.arena.FeelMotion != null && actor.arena.FeelMotion.TryGet(origin.ActionId, out var m) ? m.push * PushScale : 0;
            public float ActionPushSeconds => origin.Kind != CombatHitKind.InkPart && actor.arena.FeelMotion != null && actor.arena.FeelMotion.TryGet(origin.ActionId, out var m) ? m.pushSeconds : 0;
            public float LimitAutomaticPush(float distance) => Mathf.Min(distance, automaticPushLimit);
            public bool PlayerInvulnerable => actor.arena.Player.Invulnerable;
            public bool IsForcedMoving => actor.IsForcedMoving;
            public bool TryParryMelee() => !actor.suppressMeleeParry && actor.currentCast && actor.currentCast.TryParry(source, false);
            public void AcceptedHit(float requested, ActorVitalsSnapshot before, ActorDamageResult applied, float now)
            {
                actor.arena.RecordAcceptedHit(origin, target, requested, before, applied, actor.Kinematics, now, contact, critical);
                if(applied.Damage>0)actor.arena.ApplyPlayerHitReaction(actor,source,origin);
            }
            public void CancelAttack() { if (actor.Dead || actor.CancelsAttackOnIncomingHit && !(actor.isBoss && actor.arena.Sketch != null)) actor.CancelAttack(); }
            public void RecordDamage(float damage) => actor.arena.Record(actor.name + " damage=" + CombatDamageVariance.Format(damage));
            public void DiePlayer() => actor.arena.Player.Die();
            public void ApplyDeathVisual() { if (actor.visual) actor.visual.color = Color.gray; }
        }
        public virtual void CancelAttack() { if (currentCast) currentCast.Cancel(); currentCast = null; }

        public void PhysicsStep(float dt, float desiredX, bool freezeVertical, float? controlledVertical = null)
        {
            if(HorizontalKnockback){freezeVertical=true;controlledVertical=null;}
            var host = new MotorHost(this, controlledVertical, dt);
            int revision = jumpApexRevision;
            var motion = ActorMotorExecution.Step(dt, desiredX, freezeVertical, ref host);
            // Contact writes may set an already-zero apex velocity to zero again.
            // Their cancellation must survive committing the proposal's timer.
            if (grounded || VelocityY != motion.VelocityY) CancelJumpApex();
            else if (revision == jumpApexRevision) jumpApex = motion.Apex;
        }
        struct MotorHost : IActorMotorHost
        {
            readonly CombatActor actor;
            readonly float? controlledVertical;
            readonly float delta;
            public MotorHost(CombatActor actor, float? controlledVertical, float delta) { this.actor = actor; this.controlledVertical = controlledVertical; this.delta = delta; }
            public float Now => actor.arena.Now;
            public void ExpireShield(float now) => actor.vitals.ExpireShield(now);
            public void RefreshPlatforms(float now) => actor.platforms.Refresh(now, actor.Position.y, actor.size.y, actor.PlatformBindings);
            public void PruneUnusedPlatforms() => actor.PlatformBindings.PruneUnused();
            public ActorBodyFrame BodyFrame => actor.BodyFrame;
            public IActorSurfaceQuery SurfaceQuery => actor.SurfaceQuery;
            public float VelocityY { get => actor.VelocityY; set => actor.VelocityY = value; }
            public bool Grounded { get => actor.grounded; set => actor.grounded = value; }
            public bool WallHit { set => actor.wallHit = value; }
            // An immovable actor keeps hit/knock timers but never turns them into horizontal travel.
            public ActorMotionInput ReadMotionInput(float now, float step, float desiredX, bool freezeVertical)
                => new ActorMotionInput(now, step, actor.Immovable ? 0 : desiredX * (actor.isPlayer || actor.IsForcedMoving ? 1 : actor.arena.InkMovementScale(actor)), actor.VelocityY, actor.grounded, freezeVertical,
                    actor.IsForcedMoving, actor.Dead, actor.isDecoy, actor.Immovable ? 0 : actor.statuses.KnockSpeedAt(now, step), actor.knockUntil, actor.moveLockUntil,
                    actor.isBoss && actor.arena.Sketch != null ? 0 : actor.stunUntil, actor.isBoss && actor.arena.Sketch != null ? 0 : actor.rootUntil, actor.staggerUntil, actor.arena.Movement.RiseGravity, actor.arena.Movement.FallGravity, controlledVertical,
                    actor.isPlayer ? actor.jumpApex : default, actor.isPlayer ? actor.arena.Movement.JumpApexSeconds : 0);
            public void SetVelocity(float x, float y) => actor.kinematics.WriteVelocity(x, y);
            public void MovePosition(float deltaX, float deltaY)
            {
                var from = actor.Position;
                ActorKinematicsBinding.SetPosition(actor.kinematics, actor.transform, actor.kinematics.X + deltaX, actor.kinematics.Y + deltaY, 0);
                if (actor.isPlayer) actor.arena.RecordFootSweep(from, actor.Position, actor.size.x);
                var offset = new Vector2(actor.offset.x * actor.facing, actor.offset.y);
                var start = from + offset; var end = actor.Position + offset;
                actor.arena.RecordEnvironmentBody(new EnvironmentBodySweep(actor.ActorId, start.x, start.y, end.x, end.y, actor.size.x, actor.size.y, delta));
            }
            public void SyncTransforms() { ActorKinematicsBinding.SyncPosition(actor.kinematics, actor.transform); Physics2D.SyncTransforms(); }
        }
        public bool ProbeGround() => ActorContactResolver.ProbeGround(BodyFrame, VelocityY, SurfaceQuery);
        public bool ProbeSafeFloor(out float height) => ActorPlacementResolver.ProbeSafeFloor(BodyFrame, VelocityY, SurfaceQuery, out height);
        public bool DropThrough()
        {
            try
            {
                var motion = platforms.TryDrop(BodyFrame, VelocityY, arena.Now, SurfaceQuery, PlatformBindings);
                if (!motion.Applied) return false;
                Position += new Vector2(0, motion.DeltaY); VelocityY = motion.VelocityY; grounded = false; return true;
            }
            finally { platformBindings?.PruneUnused(); }
        }
        public void TeleportToward(Vector2 destination, bool crossWalls = false)
        {
            var motion = ActorPlacementResolver.TeleportToward(BodyFrame, destination.x, destination.y, SurfaceQuery, crossWalls);
            if (motion.Skipped) return;
            if (motion.Applied) Position = new Vector2(motion.X, motion.Y);
            Physics2D.SyncTransforms();
        }
        // Caller validates destination support and body clearance. Unlike ResetActor this
        // preserves health, status effects, equipment, cooldowns and enemies.
        protected void RelocateToCheckpoint(Vector2 destination, int direction)
        {
            platforms.Reset(); platformBindings?.PruneUnused();
            Position = destination; velocity = Vector2.zero; SyncFacing(direction);
            Physics2D.SyncTransforms(); grounded = ProbeGround(); wallHit = false;
        }
        public void ResetActor(Vector2 spawn)
        {
            HorizontalKnockback=false;
            LifeVersion++;
            CancelAttack(); Position = spawn; velocity = Vector2.zero; vitals.Reset();
            statuses.Reset(); bossControl?.Reset(); guardUntil = 0; stealthExitHasteUntil = 0;
            platforms.Reset(); platformBindings?.PruneUnused(); grounded = false;
        }
        void OnDrawGizmos()
        {
            if (arena && !arena.showBoxes) return;
            Gizmos.color = isPlayer ? Color.cyan : Color.red;
            if (hurtCollider) Gizmos.DrawWireCube(hurtCollider.bounds.center, hurtCollider.bounds.size);
            else Gizmos.DrawWireCube(Center, size);
        }
    }
}
