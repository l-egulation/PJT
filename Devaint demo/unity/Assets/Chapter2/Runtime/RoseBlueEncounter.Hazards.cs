using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Chapter2.RoseBlue;
using Deviant.Inventory.Unity;
using UnityEngine;
using HitLedger=Deviant.Chapter2.RoseBlue.HitLedger;

namespace Deviant.Chapter2
{
    public sealed partial class RoseBlueEncounter
    {
        sealed class Hazard
        {
            public Cast Cast;public string Kind,Group;public Vector2 At,Velocity,Size;public float Damage,Life,Age,WhiteAt,NextTick;
            public bool OrbTurned,FromLeft;
            public float OrbLaunchX,GuidanceStarted;
            public bool Redirected,Reflected,White,Landed,Homing=true,Inside,Consumed;public SpriteRenderer View,Art,WhiteArt;public Rigidbody2D Body;
            public RootContact Root;public readonly ReentryContact Reentry=new ReentryContact();public readonly HitLedger Hits=new HitLedger();
            // Homing bolt only: where its artwork was over the last 0.2 s, for the afterimages.
            public readonly List<(Vector2 at,float angle,float time)> Trail=new List<(Vector2,float,float)>();public SpriteRenderer[] Ghosts;
            public bool Damaging=>Kind!="flash"&&Kind!="visual-rain"&&Kind!="trap-flash";
        }
        readonly List<Hazard> hazards=new List<Hazard>();readonly HitLedger explosionHits=new HitLedger();
        readonly List<RoseBlueActor> exchangeActors=new List<RoseBlueActor>();Cast exchangeCast;float exchangeDistance,exchangeAge;Vector2 exchangeMeeting;
        Hazard Fire(Cast cast,string kind,RoseBlueActor source,Vector2 velocity,Vector2 size,float damage,string group)
        {
            var at=Hand(source);if(kind=="orb")at=new Vector2(at.x,source.Position.y+1);
            var h=new Hazard{Cast=cast,Kind=kind,Group=group,At=at,Velocity=velocity,Size=size,Damage=damage,Life=float.PositiveInfinity};
            h.View=RectView(kind,Centered(at,size.x,size.y),kind=="orb"?new Color(.15f,.2f,.35f):new Color(.85f,.42f,.56f),22);
            if(kind=="orb")h.View.sprite=arena.CircleSprite;
            h.View.transform.localScale=Vector3.one;
            var shape=h.View.gameObject.AddComponent<BoxCollider2D>();shape.size=size;shape.isTrigger=true;
            h.Body=h.View.gameObject.AddComponent<Rigidbody2D>();h.Body.bodyType=RigidbodyType2D.Kinematic;h.Body.gravityScale=0;h.Body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            var image=new GameObject("Projectile artwork").AddComponent<SpriteRenderer>();image.transform.SetParent(h.View.transform,false);image.sprite=h.View.sprite;image.color=h.View.color;image.sortingOrder=22;image.transform.localScale=size;if(arena.spriteMaterial)image.sharedMaterial=arena.spriteMaterial;
            h.View.enabled=false;BindProjectileArt(h);hazards.Add(h);return h;
        }
        Hazard Zone(Cast c,string kind,Rect box,float damage,float life,string group,bool once=false)
        {
            var h=new Hazard{Cast=c,Kind=kind,Group=group,At=box.center,Size=box.size,Damage=damage,Life=life,Root=kind=="root"?new RootContact(once):null};
            h.View=RectView(kind,box,kind=="root"?new Color(.1f,.12f,.2f,.8f):new Color(.8f,.8f,.95f,.7f),19);BindZoneArt(h);hazards.Add(h);return h;
        }
        // Phase3 bounce arrows and the phase2 stun arrow reflect off the arena's four sides until parried.
        static bool Bouncing(Hazard h)=>(h.Kind=="bounce"||h.Kind=="stun")&&!h.Reflected;
        // Blue's ink fireball bursts into a 4×4u blast where it is, on touching the player or when its fuse runs out.
        void ExplodeInk(Hazard h)
        {
            var at=h.At;var cast=h.Cast;RemoveHazard(h);
            Zone(cast,"ink-blast",Centered(at,(float)Tuning.InkBallBlastSize,(float)Tuning.InkBallBlastSize),(float)Tuning.InkBallDamage,(float)Tuning.ExplosionActive,null);
        }
        void RemoveHazard(Hazard h)
        {if(h.Consumed)return;h.Consumed=true;hazards.Remove(h);if(h.View){h.View.gameObject.SetActive(false);Destroy(h.View.gameObject);}}
        void RemoveGroup(Cast cast,string group){foreach(var h in hazards.Where(h=>h.Cast==cast&&h.Group==group).ToArray())RemoveHazard(h);}
        void StepHazards(float dt)
        {
            foreach(var h in hazards.ToArray())
            {
                if(h.Consumed)continue;
                h.Age+=dt;if(h.Age>=h.Life){if(h.Kind=="ink-ball")ExplodeInk(h);else RemoveHazard(h);continue;}
                if(h.Body)
                {
                    if(h.Kind=="homing"&&h.Homing&&h.Age<(float)Tuning.HomingLife)
                    {var v=HazardGeometry.HomingVelocity(new Point(h.At.x,h.At.y),new Point(h.Velocity.x,h.Velocity.y),new Point(arena.Player.Center.x,arena.Player.Center.y),dt);h.Velocity=new Vector2((float)v.X,(float)v.Y);}
                    if(h.Kind=="duo-homing"&&!h.Reflected&&h.Age<Tuning.DuoHomingSeconds)h.Velocity=TurnToward(h.Velocity,arena.Player.Center-h.At,(float)Tuning.DuoTurnDegrees,dt);
                    if(h.Kind=="orb"&&h.OrbTurned&&h.Age-h.GuidanceStarted<Tuning.Phase1OrbHomingSeconds)h.Velocity=TurnToward(h.Velocity,arena.Player.Center-h.At,(float)Tuning.Phase1OrbTurnDegrees,dt);
                    if(h.Kind=="bounce"&&h.Reflected)h.Velocity=(Blue.Center-h.At).normalized*(float)Tuning.BounceSpeed;
                    var segments=new List<Segment>();
                    if(Bouncing(h))
                    {
                        var pos=new Point(h.At.x,h.At.y);var vel=new Point(h.Velocity.x,h.Velocity.y);
                        var extent=RotatedExtent(h.Size,h.Velocity);var bounds=new Box(Stage.Area.xMin+extent.x,Stage.Floor+extent.y,Stage.Area.xMax-extent.x,Stage.Area.yMax-extent.y);
                        pos=new Point(Math.Max(bounds.Left,Math.Min(bounds.Right,pos.X)),Math.Max(bounds.Bottom,Math.Min(bounds.Top,pos.Y)));
                        segments=HazardGeometry.Bounce(ref pos,ref vel,bounds,dt,random);h.Velocity=new Vector2((float)vel.X,(float)vel.Y);
                    }
                    else segments.Add(new Segment(new Point(h.At.x,h.At.y),new Point(h.At.x+h.Velocity.x*dt,h.At.y+h.Velocity.y*dt)));
                    bool sweptPlayer=false;
                    foreach(var segment in segments)
                    {
                        var from=new Vector2((float)segment.From.X,(float)segment.From.Y);var to=new Vector2((float)segment.To.X,(float)segment.To.Y);var delta=to-from;
                        // Use Unity's swept collider queries, so a fast arrow cannot skip a narrow hurtbox.
                        var contacts=Physics2D.BoxCastAll(from,h.Size,h.Kind=="orb"||h.Kind=="ink-ball"?0:Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,delta.normalized,delta.magnitude);
                        foreach(var hit in contacts.OrderBy(x=>x.distance))
                        {
                            var region=hit.collider?hit.collider.GetComponent<HurtRegion>():null;if(!region)continue;var actor=region.actor;
                            if(actor==arena.Player){sweptPlayer=true;if(ProjectileContact(h,from))break;}
                            else if(actor==Machine&&h.Kind=="orb"&&h.OrbTurned){
                                StartSheetFx("orb-hit",h.At,1,false,AbovePlayer(4));
                                Model.DamageBatch(Tuning.Phase1OrbMachineDamage,0,0);RemoveHazard(h);DrainSignals();SyncHealth();break;
                            }
                            else if(actor==Blue&&(h.Reflected||h.Kind=="homing"&&h.Age>=(float)Tuning.HomingOwnerGrace))
                            {
                                if(h.Kind=="homing") {Model.HomingArrowHitsBlue();rotation.HomingRemoved(h.Cast.Id,true);h.Cast.Clock.CancelHazard("homing");DrainSignals();SyncHealth();}
                                else{if(h.Kind=="bounce")StartSheetFx("p3-blast",Blue.Center,1,false,AbovePlayer(4));Blue.Receive(h.Kind=="bounce"?(float)Tuning.ReflectedBounceDamage:h.Damage*CombatArena.LoadRunInkCatalog().damageUnitScale,arena.Player,h.Velocity.x>=0?1:-1,false,null,false,true);if(h.Group!=null)h.Cast.Clock.CancelHazard(h.Group);}
                                RemoveHazard(h);break;
                            }
                            else if(actor==Rose&&h.Reflected){Rose.Receive(h.Damage*CombatArena.LoadRunInkCatalog().damageUnitScale,arena.Player,h.Velocity.x>=0?1:-1,false,null,false,true);RemoveHazard(h);break;}
                        }
                        if(h.Consumed)break;
                        if(h.Redirected){h.Redirected=false;h.At=from;break;}
                        h.At=to;
                    }
                    if(h.Consumed)continue;
                    if(Bouncing(h)&&(!sweptPlayer||!Physics2D.OverlapBoxAll(h.At,h.Size,Mathf.Atan2(h.Velocity.y,h.Velocity.x)*Mathf.Rad2Deg).Any(col=>col.GetComponent<HurtRegion>()?.actor==arena.Player)))h.Reentry.Observe(false,arena.Now,!h.White,()=>false);
                    if(h.Kind=="orb"&&!sweptPlayer)h.Inside=false;
                    if(h.Kind=="rain"&&!h.Reflected&&h.At.y<Stage.Floor+.5f)h.At=new Vector2(h.At.x,Stage.Floor+.5f);
                    if(Bouncing(h)){var ext=RotatedExtent(h.Size,h.Velocity);h.At=new Vector2(Mathf.Clamp(h.At.x,Stage.Area.xMin+ext.x,Stage.Area.xMax-ext.x),Mathf.Clamp(h.At.y,Stage.Floor+ext.y,Stage.Area.yMax-ext.y));}
                    if (h.Kind == "orb" && !h.OrbTurned && Mathf.Abs(h.At.x - h.OrbLaunchX) >= Stage.Area.width / 2)
                    {
                        h.OrbTurned = true;
                        h.GuidanceStarted = h.Age;
                    }
                    bool guidingOrb = h.Kind == "orb" && h.OrbTurned && h.Age - h.GuidanceStarted < Tuning.Phase1OrbHomingSeconds;
                    h.Body.MovePosition(h.At);
                    if(h.Kind!="orb"&&h.Kind!="ink-ball"&&h.Velocity.sqrMagnitude>0)h.Body.MoveRotation(Mathf.Atan2(h.Velocity.y,h.Velocity.x)*Mathf.Rad2Deg);
                    // The collider and artwork share the same world rotation; homing afterimages keep the rotation of where they were.
                    foreach(var v in h.View.GetComponentsInChildren<SpriteRenderer>())if(v!=h.View&&(h.Ghosts==null||System.Array.IndexOf(h.Ghosts,v)<0))
                    {v.transform.localRotation=Quaternion.identity;if(h.White&&!h.Art)v.color=Color.Lerp(new Color(.85f,.42f,.56f),Color.white,Mathf.Clamp01(arena.Now-h.WhiteAt));}
                    bool outside=h.At.x<Stage.Area.xMin-h.Size.x||h.At.x>Stage.Area.xMax+h.Size.x||h.At.y>Stage.Area.yMax+h.Size.y||h.At.y<Stage.Origin.y-h.Size.y;
                    // The ink fireball ignores the arena bounds and bursts on its fuse even off screen.
                    if(outside&&!Bouncing(h)&&h.Kind!="bounce"&&h.Kind!="ink-ball"&&!guidingOrb)
                    {if(h.Kind=="homing"&&h.Homing&&h.Age<(float)Tuning.HomingLife)continue;if(h.Kind=="homing")rotation.HomingRemoved(h.Cast.Id,false);RemoveHazard(h);}
                }
                else if(h.Kind=="root")
                {
                    var p=arena.Player;var box=Centered(h.At,h.Size.x,h.Size.y);
                    bool touch=p.Position.y>=box.yMin-.05f&&p.Position.y<=box.yMax&&p.Center.x+p.size.x/2>box.xMin&&p.Center.x-p.size.x/2<box.xMax;
                    if(h.Root.Observe(touch&&!p.FallProtected,!ProtectsPlayer&&!p.Dead)){bossRootUntil=arena.Now+(float)(h.Group=="traps"?Tuning.TrapRoot:Tuning.RootSeconds);p.rootUntil=Mathf.Max(p.rootUntil,bossRootUntil);p.velocity=new Vector2(0,p.velocity.y);}
                }
                else if(h.Kind=="fan-area")
                {
                    float progress=Mathf.Clamp01(h.Age/(float)Tuning.FanExpandSeconds);
                    float width=h.Size.x*progress,height=h.Size.y*progress;
                    var box=new Rect(h.At.x-width/2,Stage.Floor,width,height);
                    h.View.transform.position=box.center;h.View.transform.localScale=box.size;
                    if(arena.Now>=h.NextTick&&Hit(box,random.Next((int)Tuning.FanMinDamage,(int)Tuning.FanMaxDamage+1)))
                        h.NextTick=arena.Now+(float)Tuning.FanTick;
                }
                else if(h.Kind=="swap-flank")
                {
                    float width=(float)Tuning.SwapWidth*Mathf.Clamp01(h.Age/(float)Tuning.SwapExpandSeconds);
                    var box=new Rect(h.FromLeft?Stage.Area.xMin:Stage.Area.xMax-width,Stage.Floor,width,(float)Tuning.SwapHeight);
                    h.View.transform.position=box.center;h.View.transform.localScale=box.size;
                    h.Hits.TryHit("flank",arena.Player.ActorId,()=>Hit(box,h.Damage));
                }
                else if(h.Damage>0)
                {
                    var rect=Centered(h.At,h.Size.x,h.Size.y);
                    if(h.Reflected){if(rect.Overlaps(Centered(Blue.Center,Blue.size.x,Blue.size.y)))explosionHits.TryHit("bounce-"+h.Cast.Id,Blue.ActorId,()=>Blue.Receive(h.Damage*CombatArena.LoadRunInkCatalog().damageUnitScale,arena.Player,1,false,null,false,true));}
                    else if(h.Kind=="bounce-blast")explosionHits.TryHit("bounce-"+h.Cast.Id,arena.Player.ActorId,()=>Hit(rect,h.Damage));
                    else h.Hits.TryHit("explosion",arena.Player.ActorId,()=>Hit(rect,h.Damage));
                }
            }
        }
        static Vector2 RotatedExtent(Vector2 size,Vector2 velocity)
        {var direction=velocity.sqrMagnitude>0?velocity.normalized:Vector2.right;return new Vector2(Mathf.Abs(direction.x)*size.x+Mathf.Abs(direction.y)*size.y,Mathf.Abs(direction.y)*size.x+Mathf.Abs(direction.x)*size.y)/2;}
        void ReflectProjectile(Hazard h)
        {
            h.Reflected=true;h.Redirected=true;h.Homing=false;h.Velocity=h.Kind=="bounce"?(Blue.Center-h.At).normalized*(float)Tuning.BounceSpeed:-h.Velocity;
            if(h.Kind=="bounce")h.Cast.Clock.CancelHazard(h.Group);
        }
        bool ProjectileContact(Hazard h,Vector2 from)
        {
            var p=arena.Player;if(h.Reflected||h.White||h.Landed||ProtectsPlayer)return false;
            // Parry without reflection (the projectile must be faced): the fireball is put out before it bursts,
            // a falling arrow is destroyed with its landing blast, and an orb tick is blocked while the orb stays.
            bool Block()=>!p.Invulnerable&&p.currentCast&&p.currentCast.TryParry(Blue,true,from,"attack parried");
            if(h.Kind=="ink-ball"){if(Block())RemoveHazard(h);else ExplodeInk(h);return true;}
            if(h.Kind=="rain"&&Block()){h.Cast.Clock.CancelHazard(h.Group);RemoveHazard(h);return true;}
            if(h.Kind!="orb"&&h.Kind!="rain"&&p.currentCast&&p.currentCast.TryParry(Blue,true,from))
            {
                ReflectProjectile(h);
                return true;
            }
            bool Damage()=>p.Receive(h.Damage,Blue,h.Velocity.x>=0?1:-1,false,null,false,true);
            if(h.Kind=="bounce"){if(h.Reentry.Observe(true,arena.Now,true,Damage))ImpactArt(h);}
            else if(h.Kind=="stun")
            {
                // Passes through like the phase3 bounce arrow; each fresh contact after the 0.5 s grace hits and stuns again.
                if(h.Reentry.Observe(true,arena.Now,true,()=>{if(!Damage())return false;p.stunUntil=Mathf.Max(p.stunUntil,arena.Now+(float)Tuning.StunArrowSeconds);p.CancelAction();return true;}))ImpactArt(h);
            }
            else if(h.Kind=="orb")
            {if(arena.Now>=h.NextTick){if(!Block())Damage();h.NextTick=arena.Now+(float)Tuning.Phase1OrbInterval;}h.Inside=true;}
            else if(h.Kind=="rain"){if(h.Hits.TryHit("fall",p.ActorId,Damage))ImpactArt(h);}
            else
            {
                if(Damage())ImpactArt(h);
                if(h.Kind=="homing"){rotation.HomingRemoved(h.Cast.Id,false);h.Cast.Clock.CancelHazard("homing");}
                RemoveHazard(h);return true;
            }
            return false;
        }
        void BeginExchange(Cast cast)
        {
            exchangeCast=cast;exchangeDistance=exchangeAge=0;Model.BeginExchange(Mathf.Abs(Blue.Position.x-Rose.Position.x)-2*Tuning.ExchangeSpawnOffset);
            foreach(var side in new[]{Rose,Blue})
            {
                bool rose=side==Rose;var a=new GameObject(rose?"Destructible Rose arrow":"Destructible Blue orb").AddComponent<RoseBlueActor>();a.transform.SetParent(transform,false);
                a.Initialize(arena,side.Position+Vector2.right*(rose?Mathf.Sign(Blue.Position.x-Rose.Position.x):Mathf.Sign(Rose.Position.x-Blue.Position.x))*(float)Tuning.ExchangeSpawnOffset,new Vector2(2,2),rose?arena.TechnicalSprite:arena.CircleSprite,rose?new Color(.9f,.4f,.5f):new Color(.25f,.4f,.8f),(float)(rose?Tuning.ExchangeRoseHp:Tuning.ExchangeBlueHp),false);
                // These destructible hazards never pay the ordinary enemy fallback loot.
                a.gameObject.AddComponent<InkDropSource>().tableKey="";
                a.DamageAllowed=()=>exchangeCast!=null;a.ControlAllowed=()=>false;exchangeActors.Add(a);
                // Rose's arrow is the glow arrow at twice its size; Blue's orb is the trail with the upright sphere on top.
                // Each carries a white silhouette that fades in during the last second before they meet.
                a.visual.color=Color.white;a.visual.sprite=VfxFrame(rose?"rose-arrow-flight":"blue-orb-trail",0);a.visual.transform.localScale=Vector3.one*(rose?2:1);
                SpriteRenderer Layer(string name,string sheet,Transform parent,int order)
                {var v=new GameObject(name).AddComponent<SpriteRenderer>();v.transform.SetParent(parent,false);v.sprite=VfxFrame(sheet,0);v.sortingOrder=order;v.sharedMaterial=a.visual.sharedMaterial;return v;}
                if(!rose)Layer("Exchange orb / sphere","blue-orb-body",a.visual.transform,a.visual.sortingOrder+1);
                Layer("Exchange / white",rose?"rose-arrow-flight-white":"blue-orb-trail-white",a.visual.transform,a.visual.sortingOrder+2).color=new Color(1,1,1,0);
                if(!rose)Layer("Exchange orb / white sphere","blue-orb-body-white",a.visual.transform,a.visual.sortingOrder+3).color=new Color(1,1,1,0);
            }
        }
        void StepExchangeArt()
        {
            if(exchangeCast==null||exchangeActors.Count<2)return;
            float white=Mathf.Clamp01(exchangeAge-(float)(Tuning.ExchangeMeetSeconds-1));int dir=Rose.Position.x<Blue.Position.x?1:-1;
            for(int i=0;i<2;i++)
            {
                var a=exchangeActors[i];if(!a)continue;bool rose=i==0;
                // Rose's arrow flies toward Blue and the orb toward Rose; the art faces right, so flip it for leftward travel.
                a.visual.flipX=rose?dir<0:dir>0;a.visual.sprite=VfxFrame(rose?"rose-arrow-flight":"blue-orb-trail",exchangeAge);
                foreach(Transform child in a.visual.transform)
                {
                    var v=child.GetComponent<SpriteRenderer>();bool sphere=child.name.Contains("sphere"),whiteLayer=child.name.Contains("white");
                    v.enabled=a.visual.enabled;v.flipX=sphere?false:a.visual.flipX;
                    v.sprite=VfxFrame(sphere?(whiteLayer?"blue-orb-body-white":"blue-orb-body"):rose?"rose-arrow-flight-white":"blue-orb-trail-white",exchangeAge);
                    if(whiteLayer)v.color=new Color(1,1,1,white);
                }
            }
        }
        void StepExchange(float dt)
        {
            if(exchangeCast==null||Model.Volley==null)return;var v=Model.Volley;
            v.DamageBatch(Math.Max(0,v.RoseArrowHp-exchangeActors[0].health),Math.Max(0,v.BlueOrbHp-exchangeActors[1].health));
            v.Advance(dt);exchangeAge+=dt;exchangeDistance=(float)(v.Distance+Tuning.ExchangeSpawnOffset);int dir=Rose.Position.x<Blue.Position.x?1:-1;
            exchangeActors[0].Position=Rose.Position+Vector2.right*dir*exchangeDistance;exchangeActors[1].Position=Blue.Position-Vector2.right*dir*exchangeDistance;
            exchangeMeeting=(exchangeActors[0].Center+exchangeActors[1].Center)/2;
            exchangeActors[0].PresentationVisible=v.RoseArrowHp>0;exchangeActors[1].PresentationVisible=v.BlueOrbHp>0;
            if(v.Finished){Model.ResolveExchange();ClearExchange();DrainSignals();SyncHealth();}
        }
        void ClearExchange(){foreach(var a in exchangeActors)if(a){a.gameObject.SetActive(false);Destroy(a.gameObject);}exchangeActors.Clear();exchangeCast=null;}
    }
}
