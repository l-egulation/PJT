using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        SkillHitDefinition chargedHit;
        ChargedMeleeExecution chargedMeleeExecution;
        public ChargedMeleeDefinition ChargedMelee { get; private set; }
        void BindChargedMelee(ChargedMeleeDefinition definition)
        {
            if (definition == null) return;
            ChargedMelee = definition;
            chargedHit = new SkillHitDefinition(definition.CreateHit());
            chargedMeleeExecution = new ChargedMeleeExecution(charge, chargedHit.Active, new ChargedMeleeHost(this));
        }
        void StepChargedMelee(float step) => chargedMeleeExecution.Step(step);
        sealed class ChargedMeleeHost : IChargedMeleeExecutionHost
        {
            readonly CombatCast cast;
            public ChargedMeleeHost(CombatCast cast) { this.cast = cast; }
            public float Now => cast.InkNow;
            public bool SelectTarget()
            {
                // An area charge neither needs nor reserves a victim at release.
                // Every exposed enemy intersecting the active sweep is eligible.
                return true;
            }
            public bool CommitResources() => cast.CommitResources();
            public void Activated() => cast.arena.Record("activate " + cast.ChargedMelee.Id);
            public void ReleaseResources() => cast.resources?.Dispose();
            public void Failed() { cast.FailCommand(); cast.arena.Record("not activated: " + cast.Definition.Id); }
            public void Sweep() => cast.ApplyBox(0, cast.chargedHit, cast.previousOwner, cast.owner.Position, false);
            public void Complete() { if (cast.owner.isPlayer) cast.arena.Player.AttackFinished(cast); }
            public void Dispose() => cast.Dispose();
        }
    }
}
