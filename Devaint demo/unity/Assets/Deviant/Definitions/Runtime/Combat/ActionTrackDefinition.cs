using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deviant.Combat;

namespace Deviant.Definitions
{
    public enum ActionTrackSourceTimeMode { CombatTimeFixed, FrameTime }
    public enum ActionHitAnchor { Actor, SavedPrimaryWorldPose, ProjectileWave }
    public enum ActionHorizontalControl { Locked }
    public enum ActionVerticalControl { Normal, FreezeDuringMotionThenFallFromZero }
    public enum ActionEffectTrackKind { ProjectileWave }
    public enum ActionDefenseTrackKind { OwnerGuard }

    [Serializable] public sealed class ActionHitTrackDocument
    {
        public string id, start, end;
        public ActionHitAnchor anchor;
        public BoxData box;
    }
    [Serializable] public sealed class ActionMotionTrackDocument { public string id, start, end; }
    [Serializable] public sealed class ActionEffectTrackDocument
    {
        public string id, start, end;
        public ActionEffectTrackKind kind;
    }
    [Serializable] public sealed class ActionDefenseTrackDocument
    {
        public string id, start, end;
        public ActionDefenseTrackKind kind;
    }
    [Serializable] public sealed class ActionControlTrackDocument
    {
        public string start, end;
        public ActionHorizontalControl horizontal;
        public ActionVerticalControl vertical;
    }
    [Serializable] public sealed class ActionTrackDocument
    {
        public int schemaVersion;
        public string id, activation, bodyEnd;
        public ActionTrackSourceTimeMode sourceTimeMode;
        public FrameTimelineDocument timeline;
        public ActionHitTrackDocument[] hits;
        public ActionMotionTrackDocument motion;
        public ActionControlTrackDocument control;
        public ActionEffectTrackDocument effect;
        public ActionDefenseTrackDocument defense;
        public static bool IsAbsent(ActionTrackDocument value) => value == null || value.schemaVersion == 0 &&
            string.IsNullOrEmpty(value.id) && string.IsNullOrEmpty(value.activation) && string.IsNullOrEmpty(value.bodyEnd) &&
            value.sourceTimeMode == ActionTrackSourceTimeMode.CombatTimeFixed && (value.timeline == null || value.timeline.IsEmpty) &&
            (value.hits == null || value.hits.Length == 0) &&
            (value.motion == null || string.IsNullOrEmpty(value.motion.id) && string.IsNullOrEmpty(value.motion.start) && string.IsNullOrEmpty(value.motion.end)) &&
            (value.control == null || string.IsNullOrEmpty(value.control.start) && string.IsNullOrEmpty(value.control.end) && value.control.horizontal == 0 && value.control.vertical == 0) &&
            EffectAbsent(value.effect) && DefenseAbsent(value.defense);
        internal static bool EffectAbsent(ActionEffectTrackDocument value) => value == null || string.IsNullOrEmpty(value.id) && string.IsNullOrEmpty(value.start) && string.IsNullOrEmpty(value.end) && value.kind == 0;
        internal static bool DefenseAbsent(ActionDefenseTrackDocument value) => value == null || string.IsNullOrEmpty(value.id) && string.IsNullOrEmpty(value.start) && string.IsNullOrEmpty(value.end) && value.kind == 0;
    }

    [Serializable] public sealed class ActionHitNumericDocument
    {
        public string id;
        public float damage;
    }
    [Serializable] public sealed class ActionTrackNumericDocument
    {
        public ActionHitNumericDocument[] hits;
        public float dashSpeed, wallRecovery;
        public float waveSpeed, waveDistance, waveSpawnX, waveSpawnY;
        public float guardHp, guardReduction;
    }

