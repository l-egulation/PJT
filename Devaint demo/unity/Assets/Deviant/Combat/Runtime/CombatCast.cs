using System;
using System.Collections.Generic;
using Deviant.Definitions;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatCast : MonoBehaviour
    {
        public CombatArena arena;
        public CombatActor owner;
        public SkillExecutionDefinition Definition { get; private set; }
        public bool isBasic;
        public string DefinitionVersion { get; private set; }
        public bool UsesCompiledTracks => Definition.UsesCompiledTracks;
        public ActionTrackDefinition ActionTracks => Definition.Tracks;
        public DirectMeleeSpecialDefinition DirectMeleeSpecial { get; private set; }
        public RangedActionDefinition RangedAction { get; private set; }
        public string VerticalPolicy => DirectMeleeSpecial != null || ChargedMelee != null ? "normal" : Definition.Vertical;
        bool ResetsVerticalVelocity => UsesCompiledTracks ? ActionTracks.Vertical == ActionVerticalControl.FreezeDuringMotionThenFallFromZero : Definition.Vertical.StartsWith("freeze");
        public System.Guid ExecutionId { get; } = System.Guid.NewGuid();
        WeaponState weapon;
        public Guid SourceWeaponId => weapon?.InstanceId ?? Guid.Empty;
        CombatAttackStamp attackStamp;
        IActionResources resources;
        public ActionResourceResult ResourceResult => resources?.Current;
        public bool ResourcesActive => resources != null && resources.IsActive;
        float started, damageMultiplier, dt, physicalStep;
        bool targetHit;
        CastLifecycleExecution lifecycle;
        bool CastActivated => chargedMeleeExecution?.Activated ?? lifecycle.Activated;
        bool CastCancelled => chargedMeleeExecution?.Cancelled ?? lifecycle.Cancelled;
        bool CastMotionDone => chargedMeleeExecution?.MotionDone ?? lifecycle.MotionDone;
        public bool PresentationCancelled => CastCancelled || meleeAction != null && meleeAction.State == MeleeActionState.Cancelled;
        public bool PresentationActivated => CastActivated;
        public bool PresentationMarkedTargetAlive => marked && !marked.Dead;
        ChargeExecution charge;
        public ChargeDefinition ChargePolicy => charge?.Definition;
        public ChargeReleaseTiming? ChargeRelease => charge?.Release;
        public bool ChargeActive => charge != null && charge.IsActive;
        int direction;
        BombReleaseExecution bombRelease;
        Vector2 savedPose, previousOwner, installCenter;
        CombatActor marked;
        HitLedger ledger;
        readonly List<Bounds> debugBoxes = new List<Bounds>();
        public int DebugBoxCount => debugBoxes.Count;
        public Bounds GetDebugBox(int index) => debugBoxes[index];
        SmokeEffectExecution smokeEffect;
        ForcedDashExecution forcedDash;
        CastTeleportExecution teleport;
        SkillHitTrackExecution hitTrack = new SkillHitTrackExecution(-.000001f);
        CastMotionExecution motion;
        RangedRecoilExecution rangedRecoil;
        ApproachRecastExecution approachRecast;
        public bool Approaching { get { var host = new ApproachRecastHost(this); return approachRecast.IsApproaching(ref host); } }
        public float DashVisualDuration => approachRecast.VisualDuration;
        public float RecastRemaining { get { var host = new ApproachRecastHost(this); return approachRecast.Remaining(ref host); } }
        public float RecastTimeFor(WeaponState state) { return state==weapon ? RecastRemaining : 0; }
        public bool Recoiling { get { return MissileRecoiling || rangedRecoil.IsActive(arena.Now, CastCancelled); } }
        public float StartedAt => started;
        public float MotionDuration => meleeAction?.Definition.End ?? charge?.Definition.BaseEnd ?? Definition.End;
        // Presentation reads the existing shortened deadline in the same logical clock as Age.
        internal float PresentationMotionDuration => !isBasic && (Definition.Id == "base_sword_movement" || Definition.Id == "hammer_movement")
            || ChargedRangedReleased ? lifecycle.MotionEnd
            : Definition.Id == "rapier_default" && ChargeRelease.HasValue ? ChargeRelease.Value.MotionEnd : MotionDuration;
        // A released ranged charge shot plays its shot and recovery after the release, not against the unshifted base end.
        bool ChargedRangedReleased => charge?.Release != null && chargedMeleeExecution == null && Definition.Kind == "ranged";
        public float EndTime
        {
            get
            {
                if(InkUntimed)return meleeAction?.EndTime ?? chargedMeleeExecution?.EndTime ?? started+lifecycle.MotionEnd;
                float end=meleeAction?.EndTime ?? chargedMeleeExecution?.EndTime ?? started+lifecycle.MotionEnd;
                return started+InkUnmap(end-started);
            }
        }
        // Logical action time: presentation follows a shortened windup with the hit.
        public float Age { get { return meleeAction?.Age(InkNow) ?? Mathf.Max(0, Elapsed); } }
        public float ImpactTime { get { return meleeAction?.Definition.Impact ?? chargedMeleeExecution?.ImpactTime ?? (Definition.Effect?.Type != "smoke" && Definition.Hits.Count > 0 ? HitTime(Definition.Hits[0]) : lifecycle.Activation); } }
        public void FollowBasicFacing(int facing) { if (isBasic) direction=facing; }
        float Elapsed { get { return InkNow - started; } }
        float HitTime(SkillHitDefinition h) { return h.Time + (charge?.Release?.Shift ?? 0); }
        public bool Moving
        {
            get { var host = new CastMotionHost(this); return CastMotionExecution.Moving(ref host); }
        }
        public bool FreezeVertical
        {
            get { var host = new CastMotionHost(this); return CastMotionExecution.FreezeVertical(Definition, ref host); }
        }
        public bool Invulnerable
        {
            get { var host = new CastMotionHost(this); return CastMotionExecution.Invulnerable(Definition, ref host); }
        }
        public static CombatCast Create(CombatArena arena, CombatActor owner, SkillData source, WeaponState weapon, bool basic, float startAt = -1, string definitionVersion = null, MeleeActionDefinition timeline = null, MeleeFrameDefinition frameTiming = null,
            IActionResourceSource resourceSource = null, DirectMeleeSpecialDefinition specialDefinition = null, ChargedMeleeDefinition chargedDefinition = null, ActionTrackDefinition actionTracks = null, CombatCommandKey command = default, AuthoredSkillDefinition authored = null, RangedActionDefinition rangedDefinition = null, ActionSpecialFrameDefinition specialFrame = null, Deviant.Equipment.Contracts.InkPaletteSnapshot inkPalette = null,
            float windupScale = 1)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            if (!(windupScale > 0 && windupScale <= 1)) throw new ArgumentOutOfRangeException(nameof(windupScale));
            ValidateSpecialFrameStart(arena, source, basic, definitionVersion, timeline, frameTiming, specialDefinition, chargedDefinition, actionTracks, authored, rangedDefinition, specialFrame);
            if (chargedDefinition != null && (basic || timeline != null || frameTiming != null || specialDefinition != null || definitionVersion != chargedDefinition.ContentHash ||
                !ChargedMeleeDefinition.TryCreate(chargedDefinition.FormId, source, out var chargedPayload, out _) || chargedPayload.ContentHash != chargedDefinition.ContentHash))
                throw new ArgumentException("The charged melee cast requires its complete matching start definition.");
            if (specialDefinition != null && (basic || timeline == null || source.id != specialDefinition.Id || definitionVersion != specialDefinition.ContentHash ||
                timeline.ContentHash != (frameTiming == null ? specialDefinition.Compile().ContentHash : frameTiming.CompileForStart(1).ContentHash) ||
                !DirectMeleeSpecialDefinition.TryCreate(specialDefinition.FormId, source, out var payloadDefinition, out _) || payloadDefinition.ContentHash != specialDefinition.ContentHash))
                throw new ArgumentException("The direct special requires its complete matching start definition.");
            if (timeline != null && (!basic && specialDefinition == null || !string.IsNullOrEmpty(source.kind) || source.id != timeline.Id || timeline.SourceVersion != definitionVersion))
                throw new ArgumentException("The melee runner requires its matching versioned melee action.");
            if (frameTiming != null && (timeline == null || frameTiming.Mode != MeleeTimingMode.FrameTime || frameTiming.Id != source.id ||
                (basic ? frameTiming.Family != MeleeFrameFamily.Basic || frameTiming.Action.ContentHash != timeline.SourceVersion :
                    frameTiming.Family != MeleeFrameFamily.Direct || frameTiming.Direct.ContentHash != definitionVersion)))
                throw new ArgumentException("Frame action and motion must share the same compiled action version.");
            if (actionTracks != null && (basic || timeline != null || frameTiming != null || specialDefinition != null || chargedDefinition != null || source.id != actionTracks.Id || definitionVersion != (specialFrame?.ContentHash ?? actionTracks.ContentHash)))
                throw new ArgumentException("Finite action tracks require their complete matching start definition.");
            if (authored != null && (basic || rangedDefinition != null || timeline != null || frameTiming != null || specialDefinition != null || chargedDefinition != null || actionTracks != null || source.id != authored.Id || definitionVersion != (specialFrame?.ContentHash ?? authored.ContentHash)))
                throw new ArgumentException("An authored cast requires its complete matching captured definition.");
            if (rangedDefinition != null && (basic != rangedDefinition.IsBasic || authored != null || timeline != null || frameTiming != null || specialDefinition != null || chargedDefinition != null || actionTracks != null ||
                source.id != rangedDefinition.RuntimeId || definitionVersion != rangedDefinition.ContentHash || basic && !rangedDefinition.IsStartSnapshot))
                throw new ArgumentException("The ranged cast requires its complete matching immutable start snapshot.");
            var definition = rangedDefinition?.Execution ?? authored?.Execution ?? actionTracks?.Execution ?? new SkillExecutionDefinition(source);
            var chargeDefinition = chargedDefinition?.Policy ?? (definition.ChargeMax > 0 ? new ChargeDefinition(definition.Id, definition.ChargeMin, definition.ChargeMax, definition.ChargeMinDamage, definition.ChargeMaxDamage, definition.End, steps: definition.ChargeSteps) : null);
            if (chargeDefinition != null && timeline != null) throw new ArgumentException("Charge and fixed melee policies cannot execute together.");
            var c = new GameObject("Cast_" + definition.Id).AddComponent<CombatCast>();
            c.arena = arena; c.owner = owner; c.Definition = definition; c.weapon = weapon; c.isBasic = basic;
            c.CaptureMotionActionKey(); // Freeze original weapon identity before any resource/start callback can change selection.
            c.resources = new ActionResourceExecution(new ActionResourceKey(c.ExecutionId, weapon.InstanceId, basic), resourceSource ?? new WeaponActionResourceSource(weapon));
            c.DefinitionVersion = specialFrame?.ContentHash ?? rangedDefinition?.ContentHash ?? authored?.ContentHash ?? actionTracks?.ContentHash ?? definition.Tracks?.ContentHash ?? definitionVersion; c.FrameTiming = frameTiming; c.DirectMeleeSpecial = specialDefinition;
            c.RangedAction = rangedDefinition;
            c.attackStamp = new CombatAttackStamp(weapon.InstanceId, c.MotionRequestedForm, definition.Id, c.DefinitionVersion, basic,
                basic && MotionSourceRegistry.HasInkAppearance(arena.catalog.forms[weapon.FormIndex].id) ? weapon.SpecialIndex : -1, owner.isPlayer ? inkPalette ?? weapon.InkPalette.Read() : null);
            c.SpecialFrame = specialFrame;
            // A charge's hold time stays under player control; only fixed windups shorten.
            c.switchWindupScale = chargeDefinition == null ? windupScale : 1;
            c.started = startAt >= 0 ? startAt : arena.Now; c.lifecycle = new CastLifecycleExecution(definition.Activation, definition.End);
            c.forcedDash = new ForcedDashExecution(definition.Movement);
            c.teleport = new CastTeleportExecution(definition.Movement);
            c.smokeEffect = new SmokeEffectExecution(definition.Effect);
            c.direction = owner.facing; c.previousOwner = owner.Position;
            if (chargeDefinition != null) c.charge = new ChargeExecution(c.ExecutionId, chargeDefinition, c.started);
            c.BindChargedMelee(chargedDefinition);
            if (chargedDefinition == null) c.ledger = new HitLedger();
            c.BindMeleeAction(timeline);
            if (definition.Hits.Count > 0 && definition.Effect?.Type != "smoke")
            {
                // 기습: 은신 중 시작한 플레이어 직접 타격 공격은 첫 적중 대상 하나에 추가 피해를 담는다(은신은 여기서 풀린다).
                if (owner.isPlayer && owner.Hidden) c.ambushBonus = arena.StealthAmbush.BonusFor(owner.stealthUntil > arena.Now, owner.smokeUntil > arena.Now);
                arena.BreakStealth(owner);
            }
            arena.casts.Add(c);
            c.StartCommand(command);
            if (owner.isPlayer) c.InkAction = arena.BeginInkAction(c.attackStamp,c.ExecutionId,owner);
            if (owner.isPlayer) arena.BeginAugmentAttack(c.attackStamp, c.ExecutionId);
            // Visual compilation reports its own failure and cannot reject an action.
            arena.CaptureCharacterAttack(c);
            ShurikenSkillFx.CastStarted(arena, owner, definition.Id, c.direction);
            return c;
        }
        public void BeforeMovement(float step)
        {
            try
            {
                physicalStep = step; step = InkStep(step); dt = step; previousOwner = owner.Position;
                if (meleeAction != null) { meleeAction.Prepare(InkNow, step); return; }
                if (chargedMeleeExecution != null) { chargedMeleeExecution.BeforeMovement(step, owner.isPlayer && arena.Player.specialHeld); return; }
                var host = new CastLifecycleHost(this); lifecycle.BeforeMovement(step, Definition, charge, ref host);
            }
            catch (Exception exception) { CommandException(exception); throw; }
        }
        public float HorizontalVelocity(int axis)
        {
            if(IsInkPreparing)return axis*arena.Movement.RunSpeed*arena.InkMovementScale(owner);
            var host = new CastMotionHost(this);
            return motion.HorizontalVelocity(axis, Definition, ref host);
        }
        public void AfterMovement()
        {
            if (meleeAction != null || ChargedMelee != null) return;
            if(Definition.Kind=="missile") { AfterMissileMovement(); return; }
            rangedRecoil.AfterMovement(arena.Now,CastCancelled,owner.wallHit,owner.grounded);
            var end = forcedDash.AfterMovement(CastActivated, Elapsed, dt, owner.wallHit);
            if (!end.Ended) return;
            if (end.OverridesMotionEnd) lifecycle.SetMotionEnd(end.MotionEnd);
            if (ResetsVerticalVelocity) owner.VelocityY = 0;
        }
        void Activate()
        {
            var host = new CastActivationHost(this);
            CastActivationExecution.Activate(Definition, ref host);
            if (owner == arena.Player && PresentationActivated && !PresentationCancelled)
                arena.Audio?.EmitSkillEvent(Definition.Id, "activated", owner.Position);
        }
        bool CommitResources()
        {
            var result = resources.TryActivate(ExecutionId, arena.Now);
            if (result.Status != ActionResourceStatus.Succeeded) return false;
            damageMultiplier = result.Snapshot.DamageMultiplier; return true;
        }
        void ReleaseDueBombs()
        {
            var host = new BombReleaseHost(this);
            bombRelease.ReleaseDue(Definition.Hits, ref host);
        }
        struct BombReleaseHost : IBombReleaseHost
        {
            readonly CombatCast cast;
            public BombReleaseHost(CombatCast cast) { this.cast = cast; }
            public bool Cancelled => cast.CastCancelled;
            public bool OwnerExists => cast.owner;
            public bool OwnerDead => cast.owner.Dead;
            public float Elapsed => cast.Elapsed;
            public float ChargeShift => cast.charge?.Release?.Shift ?? 0;
            public void SpawnBomb(int hitIndex, float damage) => cast.arena.SpawnBomb(cast.owner, cast.Definition.Bomb, damage * cast.damageMultiplier, cast.direction, cast.damageMultiplier, cast.HitOrigin(hitIndex, CombatHitKind.Blast));
        }
        public void MarkSmokeSpent() => smokeEffect.MarkSpent();
        void Fail()
        {
            if (chargedMeleeExecution != null) { chargedMeleeExecution.Fail(); return; }
            var host = new CastLifecycleHost(this); lifecycle.Fail(ref host);
        }
        public void Cancel()
        {
            CancelInkCharge();
            if (chargedMeleeExecution != null) chargedMeleeExecution.Cancel();
            else
            {
                var host = new CastLifecycleHost(this);
                if (!lifecycle.Cancel(ref host)) { arena.Audio?.Cancel(this); EndCommand(CombatCommandState.Cancelled, CommandCancellationReason); return; }
            }
            EndCommand(CombatCommandState.Cancelled, CommandCancellationReason);
            arena.Audio?.Cancel(this);
            ClearMissileVisual();
            if (owner && ResetsVerticalVelocity) owner.VelocityY = 0;
        }
        public void Step(float step)
        {
            try
            {
                if (IsInkPreparing) { StepInkCharge(step); return; }
                var host = new CastTickHost(this);
                CastTickExecution.Step(InkStep(step), Definition, ref host);
            }
            catch (Exception exception) { CommandException(exception); throw; }
        }
        // 사거리(range)는 앞쪽 끝까지 늘린다. 범위(radius)는 높이와 뒤쪽 너비만 늘려 앞쪽 끝(직선 사거리)을 그대로 둔다.
        public static CastSweepBox ScaleHitBox(float x, float y, float width, float height, float range, float radius, float uniform = 1)
        {
            x *= uniform; y *= uniform; width *= uniform; height *= uniform;
            x *= range; width *= range; float front = x + width * .5f, wide = width * radius;
            return new CastSweepBox(front - wide * .5f, y, wide, height * radius);
        }
        // "screen" 대상: 발동 순간 카메라에 보이는 영역 전체를 하나의 판정 상자로 쓴다. 고체 차폐는 무시한다.
        public const string ScreenTargets = "screen";
        bool TryScreenBox(Vector2 from, out CastSweepBox screen)
        {
            screen = default;
            var view = Camera.main;
            if (!view || !view.orthographic) return false;
            float height = view.orthographicSize * 2, width = height * view.aspect;
            Vector2 center = view.transform.position;
            // CastSweepExecution은 offsetX에 방향을 곱하므로 미리 곱해 둔다(direction은 ±1).
            screen = new CastSweepBox((center.x - from.x) * direction, center.y - from.y, width, height);
            return true;
        }
        void ApplyBox(int index, SkillHitDefinition h, Vector2 from, Vector2 to, bool effectCenter, CombatHitOrigin hitOrigin = default)
        {
            CastSweepBox? box = h.Box == null ? (CastSweepBox?)null : ScaleHitBox(h.Box.X, h.Box.Y, h.Box.Width, h.Box.Height, (InkAction?.RangeScale ?? 1) * (owner && owner.isPlayer ? arena.PlayerReachScale : 1), (InkAction?.MvpRadiusScale ?? 1) * (owner && owner.isPlayer ? arena.PlayerReachScale : 1), InkAction?.MvpAllGeometryScale ?? 1);
            if (h.Targets == ScreenTargets && TryScreenBox(from, out var screen)) { box = screen; to = from; }
            if (!hitOrigin.IsSpecified) hitOrigin = HitOrigin(index);
            var host = new SweepHost(this, index, h, hitOrigin, effectCenter ? 0 : (box?.X ?? 0) * direction);
            CastSweepExecution.Apply(box, direction, from.x, from.y, to.x, to.y, effectCenter, h.Targets == "nearest", ref host);
            if (owner == arena.Player) arena.FinishSketchHit();
            arena.CompleteDamageBatch();
        }
        void ApplyTarget(int index, SkillHitDefinition h, CombatActor target, CombatHitOrigin hitOrigin = default, CombatHitContact contact = default)
        {
            var host = new CastHitResponseHost(this, index, h, target, hitOrigin, contact);
            hitResponse.ApplyTarget(ref host);
        }
        public void PushTargets(float step)
        {
            var host = new CastPushHost(this);
            hitResponse.PushTargets(step, ref host);
        }
        void EndPrimary()
        {
            var host = new CastHitResponseHost(this);
            hitResponse.EndPrimary(ref host);
        }
        // projectileAttack=false deals the melee counter to the source; true only blocks (the caller decides reflection).
        // attackOrigin set to the owner's own centre accepts the attack from either facing. record overrides the trace label.
        public bool TryParry(CombatActor source, bool projectileAttack, Vector2? attackOrigin = null, string record = null)
        {
            var host = new ParryHost(this, source, attackOrigin, record);
            return CastParryExecution.TryParry(projectileAttack, ref host);
        }
        readonly struct ParryHost : ICastParryHost, ICastParryClaimHost
        {
            readonly CombatCast cast;
            readonly CombatActor source;
            readonly Vector2? attackOrigin;
            readonly string record;
            public ParryHost(CombatCast cast, CombatActor source, Vector2? attackOrigin, string record)
            { this.cast = cast; this.source = source; this.attackOrigin = attackOrigin; this.record = record; }
            public bool Activated => cast.CastActivated;
            public bool Cancelled => cast.CastCancelled;
            public bool EffectExists => cast.Definition.Effect != null;
            public string EffectType => cast.Definition.Effect.Type;
            public float ParryWindow => cast.Definition.Effect?.ParryWindow ?? .15f;
            public float ParryDamage => cast.Definition.Effect?.ParryDamage ?? 30;
            public float Elapsed => cast.Elapsed;
            public bool SourceExists => source;
            public float AttackOriginX => (attackOrigin ?? source.Center).x;
            public float OwnerCenterX => cast.owner.Center.x;
            public int Direction => cast.direction;
            public float DamageMultiplier => cast.damageMultiplier;
            public bool TryClaimParry() => cast.TryClaimShieldParry();
            public bool Receive(float damage) => source.Receive(damage, cast.owner, cast.direction, false, null, origin: cast.HitOrigin(-1, CombatHitKind.Parry));
            public void Record(bool projectileAttack)
            { cast.owner.NoteParry(); cast.arena.Record(record ?? (projectileAttack ? "projectile reflected" : "melee parry")); if (cast.Definition.Id == "shield_default") { cast.arena.ShieldPresentation?.Parry(cast.owner, cast.direction); if (cast.owner == cast.arena.Player) cast.arena.Audio?.EmitSkillEvent(cast.Definition.Id, "parry", cast.owner.Position); } }
        }
        internal void ReleaseResources()
        {
            if (chargedMeleeExecution != null) chargedMeleeExecution.ReleaseResources();
            else { resources?.Dispose(); charge?.Cancel(); }
        }
        void Dispose()
        {
            ReleaseResources();
            meleeAction?.Cancel();
            EndCommand(CombatCommandState.Cancelled, CombatCommandReason.ActorUnavailable);
            if (owner && owner.currentCast == this) owner.currentCast = null;
            arena.casts.Remove(this); Destroy(gameObject);
        }
        Vector2 TargetDestination()
        {
            var m=Definition.Movement;
            float side=m.Type=="target_teleport_strike" ? marked.facing : owner.Center.x<=marked.Center.x ? -1 : 1;
            float x=marked.Center.x+side*(marked.size.x/2+owner.size.x/2+m.Gap)-owner.offset.x*owner.facing;
            return new Vector2(x,marked.Position.y);
        }
        public bool TryApproachRecast(WeaponState selectedWeapon, CombatCommandKey command = default)
        {
            var host = new ApproachRecastHost(this, selectedWeapon, command); return approachRecast.TryStart(ref host);
        }
        void AdvanceApproach(float step)
        {
            var host = new ApproachRecastHost(this); approachRecast.Advance(step, ref host);
        }
        public void ForgetApproach() => approachRecast.Forget();
        void OnDestroy() { ReleaseResources(); meleeAction?.Cancel(); EndCommand(CombatCommandState.Cancelled, CombatCommandReason.ActorUnavailable); ClearMissileVisual(); if(projectileVisual) Destroy(projectileVisual.gameObject); }
        void OnDrawGizmos()
        {
            if (!arena || !arena.showBoxes) return;
            Gizmos.color = isBasic ? new Color(1,.8f,.1f,.7f) : new Color(.7f,.3f,1,.7f);
            foreach (var b in debugBoxes) Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
