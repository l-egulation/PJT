using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        MeleeActionRunner meleeAction;
        SkillHitDefinition meleeHit;
        public MeleeFrameDefinition FrameTiming { get; private set; }
        public string FrameTimingVersion => SpecialFrame?.UsesFrameTime == true ? SpecialFrame.SourceHash : FrameTiming?.ContentHash;
        internal float ActionSpeed => meleeAction?.Definition.Speed ?? 1;
        public string ActionTimelineVersion => SpecialFrame?.UsesFrameTime == true ? SpecialFrame.Timeline.ContentHash : meleeAction?.Definition.ContentHash;
        public MeleeActionState? ActionTimelineState => meleeAction?.State;
        public string ActionTimelineError => meleeAction?.Error;
        void BindMeleeAction(MeleeActionDefinition definition)
        {
            if (definition == null) return;
            var hit = definition.CreateHit();
            if (DirectMeleeSpecial != null) hit.pushSeconds = DirectMeleeSpecial.PushSeconds;
            meleeHit = new SkillHitDefinition(hit);
            meleeAction = new MeleeActionRunner(ExecutionId, definition, started, new MeleeActionHost(this));
        }
        sealed class MeleeActionHost : IMeleeActionHost
        {
            readonly CombatCast cast;
            public MeleeActionHost(CombatCast cast) { this.cast = cast; }
            public bool TryActivate()
            {
                // Speed/timing/geometry were fixed at start. The same resource
                // boundary captures activation-time damage for both paths.
                if (!cast.CommitResources()) { cast.Fail(); return false; }
                cast.arena.Record("activate " + cast.Definition.Id); return true;
            }
            public void Sweep() => cast.ApplyBox(0, cast.meleeHit, cast.previousOwner, cast.owner.Position, false);
            public void Complete() { if (cast.owner.isPlayer) cast.arena.Player.AttackFinished(cast); }
        }
    }
}
