using System;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // Visual observer only. Never creates a collider, hit, movement or AI command.
    public sealed class Chapter1AttackEffects : MonoBehaviour
    {
        public Chapter1AttackEffectCatalog catalog;
        public int VisibleEffects { get; private set; }
        public int VisibleAfterimages { get; private set; }
        public int DustEmissions { get; private set; }
        public Vector3 FirstDustPosition { get; private set; }
        public Sprite FirstAfterimageSprite { get; private set; }
        public Vector3 FirstAfterimagePosition { get; private set; }
        sealed class Dust
        {
            public SpriteRenderer renderer;
            public float age=-100;
            public Vector3 position;
            public int facing;
        }
        sealed class Ghost
        {
            public SpriteRenderer renderer;
            public float born=-100;
            public Vector3 position,scale;
            public Quaternion rotation;
        }
        readonly Dust[] dust={new Dust(),new Dust()};
        readonly Ghost[] ghosts={new Ghost(),new Ghost(),new Ghost()};
        const float GhostInterval=.08f, GhostLifetime=.24f, GhostHold=.12f;
        GameObject output;
        int execution=-1,ghostCursor;
        double startedAt=double.NegativeInfinity;
        float previousAge=-1,nextSnapshot;
        Vector3 previousPosition;
        int attackFacing;

        SpriteRenderer MakeRenderer(string label,SpriteRenderer source,int order)
        {
            var go=new GameObject(label);go.transform.SetParent(output.transform,false);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sharedMaterial=source.sharedMaterial;
            renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=source.sortingOrder+order;
            renderer.enabled=false;return renderer;
        }
        void Prepare(SpriteRenderer source)
        {
            if(output)return;
            output=new GameObject("Approved attack effects (visual only)");output.transform.SetParent(transform,false);
            for(int i=0;i<dust.Length;i++)dust[i].renderer=MakeRenderer("Rear hoof dust "+i,source,-1);
            for(int i=0;i<ghosts.Length;i++)ghosts[i].renderer=MakeRenderer("Historical pose "+i,source,-2);
        }
        public void Clear()
        {
            foreach(var item in dust){item.age=-100;if(item.renderer)item.renderer.enabled=false;}
            foreach(var item in ghosts){item.born=-100;if(item.renderer)item.renderer.enabled=false;}
            VisibleEffects=VisibleAfterimages=DustEmissions=0;FirstAfterimageSprite=null;
            previousAge=-1;nextSnapshot=0;execution=-1;startedAt=double.NegativeInfinity;
        }
        void OnDisable()=>Clear();
        void OnDestroy(){if(output)Destroy(output);}

        // Called after the original pose has been sampled, also by manual simulation tests.
        public void Sample(Chapter1MonsterPresentation presentation)
        {
            var actor=presentation.actor as FieldMouseEnemy;
            var clip=catalog ? catalog.Find(presentation.species) : null;
            if(!isActiveAndEnabled || !actor || actor.Brain==null || clip==null ||
               presentation.State!="attack" || !actor.SpeciesAttackVisible || actor.Dead)
            { Clear();return; }
            float age=actor.SpeciesAttackAge;
            if(float.IsNaN(age) || float.IsInfinity(age) || age<0){Clear();return;}
            Prepare(actor.visual);
            if(execution!=actor.Brain.Execution || startedAt!=actor.Brain.StartedAt || age<previousAge)
            {
                Clear();execution=actor.Brain.Execution;startedAt=actor.Brain.StartedAt;
                previousPosition=actor.transform.position;attackFacing=actor.facing;
                nextSnapshot=actor.Definition.prepare;
            }
            VisibleEffects=VisibleAfterimages=0;
            if(presentation.species!="05_boar")
            {
                int index=clip.FrameAt(age-actor.Definition.prepare);
                var renderer=dust[0].renderer;renderer.enabled=index>=0;
                if(index>=0)
                {
                    renderer.sprite=clip.frames[index];renderer.flipX=attackFacing<0;
                    renderer.sortingOrder=actor.visual.sortingOrder+1;
                    renderer.transform.position=actor.transform.position;VisibleEffects=1;
                }
            }
            else
            {
                // The approved preview kicks occur at the first drive and the next rear-foot plant.
                // Resolve those from the existing actual animation timeline, not the draft 450ms prep.
                float second=actor.Definition.prepare+actor.Definition.attackFrameDurations[3]+actor.Definition.attackFrameDurations[4];
                float[] kicks={actor.Definition.prepare,second};
                for(int i=0;i<dust.Length;i++)
                {
                    var item=dust[i];
                    if(previousAge<kicks[i] && age>=kicks[i] && age-kicks[i]<clip.Duration)
                    {
                        float blend=previousAge<0 ? 1 : Mathf.InverseLerp(previousAge,age,kicks[i]);
                        item.position=Vector3.Lerp(previousPosition,actor.transform.position,blend)+Vector3.left*(44f/32*attackFacing);
                        item.age=kicks[i];item.facing=attackFacing;DustEmissions++;
                        if(i==0)FirstDustPosition=item.position;
                    }
                    int index=clip.FrameAt(age-item.age);item.renderer.enabled=index>=0;
                    if(index>=0)
                    {
                        item.renderer.sprite=clip.frames[index];item.renderer.flipX=item.facing<0;
                        item.renderer.transform.position=item.position;VisibleEffects++;
                    }
                }
                float stop=actor.Definition.prepare+actor.Definition.active;
                if(age>=nextSnapshot && age<stop && actor.Brain.Executing &&
                   (actor.transform.position-previousPosition).sqrMagnitude>.000001f && clip.afterimages.Length==8)
                {
                    var ghost=ghosts[ghostCursor++%ghosts.Length];
                    ghost.renderer.sprite=clip.afterimages[presentation.Frame];
                    ghost.renderer.flipX=actor.visual.flipX;ghost.position=actor.visual.transform.position;
                    ghost.rotation=actor.visual.transform.rotation;ghost.scale=actor.visual.transform.localScale;
                    ghost.born=age;nextSnapshot=age+GhostInterval;
                }
                foreach(var ghost in ghosts)
                {
                    float elapsed=age-ghost.born;
                    ghost.renderer.enabled=ghost.born>=0 && elapsed>=0 && elapsed<GhostLifetime;
                    if(!ghost.renderer.enabled)continue;
                    ghost.renderer.transform.position=ghost.position;
                    ghost.renderer.transform.rotation=ghost.rotation;
                    ghost.renderer.transform.localScale=ghost.scale;
                    // Keep the pose readable until it clears the opaque body, then fade it.
                    // Fading from birth hid almost the whole silhouette behind the moving boar.
                    float opacity=1-Mathf.Clamp01((elapsed-GhostHold)/(GhostLifetime-GhostHold));
                    ghost.renderer.color=new Color(1,1,1,160f/255*opacity);
                    if(VisibleAfterimages==0){FirstAfterimageSprite=ghost.renderer.sprite;FirstAfterimagePosition=ghost.renderer.transform.position;}
                    VisibleAfterimages++;
                }
            }
            previousAge=age;previousPosition=actor.transform.position;
        }
    }
}
