using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;

namespace Deviant.Authoring
{
    public static class MotionRegistryEdits
    {
        public static AttackMotionStep[] Insert(AttackMotionStep[] steps, int after, string id, string frame, float seconds)
        {
            if (steps == null || steps.Length >= 256 || after < -1 || after >= steps.Length || string.IsNullOrWhiteSpace(id) || steps.Any(step => step.id == id))
                throw new ArgumentException("Insert requires a unique stable step ID and an existing gap in a bounded clip.");
            var values = new List<AttackMotionStep>(steps); values.Insert(after + 1, new AttackMotionStep { id = id, frame = frame, seconds = seconds }); return values.ToArray();
        }
        public static AttackMotionStep[] Remove(AttackMotionStep[] steps, int index)
        {
            if (steps == null || steps.Length <= 1 || index < 0 || index >= steps.Length) throw new ArgumentException("A body motion must retain at least one known frame step.");
            var values = new List<AttackMotionStep>(steps); values.RemoveAt(index); return values.ToArray();
        }
        public static AttackMotionStep[] Move(AttackMotionStep[] steps, int index, int delta)
        {
            if (steps == null || index < 0 || index >= steps.Length || index + delta < 0 || index + delta >= steps.Length || Math.Abs(delta) != 1)
                throw new ArgumentException("A stable frame can move only to an adjacent valid position.");
            var values = (AttackMotionStep[])steps.Clone(); var step = values[index]; values[index] = values[index + delta]; values[index + delta] = step; return values;
        }
        public static AttackMotionTiming CompileBody(MotionSourceRegistry registry, MotionActionBinding binding, FrameAttackMotionSource first,
            Catalog combat, CombatArena arena = null)
        {
            if (registry == null || binding == null || combat?.forms == null || first == null) throw new ArgumentException("A complete selected action, source graph and canonical first motion are required.");
            // Launcher bindings name the gun art of an ink slot (launcher_laser, ...); they belong to the launcher form.
            var form = combat.forms.Single(value => value.id == binding.RequestedForm || MotionSourceRegistry.HasInkAppearance(value.id) && binding.RequestedForm.StartsWith(value.id + "_", StringComparison.Ordinal));
            bool basic = binding.Key.Kind != MotionActionKind.Special;
            float impact, end;
            if (basic)
            {
                int separator = binding.Key.ActionId.LastIndexOf("_basic_", StringComparison.Ordinal);
                int index = int.Parse(binding.Key.ActionId.Substring(separator + 7), System.Globalization.CultureInfo.InvariantCulture);
                var action = form.basics[index];
                var applied = arena && binding.Key.Kind == MotionActionKind.Basic ? arena.GetBasicMelee(form.id, index) : null;
                impact = applied?.Impact ?? action.impact; end = applied?.Duration ?? action.duration;
                if (binding.Key.ActionId == FixedAttackMotion.FirstSwordAction && first.UsesFrameTime)
                {
                    if (!BasicMeleeDefinition.TryCreate(FixedAttackMotion.FirstSwordAction, action, out var source, out var error)) throw new ArgumentException(error);
                    return first.Compile(arena ? arena.FrameSwordTiming?.SourceAction ?? applied ?? source : source, form.inputSpeed).CompileMotion(1);
                }
            }
            else
            {
                var source = form.specials.Single(value => value.id == binding.Key.ActionId);
                var tracks = arena ? arena.GetActionTracks(source.id) : ActionTrackDefinition.Supports(source.id) ? new ActionTrackDefinition(source) : null;
                var charged = arena ? arena.GetChargedMelee(source.id) : null;
                var direct = arena ? arena.GetDirectMeleeSpecial(source.id) : null;
                var execution = new SkillExecutionDefinition(source);
                end = tracks?.BodyEnd ?? charged?.Policy.BaseEnd ?? direct?.Duration ?? execution.End;
                impact = tracks?.Activation ?? charged?.Policy.Minimum ?? direct?.Impact ?? (execution.Hits.Count > 0 ? execution.Hits[0].Time : execution.Activation);
                impact = Math.Min(impact, end);
            }
            var motion = registry.Motion(binding.MotionId);
            if (!motion.IsDelegated) return motion.Compile(impact, end, basic);
            var fixedBinding = first.FixedBinding;
            return basic && binding.Key.ActionId == FixedAttackMotion.FirstSwordAction ? fixedBinding.Compile(impact, end) :
                new AttackMotionTiming(FixedAttackMotion.FirstSwordId, fixedBinding.Frames.ToArray(), fixedBinding.Seconds.ToArray(), -1,
                    fixedBinding.SourceImpact, basic && !binding.IsFallback && binding.Key.Kind != MotionActionKind.Conversion ? fixedBinding.FxStart : -1, impact, end);
        }
    }
}