    public sealed class ActionHitTrack
    {
        public string Id { get; }
        public string StartMarker { get; }
        public string EndMarker { get; }
        public float Start { get; }
        public float Duration { get; }
        public float End => Start + Duration;
        public ActionHitAnchor Anchor { get; }
        public SkillBoxDefinition Box { get; }
        internal ActionHitTrack(ActionHitTrackDocument source, FrameTimeline timeline)
        {
            Id = source.id; StartMarker = source.start; EndMarker = source.end;
            Start = timeline.Times[source.start]; Duration = timeline.IntervalDuration(source.start, source.end);
            Anchor = source.anchor; Box = new SkillBoxDefinition(source.box);
        }
        internal ActionHitTrackDocument ToDocument() => new ActionHitTrackDocument {
            id = Id, start = StartMarker, end = EndMarker, anchor = Anchor,
            box = new BoxData { width = Box.Width, height = Box.Height, x = Box.X, y = Box.Y }
        };
    }
    public sealed class ActionMotionTrack
    {
        public string Id { get; }
        public string StartMarker { get; }
        public string EndMarker { get; }
        public float Start { get; }
        public float Duration { get; }
        public float End => Start + Duration;
        public float Speed { get; }
        internal ActionMotionTrack(ActionMotionTrackDocument source, FrameTimeline timeline, float speed)
        {
            Id = source.id; StartMarker = source.start; EndMarker = source.end;
            Start = timeline.Times[source.start]; Duration = timeline.IntervalDuration(source.start, source.end); Speed = speed;
        }
    }
    public sealed class ActionEffectTrack
    {
        public string Id { get; }
        public string StartMarker { get; }
        public string EndMarker { get; }
        public ActionEffectTrackKind Kind => ActionEffectTrackKind.ProjectileWave;
        public float Start { get; }
        public float Duration { get; }
        public float End => Start + Duration;
        internal ActionEffectTrack(ActionEffectTrackDocument source, FrameTimeline timeline)
        {
            Id = source.id; StartMarker = source.start; EndMarker = source.end;
            Start = timeline.Times[source.start]; Duration = timeline.IntervalDuration(source.start, source.end);
        }
    }
    public sealed class ActionDefenseTrack
    {
        public string Id { get; }
        public string StartMarker { get; }
        public string EndMarker { get; }
        public ActionDefenseTrackKind Kind => ActionDefenseTrackKind.OwnerGuard;
        public float Start { get; }
        public float Duration { get; }
        public float End => Start + Duration;
        internal ActionDefenseTrack(ActionDefenseTrackDocument source, FrameTimeline timeline)
        {
            Id = source.id; StartMarker = source.start; EndMarker = source.end;
            Start = timeline.Times[source.start]; Duration = timeline.IntervalDuration(source.start, source.end);
        }
    }

    // Bounded authored cutover, not an instruction VM. Charge/recast/missile,
    // activation resources, world geometry, claims and callbacks retain their
    // existing owners. Only spans with actual projectile-wave/owner-guard consumers are supported.
    public sealed class ActionTrackDefinition
    {
        public const int SchemaVersion = 1;
        public const string MovementId = "base_sword_movement";
        public const string FollowUpId = "base_sword_follow_up";
        public const string WaveId = "base_sword_range";
        public const string GuardId = "shield_range";
        public static IReadOnlyList<string> SupportedIds { get; } = Array.AsReadOnly(new[] { MovementId, FollowUpId, WaveId, GuardId });
        readonly SkillData provenance;
        readonly string activationMarker, bodyEndMarker, controlStartMarker, controlEndMarker;
        public string Id => provenance.id;
        public string SourceVersion { get; }
        public string ContentHash { get; }
        public ActionTrackSourceTimeMode SourceTimeMode { get; }
        public FrameTimeline Timeline { get; }
        public float Activation { get; }
        public float BodyEnd { get; }
        public float TailEnd { get; }
        public float ControlStart { get; }
        public float ControlEnd { get; }
        public ActionHorizontalControl Horizontal => ActionHorizontalControl.Locked;
        public ActionVerticalControl Vertical { get; }
        public IReadOnlyList<ActionHitTrack> Hits { get; }
        public ActionMotionTrack Motion { get; }
        public ActionEffectTrack Effect { get; }
        public ActionDefenseTrack Defense { get; }
        public SkillExecutionDefinition Execution { get; }

