using System;
using System.Globalization;
using System.Linq;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    // Unity art references are captured separately from pure timing. Every
    // attack captures its body and eligible basic Cue before the first Draw.
    internal sealed class AttackMotionSnapshot
    {
        readonly Sprite[] sprites;
        readonly float movementRecovery;
        readonly ChargeDefinition slamCharge;
        readonly CharacterClip chargePrepare, chargeHold;
        public Guid ExecutionId { get; }
        public string Form { get; }
        public string State { get; }
        public string BodyClipName { get; }
        public float LaserReleaseFraction { get; } = -1;
        public AttackMotionTiming Timing { get; }
        public CharacterEffect Effect { get; }
        public float LandingDuration { get; }
        public float StartedAt { get; }
        public MotionActionKey ActionKey { get; }
        public string RegistryVersion { get; }
        public AttackMotionSnapshot(CharacterAnimationCatalog catalog, CombatCast cast, MotionActionKey actionKey,string conversionBodyClip=null)
        {
            if (catalog?.sets == null) throw new ArgumentException("Attack motion catalog is unavailable.");
            ActionKey = actionKey;
            var registry = cast.owner.arena.RegistryFor(catalog);
            var binding = registry?.Find(actionKey);
            if (registry != null && binding == null) throw new ArgumentException("Missing complete action motion binding: " + actionKey);
            RegistryVersion = registry == null ? null : cast.owner.arena.MotionBindingHash;
            string requestedForm = binding?.RequestedForm ?? cast.MotionRequestedForm;
            State = binding?.RequestedClip ?? (cast.isBasic ? StateFor(actionKey.ActionId, out _) : "attack1");
            CharacterAnimationSet set = null;
            foreach (var candidate in catalog.sets) if (candidate != null && candidate.form == requestedForm) set = candidate;
            // Preserve the existing body fallback for forms whose art is not
            // supplied yet. A fallback body must not emit another form's Cue.
            if (set == null && requestedForm != "base_sword")
                foreach (var candidate in catalog.sets) if (candidate != null && candidate.form == "base_sword") set = candidate;
            Form = set?.form ?? requestedForm;
            bool conversionVariation=conversionBodyClip!=null &&
                (actionKey.Kind==MotionActionKind.Conversion && set?.gameplayForm=="launcher" ||
                 cast.isBasic && Form=="shuriken" && State=="attack1" && conversionBodyClip=="attack1_chain");
            var clip = set?.Find(conversionVariation?conversionBodyClip:State);
            if (!cast.isBasic || requestedForm != "base_sword") clip = clip ?? set?.Find("attack1") ?? set?.Find("idle");
            if (clip == null) throw new ArgumentException("Melee motion binding is unavailable: " + requestedForm + "/" + State + ".");
            if (registry != null && (conversionVariation ? registry.Palette(Form+"/"+clip.name)==null : Form + "/" + clip.name != binding.MotionId)) throw new ArgumentException("The explicit action body binding no longer matches its known media.");
            BodyClipName=clip.name;
            // Capture the held pose with the execution, just like the attack body.
            // It must not consult edited/reloaded media halfway through a charge.
            if (actionKey.ActionId == "rapier_default" && Form == "rapier" && BodyClipName == "attack_charge")
            {
                chargePrepare = CaptureChargePose(set.Find("charge_prepare"));
                chargeHold = CaptureChargePose(set.Find("charge_hold"));
            }
            if (Form == "greatsword" && BodyClipName == "attack_charge_slam" && cast.ChargePolicy?.ReleaseWindup > 0)
                slamCharge = cast.ChargePolicy;
            if (actionKey.ActionId == "base_sword_movement" && clip.name == "attack_dash_thrust" ||
                actionKey.ActionId == "hammer_movement" && clip.name == "attack_rush_push")
                movementRecovery = cast.Definition.Movement?.WallRecovery ?? 0;
            if (Form == "launcher_laser" && clip.sourceRelease > clip.sourceImpact)
                LaserReleaseFraction = (clip.sourceRelease - clip.sourceImpact) / Math.Max(.001f, clip.Duration - clip.sourceImpact);
            if (clip.sprites != null && clip.sprites.Length != clip.frames.Length) throw new ArgumentException("Attack sprite count does not match its motion frames.");
            if (cast.FrameTiming != null && !clip.frames.SequenceEqual(cast.FrameTiming.Media.FixedTiming.Frames))
                throw new ArgumentException("The action's shared media binding changed before its FrameTime execution started.");
            if (cast.SpecialFrame?.UsesFrameTime == true && !clip.frames.SequenceEqual(cast.SpecialFrame.Media.FixedTiming.Frames))
                throw new ArgumentException("The action's shared media binding changed before its special FrameTime execution started.");
            var motion = registry?.Motion(conversionVariation?Form+"/"+clip.name:binding.MotionId);
            // Finite actions retain their authoritative marker graph; fixed body
            // weights never retime hits, emissions, charge, flight or effect tails.
            Timing = cast.FrameTiming != null ? cast.FrameTiming.CompileMotion(cast.ActionSpeed)
                : cast.SpecialFrame?.UsesFrameTime == true ? cast.SpecialFrame.CompileMotion()
                : motion != null && !motion.IsDelegated
                ? motion.Compile(cast.UsesCompiledTracks ? cast.ActionTracks.Activation : Math.Min(cast.PresentationImpactTime, cast.MotionDuration),
                    cast.UsesCompiledTracks ? cast.ActionTracks.BodyEnd : cast.MotionDuration, !cast.UsesCompiledTracks)
                : cast.UsesCompiledTracks ? cast.ActionTracks.CompileBody(Form + "/" + clip.name, clip.frames, clip.seconds, clip.contact, clip.sourceImpact)
                : !cast.isBasic || requestedForm != "base_sword" || State != "attack1" || clip.stepIds == null
                ? new AttackMotionTiming(Form + "/" + clip.name, clip.frames, clip.seconds, clip.contact, clip.sourceImpact, clip.fxStart, Math.Min(cast.ImpactTime, cast.MotionDuration), cast.MotionDuration)
                : new FixedAttackMotion(CombatArena.MotionDocument(clip)).Compile(cast.ImpactTime, cast.MotionDuration);
            sprites = new Sprite[Timing.Frames.Count];
            bool unchangedFrames = clip.frames.Length == sprites.Length && clip.frames.SequenceEqual(Timing.Frames);
            for (int index = 0; index < sprites.Length; index++)
            {
                if (unchangedFrames)
                { sprites[index] = clip.sprites == null ? null : clip.sprites[index]; continue; }
                int source = Array.IndexOf(clip.frames, Timing.Frames[index]);
                if (source >= 0) sprites[index] = clip.sprites == null ? null : clip.sprites[source];
                else if (catalog.TryMotionSprite(Timing.Frames[index], out var sprite)) sprites[index] = sprite;
                else throw new ArgumentException("Unknown captured body frame: " + Timing.Frames[index]);
            }
            ExecutionId = cast.ExecutionId;
            StartedAt = cast.StartedAt;
            var land = set.Find("land");
            float landing = land?.seconds == null ? 0 : land.Duration;
            LandingDuration = float.IsNaN(landing) || float.IsInfinity(landing) || landing < 0 ? 0 : landing;
            var effect = cast.isBasic && actionKey.Kind != MotionActionKind.Conversion && set.form == requestedForm ? set.Effect(State) : null;
            if (effect != null) Effect = new CharacterEffect {
                name = effect.name, attack = effect.attack, frames = Copy(effect.frames), seconds = Copy(effect.seconds),
                contact = effect.contact, sourceImpact = effect.sourceImpact, fxStart = effect.fxStart, flipX = effect.flipX,
                scale = effect.scale, rotation = effect.rotation, x = Copy(effect.x), y = Copy(effect.y), sprites = Copy(effect.sprites)
            };
        }
        internal static string StateFor(string id, out string form)
        {
            int separator = id?.LastIndexOf("_basic_", StringComparison.Ordinal) ?? -1;
            if (separator <= 0 || !int.TryParse(id.Substring(separator + 7), NumberStyles.None, CultureInfo.InvariantCulture, out int hit) || hit == int.MaxValue)
                throw new ArgumentException("A versioned melee basic action identity is required.");
            form = id.Substring(0, separator); return "attack" + (hit + 1).ToString(CultureInfo.InvariantCulture);
        }
        public Sprite At(float age) => sprites[Timing.FrameAt(age)];
        public Sprite AtPresentation(float age, float impact, float end, bool dynamicMapping, bool charging = false)
        {
            if (chargeHold != null)
            {
                // Early key-up still respects the minimum preparation deadline.
                // No thrust/recovery frame may appear before the actual release.
                if (charging || age < impact)
                    return ChargePoseAt(age);
                // Hit and recovery use the released action's end, including the
                // hold shift; never the unshifted nominal end of the skill.
                return At(age, impact, end);
            }
            if (slamCharge != null)
                return sprites[Timing.ChargeSlamFrameAt(age, impact, slamCharge, charging)];
            // 발사기 모아쏘기(2026-11-10): 누르고 있는 동안은 뗄 때까지 준비의 마지막 장(낮게 버틴 자세)을 붙잡는다.
            if (charging && Form != null && Form.StartsWith("launcher", StringComparison.Ordinal))
                return At(Math.Min(age, Timing.SourceImpact * .999f));
            if (movementRecovery > 0 && end > 0 && end < Timing.End)
            {
                float stoppedAt = Math.Max(Timing.Impact, end - movementRecovery);
                if (age >= stoppedAt)
                    return At(Timing.End - movementRecovery + age - stoppedAt);
            }
            // Legacy attack1 fallback keeps its original body mapping.
            if (movementRecovery <= 0 && (ActionKey.ActionId == "base_sword_movement" || ActionKey.ActionId == "hammer_movement"))
                end = Timing.End;
            return dynamicMapping ? At(age, impact, end) : At(age);
        }
        internal Sprite FrameSprite(int index) => sprites[index];
        Sprite ChargePoseAt(float age)
        {
            float preparation = chargePrepare?.Duration ?? 0;
            return chargePrepare != null && age < preparation
                ? chargePrepare.At(age, false) : chargeHold.At(Math.Max(0, age - preparation), true);
        }
        static CharacterClip CaptureChargePose(CharacterClip clip)
        {
            if (clip == null) return null;
            if (clip.frames == null || clip.frames.Length == 0 || clip.seconds == null ||
                clip.seconds.Length != clip.frames.Length || clip.sprites == null || clip.sprites.Length != clip.frames.Length ||
                clip.seconds.Any(value => float.IsNaN(value) || float.IsInfinity(value) || value <= 0))
                throw new ArgumentException("Invalid charge pose media: " + clip.name);
            return new CharacterClip { name = clip.name, frames = Copy(clip.frames), seconds = Copy(clip.seconds), sprites = Copy(clip.sprites) };
        }
        // Charge release changes the live impact marker, not the captured media.
        // Retain the existing base-end mapping, including impact beyond that end.
        public Sprite At(float age, float impact, float end)
        {
            float sourceTime = AttackMotionTiming.MapSourceTime(age, impact, end, Timing.SourceImpact, Timing.SourceDuration);
            return sprites[AttackMotionTiming.SelectFrame(Timing.Seconds, Timing.SourceDuration, sourceTime, false)];
        }
        static T[] Copy<T>(T[] values) => values == null ? null : (T[])values.Clone();
    }
}
