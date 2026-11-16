using Deviant.Combat;
using Deviant.Integration;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace ForestCombatV2
{
    // Only observes the existing combat actor. No damage, AI or collision ownership.
    [DefaultExecutionOrder(600)]
    public sealed class Chapter1MonsterPresentation : MonoBehaviour
    {
        public TestEnemy actor;
        public Chapter1MonsterCatalog catalog;
        public string species;
        public Chapter1AttackEffects attackEffects;
        public string State { get; private set; }
        public int Frame { get; private set; }
        public Chapter1MonsterClip CurrentClip { get; private set; }
        float clock, deathAge, hurtUntil, lastMovedAt=-100;
        float lastWorldNow;
        int lastHitCount;
        bool wasDead;
        long deathToken;
        HitFeedbackBridge hitFeedback;
        Vector2 previousPosition;
        Chapter1HitFeedback feedback;
        public BasicSwordHitFeedback SwordFeedback => feedback?.Sword;
        CombatOutcomeCursor hitCursor;
        readonly CombatAcceptedHit[] hits=new CombatAcceptedHit[32];
        CombatArena boundArena;
        float hitAt=float.NegativeInfinity;
        int hitDirection;

        void Start() { Bind(); }
        void Bind()
        {
            if(!actor || !actor.arena || boundArena==actor.arena)return;
            if(boundArena)boundArena.ResetCompleted-=ResetPresentation;
            boundArena=actor.arena;boundArena.ResetCompleted+=ResetPresentation;
            boundArena.HitEvents.TryGetCursor(out hitCursor);
            previousPosition=actor.Position;
            lastWorldNow=actor.arena.Now;
        }
        void ResetPresentation()
        {
            if(hitFeedback&&actor)hitFeedback.CancelDeath(actor,deathToken);deathToken=0;
            clock=deathAge=hurtUntil=0;lastMovedAt=-100;lastHitCount=0;wasDead=false;
            hitAt=float.NegativeInfinity;State=null;
            if(actor)previousPosition=actor.Position;
            if(actor&&actor.arena)lastWorldNow=actor.arena.Now;
            feedback?.Clear();
            if(boundArena)boundArena.HitEvents.TryGetCursor(out hitCursor);
            if(attackEffects)attackEffects.Clear();
            if(actor && actor.visual)ResetVisual(actor.visual);
        }
        static void ResetVisual(SpriteRenderer visual)
        {
            visual.transform.localPosition=Vector3.zero;
            visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one;
            visual.color=Color.white;
        }
        void OnEnable() { if(actor){lastHitCount=actor.ReceivedHitCount;if(actor.arena)actor.arena.HitEvents.TryGetCursor(out hitCursor);} }
        void OnDisable() { ResetPresentation();if(actor)lastHitCount=actor.ReceivedHitCount; }
        void OnDestroy()
        {
            if(boundArena)boundArena.ResetCompleted-=ResetPresentation;
            feedback?.Dispose();
        }
        void LateUpdate() { Draw(Time.deltaTime); }
        // Public sampling also supports the arena's existing manual-simulation verifier.
        public void Draw(float deltaTime)
        {
            if(!isActiveAndEnabled || !actor || !actor.visual || !catalog || !actor.arena)return;
            Bind();
            float now=actor.arena.Now;
            if(actor.ReceivedHitCount<lastHitCount || wasDead&&!actor.Dead)ResetPresentation();
            if(actor.ReceivedHitCount>lastHitCount)
            {
                hitAt=now;hurtUntil=Mathf.Max(now+.1f,actor.staggerUntil);
                hitDirection=now<actor.knockUntil && Mathf.Abs(actor.knockSpeed)>.0001f ? (actor.knockSpeed<0?-1:1) :
                    actor.arena.Player ? (actor.Center.x<actor.arena.Player.Center.x?-1:1) : -actor.facing;
                if(feedback==null)feedback=new Chapter1HitFeedback(actor.visual);
                var idle=catalog.Find(species,"idle");
                var origin=idle!=null && idle.frames.Length>0 ? actor.visual.transform.parent.TransformPoint(
                    Chapter1HitFeedback.ImpactLocal(idle.frames[0],species,actor.facing<0,hitDirection)) : actor.visual.bounds.center;
                int observed=0;
                while(actor.arena.HitEvents.TryRead(hitCursor,hits,out var page))
                {
                    hitCursor=page.Next;
                    for(int i=0;i<page.Count;i++)
                    {
                        var hit=hits[i];if(hit.Target.ActorId!=actor.ActorId)continue;
                        observed++;
                        bool sword=actor.arena.Player && hit.Origin.Source.ActorId==actor.arena.Player.ActorId && BasicSwordHitFeedback.Matches(hit.Origin);
                        var at=origin;
                        if(sword && idle!=null && idle.frames.Length>0)
                        {
                            at=actor.visual.transform.parent.TransformPoint(Chapter1HitFeedback.ImpactLocal(idle.frames[0],species,hit.TargetPose.Facing<0,hit.Origin.SourceFacing));
                            at+=new Vector3(hit.TargetPose.X-actor.Position.x,hit.TargetPose.Y-actor.Position.y,0);
                        }
                        feedback.Hit(at,lastHitCount+observed,sword?hit.Origin:default,hit.Now);
                    }
                    if(page.Count<hits.Length)break;
                }
                // Detached fixtures / an overflowed observation page retain generic feedback.
                for(int i=lastHitCount+observed;i<actor.ReceivedHitCount;i++)feedback.Hit(origin,i+1);
            }
            else actor.arena.HitEvents.TryGetCursor(out hitCursor);
            lastHitCount=actor.ReceivedHitCount;
            if(Mathf.Abs(actor.Position.x-previousPosition.x)>.00001f)lastMovedAt=now;
            bool moving=now-lastMovedAt<.08f && Mathf.Abs(actor.velocity.x)>.001f;
            previousPosition=actor.Position;
            var speciesActor = actor as FieldMouseEnemy;
            bool speciesTiming = speciesActor && speciesActor.ExternalPresentation;
            bool attackVisible = speciesTiming ? speciesActor.SpeciesAttackVisible : actor.Telegraph>=0;
            string state=actor.Dead?"death":now<hurtUntil?"hurt":attackVisible?"attack":moving?"move":"idle";
            var clip=catalog.Find(species,state);
            if(clip==null) { clip=catalog.Find(species,"move"); }
            if(clip==null)return;
            if(state!=State || wasDead&&!actor.Dead) { clock=0; deathAge=0; }
            State=state;CurrentClip=clip;
            // Repeated Draw calls at the same combat time never advance death/idle twice.
            float elapsed=actor.arena.Controls.Current.SimulationPaused?0:Mathf.Max(0,now-lastWorldNow);lastWorldNow=now;
            clock+=elapsed;
            float age=clock;
            if(state=="death") { deathAge+=elapsed;age=deathAge; }
            else if(state=="attack")
            {
                if (speciesTiming) age=speciesActor.SpeciesAttackAge;
                else
                {
                    float actionDuration=actor.profile==2?1.45f:.95f;
                    age=actor.AttackElapsed/actionDuration*clip.Duration;
                }
            }
            else if(state=="idle" && clip.motion!="idle")age=0;
            if (speciesTiming && state=="attack") Frame=speciesActor.Brain.AttackFrame(now);
            else
            {
                if (speciesTiming && state=="move") age=age/speciesActor.Definition.moveCycle*clip.Duration;
                if (speciesTiming && state=="death") age=age/speciesActor.Definition.deathDuration*clip.Duration;
                Frame=clip.FrameAt(age,state=="move" || state=="idle" && clip.motion=="idle");
            }
            Frame=Mathf.Min(Frame,clip.frames.Length-1);
            var visual=actor.visual;
            visual.sprite=clip.frames[Frame];visual.flipX=actor.facing<0;
            ResetVisual(visual);
            visual.enabled=state!="death" || age<clip.Duration;
            if(state=="death"&&catalog.Find(species,"death")!=null)
            {
                if(!hitFeedback)hitFeedback=actor.arena.GetComponent<HitFeedbackBridge>();
                var body=catalog.Find(species,"idle");
                if(deathToken==0&&hitFeedback&&body?.frames?.Length>0)deathToken=hitFeedback.BeginDeath(actor,body.frames[0]);
                if(!visual.enabled&&hitFeedback)hitFeedback.CompleteDeath(actor,deathToken);
            }
            if(state=="hurt")
            {
                var idle=catalog.Find(species,"idle");
                if(idle!=null && idle.frames.Length>0)
                    Chapter1HitFeedback.ApplyPose(visual,idle.frames[0],species,now-hitAt,hitDirection);
            }
            feedback?.Draw(state=="hurt"?now-hitAt:float.PositiveInfinity,actor.arena.Controls.Current.SimulationPaused,now);
            if(attackEffects)attackEffects.Sample(this);
            wasDead=actor.Dead;
        }
    }
}
