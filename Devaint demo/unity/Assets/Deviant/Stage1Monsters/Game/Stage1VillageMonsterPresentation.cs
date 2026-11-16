using Deviant.Combat;
using Deviant.Integration;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    [DefaultExecutionOrder(650)]
    public sealed class Stage1VillageMonsterPresentation : MonoBehaviour
    {
        public FieldMouseEnemy Actor { get; private set; }
        public Stage1MonsterPlayer Player { get; private set; }
        int hits,execution=-1;
        float lastNow, motionStart, hurtUntil;
        float hitAt=float.NegativeInfinity;
        Chapter1HitFeedback feedback;
        HitFeedbackBridge hitFeedback;
        long deathToken;
        bool debrisSpawned;
        float debrisAt;
        // The finishing white impact stays on the body this long before the debris fully replaces it.
        const float FinishingFlashSeconds=.08f;
        Stage1VillageMonsterEffects effects;
        Stage1VillageMonsterAttack attack;
        public Stage1VillageWindupCue Windup { get; private set; }
        Vector2 previous;
        public void Bind(FieldMouseEnemy actor,Stage1MonsterPlayer player)
        {
            Actor=actor;Player=player;effects=actor.GetComponent<Stage1VillageMonsterEffects>();attack=actor.GetComponent<Stage1VillageMonsterAttack>();
            if(attack)Windup=new Stage1VillageWindupCue(actor,attack);
            Reset();actor.arena.ResetCompleted+=Reset;
        }
        void Reset()
        {
            if(!Actor)return;
            if(hitFeedback)hitFeedback.CancelDeath(Actor,deathToken);
            deathToken=0;debrisSpawned=false;
            hits=Actor.ReceivedHitCount;execution=-1;lastNow=motionStart=Actor.arena.Now;hurtUntil=0;
            hitAt=float.NegativeInfinity;feedback?.Clear();
            previous=Actor.Position;Player.Play("idle");
        }
        void OnEnable()
        { if(Actor)hits=Actor.ReceivedHitCount;hitAt=float.NegativeInfinity; }
        void OnDisable()
        { if(hitFeedback&&Actor)hitFeedback.CancelDeath(Actor,deathToken);deathToken=0;debrisSpawned=false;feedback?.Clear();hitAt=float.NegativeInfinity;if(Actor)hits=Actor.ReceivedHitCount; }
        void OnDestroy()
        { if(Actor && Actor.arena)Actor.arena.ResetCompleted-=Reset;feedback?.Dispose(); }
        void LateUpdate()=>Draw();
        public void Draw()
        {
            if(!isActiveAndEnabled || !Actor || !Player || !Actor.arena)return;
            float now=Actor.arena.Now;
            if(now<lastNow || Actor.ReceivedHitCount<hits)Reset();
            if(Actor.ReceivedHitCount>hits)
            {
                hitAt=now;hurtUntil=Mathf.Max(now,Actor.staggerUntil);
                if(feedback==null)feedback=new Chapter1HitFeedback(Actor.visual);
                feedback.VisualsEnabled=!(Actor.arena.FeelFeedback && Actor.arena.FeelFeedback.EffectsEnabled);
                int direction=now<Actor.knockUntil && Mathf.Abs(Actor.knockSpeed)>.0001f ?
                    (Actor.knockSpeed<0?-1:1) : Actor.arena.Player ?
                    (Actor.Center.x<Actor.arena.Player.Center.x?-1:1) : -Actor.facing;
                // Use the fixed torso box, not transparent sprite padding or the long hook canvas.
                Vector3 origin=Actor.Center+Vector2.left*(direction*Actor.size.x*.3f);
                for(int i=hits;i<Actor.ReceivedHitCount;i++)feedback.Hit(origin,i+1);
            }
            hits=Actor.ReceivedHitCount;
            bool moving=Mathf.Abs(Actor.Position.x-previous.x)>.00001f || Mathf.Abs(Actor.velocity.x)>.001f;
            previous=Actor.Position;
            string state=Actor.Dead?"death":now<hurtUntil?"hit":Actor.SpeciesAttackVisible?"attack":moving?"walk":"idle";
            if(Player.Motion!=state || state=="attack" && execution!=Actor.Brain.Execution)
            { Player.Play(state,true);motionStart=now;execution=Actor.Brain.Execution; }
            Player.faceRight=Actor.facing>0; // All approved source frames face left.
            Player.SampleAt(state=="attack"?(attack?attack.MotionAge:Actor.SpeciesAttackAge):now-motionStart);
            if(Actor.Dead)
            {
                if(!hitFeedback)hitFeedback=Actor.arena.GetComponent<HitFeedbackBridge>();
                var body=Player.Definition?.Find("idle");
                if(Player.Motion=="death")
                {
                    // Debris breaks out on the lethal frame together with the drops, rather than after the death clip.
                    if(deathToken==0&&hitFeedback&&body?.frames?.Length>0)
                    {
                        deathToken=hitFeedback.BeginDeath(Actor,body.frames[0]);
                        if(deathToken!=0&&(debrisSpawned=hitFeedback.CompleteDeath(Actor,deathToken)))debrisAt=now;
                    }
                }
                else if(hitFeedback)hitFeedback.ReportMissingDeathClip(Actor);
                // The finishing silhouette and ink burst keep their ownership; spawned debris replaces the body.
                if(debrisSpawned&&now-debrisAt>=FinishingFlashSeconds || Actor.arena.FeelFeedback&&Actor.arena.FeelFeedback.OwnsDeathVisual(Actor.visual))Actor.visual.enabled=false;
            }
            Actor.visual.color=Color.white;
            Actor.visual.transform.localPosition=Vector3.zero;
            Actor.visual.transform.localScale=Vector3.one;
            Windup?.Draw(state=="attack");
            // The finishing blow keeps its impact flash while the native death clip starts.
            if(feedback!=null) {
                feedback.VisualsEnabled=!(Actor.arena.FeelFeedback && Actor.arena.FeelFeedback.EffectsEnabled);
                feedback.Draw(now-hitAt,Actor.arena.Controls.Current.SimulationPaused);
            }
            if(effects)effects.Draw();
            lastNow=now;
        }
    }
}
