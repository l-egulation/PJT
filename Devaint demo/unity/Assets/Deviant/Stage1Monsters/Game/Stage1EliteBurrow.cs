using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    // Opt-in village pattern, sharing the existing execution / accepted-hit boundary.
    public sealed class Stage1EliteBurrow : MonoBehaviour
    {
        public FieldMouseEnemy Actor { get; private set; }
        public VillageBurrowSpec Spec { get; private set; }
        public bool Active { get; private set; }
        public bool Locked { get; private set; }
        public bool Emerged { get; private set; }
        public Vector2 Origin { get; private set; }
        public Vector2 Destination { get; private set; }
        public float NextAvailableAt { get; private set; }
        public int Eruptions { get; private set; }
        public int Hits { get; private set; }
        public string SelectionStatus { get; private set; }
        public float Age=>Actor?Actor.SpeciesAttackAge:0;
        public Bounds DamageBounds=>new Bounds(Destination+Vector2.up*Spec.attackHeight,
            new Vector3(Spec.attackSize[0],Spec.attackSize[1],1));
        bool concealed,bodyEnabled,hurtEnabled;
        float previousStealth,ownedStealth;
        string placementFailure;
        readonly HashSet<int> struck=new HashSet<int>();

        public void Bind(FieldMouseEnemy actor,VillageBurrowSpec spec)
        { Actor=actor;Spec=spec;actor.arena.ResetCompleted+=Reset; }
        void Reset() { Cancel();NextAvailableAt=0;Eruptions=Hits=0; }
        void OnDestroy() { Cancel();if(Actor && Actor.arena)Actor.arena.ResetCompleted-=Reset; }
        void OnDisable()=>Cancel();
        public void Cancel()
        { Restore();Active=Locked=Emerged=false;struck.Clear(); }
        public void Refresh()
        {
            if(Active && (!Actor || Actor.Dead || !Actor.aggressive || !Actor.SpeciesAttackVisible))Cancel();
        }
        public void BeginExecution(int sequence,float now)
        {
            Cancel();
            // The first attack stays a hook/claw. Later non-melee attacks may burrow.
            SelectionStatus=$"sequence={sequence}, now={now}, cooldown={NextAvailableAt}, grounded={Actor.grounded}, feet={Actor.Position}";
            if(sequence<2 || now<NextAvailableAt || !Spec.enabled)return;
            // Use exact floor support rather than the last motor contact flag.
            if(!Safe(Actor.Position)) { SelectionStatus+=" "+placementFailure;return; }
            Origin=Destination=Actor.Position;Active=true;NextAvailableAt=now+Spec.cooldown;
            SelectionStatus="burrow";
        }
        bool Safe(Vector2 feet)
        {
            if(Mathf.Abs(feet.x-Actor.spawn.x)>=Actor.Definition.leash) { placementFailure="outside leash";return false; }
            if(!Actor.FullSupport(feet,feet)) { placementFailure=$"no complete floor support at {feet.x:F5},{feet.y:F5}";return false; }
            // Reuse the motor's exact floor/bounds recheck: native overlaps also report contact margins.
            if(Actor.Blocked(feet,Actor.facing)) { placementFailure="solid body obstruction";return false; }
            var center=feet+new Vector2(Actor.offset.x*Actor.facing,Actor.offset.y);
            var size=Actor.size-Vector2.one*.025f;
            var body=new Bounds(center,new Vector3(size.x,size.y,1));
            foreach(var col in Physics2D.OverlapBoxAll(center,size,0))
                if(col.GetComponent<ArenaPit>() && body.Intersects(col.bounds))
                { placementFailure=$"blocked by {col.name} bounds={col.bounds} candidate={center} size={size}";return false; }
            return true;
        }
        public bool TryGround(Vector2 under,out Vector2 destination)
        {
            destination=Origin;float nearest=float.PositiveInfinity;bool found=false;
            foreach(var hit in Physics2D.RaycastAll(under+Vector2.up*.05f,Vector2.down,Spec.groundSearch+.05f))
            {
                if(!hit.collider.GetComponent<ArenaSurface>() || hit.normal.y<.99f || hit.distance>=nearest)continue;
                var feet=hit.point+Vector2.up*.003f;
                if(Vector2.Distance(feet,Origin)>Spec.maxTravel || !Safe(feet))continue;
                nearest=hit.distance;destination=feet;found=true;
            }
            return found;
        }
        void Conceal()
        {
            if(concealed)return;
            bodyEnabled=Actor.bodyCollider.enabled;hurtEnabled=Actor.hurtCollider.enabled;
            previousStealth=Actor.stealthUntil;
            ownedStealth=Mathf.Max(previousStealth,(float)Actor.Brain.StartedAt+Spec.eruptionStart+.1f);
            Actor.stealthUntil=ownedStealth;
            Actor.bodyCollider.enabled=false;Actor.hurtCollider.enabled=false;concealed=true;
        }
        void Restore()
        {
            if(!concealed || !Actor)return;
            if(Actor.stealthUntil==ownedStealth)Actor.stealthUntil=previousStealth;
            Actor.bodyCollider.enabled=bodyEnabled;Actor.hurtCollider.enabled=hurtEnabled;concealed=false;
        }
        public void Step(float from,float to)
        {
            Refresh();if(!Active)return;
            if(to>=Spec.meltEnd && !Emerged)Conceal();
            if(!Locked && to>=Spec.warningStart)
            {
                // Lock once. A target that dodges after this point escapes the fixed eruption.
                var target=Actor.Focus?Actor.Focus:Actor.arena.Player;
                if(!target || target.Dead || target.Hidden || !TryGround(target.Position,out var feet))
                {
                    if(!Safe(Origin)) { Actor.CancelAttack();Cancel();return; }
                    feet=Origin;
                }
                Destination=feet;Locked=true;
            }
            if(Locked && !Emerged && to>=Spec.eruptionStart)
            {
                if(!Safe(Destination)) { Actor.CancelAttack();Cancel();return; }
                Actor.Position=Destination;Actor.velocity=Vector2.zero;Actor.grounded=true;
                Restore();Physics2D.SyncTransforms();Emerged=true;Eruptions++;
            }
            if(!Emerged || from>=Spec.damageEnd || to<=Spec.eruptionStart)return;
            var bounds=DamageBounds;
            foreach(var target in Actor.arena.TargetsInBox(bounds.center,bounds.size))
            {
                if(!Active || !Actor.Brain.Executing)break;
                if(!target || target.Dead || target.Hidden || target!=Actor.arena.Player && !target.isDecoy ||
                   struck.Contains(target.ActorId) || Actor.arena.Occluded(Actor.Center,target.Center,false))continue;
                int parries=target.ParryCount;
                if(Actor.DeliverSpeciesHit(target,Actor.Definition.damage,2)) { struck.Add(target.ActorId);Hits++; }
                else if(target.ParryCount!=parries)struck.Add(target.ActorId);
            }
        }
    }
}
