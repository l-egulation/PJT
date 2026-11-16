using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deviant.Combat;

namespace Deviant.Definitions
{
    [Serializable] public sealed class SkillHitNumericData
    {
        public float time, active, damage, root, stun, push, pushSeconds;
        public BoxData box;
    }
    [Serializable] public sealed class SkillMoveNumericData
    {
        public float start, duration, distance, speed, wallRecovery, gap, recastWindow, targetRadius, endDamage;
    }
    [Serializable] public sealed class SkillEffectNumericData
    {
        public float duration, hp, radius, x, y, parryWindow, parryDamage;
        public BoxData box;
    }
    [Serializable] public sealed class SkillNumericDocument
    {
        public float activation, end;
        public SkillHitNumericData[] hits;
        public bool hasMovement, hasEffect;
        public SkillMoveNumericData movement;
        public SkillEffectNumericData effect;
        public float[] invulStart, invulEnd, verticalFreeze;
    }

    // Closed authored-content validation. The permissive transport snapshot and
    // dedicated Native policies remain usable by their existing technical callers.
    // This is not an instruction language, runner, clock or mutable state owner.
    public sealed class AuthoredSkillDefinition
    {
        sealed class Policy
        {
            public readonly string Id, Form, Vertical, Facing, Movement, Effect;
            public readonly string[] Anchors, Targets;
            public readonly int Invulnerability, Capacity;
            public readonly bool Root, Stun, Push, PushWithCaster, PrimaryPush;
            public readonly string Wall;
            public Policy(string id, string vertical, string facing, string movement, string effect, string[] anchors, string[] targets,
                int invulnerability = 0, int capacity = 1, bool root = false, bool stun = false, bool push = false, bool pushWithCaster = false, bool primaryPush = false, string wall = "end")
            {
                Id = id; Form = id.Substring(0, id.IndexOf('_')); Vertical = vertical; Facing = facing; Movement = movement; Effect = effect;
                Anchors = anchors; Targets = targets; Invulnerability = invulnerability; Capacity = capacity;
                Root = root; Stun = stun; Push = push; PushWithCaster = pushWithCaster; PrimaryPush = primaryPush; Wall = wall;
            }
        }
        static string[] Repeat(string value, int count) => Enumerable.Repeat(value, count).ToArray();
        static readonly Policy[] Policies = {
            new Policy("whip_default", "normal", "windup_start", null, null, Repeat("actor",1), Repeat("all",1), root:true),
            new Policy("whip_range", "normal", "windup_start", null, null, Repeat("actor",1), Repeat("all",1), root:true),
            new Policy("whip_movement", "normal", "windup_start", "target_dash_recast", null, Repeat("actor",1), Repeat("all",1), root:true),
            new Policy("whip_follow_up", "normal", "windup_start", null, "target_mark", new[] { "actor", "marked_target" }, new[] { "all", "all_marked" }, root:true),
            new Policy("rapier_movement", "normal", "windup_input_else_facing", "target_teleport_strike", null, Repeat("actor",1), Repeat("all",1), wall:"teleport_endpoint_clear"),
            new Policy("rapier_follow_up", "freeze_0.10_to_0.67_then_fall_from_zero", "windup_start", "target_teleport_strike", null, Repeat("actor",5), Repeat("all",5), invulnerability:1),
            new Policy("dagger_default", "normal", "windup_start", null, "stealth", Array.Empty<string>(), Array.Empty<string>(), invulnerability:1),
            new Policy("dagger_range", "normal", "windup_start", null, "smoke", Repeat("saved_primary_world_pose",8), Repeat("all",8), invulnerability:1),
            new Policy("dagger_movement", "freeze_during_movement_then_fall_from_zero", "windup_input_else_facing", "dash", null, Repeat("actor",1), Repeat("all",1), capacity:3),
            new Policy("dagger_follow_up", "normal", "windup_start", null, null, Repeat("actor",6), Repeat("all",6)),
            new Policy("hammer_default", "normal", "windup_start", null, null, Repeat("actor",1), Repeat("all",1), push:true),
            // 지축 강타는 화면 암전 연출에 맞춰 카메라 화면 안의 모든 적을 친다(박스는 카메라가 없을 때의 대체 판정).
            new Policy("hammer_range", "grounded_required", "windup_start", null, null, Repeat("actor",1), Repeat("screen",1), stun:true),
            new Policy("hammer_movement", "freeze_during_movement_then_fall_from_zero", "windup_input_else_facing", "dash", null, Repeat("actor",1), Repeat("all",1), primaryPush:true),
            new Policy("hammer_follow_up", "grounded_required", "windup_start", null, "scheduled_world_hits", new[] { "actor", "saved_primary_world_pose", "saved_primary_world_pose", "saved_primary_world_pose" }, Repeat("all",4)),
            new Policy("shield_default", "normal", "windup_start", null, "parry", Array.Empty<string>(), Array.Empty<string>()),
            new Policy("shield_movement", "freeze_during_movement_then_fall_from_zero", "windup_input_else_facing", "dash", null, Repeat("actor",1), Repeat("all",1), pushWithCaster:true),
            new Policy("shield_follow_up", "normal", "windup_start", null, "decoy", Array.Empty<string>(), Array.Empty<string>()),
            new Policy("greatsword_movement", "normal", "windup_start", "dash", null, Repeat("actor",8), Repeat("all",8), wall:"stop_translation_continue_spin"),
            new Policy("greatsword_follow_up", "normal", "windup_start", null, "delayed_world_sword", Repeat("saved_spawn_world_pose",3), Repeat("all",3))
        };
        public static IReadOnlyList<string> SupportedIds { get; } = Array.AsReadOnly(Policies.Select(policy => policy.Id).ToArray());
        readonly SkillData source;
        readonly Policy policy;
        public string Id => source.id;
        public string FormId => policy.Form;
        public string SourceVersion { get; }
        public string ContentHash { get; }
        public SkillExecutionDefinition Execution { get; }
        public static bool Supports(string id) => Policies.Any(value => value.Id == id);
        public static bool TryCreate(string formId, SkillData data, out AuthoredSkillDefinition definition, out string error)
        {
            definition = null;
            try { definition = new AuthoredSkillDefinition(formId, data); error = null; return true; }
            catch (ArgumentException exception) { error = exception.Message; return false; }
        }
        public AuthoredSkillDefinition(string formId, SkillData data)
        {
            policy = Policies.SingleOrDefault(value => value.Id == data?.id) ?? throw new ArgumentException("An explicitly supported authored skill ID is required.");
            Validate(formId, data, policy); source = Copy(data);
            SourceVersion = Hash(writer => WritePolicy(writer, source, policy));
            ContentHash = Hash(writer => { writer.Write("authored-skill-v1"); writer.Write(SourceVersion); WriteNumeric(writer, ToNumericData()); });
            Execution = new SkillExecutionDefinition(source, null, this);
        }
        public SkillData ToData() => Copy(source);
        public SkillNumericDocument ToNumericData() => new SkillNumericDocument {
            activation = source.activation, end = source.end,
            hasMovement = source.movement != null, hasEffect = source.effect != null,
            hits = source.hits.Select(hit => new SkillHitNumericData { time = hit.time, active = hit.active, damage = hit.damage, root = hit.root, stun = hit.stun, push = hit.push, pushSeconds = hit.pushSeconds, box = Box(hit.box) }).ToArray(),
            movement = source.movement == null ? null : new SkillMoveNumericData { start = source.movement.start, duration = source.movement.duration, distance = source.movement.distance, speed = source.movement.speed,
                wallRecovery = source.movement.wallRecovery, gap = source.movement.gap, recastWindow = source.movement.recastWindow, targetRadius = source.movement.targetRadius, endDamage = source.movement.endDamage },
            effect = source.effect == null ? null : new SkillEffectNumericData { duration = source.effect.duration, hp = source.effect.hp, radius = source.effect.radius, x = source.effect.x, y = source.effect.y,
                parryWindow = source.effect.parryWindow, parryDamage = source.effect.parryDamage, box = Box(source.effect.box) },
            invulStart = (float[])source.invulStart.Clone(), invulEnd = (float[])source.invulEnd.Clone(), verticalFreeze = (float[])source.verticalFreeze.Clone()
        };
        public bool CanEdit(string group, string field, int hitIndex = 0)
        {
            if (group == "hit") return hitIndex >= 0 && hitIndex < source.hits.Length && (field == "time" || field == "active" || field == "damage" || field == "box" && source.hits[hitIndex].box != null ||
                field == "root" && policy.Root && hitIndex == 0 || field == "stun" && policy.Stun || (field == "push" || field == "pushSeconds") && policy.Push);
            if (group == "movement") {
                if (source.movement == null) return false;
                if (policy.Movement == "dash") return field == "start" || field == "duration" || field == "speed" || field == "wallRecovery" && policy.Wall == "end" || field == "endDamage" && policy.PrimaryPush;
                if (policy.Movement == "target_dash_recast") return field == "distance" || field == "speed" || field == "gap" || field == "recastWindow";
                return field == "start" || field == "gap" || field == "targetRadius";
            }
            if (group == "effect") return source.effect != null && ((field == "duration" && (policy.Effect == "stealth" || policy.Effect == "smoke" || policy.Effect == "decoy")) ||
                (field == "hp" || field == "radius") && policy.Effect == "decoy" || (field == "x" || field == "y") && (policy.Effect == "decoy" || policy.Effect == "delayed_world_sword") ||
                (field == "parryWindow" || field == "parryDamage") && policy.Effect == "parry" || field == "box" && (policy.Effect == "smoke" || policy.Effect == "decoy"));
            return false;
        }
        public AuthoredSkillDefinition Recompile(SkillNumericDocument numeric)
        {
            if (numeric == null || numeric.hits == null || numeric.hits.Length != source.hits.Length || numeric.hasMovement != (source.movement != null) || numeric.hasEffect != (source.effect != null) ||
                (numeric.hasMovement ? numeric.movement == null : !EmptyNumericMove(numeric.movement)) || (numeric.hasEffect ? numeric.effect == null : !EmptyNumericEffect(numeric.effect)))
                throw new ArgumentException("A whole fixed-shape authored numeric candidate is required.");
            if (policy.Movement == "dash" && numeric.movement != null && numeric.movement.distance != source.movement.distance)
                throw new ArgumentException("Dash distance is a derived speed/duration annotation, not an independently editable value.");
            var data = Copy(source); data.activation = numeric.activation; data.end = numeric.end;
            for (int index = 0; index < data.hits.Length; index++) {
                var hit = numeric.hits[index] ?? throw new ArgumentException("Every existing hit is required."); var target = data.hits[index];
                target.time = hit.time; target.active = hit.active; target.damage = hit.damage; target.root = hit.root; target.stun = hit.stun; target.push = hit.push; target.pushSeconds = hit.pushSeconds; target.box = Box(hit.box);
            }
            if (data.movement != null) { var move = numeric.movement; var target = data.movement;
                target.start = move.start; target.duration = move.duration; target.distance = policy.Movement == "dash" && (move.speed != source.movement.speed || move.duration != source.movement.duration) ? move.speed * move.duration : move.distance; target.speed = move.speed; target.wallRecovery = move.wallRecovery; target.gap = move.gap;
                target.recastWindow = move.recastWindow; target.targetRadius = move.targetRadius; target.endDamage = move.endDamage; }
            if (data.effect != null) { var effect = numeric.effect; var target = data.effect;
                target.duration = effect.duration; target.hp = effect.hp; target.radius = effect.radius; target.x = effect.x; target.y = effect.y; target.parryWindow = effect.parryWindow; target.parryDamage = effect.parryDamage; target.box = Box(effect.box); }
            data.invulStart = Clone(numeric.invulStart); data.invulEnd = Clone(numeric.invulEnd); data.verticalFreeze = Clone(numeric.verticalFreeze);
            var result = new AuthoredSkillDefinition(FormId, data);
            if (result.SourceVersion != SourceVersion) throw new ArgumentException("An authored numeric candidate cannot change fixed policy metadata.");
            return result;
        }
        static void Validate(string form, SkillData data, Policy policy)
        {
            if (data == null || form != policy.Form || !string.IsNullOrEmpty(data.kind) || data.unavailable || !ActionTrackDocument.IsAbsent(data.actionTracks) ||
                data.inputSpeed != 0 || data.vertical != policy.Vertical || data.facing != policy.Facing || data.capacity != policy.Capacity || !Nonnegative(data.cooldown) || data.cooldown == 0 ||
                data.chargeMin != 0 || data.chargeMax != 0 || data.chargeMinDamage != 0 || data.chargeMaxDamage != 0 || data.basicLungeDistance != 0 || data.basicLungeStart != 0 || data.basicLungeDuration != 0 ||
                !Nonnegative(data.activation) || !Finite(data.end) || data.end <= data.activation || policy.Id == "shield_default" && data.activation != 0)
                throw new ArgumentException("The authored identity/fixed policy and finite action timing are required.");
            if (data.hits == null || data.hits.Length != policy.Anchors.Length) throw new ArgumentException("The complete existing hit list and order are required.");
            if (data.bomb != null && (data.bomb.speed != 0 || data.bomb.radius != 0) || data.ranged != null && (!string.IsNullOrEmpty(data.ranged.type) || data.ranged.speed != 0) || data.missile != null && data.missile.speed != 0)
                throw new ArgumentException("These authored melee actions cannot acquire bomb/ranged/missile policies.");
            for (int index = 0; index < data.hits.Length; index++) {
                var hit = data.hits[index];
                if (hit == null || hit.anchor != policy.Anchors[index] || hit.targets != policy.Targets[index] || !Nonnegative(hit.time) || hit.time < data.activation || !Positive(hit.active) || !Finite(hit.time + hit.active) ||
                    !Nonnegative(hit.damage) || !Nonnegative(hit.root) || !Nonnegative(hit.stun) || !Nonnegative(hit.push) || !Nonnegative(hit.pushSeconds) ||
                    ((!policy.Root || index > 0) && hit.root != 0) || (!policy.Stun && hit.stun != 0) || (!policy.Push && hit.push != 0) || hit.pushWithCaster != policy.PushWithCaster)
                    throw new ArgumentException("Existing hit anchors/targets/control policy and finite numeric windows are required.");
                if (hit.anchor == "marked_target" ? hit.box != null && !EmptyBox(hit.box) : !ValidBox(hit.box)) throw new ArgumentException("Every spatial hit keeps a finite positive box; marked-target hits have no geometry.");
                if ((hit.anchor == "actor" || hit.anchor == "teleport_target") && hit.time + hit.active > data.end) throw new ArgumentException("Direct hit windows must fit inside their body action.");
                if ((data.id == "hammer_range" || data.id == "hammer_follow_up" || data.id == "greatsword_movement") && hit.box != null && hit.box.x != 0) throw new ArgumentException("The existing both-sides box remains root-symmetric.");
            }
            if (policy.Movement == null ? !EmptyMove(data.movement) : data.movement == null || data.movement.type != policy.Movement || data.movement.wallPolicy != policy.Wall)
                throw new ArgumentException("The existing dedicated movement policy must remain unchanged.");
            if (policy.Movement != null) {
                var move = data.movement;
                foreach (float value in new[] { move.start, move.duration, move.distance, move.speed, move.wallRecovery, move.gap, move.recastWindow, move.targetRadius, move.endDamage }) if (!Nonnegative(value)) throw new ArgumentException("Movement numeric values must be finite and nonnegative.");
                if (move.primaryPush != policy.PrimaryPush || !policy.PrimaryPush && move.endDamage != 0) throw new ArgumentException("The existing primary-push policy is fixed.");
                if (policy.Movement == "dash" && (!Positive(move.duration) || !Positive(move.speed) || move.start < data.activation || move.start + move.duration > data.end)) throw new ArgumentException("Dash movement needs one positive interval within the body.");
                if (policy.Movement == "target_dash_recast" && (!Positive(move.speed) || !Positive(move.recastWindow))) throw new ArgumentException("Approach recast needs positive speed and window; zero distance means unlimited.");
                if (policy.Movement == "target_teleport_strike" && (!Positive(move.targetRadius) || move.start < data.activation || move.start > data.end)) throw new ArgumentException("Target teleport needs an admitted target radius and in-body start.");
                if (!EmptyBox(move.targetBox)) throw new ArgumentException("These existing target policies do not author a second target box.");
            }
            if (policy.Effect == null ? !EmptyEffect(data.effect) : data.effect == null || data.effect.type != policy.Effect) throw new ArgumentException("The existing effect kind cannot be edited.");
            if (policy.Effect != null) {
                var effect = data.effect;
                if (!Nonnegative(effect.duration) || !Nonnegative(effect.hp) || !Nonnegative(effect.radius) || !Finite(effect.x) || !Finite(effect.y) || effect.speed != 0 || effect.distance != 0 || effect.reduction != 0 ||
                    !Nonnegative(effect.parryWindow) || !Nonnegative(effect.parryDamage)) throw new ArgumentException("Effect numeric values must match this fixed policy.");
                if ((policy.Effect == "smoke" || policy.Effect == "decoy") ? !ValidBox(effect.box) : !EmptyBox(effect.box)) throw new ArgumentException("Only the existing smoke/decoy effect geometry can be authored.");
                if ((policy.Effect == "stealth" || policy.Effect == "smoke" || policy.Effect == "decoy") && !Positive(effect.duration)) throw new ArgumentException("An existing persistent effect needs a positive duration.");
                if (policy.Effect == "decoy" && (!Positive(effect.hp) || !Positive(effect.radius))) throw new ArgumentException("A decoy needs positive HP and taunt radius.");
                if (policy.Effect == "parry" && (!Positive(effect.parryWindow) || effect.parryWindow > data.end)) throw new ArgumentException("The parry window must fit inside the body.");
                if (policy.Effect != "parry" && (effect.parryWindow != 0 || effect.parryDamage != 0)) throw new ArgumentException("Only parry has parry numeric fields.");
                if (policy.Effect != "decoy" && (effect.hp != 0 || effect.radius != 0)) throw new ArgumentException("Only decoy has HP/radius numeric fields.");
            }
            var starts = data.invulStart ?? Array.Empty<float>(); var ends = data.invulEnd ?? Array.Empty<float>();
            if (starts.Length != policy.Invulnerability || ends.Length != starts.Length) throw new ArgumentException("The existing invulnerability span list must stay fixed.");
            for (int index = 0; index < starts.Length; index++) if (!Nonnegative(starts[index]) || !Finite(ends[index]) || ends[index] <= starts[index] || ends[index] > data.end) throw new ArgumentException("Invulnerability spans must be ordered and fit inside the body.");
            var freeze = data.verticalFreeze ?? Array.Empty<float>();
            if (data.id == "rapier_follow_up" ? freeze.Length != 2 || !Nonnegative(freeze[0]) || !Finite(freeze[1]) || freeze[1] <= freeze[0] || freeze[1] > data.end : freeze.Length != 0)
                throw new ArgumentException("Only the existing rapier follow-up owns an explicit vertical-freeze interval.");
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Nonnegative(float value) => Finite(value) && value >= 0;
        static bool Positive(float value) => Finite(value) && value > 0;
        static bool ValidBox(BoxData box) => box != null && Positive(box.width) && Positive(box.height) && Finite(box.x) && Finite(box.y);
        static bool EmptyBox(BoxData box) => box == null || box.width == 0 && box.height == 0 && box.x == 0 && box.y == 0;
        static bool EmptyNumericMove(SkillMoveNumericData value) => value == null || value.start == 0 && value.duration == 0 && value.distance == 0 && value.speed == 0 && value.wallRecovery == 0 && value.gap == 0 && value.recastWindow == 0 && value.targetRadius == 0 && value.endDamage == 0;
        static bool EmptyNumericEffect(SkillEffectNumericData value) => value == null || value.duration == 0 && value.hp == 0 && value.radius == 0 && value.x == 0 && value.y == 0 && value.parryWindow == 0 && value.parryDamage == 0 && EmptyBox(value.box);
        static bool EmptyMove(MoveData move) => move == null || string.IsNullOrEmpty(move.type) && string.IsNullOrEmpty(move.wallPolicy) && move.start == 0 && move.duration == 0 && move.distance == 0 && move.speed == 0 && move.wallRecovery == 0 && move.gap == 0 && move.recastWindow == 0 && move.targetRadius == 0 && move.endDamage == 0 && !move.primaryPush && EmptyBox(move.targetBox);
        static bool EmptyEffect(EffectData effect) => effect == null || string.IsNullOrEmpty(effect.type) && effect.duration == 0 && effect.hp == 0 && effect.radius == 0 && effect.x == 0 && effect.y == 0 && effect.speed == 0 && effect.distance == 0 && effect.reduction == 0 && effect.parryWindow == 0 && effect.parryDamage == 0 && EmptyBox(effect.box);
        static float[] Clone(float[] values) => values == null || values.Length == 0 ? Array.Empty<float>() : (float[])values.Clone();
        static BoxData Box(BoxData box) => box == null || EmptyBox(box) ? null : new BoxData { width = box.width, height = box.height, x = box.x, y = box.y };
        static SkillData Copy(SkillData data) => new SkillData {
            id = data.id, name = data.name, slot = data.slot, description = data.description, kind = data.kind, unavailable = data.unavailable, cooldown = data.cooldown, capacity = data.capacity,
            activation = data.activation, end = data.end, inputSpeed = data.inputSpeed, vertical = data.vertical, facing = data.facing,
            hits = data.hits.Select(hit => new HitData { time = hit.time, active = hit.active, damage = hit.damage, root = hit.root, stun = hit.stun, push = hit.push, pushSeconds = hit.pushSeconds,
                pushWithCaster = hit.pushWithCaster, anchor = hit.anchor, targets = hit.targets, box = Box(hit.box) }).ToArray(),
            movement = EmptyMove(data.movement) ? null : new MoveData { type = data.movement.type, wallPolicy = data.movement.wallPolicy, start = data.movement.start, duration = data.movement.duration,
                distance = data.movement.distance, speed = data.movement.speed, wallRecovery = data.movement.wallRecovery, gap = data.movement.gap, recastWindow = data.movement.recastWindow,
                targetRadius = data.movement.targetRadius, targetBox = Box(data.movement.targetBox), primaryPush = data.movement.primaryPush, endDamage = data.movement.endDamage },
            effect = EmptyEffect(data.effect) ? null : new EffectData { type = data.effect.type, duration = data.effect.duration, hp = data.effect.hp, reduction = data.effect.reduction, speed = data.effect.speed,
                distance = data.effect.distance, radius = data.effect.radius, x = data.effect.x, y = data.effect.y, parryWindow = data.effect.parryWindow, parryDamage = data.effect.parryDamage, box = Box(data.effect.box) },
            invulStart = Clone(data.invulStart), invulEnd = Clone(data.invulEnd), verticalFreeze = Clone(data.verticalFreeze)
        };
        static void WritePolicy(BinaryWriter writer, SkillData data, Policy policy)
        {
            writer.Write("authored-policy-v1"); foreach (string value in new[] { policy.Form, data.id, data.name, data.slot, data.description, data.kind, data.vertical, data.facing }) writer.Write(value ?? "");
            writer.Write(data.cooldown); writer.Write(data.capacity); writer.Write(data.inputSpeed); writer.Write(data.hits.Length);
            foreach (var hit in data.hits) { writer.Write(hit.anchor); writer.Write(hit.targets); writer.Write(hit.pushWithCaster); writer.Write(hit.box != null); }
            foreach (var hit in data.hits) if (!policy.Push) writer.Write(hit.pushSeconds);
            writer.Write(data.movement != null); if (data.movement != null) { writer.Write(data.movement.type); writer.Write(data.movement.wallPolicy); writer.Write(data.movement.primaryPush); }
            writer.Write(data.effect != null); if (data.effect != null) { writer.Write(data.effect.type); writer.Write(data.effect.box != null); }
            // Unconsumed or coupled annotation values are fixed metadata, not
            // pretend editor knobs. Only real descriptor-consumed fields are
            // excluded from this policy hash.
            if (data.movement != null) {
                var move = data.movement;
                if (policy.Movement == "dash") { writer.Write(move.gap); writer.Write(move.recastWindow); writer.Write(move.targetRadius); if (policy.Wall != "end") writer.Write(move.wallRecovery); }
                else if (policy.Movement == "target_dash_recast") { writer.Write(move.start); writer.Write(move.duration); writer.Write(move.wallRecovery); writer.Write(move.targetRadius); writer.Write(move.endDamage); }
                else { writer.Write(move.duration); writer.Write(move.distance); writer.Write(move.speed); writer.Write(move.wallRecovery); writer.Write(move.recastWindow); writer.Write(move.endDamage); }
            }
            if (data.effect != null) {
                var effect = data.effect;
                if (policy.Effect != "stealth" && policy.Effect != "smoke" && policy.Effect != "decoy") writer.Write(effect.duration);
                if (policy.Effect != "decoy" && policy.Effect != "delayed_world_sword") { writer.Write(effect.x); writer.Write(effect.y); }
            }
            writer.Write(data.invulStart.Length); writer.Write(data.verticalFreeze.Length);
        }
        static void WriteBox(BinaryWriter writer, BoxData box) { writer.Write(box != null); if (box != null) { writer.Write(box.width); writer.Write(box.height); writer.Write(box.x); writer.Write(box.y); } }
        static void WriteNumeric(BinaryWriter writer, SkillNumericDocument data)
        {
            writer.Write(data.activation); writer.Write(data.end);
            foreach (var hit in data.hits) { foreach (float value in new[] { hit.time, hit.active, hit.damage, hit.root, hit.stun, hit.push, hit.pushSeconds }) writer.Write(value); WriteBox(writer, hit.box); }
            if (data.movement != null) foreach (float value in new[] { data.movement.start, data.movement.duration, data.movement.distance, data.movement.speed, data.movement.wallRecovery, data.movement.gap, data.movement.recastWindow, data.movement.targetRadius, data.movement.endDamage }) writer.Write(value);
            if (data.effect != null) { foreach (float value in new[] { data.effect.duration, data.effect.hp, data.effect.radius, data.effect.x, data.effect.y, data.effect.parryWindow, data.effect.parryDamage }) writer.Write(value); WriteBox(writer, data.effect.box); }
            for (int index = 0; index < data.invulStart.Length; index++) { writer.Write(data.invulStart[index]); writer.Write(data.invulEnd[index]); }
            foreach (float value in data.verticalFreeze) writer.Write(value);
        }
        static string Hash(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream()) { using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer);
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant(); }
        }
    }
}
