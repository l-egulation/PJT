using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public readonly struct ActionGeometrySample
    {
        public float Seconds { get; }
        public int Facing { get; }
        public Vector2 Root { get; }
        public Rect Body { get; }
        public Rect Hurtbox { get; }
        public Rect Hitbox { get; }
        public bool HitActive { get; }
        public int FrameIndex { get; }
        public Sprite Sprite { get; }
        public Rect SpriteBounds { get; }
        internal ActionGeometrySample(float seconds, int facing, Vector2 root, Rect body, Rect hitbox, bool active, int frame, Sprite sprite)
        {
            Seconds = seconds; Facing = facing; Root = root; Body = body; Hurtbox = body;
            Hitbox = hitbox; HitActive = active; FrameIndex = frame; Sprite = sprite;
            SpriteBounds = sprite ? ActionGeometryPreviewModel.SpriteRect(sprite, root, facing) : default;
        }
    }

    // A display-only snapshot. Sampling has no clock, actor, execution, resource,
    // damage or Cue port, and never advances a gameplay runner in either direction.
    public sealed class ActionGeometryPreviewModel
    {
        public const float ReferencePixelsPerUnit = 32;
        readonly Sprite[] sprites;
        readonly BasicMeleeDefinition source;
        readonly ActionTrackDefinition tracks;
        readonly int selectedHit;
        readonly DirectMeleeSpecialDefinition direct;
        readonly SkillBoxDefinition directBox;
        readonly ChargedMeleeDefinition charged;
        readonly ChargeReleaseTiming chargedRelease;
        readonly SkillBoxDefinition chargedBox;
        readonly float chargedActive;
        readonly AuthoredSkillDefinition authored;
        public ChargedMeleeDefinition Charged => charged;
        public ChargeReleaseTiming? Release => charged == null ? (ChargeReleaseTiming?)null : chargedRelease;
        public AuthoredSkillDefinition Authored => authored;
        public DirectMeleeSpecialDefinition Direct => direct;
        public ActionTrackDefinition Tracks => tracks;
        public string ActionId => authored?.Id ?? charged?.Id ?? tracks?.Id ?? Action.Id;
        public string SelectedHitId => authored != null ? selectedHit < authored.Execution.Hits.Count ? "hit-" + selectedHit : "effect" : charged?.Id ?? (tracks == null ? Action.Id : tracks.Hits.Count == 0 ? tracks.Defense.Id : tracks.Hits[selectedHit].Id);
        public float End => authored != null ? Math.Max(authored.Execution.End, authored.Execution.Hits.Count == 0 ? authored.Execution.Activation + (authored.Execution.Effect?.Duration ?? 0) : authored.Execution.Hits.Max(hit => hit.Time + hit.Active)) : charged == null ? tracks == null ? Action.End : tracks.Defense?.End ?? tracks.TailEnd : chargedRelease.MotionEnd;
        public float BodyEnd => authored?.Execution.End ?? (charged == null ? tracks?.BodyEnd ?? Action.End : charged.Policy.BaseEnd);
        public MeleeActionDefinition Action { get; }
        public AttackMotionTiming Motion { get; }
        public string RequestedBinding { get; }
        public string BoundBinding { get; }
        public string MediaDiagnostic { get; }
        public string Version { get; }
        public int LoadedSprites { get; }
        public int SpriteCount => sprites.Length;

        ActionGeometryPreviewModel(BasicMeleeDefinition source, MeleeActionDefinition action, AttackMotionTiming motion,
            Sprite[] sprites, string requested, string bound, string diagnostic, string version)
        {
            this.source = source; Action = action; Motion = motion; this.sprites = sprites;
            RequestedBinding = requested; BoundBinding = bound; MediaDiagnostic = diagnostic; Version = version;
            LoadedSprites = sprites.Count(sprite => sprite);
        }
        ActionGeometryPreviewModel(ActionTrackDefinition tracks, int selectedHit, AttackMotionTiming motion, Sprite[] sprites, string diagnostic)
        {
            this.tracks = tracks; this.selectedHit = selectedHit; Motion = motion; this.sprites = sprites;
            RequestedBinding = (tracks.Id == ActionTrackDefinition.GuardId ? "shield" : "base_sword") + "/attack1"; BoundBinding = motion?.Id ?? "없음"; MediaDiagnostic = diagnostic;
            Version = tracks.ContentHash; LoadedSprites = sprites.Count(sprite => sprite);
        }

        public static ActionGeometryPreviewModel Compile(ActionTrackDefinition tracks, int selectedHit, CharacterAnimationCatalog media, Func<string, Sprite> resolveSprite = null)
        {
            if (tracks == null || selectedHit < 0 || (tracks.Hits.Count == 0 ? selectedHit != 0 : selectedHit >= tracks.Hits.Count))
                throw new ArgumentException("A finite action and one of its stable hit/defense spans are required.");
            string form = tracks.Id == ActionTrackDefinition.GuardId ? "shield" : "base_sword";
            var set = media?.sets?.FirstOrDefault(item => item != null && item.form == form) ?? media?.sets?.FirstOrDefault(item => item != null && item.form == "base_sword");
            var clip = Find(set, "attack1") ?? Find(set, "idle");
            if (clip == null) return new ActionGeometryPreviewModel(tracks, selectedHit, null, Array.Empty<Sprite>(), "미디어 바인딩 누락. 형상만 표시합니다.");
            AttackMotionTiming timing;
            try { timing = tracks.CompileBody(set.form + "/" + clip.name, clip.frames, clip.seconds, clip.contact, clip.sourceImpact); }
            catch (ArgumentException error) { return new ActionGeometryPreviewModel(tracks, selectedHit, null, Array.Empty<Sprite>(), "미디어 시간 메타데이터 오류: " + error.Message); }
            var captured = new Sprite[timing.Frames.Count];
            if (clip.sprites != null && clip.sprites.Length != captured.Length)
                return new ActionGeometryPreviewModel(tracks, selectedHit, timing, captured, "실행 미디어 스냅샷의 프레임 수 불일치. 형상만 표시합니다.");
            for (int index = 0; index < captured.Length; index++)
                captured[index] = clip.sprites != null ? clip.sprites[index] : resolveSprite != null ? resolveSprite(timing.Frames[index]) : Resources.Load<Sprite>(timing.Frames[index]);
            int missing = captured.Count(sprite => !sprite);
            string diagnostic = missing == 0 ? "" : "미디어 누락 " + missing + "/" + captured.Length + ". 누락 프레임은 형상만 표시합니다.";
            return new ActionGeometryPreviewModel(tracks, selectedHit, timing, captured, diagnostic);
        }
        ActionGeometryPreviewModel(DirectMeleeSpecialDefinition direct, AttackMotionTiming motion, Sprite[] sprites, string bound, string diagnostic, MeleeFrameDefinition frame = null)
        {
            this.direct = direct; Action = frame == null ? direct.Compile() : frame.CompileForStart(1); Motion = motion; this.sprites = sprites;
            directBox = new SkillBoxDefinition(direct.ToBasicData().box);
            RequestedBinding = direct.FormId + "/attack1"; BoundBinding = bound; MediaDiagnostic = diagnostic; Version = frame?.ContentHash ?? direct.ContentHash;
            LoadedSprites = sprites.Count(sprite => sprite);
        }
        public static ActionGeometryPreviewModel Compile(DirectMeleeSpecialDefinition direct, CharacterAnimationCatalog media, Func<string, Sprite> resolveSprite = null, MeleeFrameDefinition frame = null)
        {
            if (direct == null) throw new ArgumentNullException(nameof(direct));
            var set = media?.sets?.FirstOrDefault(value => value != null && value.form == direct.FormId) ?? media?.sets?.FirstOrDefault(value => value != null && value.form == "base_sword");
            var clip = Find(set, "attack1") ?? Find(set, "idle");
            string bound = clip == null ? "없음" : set.form + "/" + clip.name;
            if (clip == null) return new ActionGeometryPreviewModel(direct, null, Array.Empty<Sprite>(), bound, "미디어 바인딩 누락. 형상만 표시합니다.");
            AttackMotionTiming timing;
            try { timing = frame == null ? new AttackMotionTiming(bound, clip.frames, clip.seconds, clip.contact, clip.sourceImpact, -1, direct.Impact, direct.Duration) : frame.CompileMotion(1); }
            catch (ArgumentException error) { return new ActionGeometryPreviewModel(direct, null, Array.Empty<Sprite>(), bound, "미디어 시간 메타데이터 오류: " + error.Message); }
            var captured = new Sprite[timing.Frames.Count];
            if (clip.sprites != null && clip.sprites.Length != captured.Length) return new ActionGeometryPreviewModel(direct, timing, captured, bound, "실행 미디어 프레임 수 불일치. 형상만 표시합니다.");
            for (int index = 0; index < captured.Length; index++) captured[index] = clip.sprites != null ? clip.sprites[index] : resolveSprite != null ? resolveSprite(timing.Frames[index]) : Resources.Load<Sprite>(timing.Frames[index]);
            int missing = captured.Count(sprite => !sprite);
            string diagnostic = bound != direct.FormId + "/attack1" ? "기존 런타임 본체 대체 바인딩: " + bound + ". 선택 형태의 실제 아트 아님. " : "";
            if (missing != 0) diagnostic += "미디어 누락 " + missing + "/" + captured.Length + ". 형상만 표시합니다.";
            return new ActionGeometryPreviewModel(direct, timing, captured, bound, diagnostic, frame);
        }
        ActionGeometryPreviewModel(ChargedMeleeDefinition charged, ChargeReleaseTiming release, AttackMotionTiming motion, Sprite[] sprites, string bound, string diagnostic)
        {
            this.charged = charged; chargedRelease = release; var numeric = charged.ToNumericData(); chargedBox = new SkillBoxDefinition(numeric.box); chargedActive = numeric.active; Motion = motion; this.sprites = sprites;
            RequestedBinding = charged.FormId + "/" + ChargedClip(charged); BoundBinding = bound; MediaDiagnostic = diagnostic; Version = charged.ContentHash; LoadedSprites = sprites.Count(sprite => sprite);
        }
        static string ChargedClip(ChargedMeleeDefinition charged) =>
            charged.FormId == "greatsword" ? "attack_charge_slam" : "attack1";

        public static ActionGeometryPreviewModel Compile(ChargedMeleeDefinition charged, float releaseAt, CharacterAnimationCatalog media, Func<string, Sprite> resolveSprite = null)
        {
            if (charged == null) throw new ArgumentNullException(nameof(charged));
            var release = charged.Policy.Resolve(releaseAt);
            var set = media?.sets?.FirstOrDefault(value => value != null && value.form == charged.FormId) ?? media?.sets?.FirstOrDefault(value => value != null && value.form == "base_sword");
            string requestedClip = ChargedClip(charged);
            var clip = Find(set, requestedClip) ?? Find(set, "attack1") ?? Find(set, "idle"); string bound = clip == null ? "없음" : set.form + "/" + clip.name;
            if (clip == null) return new ActionGeometryPreviewModel(charged, release, null, Array.Empty<Sprite>(), bound, "미디어 바인딩 누락. 형상만 표시합니다.");
            AttackMotionTiming timing;
            try { timing = new AttackMotionTiming(bound, clip.frames, clip.seconds, clip.contact, clip.sourceImpact, -1, Math.Min(charged.Policy.Minimum, charged.Policy.BaseEnd), charged.Policy.BaseEnd); }
            catch (ArgumentException error) { return new ActionGeometryPreviewModel(charged, release, null, Array.Empty<Sprite>(), bound, "미디어 시간 메타데이터 오류: " + error.Message); }
            var sprites = new Sprite[timing.Frames.Count];
            if (clip.sprites != null && clip.sprites.Length != sprites.Length) return new ActionGeometryPreviewModel(charged, release, timing, sprites, bound, "시작 매체 프레임 수 불일치. 형상만 표시합니다.");
            for (int index = 0; index < sprites.Length; index++) sprites[index] = clip.sprites != null ? clip.sprites[index] : resolveSprite != null ? resolveSprite(timing.Frames[index]) : Resources.Load<Sprite>(timing.Frames[index]);
            int missing = sprites.Count(sprite => !sprite); string diagnostic = bound != charged.FormId + "/" + requestedClip ? "기존 본체 대체 바인딩: " + bound + ". 선택 형태의 실제 아트 아님. " : "";
            if (missing != 0) diagnostic += "미디어 누락 " + missing + "/" + sprites.Length;
            return new ActionGeometryPreviewModel(charged, release, timing, sprites, bound, diagnostic);
        }
        ActionGeometryPreviewModel(AuthoredSkillDefinition authored, int selectedHit, AttackMotionTiming motion, Sprite[] sprites, string bound, string diagnostic)
        {
            this.authored = authored; this.selectedHit = selectedHit; Motion = motion; this.sprites = sprites;
            RequestedBinding = authored.FormId + "/attack1"; BoundBinding = bound; MediaDiagnostic = diagnostic; Version = authored.ContentHash; LoadedSprites = sprites.Count(sprite => sprite);
        }
        public static ActionGeometryPreviewModel Compile(AuthoredSkillDefinition authored, int selectedHit, CharacterAnimationCatalog media, Func<string, Sprite> resolveSprite = null)
        {
            if (authored == null || selectedHit < 0 || selectedHit >= Math.Max(1, authored.Execution.Hits.Count)) throw new ArgumentException("Select an existing authored hit/effect.");
            var set = media?.sets?.FirstOrDefault(value => value != null && value.form == authored.FormId) ?? media?.sets?.FirstOrDefault(value => value != null && value.form == "base_sword");
            var clip = Find(set, "attack1") ?? Find(set, "idle"); string bound = clip == null ? "없음" : set.form + "/" + clip.name;
            if (clip == null) return new ActionGeometryPreviewModel(authored, selectedHit, null, Array.Empty<Sprite>(), bound, "미디어 바인딩 누락. 형상만 표시합니다.");
            var execution = authored.Execution; float impact = execution.Hits.Count == 0 ? execution.Activation : execution.Hits[0].Time;
            AttackMotionTiming timing;
            try { timing = new AttackMotionTiming(bound, clip.frames, clip.seconds, clip.contact, clip.sourceImpact, -1, Math.Min(impact, execution.End), execution.End); }
            catch (ArgumentException error) { return new ActionGeometryPreviewModel(authored, selectedHit, null, Array.Empty<Sprite>(), bound, "미디어 시간 오류: " + error.Message); }
            var sprites = new Sprite[timing.Frames.Count];
            if (clip.sprites != null && clip.sprites.Length != sprites.Length) return new ActionGeometryPreviewModel(authored, selectedHit, timing, sprites, bound, "시작 매체 프레임 수 불일치. 형상만 표시합니다.");
            for (int index = 0; index < sprites.Length; index++) sprites[index] = clip.sprites != null ? clip.sprites[index] : resolveSprite != null ? resolveSprite(timing.Frames[index]) : Resources.Load<Sprite>(timing.Frames[index]);
            string diagnostic = "target teleport/approach와 지형·접지·효과 생성은 예측하지 않습니다. 저장 위치는 시작 원점의 평지 표본입니다.";
            if (sprites.Any(sprite => !sprite)) diagnostic += " 실제 미디어 누락; 형상만 표시합니다.";
            return new ActionGeometryPreviewModel(authored, selectedHit, timing, sprites, bound, diagnostic);
        }

        public static ActionGeometryPreviewModel Compile(BasicMeleeDefinition action, Catalog forms, CharacterAnimationCatalog media,
            FixedAttackMotion fixedMotion = null, MeleeFrameDefinition frame = null, Func<string, Sprite> resolveSprite = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            string state = StateFor(action.Id, out var requestedForm);
            var form = forms?.forms?.FirstOrDefault(item => item != null && item.id == requestedForm);
            if (form == null || !Finite(form.inputSpeed)) throw new ArgumentException("The selected form's existing ground-advance metadata is unavailable: " + requestedForm);
            if (frame != null && frame.Action.ContentHash != action.ContentHash)
                throw new ArgumentException("Frame geometry and body timing must use the same compiled candidate.");
            if (fixedMotion != null && action.Id != FixedAttackMotion.FirstSwordAction)
                throw new ArgumentException("The delegated fixed first-sword motion cannot be applied to another hit.");

            var compiled = frame == null ? MeleeActionDefinition.ForBasic(action, 1, form.inputSpeed) : frame.CompileForStart(1);
            string requested = requestedForm + "/" + state;
            CharacterAnimationSet set = null;
            if (media?.sets != null)
            {
                foreach (var item in media.sets) if (item != null && item.form == requestedForm) set = item;
                if (set == null && requestedForm != "base_sword")
                    foreach (var item in media.sets) if (item != null && item.form == "base_sword") set = item;
            }
            // Match AttackMotionSnapshot's existing read-only body selection:
            // absent form -> base_sword; non-sword missing state -> attack1/idle.
            // This does not substitute a different Sprite for a missing image.
            var clip = Find(set, state);
            if (requestedForm != "base_sword") clip = clip ?? Find(set, "attack1") ?? Find(set, "idle");
            string bound = clip == null ? "없음" : set.form + "/" + clip.name;
            string diagnostic = clip == null ? "미디어 바인딩 누락: " + requested + ". 형상만 표시합니다." :
                requested != bound ? "기존 런타임 본체 대체 바인딩: " + requested + " → " + bound + " (선택 형태의 실제 아트 아님)." : "";
            AttackMotionTiming timing = null;
            bool bindingMatches = true;
            if (frame != null)
            {
                timing = frame.CompileMotion(1);
                bindingMatches = clip?.frames != null && clip.frames.SequenceEqual(timing.Frames);
            }
            else if (fixedMotion != null)
            {
                timing = fixedMotion.Compile(compiled.Impact, compiled.End);
                bindingMatches = clip?.frames != null && clip.frames.SequenceEqual(timing.Frames);
            }
            else if (clip != null)
            {
                try
                {
                    // The existing runtime timing API owns source-time mapping.
                    timing = new AttackMotionTiming(set.form + "/" + state, clip.frames, clip.seconds,
                        clip.contact, clip.sourceImpact, clip.fxStart, compiled.Impact, compiled.End);
                }
                catch (ArgumentException error) { diagnostic = "미디어 시간 메타데이터 오류: " + error.Message + " 형상만 표시합니다."; }
            }
            if (!bindingMatches && clip != null)
                diagnostic = "선택 후보와 카탈로그의 프레임 바인딩 불일치. 다른 이미지를 대입하지 않고 형상만 표시합니다.";
            if (timing == null)
                return new ActionGeometryPreviewModel(action, compiled, null, Array.Empty<Sprite>(), requested, bound, diagnostic, action.ContentHash);

            var captured = new Sprite[timing.Frames.Count];
            var missing = new List<string>();
            bool useCaptured = bindingMatches && clip?.frames != null && clip.frames.SequenceEqual(timing.Frames) && clip.sprites != null;
            if (useCaptured && clip.sprites.Length != captured.Length)
            {
                diagnostic = "실행 미디어 스냅샷의 프레임 수 불일치. 형상만 표시합니다.";
                bindingMatches = false;
            }
            for (int i = 0; i < captured.Length; i++)
            {
                if (bindingMatches)
                    captured[i] = useCaptured ? clip.sprites[i] : resolveSprite != null ? resolveSprite(timing.Frames[i]) : Resources.Load<Sprite>(timing.Frames[i]);
                if (!captured[i]) missing.Add(timing.Frames[i]);
            }
            if (missing.Count > 0)
                diagnostic += (diagnostic.Length == 0 ? "" : "\n") + "미디어 누락 " + missing.Count + "/" + captured.Length + ": " + string.Join(", ", missing) + ". 누락 프레임은 형상만 표시합니다.";
            if (captured.Any(sprite => sprite && sprite.pixelsPerUnit != ReferencePixelsPerUnit))
                diagnostic += "\nPPU 32가 아닌 바인딩이 있습니다. 이미지는 실제 Sprite.pixelsPerUnit/rect/pivot 크기로 표시합니다.";
            return new ActionGeometryPreviewModel(action, compiled, timing, captured, requested, bound, diagnostic,
                frame?.ContentHash ?? action.ContentHash + "/" + timing.ContentHash);
        }

        public ActionGeometrySample Sample(float seconds, int facing, int inputDirection)
        {
            if (!Finite(seconds) || facing != -1 && facing != 1 || inputDirection < -1 || inputDirection > 1)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (charged != null)
            {
                float chargedTime = Mathf.Clamp(seconds, 0, chargedRelease.MotionEnd);
                float impact = charged.Policy.Minimum + chargedRelease.Shift + charged.Policy.ReleaseWindup;
                int frame = -1;
                if (Motion != null)
                {
                    if (BoundBinding == "greatsword/attack_charge_slam" && charged.Policy.ReleaseWindup > 0)
                        frame = Motion.ChargeSlamFrameAt(chargedTime, impact, charged.Policy, chargedTime < chargedRelease.ReleaseTime);
                    else
                    {
                        float sourceTime = AttackMotionTiming.MapSourceTime(chargedTime, impact, charged.Policy.BaseEnd, Motion.SourceImpact, Motion.SourceDuration);
                        frame = AttackMotionTiming.SelectFrame(Motion.Seconds, Motion.SourceDuration, sourceTime, false);
                    }
                }
                return new ActionGeometrySample(chargedTime, facing, Vector2.zero,
                    Box(Vector2.zero, CombatRules.BodyWidth, CombatRules.BodyHeight, CombatRules.BodyOffsetX, CombatRules.BodyHeight / 2, facing),
                    Box(Vector2.zero, chargedBox.Width, chargedBox.Height, chargedBox.X, chargedBox.Y, facing), chargedTime >= impact && chargedTime < impact + chargedActive,
                    frame, frame < 0 ? null : sprites[frame]);
            }
            if (authored != null)
            {
                var definition = authored.Execution; float sampleTime = Mathf.Clamp(seconds, 0, End); var rootPosition = new Vector2(AdvanceAt(sampleTime, facing), 0);
                var hit = definition.Hits.Count == 0 ? null : definition.Hits[selectedHit]; var box = hit?.Box ?? definition.Effect?.Box;
                bool spatial = hit != null ? hit.Box != null : box != null;
                var origin = hit == null || hit.Anchor != "actor" ? new Vector2(AdvanceAt(definition.Activation, facing), 0) : rootPosition;
                if (hit == null && definition.Effect?.Type == "decoy") origin += new Vector2(definition.Effect.X * facing, definition.Effect.Y);
                if (hit?.Anchor == "saved_spawn_world_pose") origin += new Vector2(definition.Effect.X * facing, definition.Effect.Y);
                float impact = definition.Hits.Count == 0 ? definition.Activation : definition.Hits[0].Time;
                int frame = Motion == null || sampleTime >= definition.End ? -1 : AttackMotionTiming.SelectFrame(Motion.Seconds, Motion.SourceDuration,
                    AttackMotionTiming.MapSourceTime(sampleTime, impact, definition.End, Motion.SourceImpact, Motion.SourceDuration), false);
                bool active = spatial && (hit != null ? sampleTime >= hit.Time && sampleTime < hit.Time + hit.Active : sampleTime >= definition.Activation && sampleTime < definition.Activation + definition.Effect.Duration);
                return new ActionGeometrySample(sampleTime, facing, rootPosition, Box(rootPosition, CombatRules.BodyWidth, CombatRules.BodyHeight, CombatRules.BodyOffsetX, CombatRules.BodyHeight / 2, facing),
                    box == null ? default : Box(origin, box.Width, box.Height, box.X, box.Y, facing), active, frame, frame < 0 ? null : sprites[frame]);
            }
            if (tracks != null)
            {
                float finiteTime = Mathf.Clamp(seconds, 0, End);
                var finiteRoot = new Vector2(AdvanceAt(finiteTime, facing), 0);
                var hit = tracks.Hits.Count == 0 ? null : tracks.Hits[selectedHit];
                var origin = hit == null || hit.Anchor == ActionHitAnchor.Actor ? finiteRoot : hit.Anchor == ActionHitAnchor.ProjectileWave ? EffectAt(finiteTime, facing) : new Vector2(AdvanceAt(tracks.Activation, facing), 0);
                var box = hit?.Box;
                var finiteBody = Box(finiteRoot, CombatRules.BodyWidth, CombatRules.BodyHeight, CombatRules.BodyOffsetX, CombatRules.BodyHeight / 2, facing);
                int frame = finiteTime < tracks.BodyEnd ? Motion?.FrameAt(finiteTime) ?? -1 : -1;
                return new ActionGeometrySample(finiteTime, facing, finiteRoot, finiteBody, box == null ? default : Box(origin, box.Width, box.Height, box.X, box.Y, facing),
                    hit != null && finiteTime >= hit.Start && (hit.Anchor == ActionHitAnchor.ProjectileWave ? finiteTime < tracks.Activation + tracks.Execution.Effect.Distance / tracks.Execution.Effect.Speed : finiteTime < hit.End), frame, frame < 0 ? null : sprites[frame]);
            }
            if (direct != null)
            {
                float directTime = Mathf.Clamp(seconds, 0, Action.End); var box = directBox;
                int frame = Motion?.FrameAt(directTime) ?? -1;
                return new ActionGeometrySample(directTime, facing, Vector2.zero,
                    Box(Vector2.zero, CombatRules.BodyWidth, CombatRules.BodyHeight, CombatRules.BodyOffsetX, CombatRules.BodyHeight / 2, facing),
                    Box(Vector2.zero, box.Width, box.Height, box.X, box.Y, facing), directTime >= Action.Impact && directTime < Action.Impact + Action.Active, frame, frame < 0 ? null : sprites[frame]);
            }
            float time = Mathf.Clamp(seconds, 0, Action.End);
            var root = new Vector2(AdvanceAt(time, inputDirection), 0);
            var body = Box(root, CombatRules.BodyWidth, CombatRules.BodyHeight, CombatRules.BodyOffsetX, CombatRules.BodyHeight / 2, facing);
            var hitbox = Box(root, source.Width, source.Height, source.OffsetX, source.OffsetY, facing);
            int index = Motion?.FrameAt(time) ?? -1;
            return new ActionGeometrySample(time, facing, root, body, hitbox,
                time >= Action.Impact && time < Action.Impact + Action.Active, index, index < 0 ? null : sprites[index]);
        }
        public float AdvanceAt(float seconds, int inputDirection)
        {
            if (!Finite(seconds) || inputDirection < -1 || inputDirection > 1) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (authored != null) {
                var movement = authored.Execution.Movement; return movement == null || movement.Type != "dash" ? 0 : inputDirection * movement.Speed * Mathf.Clamp(seconds - movement.Start, 0, movement.Duration);
            }
            if (direct != null || charged != null) return 0;
            if (tracks != null)
            {
                var movement = tracks.Motion;
                return movement == null ? 0 : inputDirection * movement.Speed * Mathf.Clamp(Mathf.Clamp(seconds, 0, tracks.BodyEnd) - movement.Start, 0, movement.Duration);
            }
            // Integrate the compiled constant-speed interval, clipped by action
            // completion just as the current runner stops moving when completed.
            float elapsed = Mathf.Clamp(Mathf.Clamp(seconds, 0, Action.End) - Action.AdvanceStart, 0, Action.AdvanceDuration);
            return inputDirection * Action.AdvanceDistance / Action.AdvanceDuration * elapsed;
        }
        // Unobstructed display sampling only; actual wave collision/lifetime and
        // fixed-tick sweep remain with SkillWaveExecution. No runner is advanced.
        public Vector2 EffectAt(float seconds, int facing)
        {
            if (!Finite(seconds) || facing != -1 && facing != 1) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (tracks?.Effect == null) return Vector2.zero;
            var effect = tracks.Execution.Effect;
            float age = Mathf.Max(0, seconds - tracks.Effect.Start);
            return new Vector2(facing * (effect.X + Mathf.Min(effect.Distance, effect.Speed * age)), effect.Y);
        }
        public string FrameReference(int index) => index >= 0 && Motion != null && index < Motion.Frames.Count ? Motion.Frames[index] : "없음";
        public Sprite BoundSprite(int index) => index >= 0 && index < sprites.Length ? sprites[index] : null;
        public static Rect SpriteRect(Sprite sprite, Vector2 root, int facing)
        {
            if (!sprite) throw new ArgumentNullException(nameof(sprite));
            if (facing != -1 && facing != 1) throw new ArgumentOutOfRangeException(nameof(facing));
            var rect = sprite.rect; var pivot = sprite.pivot; float ppu = sprite.pixelsPerUnit;
            float left = facing > 0 ? -pivot.x : pivot.x - rect.width;
            return new Rect(root.x + left / ppu, root.y - pivot.y / ppu, rect.width / ppu, rect.height / ppu);
        }
        static Rect Box(Vector2 root, float width, float height, float x, float y, int facing)
            => new Rect(root.x + x * facing - width / 2, root.y + y - height / 2, width, height);
        static CharacterClip Find(CharacterAnimationSet set, string state)
        {
            if (set?.clips != null) foreach (var clip in set.clips) if (clip != null && clip.name == state) return clip;
            return null;
        }
        static string StateFor(string id, out string form)
        {
            int separator = id?.LastIndexOf("_basic_", StringComparison.Ordinal) ?? -1;
            if (separator <= 0 || !int.TryParse(id.Substring(separator + 7), NumberStyles.None, CultureInfo.InvariantCulture, out int hit) || hit == int.MaxValue)
                throw new ArgumentException("An existing melee basic action identity is required.");
            form = id.Substring(0, separator); return "attack" + (hit + 1).ToString(CultureInfo.InvariantCulture);
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