        public ActionTrackDefinition(SkillData source) : this(source, source?.actionTracks) { }
        public ActionTrackDefinition(SkillData source, ActionTrackDocument document)
        {
            ValidateSource(source);
            if (document == null || document.schemaVersion != SchemaVersion || document.id != source.id ||
                !Enum.IsDefined(typeof(ActionTrackSourceTimeMode), document.sourceTimeMode) || document.timeline == null)
                throw new ArgumentException("Expected a complete schemaVersion 1 finite track source with an explicit supported timing mode for " + source.id + ".");
            provenance = CopySource(source);
            SourceTimeMode = document.sourceTimeMode;
            SourceVersion = Hash(writer => { writer.Write("finite-action-policy-v1"); WriteMetadata(writer, provenance, false); });
            Timeline = document.timeline.Compile();
            activationMarker = document.activation; bodyEndMarker = document.bodyEnd;
            Activation = Time(activationMarker); BodyEnd = Time(bodyEndMarker);
            if (BodyEnd <= 0 || (SourceTimeMode == ActionTrackSourceTimeMode.CombatTimeFixed ? BodyEnd != Timeline.Duration : Math.Abs(BodyEnd - Timeline.Duration) > .000001f) || Activation >= BodyEnd)
                throw new ArgumentException("Activation must precede body end, which must equal the finite action frame duration.");
            var control = document.control;
            if (control == null || control.horizontal != ActionHorizontalControl.Locked ||
                !Enum.IsDefined(typeof(ActionVerticalControl), control.vertical))
                throw new ArgumentException("The existing locked-input control span is required.");
            controlStartMarker = control.start; controlEndMarker = control.end;
            ControlStart = Time(control.start); ControlEnd = Time(control.end); Vertical = control.vertical;
            if (ControlStart != 0 || ControlEnd != BodyEnd ||
                (source.id == MovementId ? Vertical != ActionVerticalControl.FreezeDuringMotionThenFallFromZero : Vertical != ActionVerticalControl.Normal))
                throw new ArgumentException("Control must cover this whole body and retain its existing vertical policy.");
            string[] stableIds = source.id == MovementId ? new[] { "thrust" } : source.id == FollowUpId ? new[] { "primary", "echo" } : source.id == WaveId ? new[] { "wave" } : Array.Empty<string>();
            if (document.hits == null || document.hits.Length != stableIds.Length)
                throw new ArgumentException("The complete existing stable hit list is required.");
            var hits = new ActionHitTrack[stableIds.Length];
            float tail = BodyEnd;
            for (int index = 0; index < hits.Length; index++)
            {
                var hit = document.hits[index];
                var expectedAnchor = source.id == WaveId ? ActionHitAnchor.ProjectileWave : index == 0 ? ActionHitAnchor.Actor : ActionHitAnchor.SavedPrimaryWorldPose;
                if (hit == null || hit.id != stableIds[index] || hit.anchor != expectedAnchor || !ValidBox(hit.box))
                    throw new ArgumentException("Stable hit IDs/order, existing anchors and finite positive box geometry are required.");
                Time(hit.start); Time(hit.end);
                hits[index] = new ActionHitTrack(hit, Timeline);
                if (hits[index].Start < Activation || expectedAnchor == ActionHitAnchor.Actor && hits[index].End > BodyEnd)
                    throw new ArgumentException("Every hit follows activation; actor hits must fit inside the body span.");
                tail = Math.Max(tail, hits[index].End);
            }
            if (hits.Length > 0 && hits[0].Start != Activation) throw new ArgumentException("The primary hit and activation share the same action-local time.");
            Hits = Array.AsReadOnly(hits);
            if (source.id == MovementId)
            {
                var motion = document.motion;
                if (motion == null || motion.id != "dash") throw new ArgumentException("The existing dash interval is required.");
                Time(motion.start); Time(motion.end);
                Motion = new ActionMotionTrack(motion, Timeline, source.movement.speed);
                if (Motion.Start != Activation || Motion.Start != hits[0].Start || Motion.Duration != hits[0].Duration || Motion.End > BodyEnd)
                    throw new ArgumentException("The thrust hit and forced movement must share one interval before recovery.");
            }
            else if (document.motion != null && (!string.IsNullOrEmpty(document.motion.id) || !string.IsNullOrEmpty(document.motion.start) || !string.IsNullOrEmpty(document.motion.end)))
                throw new ArgumentException("This action has no forced actor movement.");
            if (source.id == WaveId)
            {
                var effect = document.effect;
                if (effect == null || effect.id != "wave" || effect.kind != ActionEffectTrackKind.ProjectileWave || !ActionTrackDocument.DefenseAbsent(document.defense))
                    throw new ArgumentException("The existing independent projectile-wave span is required without defense.");
                Time(effect.start); Time(effect.end); Effect = new ActionEffectTrack(effect, Timeline);
                if (Effect.Start != Activation || Effect.Start != hits[0].Start || Effect.Duration != hits[0].Duration || hits[0].Box.X != 0 || hits[0].Box.Y != 0)
                    throw new ArgumentException("The projectile span and effect-centered wave hit share activation and retention duration.");
                tail = Math.Max(tail, Effect.End);
            }
            else if (!ActionTrackDocument.EffectAbsent(document.effect)) throw new ArgumentException("This action has no authored projectile effect span.");
            if (source.id == GuardId)
            {
                var defense = document.defense;
                if (defense == null || defense.id != "guard" || defense.kind != ActionDefenseTrackKind.OwnerGuard)
                    throw new ArgumentException("The existing owner-guard defense span is required.");
                Time(defense.start); Time(defense.end); Defense = new ActionDefenseTrack(defense, Timeline);
                if (Defense.Start != Activation) throw new ArgumentException("Owner guard begins at its activation marker.");
            }
            else if (!ActionTrackDocument.DefenseAbsent(document.defense)) throw new ArgumentException("This action has no authored defense span.");
            TailEnd = tail;
            ContentHash = Hash(writer => {
                // Retain the original complete content encoding while the public
                // SourceVersion now identifies fixed policy, not editable values.
                writer.Write(SourceTimeMode == ActionTrackSourceTimeMode.CombatTimeFixed ? "finite-action-tracks-v1" : "finite-frame-action-tracks-v1"); writer.Write(Hash(metadata => WriteMetadata(metadata, provenance))); writer.Write(Timeline.ContentHash);
                writer.Write(activationMarker); writer.Write(bodyEndMarker); writer.Write((int)SourceTimeMode);
                writer.Write(controlStartMarker); writer.Write(controlEndMarker); writer.Write((int)Horizontal); writer.Write((int)Vertical);
                foreach (var hit in Hits) {
                    writer.Write(hit.Id); writer.Write(hit.StartMarker); writer.Write(hit.EndMarker); writer.Write((int)hit.Anchor);
                    writer.Write(hit.Box.Width); writer.Write(hit.Box.Height); writer.Write(hit.Box.X); writer.Write(hit.Box.Y);
                }
                writer.Write(Motion != null);
                if (Motion != null) { writer.Write(Motion.Id); writer.Write(Motion.StartMarker); writer.Write(Motion.EndMarker); }
                if (Effect != null) { writer.Write("projectile-effect-v1"); writer.Write(Effect.Id); writer.Write(Effect.StartMarker); writer.Write(Effect.EndMarker); writer.Write((int)Effect.Kind); }
                if (Defense != null) { writer.Write("owner-defense-v1"); writer.Write(Defense.Id); writer.Write(Defense.StartMarker); writer.Write(Defense.EndMarker); writer.Write((int)Defense.Kind); }
            });
            Execution = new SkillExecutionDefinition(provenance, this);
        }
        float Time(string marker)
        {
            if (string.IsNullOrEmpty(marker) || !Timeline.Times.TryGetValue(marker, out var time))
                throw new ArgumentException("Missing action track marker: " + marker);
            return time;
        }
        public ActionTrackDocument ToDocument() => new ActionTrackDocument {
            schemaVersion = SchemaVersion, id = Id, sourceTimeMode = SourceTimeMode,
            activation = activationMarker, bodyEnd = bodyEndMarker, timeline = FrameTimelineDocument.From(Timeline),
            control = new ActionControlTrackDocument { start = controlStartMarker, end = controlEndMarker, horizontal = Horizontal, vertical = Vertical },
            hits = Hits.Select(hit => hit.ToDocument()).ToArray(),
            motion = Motion == null ? null : new ActionMotionTrackDocument { id = Motion.Id, start = Motion.StartMarker, end = Motion.EndMarker },
            effect = Effect == null ? null : new ActionEffectTrackDocument { id = Effect.Id, start = Effect.StartMarker, end = Effect.EndMarker, kind = Effect.Kind },
            defense = Defense == null ? null : new ActionDefenseTrackDocument { id = Defense.Id, start = Defense.StartMarker, end = Defense.EndMarker, kind = Defense.Kind }
        };
        // Old timing/geometry fields remain provenance. Tracks alone own their
        // active values; only the explicit non-timing numeric payload is editable.
        public SkillData ToData()
        {
            var result = CopySource(provenance); result.actionTracks = ToDocument(); return result;
        }
        public ActionTrackNumericDocument ToNumericData() => new ActionTrackNumericDocument {
            hits = Hits.Select((hit, index) => new ActionHitNumericDocument { id = hit.Id, damage = provenance.hits[index].damage }).ToArray(),
            dashSpeed = Id == MovementId ? provenance.movement.speed : 0,
            wallRecovery = Id == MovementId ? provenance.movement.wallRecovery : 0,
            waveSpeed = Id == WaveId ? provenance.effect.speed : 0,
            waveDistance = Id == WaveId ? provenance.effect.distance : 0,
            waveSpawnX = Id == WaveId ? provenance.effect.x : 0,
            waveSpawnY = Id == WaveId ? provenance.effect.y : 0,
            guardHp = Id == GuardId ? provenance.effect.hp : 0,
            guardReduction = Id == GuardId ? provenance.effect.reduction : 0
        };
        public ActionTrackDefinition Recompile(ActionTrackDocument document) => Recompile(document, ToNumericData());
        public ActionTrackDefinition Recompile(ActionTrackDocument document, ActionTrackNumericDocument numeric)
        {
            if (numeric?.hits == null || numeric.hits.Length != Hits.Count ||
                Id != MovementId && (numeric.dashSpeed != 0 || numeric.wallRecovery != 0) ||
                Id != WaveId && (numeric.waveSpeed != 0 || numeric.waveDistance != 0 || numeric.waveSpawnX != 0 || numeric.waveSpawnY != 0) ||
                Id != GuardId && (numeric.guardHp != 0 || numeric.guardReduction != 0))
                throw new ArgumentException("The complete fixed-shape finite numeric candidate is required without unrelated numeric groups.");
            var source = CopySource(provenance);
            for (int index = 0; index < Hits.Count; index++) {
                var hit = numeric.hits[index];
                if (hit == null || hit.id != Hits[index].Id || !Nonnegative(hit.damage)) throw new ArgumentException("Existing stable hit IDs/order and finite nonnegative damage are required.");
                source.hits[index].damage = hit.damage;
            }
            if (Id == MovementId) { source.movement.speed = numeric.dashSpeed; source.movement.wallRecovery = numeric.wallRecovery; }
            if (Id == WaveId) { source.effect.speed = numeric.waveSpeed; source.effect.distance = numeric.waveDistance; source.effect.x = numeric.waveSpawnX; source.effect.y = numeric.waveSpawnY; }
            if (Id == GuardId) { source.effect.hp = numeric.guardHp; source.effect.reduction = numeric.guardReduction; }
            return new ActionTrackDefinition(source, document);
        }
        public AttackMotionTiming CompileBody(string binding, string[] frames, float[] seconds, int contact, float sourceImpact)
            => SourceTimeMode == ActionTrackSourceTimeMode.FrameTime
                ? AttackMotionTiming.FrameTime(binding, frames, Timeline.Frames.Select(frame => frame.Seconds).ToArray(), Activation, -1, BodyEnd, null)
                : new AttackMotionTiming(binding, frames, seconds, contact, sourceImpact, -1, Activation, BodyEnd);
        internal IReadOnlyList<SkillHitDefinition> CompileHits()
        {
            var result = new SkillHitDefinition[Hits.Count];
            for (int index = 0; index < result.Length; index++)
            {
                var hit = CopyHit(provenance.hits[index]); var track = Hits[index];
                hit.time = track.Start; hit.active = track.Duration;
                hit.box = new BoxData { width = track.Box.Width, height = track.Box.Height, x = track.Box.X, y = track.Box.Y };
                result[index] = new SkillHitDefinition(hit, track.Id, track.Anchor);
            }
            return Array.AsReadOnly(result);
        }
        internal SkillMovementDefinition CompileMovement()
        {
            if (Motion == null) return null;
            var movement = CopyMovement(provenance.movement); movement.start = Motion.Start; movement.duration = Motion.Duration;
            return new SkillMovementDefinition(movement, true);
        }
        internal SkillEffectDefinition CompileEffect()
        {
            var effect = CopyEffect(provenance.effect);
            if (effect == null) return null;
            if (Effect != null) effect.duration = Effect.Duration;
            if (Defense != null) effect.duration = Defense.Duration;
            return new SkillEffectDefinition(effect);
        }
        public static bool Supports(string id) => id == MovementId || id == FollowUpId || id == WaveId || id == GuardId;
        static void ValidateSource(SkillData source)
        {
            if (source == null || !Supports(source.id) || !string.IsNullOrEmpty(source.kind) || source.unavailable ||
                !ValidEffect(source.effect, source.id) || source.chargeMin != 0 || source.chargeMax != 0 || source.chargeMinDamage != 0 || source.chargeMaxDamage != 0 ||
                source.bomb != null && source.bomb.speed != 0 || source.ranged != null && !string.IsNullOrEmpty(source.ranged.type) || source.missile != null && source.missile.speed != 0 ||
                source.invulStart != null && source.invulStart.Length != 0 || source.invulEnd != null && source.invulEnd.Length != 0 ||
                source.basicLungeDistance != 0 || source.basicLungeStart != 0 || source.basicLungeDuration != 0 ||
                source.inputSpeed != 0 || source.capacity != 1 || !FrameTimeline.Finite(source.cooldown) || source.cooldown <= 0 ||
                source.facing != (source.id == MovementId ? "windup_input_else_facing" : "windup_start"))
                throw new ArgumentException("Only the migrated finite melee policies without charge, recast, missile or invulnerability are supported.");
            int count = source.id == MovementId || source.id == WaveId ? 1 : source.id == FollowUpId ? 2 : 0;
            if (source.hits == null || source.hits.Length != count) throw new ArgumentException("The existing complete hit metadata is required.");
            for (int index = 0; index < count; index++)
            {
                var hit = source.hits[index];
                if (hit == null || hit.anchor != (source.id == WaveId ? "projectile" : index == 0 ? "actor" : "saved_primary_world_pose") || hit.targets != "all" ||
                    !Nonnegative(hit.damage) || !Nonnegative(hit.root) || !Nonnegative(hit.stun) || !Nonnegative(hit.push) || !Nonnegative(hit.pushSeconds))
                    throw new ArgumentException("Existing hit metadata and anchors must remain valid.");
            }
            if (source.id == MovementId)
            {
                var move = source.movement;
                if (source.vertical != "freeze_during_movement_then_fall_from_zero" || move == null || move.type != "dash" || move.wallPolicy != "end" ||
                    !FrameTimeline.Finite(move.speed) || move.speed <= 0 || !Nonnegative(move.distance) || !Nonnegative(move.wallRecovery) ||
                    !FrameTimeline.Finite(move.gap) || move.recastWindow != 0 || move.targetRadius != 0 || move.primaryPush || move.endDamage != 0 || !EmptyBox(move.targetBox))
                    throw new ArgumentException("The existing forced dash/solid-wall recovery policy is required.");
            }
            else if (source.vertical != "normal" || source.movement != null && (!string.IsNullOrEmpty(source.movement.type) || !string.IsNullOrEmpty(source.movement.wallPolicy) ||
                source.movement.distance != 0 || source.movement.speed != 0 || source.movement.wallRecovery != 0 || source.movement.primaryPush || source.movement.endDamage != 0))
                throw new ArgumentException("This finite action retains normal vertical physics and no forced actor movement.");
        }
        static bool EmptyEffect(EffectData effect, string id) => effect == null ||
            (string.IsNullOrEmpty(effect.type) || id == FollowUpId && effect.type == "scheduled_world_hit") &&
            effect.duration == 0 && effect.hp == 0 && effect.reduction == 0 && effect.speed == 0 && effect.distance == 0 && effect.radius == 0 && effect.x == 0 && effect.y == 0 && EmptyBox(effect.box);
        static bool ValidEffect(EffectData effect, string id)
        {
            if (id == WaveId) return effect != null && effect.type == "projectile" && FrameTimeline.Finite(effect.speed) && effect.speed > 0 &&
                FrameTimeline.Finite(effect.distance) && effect.distance > 0 && FrameTimeline.Finite(effect.x) && FrameTimeline.Finite(effect.y) &&
                effect.hp == 0 && effect.reduction == 0 && effect.radius == 0 && EmptyBox(effect.box);
            if (id == GuardId) return effect != null && effect.type == "owner_guard" && Nonnegative(effect.hp) && Nonnegative(effect.reduction) && effect.reduction <= 1 &&
                effect.speed == 0 && effect.distance == 0 && effect.radius == 0 && effect.x == 0 && effect.y == 0 && EmptyBox(effect.box);
            return EmptyEffect(effect, id);
        }
        static bool EmptyBox(BoxData box) => box == null || box.width == 0 && box.height == 0 && box.x == 0 && box.y == 0;
        static bool Nonnegative(float value) => FrameTimeline.Finite(value) && value >= 0;
        static bool ValidBox(BoxData box) => box != null && FrameTimeline.Finite(box.width) && box.width > 0 && FrameTimeline.Finite(box.height) && box.height > 0 && FrameTimeline.Finite(box.x) && FrameTimeline.Finite(box.y);
        static BoxData CopyBox(BoxData box) => box == null ? null : new BoxData { width = box.width, height = box.height, x = box.x, y = box.y };
        static HitData CopyHit(HitData hit) => new HitData { time = hit.time, active = hit.active, damage = hit.damage, anchor = hit.anchor, targets = hit.targets,
            root = hit.root, stun = hit.stun, push = hit.push, pushSeconds = hit.pushSeconds, pushWithCaster = hit.pushWithCaster, box = CopyBox(hit.box) };
        static MoveData CopyMovement(MoveData move) => move == null || string.IsNullOrEmpty(move.type) ? null : new MoveData { type = move.type, wallPolicy = move.wallPolicy,
            start = move.start, duration = move.duration, distance = move.distance, speed = move.speed, wallRecovery = move.wallRecovery, gap = move.gap,
            recastWindow = move.recastWindow, targetRadius = move.targetRadius, targetBox = CopyBox(move.targetBox), primaryPush = move.primaryPush, endDamage = move.endDamage };
        static SkillData CopySource(SkillData source) => new SkillData { id = source.id, name = source.name, slot = source.slot, kind = source.kind,
            description = source.description, facing = source.facing, vertical = source.vertical, cooldown = source.cooldown, capacity = source.capacity,
            activation = source.activation, end = source.end, inputSpeed = source.inputSpeed, hits = source.hits.Select(CopyHit).ToArray(),
            movement = CopyMovement(source.movement), effect = CopyEffect(source.effect), invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>() };
        static EffectData CopyEffect(EffectData effect) => effect == null || string.IsNullOrEmpty(effect.type) ? null : new EffectData {
            type = effect.type, duration = effect.duration, hp = effect.hp, reduction = effect.reduction, speed = effect.speed,
            distance = effect.distance, radius = effect.radius, x = effect.x, y = effect.y, box = CopyBox(effect.box)
        };
        static void WriteMetadata(BinaryWriter writer, SkillData source, bool includeNumeric = true)
        {
            writer.Write(source.id); writer.Write(source.name ?? ""); writer.Write(source.slot ?? ""); writer.Write(source.description ?? "");
            writer.Write(source.facing ?? ""); writer.Write(source.vertical ?? ""); writer.Write(source.inputSpeed); writer.Write(source.cooldown); writer.Write(source.capacity);
            writer.Write(source.effect?.type ?? "");
            writer.Write(source.hits.Length);
            foreach (var hit in source.hits) {
                writer.Write(hit.anchor); writer.Write(hit.targets); if (includeNumeric) writer.Write(hit.damage); writer.Write(hit.root); writer.Write(hit.stun);
                writer.Write(hit.push); writer.Write(hit.pushSeconds); writer.Write(hit.pushWithCaster);
            }
            writer.Write(source.movement != null);
            if (source.movement != null) {
                var move = source.movement; writer.Write(move.type); writer.Write(move.wallPolicy); if (includeNumeric) writer.Write(move.speed); writer.Write(move.distance);
                if (includeNumeric) writer.Write(move.wallRecovery); writer.Write(move.gap); writer.Write(move.recastWindow); writer.Write(move.targetRadius); writer.Write(move.primaryPush); writer.Write(move.endDamage);
            }
            if (source.id == WaveId || source.id == GuardId) {
                var effect = source.effect;
                if (includeNumeric || source.id != GuardId) { writer.Write(effect.hp); writer.Write(effect.reduction); }
                if (includeNumeric || source.id != WaveId) { writer.Write(effect.speed); writer.Write(effect.distance); }
                writer.Write(effect.radius);
                if (includeNumeric || source.id != WaveId) { writer.Write(effect.x); writer.Write(effect.y); }
            }
        }
        static string Hash(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream()) {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer);
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
