using System;
using System.Linq;

namespace Deviant.Definitions
{
    // These guards compare inactive original numeric sources, not the derived
    // effective FrameTime payload. No active timeline is baked back to numbers.
    public static class SpecialFrameTimingGuards
    {
        public static void RequireAuthoredNumericEdit(AuthoredSkillDefinition original, AuthoredSkillDefinition candidate)
        {
            if (original == null || candidate == null || original.Id != candidate.Id || original.SourceVersion != candidate.SourceVersion)
                throw new ArgumentException("The same complete original authored special policy is required.");
            var left = original.ToNumericData(); var right = candidate.ToNumericData();
            bool same = Same(left.activation, right.activation) && Same(left.end, right.end) && left.hits.Length == right.hits.Length;
            for (int index = 0; same && index < left.hits.Length; index++) same &= Same(left.hits[index].time, right.hits[index].time) && Same(left.hits[index].active, right.hits[index].active);
            var movement = original.Execution.Movement;
            if (movement?.Type == "dash") same &= Same(left.movement.start, right.movement.start) && Same(left.movement.duration, right.movement.duration);
            else if (movement?.Type == "target_teleport_strike") same &= Same(left.movement.start, right.movement.start);
            same &= SameList(left.invulStart, right.invulStart) && SameList(left.invulEnd, right.invulEnd) && SameList(left.verticalFreeze, right.verticalFreeze);
            if (ActionSpecialFrameCompiler.OwnsEffectDuration(original)) same &= Same(left.effect.duration, right.effect.duration);
            if (original.Execution.Effect?.Type == "parry") same &= Same(left.effect.parryWindow, right.effect.parryWindow);
            if (!same) throw new ArgumentException("An active special FrameTime source owns these gameplay timing channels. Edit its complete local timeline or explicitly return to fixed mode; numeric timing was not applied.");
        }
        public static void RequireFiniteNumericEdit(ActionTrackDefinition original, ActionTrackDefinition candidate)
        {
            if (original == null || candidate == null || original.Id != candidate.Id || original.SourceVersion != candidate.SourceVersion ||
                original.SourceTimeMode != ActionTrackSourceTimeMode.CombatTimeFixed || candidate.SourceTimeMode != ActionTrackSourceTimeMode.CombatTimeFixed)
                throw new ArgumentException("The same original fixed finite numeric policy is required; derived frame payloads are not numeric source edits.");
            if (TemporalVersion(original) != TemporalVersion(candidate))
                throw new ArgumentException("The active special FrameTime source owns finite gameplay intervals. Numeric/geometry edits cannot replace its timeline or temporal channel references.");
        }
        public static string TemporalVersion(ActionTrackDefinition value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value)); var graph = value.ToDocument();
            return SpecialFrameValues.Hash(writer => {
                writer.Write("finite-inactive-timing-v1"); writer.Write(value.Timeline.ContentHash); writer.Write(graph.activation); writer.Write(graph.bodyEnd);
                writer.Write(graph.control.start); writer.Write(graph.control.end); writer.Write((int)graph.control.horizontal); writer.Write((int)graph.control.vertical);
                foreach (var hit in graph.hits) { writer.Write(hit.id); writer.Write(hit.start); writer.Write(hit.end); writer.Write((int)hit.anchor); }
                writer.Write(graph.motion?.id ?? ""); writer.Write(graph.motion?.start ?? ""); writer.Write(graph.motion?.end ?? "");
                writer.Write(graph.effect?.id ?? ""); writer.Write(graph.effect?.start ?? ""); writer.Write(graph.effect?.end ?? "");
                writer.Write(graph.defense?.id ?? ""); writer.Write(graph.defense?.start ?? ""); writer.Write(graph.defense?.end ?? "");
            });
        }
        public static bool OwnsAuthoredField(AuthoredSkillDefinition value, string group, string field)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (group == "action") return field == "activation" || field == "end";
            if (group == "hit") return field == "time" || field == "active";
            if (group == "movement") return value.Execution.Movement?.Type == "dash" && (field == "start" || field == "duration") || value.Execution.Movement?.Type == "target_teleport_strike" && field == "start";
            if (group == "effect") return ActionSpecialFrameCompiler.OwnsEffectDuration(value) && field == "duration" || value.Execution.Effect?.Type == "parry" && field == "parryWindow";
            return group == "invulnerability" || group == "verticalFreeze";
        }
        static bool SameList(float[] left, float[] right) => left.Length == right.Length && left.Select((value, index) => Same(value, right[index])).All(value => value);
        static bool Same(float left, float right) => BitConverter.SingleToInt32Bits(left) == BitConverter.SingleToInt32Bits(right);
    }
}
