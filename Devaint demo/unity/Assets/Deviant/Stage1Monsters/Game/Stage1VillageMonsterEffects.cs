using Deviant.Combat;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    public sealed class Stage1VillageMonsterEffects : MonoBehaviour
    {
        public SpriteRenderer EffectRenderer { get; private set; }
        FieldMouseEnemy actor;
        Stage1VillageMonsterAttack attack;
        Stage1MonsterEffectCatalog catalog;
        Stage1MonsterClip normal,melt,warning,burst,emerge;
        ForestCombatV2.Chapter1AttackWarning genericWarning;
        bool suppressedWarning;
        Stage1BurrowInk ink;
        Stage1ThornAfterimages trails;
        public void Bind(FieldMouseEnemy owner,Stage1VillageMonsterAttack execution,Stage1MonsterEffectCatalog source)
        {
            actor=owner;attack=execution;catalog=source;
            normal=source.Find(owner.Definition.id=="01_thorn-root"?"thorn-slash":
                owner.Definition.id=="02_broom-stalker"?"broom-dust":
                owner.Definition.id=="04_well-warden"?"elite-claw-sweep":"");
            melt=source.Find("elite-burrow-body");warning=source.Find("elite-ground-warning");
            burst=source.Find("elite-eruption-fx");emerge=source.Find("elite-emerge-body");
            var node=new GameObject("Native attack effect");node.transform.SetParent(transform,false);
            EffectRenderer=node.AddComponent<SpriteRenderer>();EffectRenderer.sharedMaterial=actor.visual.sharedMaterial;
            EffectRenderer.sortingLayerID=actor.visual.sortingLayerID;EffectRenderer.sortingOrder=actor.visual.sortingOrder+2;
            EffectRenderer.enabled=false;
            if(attack.Burrow)
            { ink=gameObject.AddComponent<Stage1BurrowInk>();ink.Bind(attack,owner.visual.GetComponent<Stage1MonsterPlayer>()); }
            if(owner.Definition.id=="01_thorn-root")
            {trails=gameObject.AddComponent<Stage1ThornAfterimages>();trails.Bind(attack);}
            actor.arena.ResetCompleted+=Clear;
        }
        void OnDestroy() { if(actor && actor.arena)actor.arena.ResetCompleted-=Clear; }
        void OnDisable()
        { Clear();if(suppressedWarning && genericWarning)genericWarning.enabled=true;suppressedWarning=false; }
        public void Clear() { if(EffectRenderer)EffectRenderer.enabled=false;if(ink)ink.Clear(); }
        void FX(Stage1MonsterClip clip,float age,Vector2 at,bool mirror=true)
        {
            if(clip==null || age<0 || age>=clip.DurationMs/1000f)return;
            EffectRenderer.sprite=clip.frames[clip.FrameAt(age)];EffectRenderer.transform.position=at;
            EffectRenderer.flipX=mirror && actor.facing>0;EffectRenderer.color=Color.white;EffectRenderer.enabled=true;
            // Only the ordinary attack stroke is resized; it grows from its ground pivot towards the hit box.
            EffectRenderer.transform.localScale=Vector3.one*(clip==normal?attack.Spec.attackEffectScale:1);
        }
        public void Draw()
        {
            Clear();if(!actor || !isActiveAndEnabled)return;
            if(trails)trails.Draw();
            var burrow=attack.Burrow;if(burrow)burrow.Refresh();
            bool customWarning=attack.Spec.attackKind=="spit" || burrow&&burrow.Active;
            if(!customWarning && suppressedWarning)
            { if(genericWarning)genericWarning.enabled=true;suppressedWarning=false; }
            if(customWarning)
            {
                if(!genericWarning)genericWarning=actor.GetComponent<ForestCombatV2.Chapter1AttackWarning>();
                if(genericWarning&&genericWarning.enabled){suppressedWarning=true;genericWarning.enabled=false;genericWarning.Clear();}
            }
            if(actor.Dead || !actor.SpeciesAttackVisible)return;
            float age=actor.SpeciesAttackAge;
            if(burrow && burrow.Active)
            {
                if(ink){ink.Draw();return;}
                var spec=burrow.Spec;
                if(age<spec.meltEnd)
                    actor.visual.sprite=melt.frames[melt.FrameAt(age)];
                else if(age<spec.eruptionStart)actor.visual.enabled=false;
                else
                {
                    actor.visual.enabled=true;
                    actor.visual.sprite=emerge.frames[emerge.FrameAt(age-spec.eruptionStart)];
                    FX(burst,age-spec.eruptionStart,burrow.Destination);
                }
                if(burrow.Locked && age>=spec.warningStart && age<spec.eruptionStart)
                    FX(warning,age-spec.warningStart,burrow.Destination,false);
                // The generic forward-box telegraph is wrong for a locked ground eruption.
                if(!genericWarning)genericWarning=actor.GetComponent<ForestCombatV2.Chapter1AttackWarning>();
                if(genericWarning && genericWarning.enabled)
                { suppressedWarning=true;genericWarning.enabled=false;genericWarning.Clear(); }
                return;
            }
            float onset=attack.Spec.attackEffectStart>=0?attack.Spec.attackEffectStart:attack.Spec.damageStart;
            Vector2 position=actor.Position;
            if(normal!=null&&normal.id=="elite-claw-sweep")position+=Vector2.right*(actor.facing*.5f);
            if(normal!=null)FX(normal,attack.MotionAge-onset,position);
        }
    }
}
