using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.World;
using UnityEngine;

namespace Deviant.Game
{
    // Presentation observes common combat state; it never changes HP, colliders or time.
    public class ForestEnemyPresentation : MonoBehaviour
    {
        public TestEnemy actor;
        public InkMonsterData data;
        InkMonsterMotion motion;
        float clock, flashUntil, deathAge;
        float lastWorldNow;
        bool wasDead;
        long deathToken;
        HitFeedbackBridge hitFeedback;
        readonly CombatAcceptedHit[] hitPage = new CombatAcceptedHit[64];
        CombatOutcomeCursor hitCursor;
        CombatActorHandle hitTarget;
        CombatArena hitArena;
        public long LostHitFacts { get; private set; }
        void OnEnable() { BindHitArena(); }
        void BindHitArena()
        {
            UnbindHitArena();
            if (!actor || !actor.arena) return;
            hitArena = actor.arena;
            hitArena.Initialized += BindHitSession; hitArena.ResetCompleted += BindHitSession;
            BindHitSession();
        }
        void BindHitSession()
        {
            if(hitFeedback&&actor)hitFeedback.CancelDeath(actor,deathToken);deathToken=0;
            hitCursor = default; hitTarget = default; flashUntil = 0;
            lastWorldNow=actor&&actor.arena?actor.arena.Now:0;
            if (hitArena && actor && hitArena.TryBindActor(actor, out hitTarget)) hitArena.HitEvents.TryGetCursor(out hitCursor);
        }
        void UnbindHitArena()
        {
            if (hitArena) { hitArena.Initialized -= BindHitSession; hitArena.ResetCompleted -= BindHitSession; }
            hitArena = null; hitTarget = default;
        }
        void PollAcceptedHits()
        {
            if (!hitArena || hitTarget.ActorId <= 0) return;
            while (hitArena.HitEvents.TryRead(hitCursor, hitPage, out var page))
            {
                hitCursor = page.Next; LostHitFacts += page.DroppedBefore;
                for (int i = 0; i < page.Count; i++) if (hitPage[i].Target.Equals(hitTarget))
                    flashUntil = hitPage[i].Now + .08f;
                if (page.Count < hitPage.Length) break;
            }
        }
        void OnDisable() { if(hitFeedback&&actor)hitFeedback.CancelDeath(actor,deathToken);deathToken=0;UnbindHitArena(); }
        void Start()
        {
            motion = gameObject.AddComponent<InkMonsterMotion>();
            motion.data = data; motion.target = actor.visual;
            BindHitArena();
        }
        void LateUpdate()
        {
            if (!motion) return;
            float worldDelta=Mathf.Max(0,actor.arena.Now-lastWorldNow);lastWorldNow=actor.arena.Now;
            clock += worldDelta;
            PollAcceptedHits();
            var visual = actor.visual;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            visual.color = actor.arena.Now < flashUntil ? Color.red : Color.white;
            visual.enabled = true;
            if (actor.Dead)
            {
                if (!wasDead) deathAge = 0;
                deathAge += worldDelta;
                if (data.death != null && data.death.frames != null && data.death.frames.Length > 0)
                {
                    if(!hitFeedback)hitFeedback=actor.arena.GetComponent<HitFeedbackBridge>();
                    if(deathToken==0&&hitFeedback&&data.move?.frames?.Length>0)deathToken=hitFeedback.BeginDeath(actor,data.move.frames[0]);
                    motion.ShowDeath(deathAge, actor.facing);
                    visual.enabled = deathAge < data.death.Duration;
                    if(!visual.enabled&&hitFeedback)hitFeedback.CompleteDeath(actor,deathToken);
                }
                else
                {
                    if(!wasDead)
                    {
                        if(!hitFeedback)hitFeedback=actor.arena.GetComponent<HitFeedbackBridge>();
                        if(hitFeedback)hitFeedback.ReportMissingDeathClip(actor);
                    }
                    float t = Mathf.Clamp01(deathAge / .6f);
                    visual.transform.localRotation = Quaternion.Euler(0,0,-actor.facing*70*t);
                    visual.transform.localPosition = Vector3.down * .3f * t;
                    visual.transform.localScale = new Vector3(1,1-.6f*t,1);
                    visual.color = new Color(1,1,1,1-t);
                }
            }
            else
            {
                if (wasDead) { if(hitFeedback)hitFeedback.CancelDeath(actor,deathToken);deathToken=0;flashUntil = 0; clock = 0; }
                bool attacking = actor.Telegraph >= 0;
                motion.Show(attacking ? "attack" : "move", attacking ? actor.AttackElapsed : clock, actor.facing);
            }
            wasDead = actor.Dead;
        }
    }
}
