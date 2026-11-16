using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Chapter2
{
    public struct C2Arc
    {
        public Vector2 start,end;public float duration,horizontal,vertical,gravity;
        public Vector2 Sample(float age)=>start+new Vector2(horizontal*age,vertical*age-gravity*age*age/2);
        public static bool TryCreate(Vector2 start,Vector2 end,float horizontalSpeed,float rise,out C2Arc arc,float minimumFlightSeconds=0)
        {
            arc=default;if(horizontalSpeed<=0||rise<=0||end.y-start.y>rise+.00001f||float.IsNaN(minimumFlightSeconds)||float.IsInfinity(minimumFlightSeconds)||minimumFlightSeconds<0)return false;
            float dx=end.x-start.x,dy=end.y-start.y,flight;
            if(Mathf.Abs(dx)<1f/32)
            {
                // Numerical degenerate case: approved vertical toss, normal speed is unchanged elsewhere.
                float v=Mathf.Sqrt(60*rise);flight=(v+Mathf.Sqrt(Mathf.Max(0,v*v-60*dy)))/30;
            }
            else flight=Mathf.Abs(dx)/horizontalSpeed;
            flight=Mathf.Max(flight,minimumFlightSeconds);
            if(flight<.00001f)return false;
            float rootG=(Mathf.Sqrt(2*rise)+Mathf.Sqrt(2*Mathf.Max(0,rise-dy)))/flight;
            arc=new C2Arc{start=start,end=end,duration=flight,horizontal=Mathf.Abs(dx)<1f/32?0:dx/flight,gravity=rootG*rootG,vertical=Mathf.Sqrt(2*rise)*rootG};return true;
        }
    }
    // Arena-owned effects continue after body recovery/cancellation and the thrower's death.
    // H3's death cleanup and room/reset cleanup are explicit independent policies.
    public sealed class Chapter2NormalOrdnance : MonoBehaviour
    {
        public sealed class Shot
        {
            public Chapter2NormalEnemy source;public C2ApprovedNormal config;public CombatHitOrigin origin;
            public ArenaSurface floor;public Vector2 target,position;public C2Arc arc;public float born;public int direction;
        }
        public sealed class Hazard
        {
            public Chapter2NormalEnemy source;public C2ApprovedNormal config;public CombatHitOrigin origin;
            public Vector2 point;public float born,next;public int direction;
        }
        sealed class Impact {public string clip;public Vector2 point;public float born;public Chapter2NormalEnemy source;}
        readonly List<Shot> shots=new List<Shot>();readonly List<Hazard> hazards=new List<Hazard>();readonly List<Impact> impacts=new List<Impact>();
        readonly List<SpriteRenderer> views=new List<SpriteRenderer>();int used;CombatArena arena;C2FxData fx;readonly Dictionary<int,float> lastHazardHit=new Dictionary<int,float>();
        public IReadOnlyList<Shot> Shots=>shots;public IReadOnlyList<Hazard> Hazards=>hazards;
        public int FlyingCount=>shots.Count;public int HazardCount=>hazards.Count;
        public static Chapter2NormalOrdnance Get(CombatArena owner)
        {
            var m=owner.GetComponent<Chapter2NormalOrdnance>();if(m)return m;
            m=owner.gameObject.AddComponent<Chapter2NormalOrdnance>();m.arena=owner;
            m.fx=JsonUtility.FromJson<C2FxData>(Resources.Load<TextAsset>("Chapter2AttackFx").text);
            owner.PlayerStepCompleted+=m.Step;owner.ResetCompleted+=m.Clear;owner.Deactivated+=m.Clear;return m;
        }
        public bool HasFlying(Chapter2NormalEnemy source)=>shots.Exists(x=>x.source==source);
        public Vector2 LatestPosition(Chapter2NormalEnemy source){for(int i=shots.Count-1;i>=0;i--)if(shots[i].source==source)return shots[i].position;return Vector2.zero;}
        public int EffectCount(Chapter2NormalEnemy source)=>shots.FindAll(x=>x.source==source).Count+hazards.FindAll(x=>x.source==source).Count+impacts.FindAll(x=>x.source==source).Count;
        public bool TryFloor(Vector2 from,out ArenaSurface surface,out Vector2 point)
        {
            surface=null;point=default;float best=float.PositiveInfinity;
            foreach(var s in FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None))
            {
                var c=s.GetComponent<Collider2D>();if(!c||!c.enabled||!s.gameObject.activeInHierarchy)continue;var b=c.bounds;
                if(from.x<b.min.x-.0001f||from.x>b.max.x+.0001f||b.max.y>from.y+.001f)continue;
                float distance=from.y-b.max.y;if(distance<best){best=distance;surface=s;point=new Vector2(from.x,b.max.y);}
            }
            return surface;
        }
        public void Launch(Chapter2NormalEnemy source,C2ApprovedNormal config,Vector2 launch,Vector2 target,ArenaSurface floor,C2Arc arc,float at,CombatHitOrigin origin)
        {shots.Add(new Shot{source=source,config=config,origin=origin,floor=floor,target=target,arc=arc,born=at,position=launch,direction=source.facing});}
        public void AddHazard(Chapter2NormalEnemy source,C2ApprovedNormal config,Vector2 point,float at,CombatHitOrigin origin,int direction)
        {hazards.Add(new Hazard{source=source,config=config,point=point,born=at,next=at+config.hazardFirst,origin=origin,direction=direction});}
        public void ClearOwner(Chapter2NormalEnemy source)
        {shots.RemoveAll(x=>x.source==source);hazards.RemoveAll(x=>x.source==source);impacts.RemoveAll(x=>x.source==source);Render();}
        public void Clear(){shots.Clear();hazards.Clear();impacts.Clear();lastHazardHit.Clear();foreach(var v in views)if(v)v.enabled=false;}
        static bool TargetSurfaceStillValid(Shot s)
        {
            if(!s.floor)return false;var c=s.floor.GetComponent<Collider2D>();if(!c||!c.enabled||!c.gameObject.activeInHierarchy)return false;
            var b=c.bounds;return Mathf.Abs(b.max.y-s.target.y)<.02f&&s.target.x>=b.min.x-.001f&&s.target.x<=b.max.x+.001f;
        }
        public void Step(float dt)
        {
            if(!arena||!arena.Player||dt<=0)return;
            if(arena.Player.Dead){Clear();return;}
            float now=arena.Now+dt;
            for(int i=shots.Count-1;i>=0;i--)
            {
                var s=shots[i];if(!s.source||s.config.id=="H3"&&s.source.Dead){shots.RemoveAt(i);continue;}
                float age=now-s.born;if(age<0)continue;
                if(age>=s.config.projectileLifetime){shots.RemoveAt(i);continue;}
                var next=s.arc.Sample(age);var delta=next-s.position;bool removed=false;
                var hits=Physics2D.BoxCastAll(s.position,s.config.attack.Size,0,delta.normalized,delta.magnitude);
                Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
                foreach(var hit in hits)
                {
                    var actor=hit.collider.GetComponentInParent<CombatActor>();
                    if(actor&&(actor==arena.Player||actor.isDecoy&&actor.arena==arena)&&!actor.Hidden&&!actor.Dead)
                    {
                        bool accepted=C2NormalHit.Apply(s.source,actor,s.config.damage,s.direction,s.config.playerLock,0,0,false,s.origin,true);
                        if(accepted){s.source.CountOrdnanceHit();ImpactShot(s,hit.centroid,now,true);removed=true;break;}
                    }
                    if(TargetSurfaceStillValid(s)&&hit.collider==s.floor.GetComponent<Collider2D>()&&delta.y<0&&hit.normal.y>.5f)
                    {ImpactShot(s,new Vector2(hit.point.x,s.target.y),now,false);removed=true;break;}
                }
                s.position=next;if(removed)shots.RemoveAt(i);
            }
            // Absolute per-hazard ticks, with a single accepted-hit throttle across every caster.
            for(int i=hazards.Count-1;i>=0;i--)
            {
                var h=hazards[i];if(!h.source||h.source.Dead||now>=h.born+h.config.hazardDuration){hazards.RemoveAt(i);continue;}
                while(now+.00001f>=h.next)
                {
                    float tickAt=h.next;h.next+=h.config.hazardTick;
                    if(tickAt>=h.born+h.config.hazardDuration)break;
                    var box=new Bounds(h.point+Vector2.up*h.config.hazard.Size.y/2,h.config.hazard.Size);
                    foreach(var target in C2NormalHit.Targets(arena))
                    {
                        float last=lastHazardHit.TryGetValue(target.ActorId,out float value)?value:float.NegativeInfinity;
                        if(!target.grounded||!target.hurtCollider.bounds.Intersects(box)||tickAt-last<h.config.hazardTick-.00001f)continue;
                        if(C2NormalHit.Apply(h.source,target,h.config.hazardDamage,h.direction,0,0,0,true,h.origin,true))
                        {lastHazardHit[target.ActorId]=tickAt;h.source.CountOrdnanceHit();}
                    }
                }
            }
            impacts.RemoveAll(x=>now-x.born>.6f);
        }
        void ImpactShot(Shot s,Vector2 at,float now,bool direct)
        {
            s.source.ImpactAt=now;
            impacts.Add(new Impact{source=s.source,point=at,born=now,clip=s.config.id=="H3"?"h3-burst":"r-break"});
            if(s.config.id!="H3")return;
            Vector2 floorPoint=at;
            if(direct&&!TryFloor(at,out _,out floorPoint))return;
            AddHazard(s.source,s.config,floorPoint,now,s.origin,s.direction);
        }
        C2FxClip Clip(string id)=>Array.Find(fx.clips,c=>c.id==id)??throw new InvalidOperationException(id);
        void Show(string clip,float age,Vector2 at,Chapter2NormalEnemy source)
        {
            if(used==views.Count){var node=new GameObject("Existing chapter2 projectile/hazard FX");node.transform.SetParent(transform,false);views.Add(node.AddComponent<SpriteRenderer>());}
            var r=views[used++];r.enabled=true;r.sprite=Clip(clip).Sample(age);r.transform.position=at;r.transform.localScale=Vector3.one;r.color=Color.white;
            r.sharedMaterial=arena.spriteMaterial;r.sortingOrder=source&&source.visual?source.visual.sortingOrder+2:12;
        }
        public void Render()
        {
            foreach(var r in views)if(r)r.enabled=false;used=0;if(arena==null||fx==null)return;
            foreach(var s in shots)Show(s.config.id=="H3"?"h3-orb":"r-can",0,s.position,s.source);
            foreach(var h in hazards)Show("h3-puddle",arena.Now-h.born,h.point,h.source);
            foreach(var x in impacts)if(arena.Now-x.born<Clip(x.clip).Duration)Show(x.clip,arena.Now-x.born,x.point,x.source);
        }
        void LateUpdate()=>Render();
        void OnDestroy(){if(arena){arena.PlayerStepCompleted-=Step;arena.ResetCompleted-=Clear;arena.Deactivated-=Clear;}Clear();}
    }
}
