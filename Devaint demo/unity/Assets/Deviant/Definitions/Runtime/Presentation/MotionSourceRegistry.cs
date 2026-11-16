using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deviant.Combat;

namespace Deviant.Definitions
{
    public enum MotionActionKind { Basic, Special, Conversion }
    public readonly struct MotionActionKey : IEquatable<MotionActionKey>
    {
        public string ActionId { get; }
        public MotionActionKind Kind { get; }
        public int Variant { get; }
        public MotionActionKey(string actionId, MotionActionKind kind, int variant = -1)
        { ActionId = actionId; Kind = kind; Variant = variant; }
        public bool Equals(MotionActionKey other) => ActionId == other.ActionId && Kind == other.Kind && Variant == other.Variant;
        public override bool Equals(object obj) => obj is MotionActionKey other && Equals(other);
        public override int GetHashCode() => unchecked(((ActionId?.GetHashCode() ?? 0) * 397 ^ (int)Kind) * 397 ^ Variant);
        public override string ToString() => ActionId + "/" + Kind + (Variant < 0 ? "" : "/" + Variant.ToString(CultureInfo.InvariantCulture));
    }
    [Serializable] public sealed class MotionPaletteDocument { public string id, form, clip; public string[] frames; }
    [Serializable] public sealed class MotionClipDocument
    {
        public string id, palette, delegatedSource, mode;
        public AttackMotionStep[] steps;
        public float sourceImpact, fxStart;
    }
    [Serializable] public sealed class MotionActionBindingDocument
    {
        public string actionId, form, clip, motion;
        public string frameTimingSource;
        public MotionActionKind kind;
        public int variant = -1;
        public bool fallback;
    }
    [Serializable] public sealed class MotionRegistryDocument
    {
        public int schemaVersion;
        public MotionPaletteDocument[] palettes;
        public MotionClipDocument[] motions;
        public MotionActionBindingDocument[] actions;
        public bool IsEmpty => schemaVersion == 0 && (palettes == null || palettes.Length == 0) && (motions == null || motions.Length == 0) && (actions == null || actions.Length == 0);
    }
    public sealed class MotionPalette
    {
        public string Id { get; }
        public string Form { get; }
        public string Clip { get; }
        public IReadOnlyList<string> Frames { get; }
        internal MotionPalette(MotionPaletteDocument source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.id) || string.IsNullOrWhiteSpace(source.form) || string.IsNullOrWhiteSpace(source.clip) ||
                source.id != source.form + "/" + source.clip || source.frames == null || source.frames.Length == 0 || source.frames.Length > 256 || source.frames.Any(string.IsNullOrWhiteSpace) || source.frames.Distinct(StringComparer.Ordinal).Count() != source.frames.Length)
                throw new ArgumentException("A motion palette requires its exact supplied form/clip and unique known frame references.");
            Id = source.id; Form = source.form; Clip = source.clip; Frames = Array.AsReadOnly((string[])source.frames.Clone());
        }
        internal MotionPaletteDocument ToDocument() => new MotionPaletteDocument { id = Id, form = Form, clip = Clip, frames = Frames.ToArray() };
    }
    public sealed class FixedBodyMotion
    {
        public const string FirstSourcePath = "data/combat/presentation/base_sword_attack1.json";
        public string Id { get; }
        public string PaletteId { get; }
        public string DelegatedSource { get; }
        public bool IsDelegated => DelegatedSource != null;
        public IReadOnlyList<string> StepIds { get; }
        public IReadOnlyList<string> Frames { get; }
        public IReadOnlyList<float> Seconds { get; }
        public float SourceImpact { get; }
        public float SourceDuration { get; }
        public float FxStart { get; }
        public string ContentHash { get; }
        internal FixedBodyMotion(MotionClipDocument source, MotionPalette palette)
        {
            if (source == null || source.id != palette.Id || source.palette != palette.Id) throw new ArgumentException("A motion must use its supplied media palette identity.");
            Id = source.id; PaletteId = source.palette;
            if (!string.IsNullOrEmpty(source.delegatedSource))
            {
                if (source.id != FixedAttackMotion.FirstSwordId || source.delegatedSource != FirstSourcePath || source.steps != null && source.steps.Length != 0 ||
                    !string.IsNullOrEmpty(source.mode) || source.sourceImpact != 0 || source.fxStart != 0)
                    throw new ArgumentException("First sword timing has one delegated canonical source; duplicate registry timing is forbidden.");
                DelegatedSource = source.delegatedSource; StepIds = Frames = Array.Empty<string>(); Seconds = Array.Empty<float>();
                ContentHash = MotionSourceRegistry.Hash(writer => { writer.Write(Id); writer.Write(PaletteId); writer.Write(DelegatedSource); }); return;
            }
            if (source.id == FixedAttackMotion.FirstSwordId) throw new ArgumentException("First sword must delegate its existing canonical source instead of duplicating timing.");
            if (source.mode != FixedAttackMotion.Mode) throw new ArgumentException("Only CombatTimeFixed body weights are supported here. Gameplay FrameTime belongs to the existing first-sword compiler.");
            if (source.steps == null || source.steps.Length == 0 || source.steps.Length > 256 || source.steps.Any(step => step == null || string.IsNullOrWhiteSpace(step.id) || !palette.Frames.Contains(step.frame)) ||
                source.steps.Select(step => step.id).Distinct(StringComparer.Ordinal).Count() != source.steps.Length)
                throw new ArgumentException("Body steps require unique stable IDs and 1..256 references from their known media palette.");
            var timing = new AttackMotionTiming(Id, source.steps.Select(step => step.frame).ToArray(), source.steps.Select(step => step.seconds).ToArray(), -1, source.sourceImpact, source.fxStart, 0, 1);
            if (source.sourceImpact < 0 || source.sourceImpact > timing.SourceDuration || source.fxStart < 0 && source.fxStart != -1 || source.fxStart > timing.SourceDuration)
                throw new ArgumentException("Source contact must be within the media, including zero; Cue origin must be within media or -1.");
            StepIds = Array.AsReadOnly(source.steps.Select(step => step.id).ToArray()); Frames = timing.Frames; Seconds = timing.Seconds;
            SourceDuration = timing.SourceDuration; SourceImpact = source.sourceImpact; FxStart = source.fxStart;
            ContentHash = MotionSourceRegistry.Hash(writer => { writer.Write(Id); writer.Write(PaletteId); writer.Write(timing.ContentHash); foreach (var step in StepIds) writer.Write(step); });
        }
        public AttackMotionTiming Compile(float impact, float end, bool cueEnabled = true)
        {
            if (IsDelegated) throw new InvalidOperationException("First-sword motion is compiled only by its existing canonical source.");
            return new AttackMotionTiming(Id, Frames.ToArray(), Seconds.ToArray(), -1, SourceImpact, cueEnabled ? FxStart : -1, impact, end);
        }
        public MotionClipDocument ToDocument() => IsDelegated ? new MotionClipDocument { id = Id, palette = PaletteId, delegatedSource = DelegatedSource } :
            new MotionClipDocument { id = Id, palette = PaletteId, mode = FixedAttackMotion.Mode, sourceImpact = SourceImpact, fxStart = FxStart,
                steps = StepIds.Select((id, index) => new AttackMotionStep { id = id, frame = Frames[index], seconds = Seconds[index] }).ToArray() };
    }
    public sealed class MotionActionBinding
    {
        public MotionActionKey Key { get; }
        public string RequestedForm { get; }
        public string RequestedClip { get; }
        public string MotionId { get; }
        public bool IsFallback { get; }
        public string FrameTimingSource { get; }
        internal MotionActionBinding(MotionActionBindingDocument source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.actionId) || string.IsNullOrWhiteSpace(source.form) || string.IsNullOrWhiteSpace(source.clip) || string.IsNullOrWhiteSpace(source.motion) ||
                !Enum.IsDefined(typeof(MotionActionKind), source.kind) || (source.kind == MotionActionKind.Conversion ? source.variant < 0 : source.variant != -1))
                throw new ArgumentException("An action motion binding requires explicit stable identity, kind, supplied media and conversion variant.");
            Key = new MotionActionKey(source.actionId, source.kind, source.variant); RequestedForm = source.form; RequestedClip = source.clip; MotionId = source.motion; IsFallback = source.fallback;
            string expectedSource = keyFrameSource(Key);
            if (!string.IsNullOrEmpty(source.frameTimingSource) && source.frameTimingSource != expectedSource)
                throw new ArgumentException("Frame timing source must match the supported existing melee action binding.");
            FrameTimingSource = string.IsNullOrEmpty(source.frameTimingSource) ? null : source.frameTimingSource;
        }
        internal MotionActionBindingDocument ToDocument() => new MotionActionBindingDocument { actionId = Key.ActionId, kind = Key.Kind, variant = Key.Variant, form = RequestedForm, clip = RequestedClip, motion = MotionId, fallback = IsFallback, frameTimingSource = FrameTimingSource };
        static string keyFrameSource(MotionActionKey key)
        {
            if (key.Kind == MotionActionKind.Basic && key.ActionId == FixedAttackMotion.FirstSwordAction) return FixedBodyMotion.FirstSourcePath;
            if (key.Kind == MotionActionKind.Special && DirectMeleeSpecialDefinition.SupportedIds.Contains(key.ActionId)) return "data/combat/presentation/melee_frame_bindings.json";
            if (key.Kind == MotionActionKind.Special)
            {
                switch (key.ActionId)
                {
                    case "base_sword_movement": case "base_sword_follow_up": case "base_sword_range": case "shield_range":
                    case "whip_default": case "whip_range": case "whip_movement": case "whip_follow_up":
                    case "rapier_movement": case "rapier_follow_up": case "dagger_default": case "dagger_range": case "dagger_movement": case "dagger_follow_up":
                    case "hammer_default": case "hammer_range": case "hammer_movement": case "hammer_follow_up":
                    case "shield_default": case "shield_movement": case "shield_follow_up": case "greatsword_movement": case "greatsword_follow_up":
                        return "data/combat/presentation/special_frame_bindings.json";
                }
            }
            if (key.Kind != MotionActionKind.Basic) return null;
            int separator = key.ActionId.LastIndexOf("_basic_", StringComparison.Ordinal); if (separator <= 0) return null;
            string form = key.ActionId.Substring(0, separator);
            return form == "base_sword" || form == "whip" || form == "rapier" || form == "dagger" || form == "hammer" || form == "shield" || form == "greatsword" ? "data/combat/presentation/melee_frame_bindings.json" : null;
        }
    }
    public sealed class MotionSourceRegistry
    {
        public const int SchemaVersion = 1;
        readonly Dictionary<MotionActionKey, MotionActionBinding> actions;
        readonly Dictionary<string, FixedBodyMotion> motions;
        readonly Dictionary<string, MotionPalette> palettes;
        public IReadOnlyList<MotionActionBinding> Actions { get; }
        public IReadOnlyList<FixedBodyMotion> Motions { get; }
        public IReadOnlyList<MotionPalette> Palettes { get; }
        public string ContentHash { get; }
        public MotionSourceRegistry(MotionRegistryDocument source, Catalog combat)
        {
            if (source == null || source.schemaVersion != SchemaVersion || source.palettes == null || source.motions == null || source.actions == null ||
                source.palettes.Length == 0 || source.palettes.Length > 256 || source.motions.Length > 256 || source.actions.Length > 256) throw new ArgumentException("A complete bounded motion registry schema1 is required.");
            palettes = new Dictionary<string, MotionPalette>(StringComparer.Ordinal);
            foreach (var value in source.palettes) { var palette = new MotionPalette(value); if (palettes.ContainsKey(palette.Id)) throw new ArgumentException("Duplicate media palette."); palettes.Add(palette.Id, palette); }
            motions = new Dictionary<string, FixedBodyMotion>(StringComparer.Ordinal);
            foreach (var value in source.motions)
            {
                if (value == null || !palettes.TryGetValue(value.palette ?? "", out var palette)) throw new ArgumentException("Unknown motion media palette.");
                var motion = new FixedBodyMotion(value, palette); if (motions.ContainsKey(motion.Id)) throw new ArgumentException("Duplicate motion source."); motions.Add(motion.Id, motion);
            }
            if (motions.Count != palettes.Count || palettes.Keys.Any(id => !motions.ContainsKey(id))) throw new ArgumentException("Every supplied attack palette requires one complete motion or explicit first-source delegation.");
            var expected = ExpectedBindings(combat, palettes.Values).ToDictionary(binding => binding.Key);
            actions = new Dictionary<MotionActionKey, MotionActionBinding>();
            foreach (var value in source.actions)
            {
                var binding = new MotionActionBinding(value);
                if (!expected.TryGetValue(binding.Key, out var canonical) || canonical.RequestedForm != binding.RequestedForm || canonical.RequestedClip != binding.RequestedClip || canonical.MotionId != binding.MotionId || canonical.IsFallback != binding.IsFallback || actions.ContainsKey(binding.Key))
                    throw new ArgumentException("Unknown, duplicate or changed action/media/fallback binding: " + binding.Key);
                actions.Add(binding.Key, binding);
            }
            if (actions.Count != expected.Count) throw new ArgumentException("Motion registry must bind every executable action and each conversion variant, without unavailable actions.");
            Actions = Array.AsReadOnly(actions.Values.OrderBy(value => value.Key.ActionId, StringComparer.Ordinal).ThenBy(value => value.Key.Kind).ThenBy(value => value.Key.Variant).ToArray());
            Motions = Array.AsReadOnly(motions.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray()); Palettes = Array.AsReadOnly(palettes.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray());
            ContentHash = Hash(writer => { writer.Write("motion-registry-v1"); writer.Write(Palettes.Count); foreach (var palette in Palettes) { writer.Write(palette.Id); writer.Write(palette.Frames.Count); foreach (var frame in palette.Frames) writer.Write(frame); }
                writer.Write(Motions.Count); foreach (var motion in Motions) writer.Write(motion.ContentHash); writer.Write(Actions.Count); foreach (var binding in Actions) { writer.Write(binding.Key.ActionId); writer.Write((int)binding.Key.Kind); writer.Write(binding.Key.Variant); writer.Write(binding.RequestedForm); writer.Write(binding.RequestedClip); writer.Write(binding.MotionId); writer.Write(binding.IsFallback); writer.Write(binding.FrameTimingSource ?? ""); } });
        }
        public MotionActionBinding Find(MotionActionKey key) => actions.TryGetValue(key, out var result) ? result : null;
        public FixedBodyMotion Motion(string id) => motions.TryGetValue(id, out var result) ? result : null;
        public MotionPalette Palette(string id) => palettes.TryGetValue(id, out var result) ? result : null;
        public MotionRegistryDocument ToDocument() => new MotionRegistryDocument { schemaVersion = SchemaVersion, palettes = Palettes.Select(value => value.ToDocument()).ToArray(), motions = Motions.Select(value => value.ToDocument()).ToArray(), actions = Actions.Select(value => value.ToDocument()).ToArray() };
        public IReadOnlyList<MotionActionBinding> AffectedActions(string motion) => Array.AsReadOnly(Actions.Where(value => value.MotionId == motion).ToArray());
        public string BindingHash(FixedAttackMotion firstMotion)
        {
            if (firstMotion == null) throw new ArgumentNullException(nameof(firstMotion));
            return Hash(writer => { writer.Write(ContentHash); writer.Write(firstMotion.ContentHash); });
        }
        public static IReadOnlyList<MotionActionBinding> ExpectedBindings(Catalog combat, IEnumerable<MotionPalette> media)
        {
            if (combat?.forms == null) throw new ArgumentException("Combat action inventory is required.");
            var known = media.ToDictionary(value => value.Id, StringComparer.Ordinal); var result = new List<MotionActionBinding>();
            foreach (var form in combat.forms)
            {
                if (form?.basics == null || form.specials == null || string.IsNullOrWhiteSpace(form.id)) throw new ArgumentException("Complete form action inventory is required.");
                for (int index = 0; index < form.basics.Length; index++)
                {
                    string action = form.id + "_basic_" + index.ToString(CultureInfo.InvariantCulture);
                    if (HasInkAppearance(form.id))
                        for (int variant = 0; variant < InkAppearanceCount; variant++)
                        {
                            string visual=ConversionForm(form.id,variant);
                            bool supplied=known.ContainsKey(visual+"/attack_single");
                            Add(action,MotionActionKind.Conversion,variant,supplied?visual:form.id,supplied?"attack_single":"attack"+(index+1).ToString(CultureInfo.InvariantCulture));
                        }
                    else Add(action, MotionActionKind.Basic, -1, form.id, "attack" + (index + 1).ToString(CultureInfo.InvariantCulture));
                }
                foreach (var skill in form.specials)
                    if (skill != null && !skill.unavailable)
                    {
                        // Use the approved dedicated cleave only for this action;
                        // older catalogs retain their existing attack1 media.
                        string clip = form.id == "base_sword" && skill.id == "base_sword_default" && known.ContainsKey("base_sword/attack_strong_slash")
                            ? "attack_strong_slash" : "attack1";
                        if (form.id == "hammer" && skill.id == "hammer_default" && known.ContainsKey("hammer/attack_default_slam"))
                            clip = "attack_default_slam";
                        if (form.id == "hammer" && skill.id == "hammer_movement" && known.ContainsKey("hammer/attack_rush_push"))
                            clip = "attack_rush_push";
                        // 여진은 내려찍기 한 번, 지축 강타는 치켜든 자세를 0.1초 더 버티는 내려찍기.
                        if (form.id == "hammer" && skill.id == "hammer_follow_up" && known.ContainsKey("hammer/attack_default_slam"))
                            clip = "attack_default_slam";
                        if (form.id == "hammer" && skill.id == "hammer_range" && known.ContainsKey("hammer/attack_range_slam"))
                            clip = "attack_range_slam";
                        if (form.id == "base_sword" && skill.id == "base_sword_movement" && known.ContainsKey("base_sword/attack_dash_thrust"))
                            clip = "attack_dash_thrust";
                        if (form.id == "base_sword" && skill.id == "base_sword_range" && known.ContainsKey("base_sword/attack_reviewed_wave"))
                            clip = "attack_reviewed_wave";
                        if (form.id == "base_sword" && skill.id == "base_sword_follow_up" && known.ContainsKey("base_sword/attack_reviewed_echo"))
                            clip = "attack_reviewed_echo";
                        if (form.id == "rapier") {
                            string rapierClip = skill.id == "rapier_default" ? "attack_charge" : skill.id == "rapier_range" ? "attack_long_thrust" :
                                skill.id == "rapier_movement" ? "attack_held_thrust" : skill.id == "rapier_follow_up" ? "attack_flurry" : null;
                            if (rapierClip != null && known.ContainsKey("rapier/" + rapierClip)) clip = rapierClip;
                        }
                        if (form.id == "greatsword") {
                            string greatswordClip = skill.id switch {
                                "greatsword_default" => "attack_charge_slam",
                                "greatsword_range" => "attack_summon_sword",
                                "greatsword_movement" => "attack_spin_advance",
                                "greatsword_follow_up" => "attack_throw_sword",
                                _ => null
                            };
                            if (greatswordClip != null && known.ContainsKey("greatsword/" + greatswordClip)) clip = greatswordClip;
                        }
                        // 수리검 특수 넷(2026-11-08): 큰 관통·폭발·적 뒤 이동·전진 회전 몸 모션.
                        if (form.id == "shuriken") {
                            string shurikenClip = skill.id switch {
                                "shuriken_basic_special" => "attack_pierce",
                                "shuriken_range_special" => "attack_explosive",
                                "shuriken_move_special" => "attack_step_throw",
                                "shuriken_followup_special" => "attack_roll",
                                _ => null
                            };
                            if (shurikenClip != null && known.ContainsKey("shuriken/" + shurikenClip)) clip = shurikenClip;
                        }
                        // 채찍 스킬 몸 모션(2026-11-07 확정): 스킬마다 전용 그림. 그림이 없는 이전 카탈로그는 attack1 을 유지한다.
                        if (form.id == "whip") {
                            string whipClip = skill.id switch {
                                "whip_default" => "attack_wrap",
                                "whip_range" => "attack_net",
                                "whip_movement" => "attack_knot",
                                "whip_follow_up" => "attack_tighten",
                                _ => null
                            };
                            if (whipClip != null && known.ContainsKey("whip/" + whipClip)) clip = whipClip;
                        }
                        // 단검 특수 넷(2026-11-07 확정): 숨죽이기·먹안개·삼연 도약·그림자 난무 몸 모션.
                        if (form.id == "dagger") {
                            string daggerClip = skill.id switch {
                                "dagger_default" => "attack_hold_breath",
                                "dagger_range" => "attack_ink_smoke",
                                "dagger_movement" => "attack_triple_leap",
                                "dagger_follow_up" => "attack_flurry",
                                _ => null
                            };
                            if (daggerClip != null && known.ContainsKey("dagger/" + daggerClip)) clip = daggerClip;
                        }
                        // 폭탄 특수 넷(2026-11-10 확정): 평타 던지기 대신 3연속 차기·두 손 스로인·미사일 다이빙·바로 던지기 몸 모션.
                        if (form.id == "bomb") {
                            string bombClip = skill.id switch {
                                "bomb_basic_special" => "attack_triple_kick",
                                "bomb_range_special" => "attack_big_heave",
                                "bomb_move_special" => "attack_missile",
                                "bomb_followup_special" => "attack_timed_toss",
                                _ => null
                            };
                            if (bombClip != null && known.ContainsKey("bomb/" + bombClip)) clip = bombClip;
                        }
                        // Each launcher skill belongs to one ink, so it plays that ink's gun art and its approved fire clip.
                        if (HasInkAppearance(form.id))
                        {
                            int slot = Array.IndexOf(form.specials, skill); string gun = ConversionForm(form.id, slot);
                            // 발사기 특수 넷(2026-11-10 확정): 잉크마다 그 총을 든 전용 몸 모션. 그림이 없는 이전 카탈로그는 attack_single 을 유지한다.
                            string launcherClip = gun switch {
                                "launcher" => "attack_charge_shot",
                                "launcher_laser" => "attack_laser_beam",
                                "launcher_shotgun" => "attack_blast_jump",
                                "launcher_grenade" => "attack_kneel_reload",
                                _ => null
                            };
                            Add(skill.id, MotionActionKind.Special, -1, gun, launcherClip != null && known.ContainsKey(gun + "/" + launcherClip) ? launcherClip : "attack_single"); continue;
                        }
                        Add(skill.id, MotionActionKind.Special, -1, form.id, clip);
                    }
            }
            void Add(string action, MotionActionKind kind, int variant, string form, string clip)
            {
                string requested = form + "/" + clip, selected = requested;
                bool suppliedForm = known.Values.Any(value => value.Form == form);
                if (!suppliedForm) selected = "base_sword/" + clip;
                if (!known.ContainsKey(selected) && (kind == MotionActionKind.Special || form != "base_sword")) selected = (suppliedForm ? form : "base_sword") + "/attack1";
                if (!known.ContainsKey(selected)) throw new ArgumentException("No existing attack media/fallback can bind " + action + ". Art is not synthesized.");
                result.Add(new MotionActionBinding(new MotionActionBindingDocument { actionId = action, kind = kind, variant = variant, form = form, clip = clip, motion = selected, fallback = selected != requested }));
            }
            return result.AsReadOnly();
        }
        // The launcher draws a different gun per equipped ink slot (base/range/move/followup).
        public const int InkAppearanceCount = 4;
        public static bool HasInkAppearance(string form) => form == "launcher";
        public static string ConversionForm(string form,int variant)
        {
            if(form!="launcher")return form;
            switch(variant){case 1:return "launcher_laser";case 2:return "launcher_shotgun";case 3:return "launcher_grenade";default:return form;}
        }
        internal static string Hash(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream()) { using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer);
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant(); }
        }
    }
}
