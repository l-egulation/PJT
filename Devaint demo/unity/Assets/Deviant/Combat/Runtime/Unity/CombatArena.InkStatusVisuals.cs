using UnityEngine;

namespace Deviant.Combat
{
    // Detached observation. Reading this never expires/removes combat state or advances a clock.
    public readonly struct InkStatusVisualSnapshot
    {
        public readonly float Shield, Guard, Slow;
        public readonly int Marks, Frost, Life;
        // MarkDisplay: drops shown over the head (the marks still to pop while a chain bursts, else the stacked marks).
        // Poison: MVP poison stacks (사약·보랏빛 낙인).
        public readonly int MarkDisplay, Poison;
        public readonly bool Frozen, Rooted, Burning, Stunned;
        public bool Any=>Shield>0 || Guard>0 || Slow>0 || Marks>0 || MarkDisplay>0 || Poison>0 || Frost>0 || Frozen || Rooted || Burning || Stunned;
        public InkStatusVisualSnapshot(float shield,float guard,float slow,int marks,int frost,int life,bool frozen,bool rooted,bool burning,bool stunned,int markDisplay=0,int poison=0)
        {Shield=shield;Guard=guard;Slow=slow;Marks=marks;Frost=frost;Life=life;Frozen=frozen;Rooted=rooted;Burning=burning;Stunned=stunned;MarkDisplay=Mathf.Max(marks,markDisplay);Poison=poison;}
    }
    public partial class CombatArena
    {
        public bool ShowInkStatusVisuals=true;
        InkStatusVisuals inkStatusVisuals;
        public InkStatusVisualSnapshot ReadInkStatusVisuals(CombatActor actor)
        {
            if(!actor || actor.arena!=this || actor.Dead || !actor.isActiveAndEnabled)return default;
            float shield=Now<actor.guardUntil?Mathf.Max(0,actor.shield):0;
            float guard=Now<actor.guardUntil?Mathf.Max(0,actor.guardReduction):0;
            if(actor==Player)
            {
                foreach(var layer in inkShields)if(layer.Life==actor.LifeVersion && layer.Until>Now)shield+=Mathf.Max(0,layer.Amount);
                foreach(var layer in inkGuards)if(layer.Life==actor.LifeVersion && layer.Until>Now)guard+=Mathf.Max(0,layer.Amount);
            }
            float slow=0;int marks=0,frost=0;bool frozen=false,burning=false;
            if(inkTargets.TryGetValue(actor,out var state) && state.Life==actor.LifeVersion)
            {
                foreach(var mark in state.Marks)if(mark.Until>Now)marks++;
                foreach(var effect in state.Slows)if(effect.Until>Now)slow=Mathf.Max(slow,effect.Amount);
                frost=state.FrostUntil>Now?state.Frost:0;
                frozen=state.FrozenUntil>Now && actor.stunUntil>Now;
            }
            // 런 잉크 슬로우(보라)도 같은 슬로우 표시를 쓴다.
            slow=Mathf.Max(slow,ReadRunInkSlow(actor));
            foreach(var damage in inkDamage)
                if(damage.Burn && damage.Remaining>0 && damage.Cause.Valid && damage.Cause.Target==actor){burning=true;break;}
            // 채찍 스킬이 건 속박은 채찍 감김(WhipSkillVisuals)이 그린다. 파츠 속박이 더 길면 그때부터 밧줄을 그린다.
            bool rooted=actor.rootUntil>Now && !TryReadWhipRoot(actor,out _);
            return new InkStatusVisualSnapshot(shield,guard,slow,marks,frost,actor.LifeVersion,frozen,rooted,burning,actor.stunUntil>Now && !frozen,ReadMvpMarkDisplay(actor),ReadMvpPoison(actor));
        }
        public void RefreshInkStatusVisuals()
        {
            if(!ShowInkStatusVisuals || !isActiveAndEnabled || !Player){inkStatusVisuals?.Hide();return;}
            if(inkStatusVisuals==null)inkStatusVisuals=new InkStatusVisuals(this);
            inkStatusVisuals.Refresh();
        }
        void HideInkStatusVisuals()=>inkStatusVisuals?.Hide();
        void ReleaseInkStatusVisuals(){inkStatusVisuals?.Dispose();inkStatusVisuals=null;}
        // Read-only probes for tests and diagnostics: live particles of one status sheet over an actor, and its body tint.
        public int ReadInkStatusParticles(CombatActor actor,string sheet)=>inkStatusVisuals?.ParticleCount(actor,sheet)??0;
        public Color ReadInkStatusBodyTint(CombatActor actor)=>inkStatusVisuals?.BodyTint(actor)??Color.clear;
    }
}
