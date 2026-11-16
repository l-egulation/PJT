using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Chapter2.RoseBlue;
using UnityEngine;
using RTarget=Deviant.Chapter2.RoseBlue.Target;

namespace Deviant.Chapter2
{
    public sealed partial class RoseBlueEncounter
    {
        sealed class Cast
        {
            public bool RoseCancelled,BlueCancelled,PresentationCancelled;public long Id;public Pattern Pattern;public PatternClock Clock;public Vector2 Lock;
            public Vector2 RoseHome,BlueHome;
            public bool RoseReturned,BlueReturned;
            public Vector2[] Aim=new Vector2[(int)Tuning.BounceCount];public int[] Platforms;public bool FromRight,Closed,CastClosed,DiveStarted;
            public ArtFx ChargeFx;public readonly ArtFx[] PressFx=new ArtFx[2],WhirlFx=new ArtFx[2];
            public readonly Dictionary<string,SpriteRenderer> Warnings=new Dictionary<string,SpriteRenderer>();
        }
        readonly List<Cast> casts=new List<Cast>();
        public void BeginPattern(Pattern pattern)
        {
            var cast=new Cast{Id=rotation.Begin(pattern),Pattern=pattern,Clock=new PatternClock(Patterns.Build(pattern,0))};caster=cast.Id;
            if(pattern==Pattern.LayerShots)
            {
                Stage.Field.PickLayerShots(arena.Player.Center.y-Stage.Origin.y,true,out int roseLayer,out bool left,out int blueLayer);
                float Height(int layer)=>(float)Enumerable.Range(0,Stage.Field.Count).Select(i=>Stage.Field[i]).First(p=>p.Layer==layer).Top;
                Rose.Position=new Vector2(left?Stage.Area.xMin+Rose.size.x/2:Stage.Area.xMax-Rose.size.x/2,Stage.Origin.y+Height(roseLayer));
                Blue.Position=new Vector2(!left?Stage.Area.xMin+Blue.size.x/2:Stage.Area.xMax-Blue.size.x/2,Stage.Origin.y+Height(blueLayer));
            }
            if(pattern==Pattern.TrapSnipe)cast.Platforms=Stage.Field.PickTraps();
            if(pattern==Pattern.SequenceBlasts)cast.Platforms=Stage.Field.PickSequence();
            if(Model.Phase==Phase.Duo)PrepareDuo(cast);
            if(pattern==Pattern.MachineAndPlatforms)Blue.Position=Stage.PlatformCenter(Stage.Field.MachinePlatform);
            if(pattern==Pattern.ArrowRain)cast.FromRight=random.Next(2)==0;
            if(pattern==Pattern.ExpandingFan)TeleportBoss(Blue,new Vector2(Stage.CameraCenter.x,Stage.Floor));
            casts.Add(cast);foreach(var cue in cast.Clock.Advance(0))Execute(cast,cue);
        }
        void StepCasts(float dt)
        {
            foreach(var cast in casts.ToArray())
            {
                var due = arena.Sketch == null ? cast.Clock.Advance(dt) : cast.Clock.Advance(dt, 1-arena.ReadRunInkMotionSlow(Rose), 1-arena.ReadRunInkMotionSlow(Blue), cue => SketchCueCaster(cast, cue));
                foreach(var cue in due)Execute(cast,cue);
                StepDuoPresentation(cast);
                if(cast.Pattern==Pattern.TrapSnipe&&cast.Clock.Age>=Tuning.TrapTell+Tuning.SniperDelay&&cast.Clock.Age<Tuning.TrapTell+Tuning.SniperDelay+Tuning.SniperTrack)Warning(cast,"sniper",Centered(arena.Player.Center,(float)Tuning.SniperMarkerSize,(float)Tuning.SniperMarkerSize));
                if(!cast.Clock.HasPendingAttack&&!cast.CastClosed){cast.CastClosed=true;if(caster==cast.Id){rotation.CastingFinished();caster=0;}}
                if(!cast.Closed&&!cast.Clock.HasPendingAttack&&!hazards.Any(h=>h.Cast==cast&&h.Damaging)&&exchangeCast!=cast)
                {cast.Closed=true;rotation.DamageEnded(cast.Id);}
                if(cast.Clock.Finished&&!hazards.Any(h=>h.Cast==cast)&&exchangeCast!=cast){ClearWarnings(cast);casts.Remove(cast);}
            }
        }
        int SketchCueCaster(Cast cast, Cue cue)
        {
            if (Model.Phase == Phase.FusedBlue) return 2;
            return cue.Channel switch
            {
                "rose-arrow" or "stun-arrow" or "duo-arrow" or "sniper" or "sniper-track" => 1,
                "blue-orb" or "ink-ball" or "root-zone" or "blue-return" or "machine-burst" or "platform-rain" => 2,
                "sequence-flash" or "sequence-hide" or "sequence-blast" => cue.Index % 2 == 0 ? 1 : 2,
                _ => 0
            };
        }
        double SketchMotionAge(Cast cast, bool rose) => arena.Sketch == null ? cast.Clock.Age : rose ? cast.Clock.RoseAge : cast.Clock.BlueAge;
        void Execute(Cast c,Cue cue)
        {
            string key=cue.Channel+"-"+cue.Index;
            if(cue.Kind==CueKind.Remove){RemoveGroup(c,cue.Group);RemoveWarning(c,key);return;}
            switch(cue.Channel)
            {
                case "layer-pair":
                    Face(Rose,Stage.CameraCenter.x);Face(Blue,Stage.CameraCenter.x);
                    Warning(c,"rose-line",new Rect(Stage.Area.xMin,Rose.Position.y+.8f,30,.3f));Warning(c,"blue-line",new Rect(Stage.Area.xMin,Blue.Position.y,30,2));break;
                case "rose-arrow":Fire(c,"straight",Rose,new Vector2(Rose.facing*(float)Tuning.Phase1ShotSpeed,0),new Vector2((float)Tuning.ArrowWidth,(float)Tuning.ArrowHeight),Stage.Field.RoseArrowDamage(),null);if(cue.Index==2)RemoveWarning(c,"rose-line");break;
                case "blue-orb":FireLayerOrb(c);RemoveWarning(c,"blue-line");break;
                case "all-platforms":foreach(int i in c.Platforms)Warning(c,"trap-"+i,Stage.PlatformDanger(i));break;
                case "traps":PulseTraps(c);break;
                case "sequence-flash":SequenceFlash(c,cue.Index);break;
                case "sequence-hide":RemoveWarning(c,"sequence-"+cue.Index);break;
                case "sequence-warning":for(int i=0;i<c.Platforms.Length;i++)Warning(c,"sequence-"+i,SequenceArea(c,i));break;
                case "sequence-blast":RemoveWarning(c,"sequence-"+cue.Index);var sequenceBox=SequenceArea(c,cue.Index);Hit(sequenceBox,(float)Tuning.SequenceDamage);
                    // The zone itself bursts from its middle: odd order numbers Rose (arrow slash, rose bloom), even Blue (rings).
                    Zone(c,"flash",sequenceBox,0,(float)Tuning.SequenceBlastLife,null).View.enabled=false;
                    StartSheetFx(cue.Index%2==0?"sequence-blast-rose":"sequence-blast-blue",new Vector2(sequenceBox.center.x,sequenceBox.yMin),1,false,AbovePlayer(4));break;
                case "sequence-finished":break;
                case "sniper-track":Warning(c,"sniper",Centered(arena.Player.Center,(float)Tuning.SniperMarkerSize,(float)Tuning.SniperMarkerSize));break;
                case "sniper":if(cue.Kind==CueKind.LockTarget){c.Lock=arena.Player.Center;Warning(c,"sniper",Centered(c.Lock,(float)Tuning.SniperDamageSize,(float)Tuning.SniperDamageSize));}else{Hit(Centered(c.Lock,(float)Tuning.SniperDamageSize,(float)Tuning.SniperDamageSize),(float)Tuning.SniperDamage);StartSheetFx("snipe-hit",c.Lock,1,false,AbovePlayer(9));RemoveWarning(c,"sniper");}break;
                case "machine-platform":Warning(c,"machine",Stage.PlatformDanger(Stage.Field.MachinePlatform));break;
                case "machine-burst":RemoveWarning(c,"machine");var burstBox=Stage.PlatformDanger(Stage.Field.MachinePlatform);Hit(burstBox,(float)Tuning.MachineBurstDamage);
                    if(!c.PresentationCancelled)StartSheetFx("machine-ink-blast",new Vector2(burstBox.center.x,burstBox.yMin),1,false,AbovePlayer(4));break;
                case "other-platforms":for(int i=0;i<Stage.Field.Count;i++)if(i!=Stage.Field.MachinePlatform)Warning(c,"platform-"+i,Stage.PlatformDanger(i));break;
                case "platform-rain":
                    if(cue.Kind==CueKind.Spawn){for(int i=0;i<Stage.Field.Count;i++)if(i!=Stage.Field.MachinePlatform){RemoveWarning(c,"platform-"+i);Zone(c,"visual-rain",Stage.PlatformDanger(i),0,(float)Tuning.PlatformRainLife,"platform-rain");}}
                    if(cue.Kind==CueKind.Damage){bool hit=false;for(int i=0;i<Stage.Field.Count;i++)if(i!=Stage.Field.MachinePlatform&&!hit)hit=Hit(Stage.PlatformDanger(i),(float)Tuning.PlatformRainDamage);}break;
                case "stun-and-ink":
                    // Rose locks her aim when the tell starts; Blue charges ink in front of her hand for the last half second ("ink-charge").
                    Face(Rose,arena.Player.Center.x);Face(Blue,arena.Player.Center.x);c.Aim[0]=(arena.Player.Center-Hand(Rose)).normalized;AimShotWarning(c);break;
                case "ink-charge":c.ChargeFx=StartSheetFx("ink-charge",Hand(Blue),1,false,24);break;
                case "stun-arrow":
                    var stun=Fire(c,"stun",Rose,c.Aim[0]*(float)Tuning.StunArrowSpeed,new Vector2((float)Tuning.ArrowWidth,(float)Tuning.ArrowHeight),(float)Tuning.StunArrowDamage,null);
                    stun.Life=(float)Tuning.StunArrowLife;RemoveWarning(c,"stun");break;
                case "ink-ball":
                    StopFx(c.ChargeFx);c.ChargeFx=null;
                    var ink=Fire(c,"ink-ball",Blue,(arena.Player.Center-Hand(Blue)).normalized*(float)Tuning.InkBallSpeed,Vector2.one*(float)Tuning.InkBallSize,0,null);
                    ink.Life=(float)Tuning.InkBallFuse;break;
                case "root-zone":if(cue.Kind==CueKind.LockTarget)c.Lock=new Vector2(Stage.Origin.x+(float)Stage.Field.ClampedRootCenter(arena.Player.Position.x-Stage.Origin.x),Stage.Floor);else{
                    StartSheetFx("dive-impact",c.Lock,1,false,AbovePlayer(3));
                    var player=arena.Player;
                    bool grounded=player.Position.y>=Stage.Floor-.05f&&player.Position.y<=Stage.Floor+.12f;
                    bool inside=Mathf.Abs(player.Center.x-c.Lock.x)<(float)Tuning.RootWidth/2+player.size.x/2;
                    if(grounded&&inside&&!player.Invulnerable&&!player.FallProtected&&!ProtectsPlayer&&!player.Dead){
                        bossRootUntil=arena.Now+(float)Tuning.RootSeconds;
                        player.rootUntil=Mathf.Max(player.rootUntil,bossRootUntil);
                        player.velocity=new Vector2(0,player.velocity.y);
                    }
                }break;
                case "root-and-shots":Face(Rose,arena.Player.Center.x);break;
                case "duo-arrow":var guided=Fire(c,"duo-homing",Rose,(arena.Player.Center-Hand(Rose)).normalized*(float)Tuning.DuoShotSpeed,new Vector2((float)Tuning.ArrowWidth,(float)Tuning.ArrowHeight),(float)Tuning.DuoShotDamage,null);guided.Life=(float)Tuning.DuoShotLife;break;
                case "blue-return":ReturnBoss(c,false);break;
                case "exchange":Warning(c,"exchange",new Rect(Stage.Area.xMin,Stage.Floor,30,2));break;
                case "exchange-pair":RemoveWarning(c,"exchange");BeginExchange(c);break;
                case "duo":
                    bool roseLeft=Rose.Position.x<Blue.Position.x;
                    TeleportBoss(Rose,new Vector2(Stage.Area.xMin+(roseLeft?23.125f:6.875f),Stage.Floor));
                    TeleportBoss(Blue,new Vector2(Stage.Area.xMin+(roseLeft?6.875f:23.125f),Stage.Floor));break;
                case "swap-flanks":
                    var left=new Rect(Stage.Area.xMin,Stage.Floor,(float)Tuning.SwapWidth,(float)Tuning.SwapHeight);
                    var right=new Rect(Stage.Area.xMax-(float)Tuning.SwapWidth,Stage.Floor,(float)Tuning.SwapWidth,(float)Tuning.SwapHeight);
                    if(cue.Kind==CueKind.Tell)
                    {
                        // One controller per side carries the tell, the 0.8 s growth and the fade: Rose's arrow volley and Blue's ink whirl.
                        foreach(bool l in new[]{true,false}){bool roseOwner=(Rose.Position.x<Blue.Position.x)==l;var wall=new Vector2(l?Stage.Area.xMin:Stage.Area.xMax,Stage.Floor);
                            c.PressFx[l?0:1]=roseOwner?StartRoseVolley(wall,l):StartInkWhirl(wall,l);}
                    }
                    else{
                        foreach(bool l in new[]{true,false}){bool roseOwner=(Rose.Position.x<Blue.Position.x)==l;if(roseOwner?c.RoseCancelled:c.BlueCancelled){StopFx(c.PressFx[l?0:1]);continue;}
                            var z=Zone(c,"swap-flank",l?left:right,(float)Tuning.SwapDamage,(float)(Tuning.SwapAreaEnd-Tuning.SwapTell),null);z.FromLeft=l;}}
                    break;
                case "swap-finished":break;
                case "homing":
                    if(cue.Kind==CueKind.Tell){Face(Blue,arena.Player.Center.x);Warning(c,"homing",Centered(Blue.Center,(float)Tuning.HomingWidth,(float)Tuning.HomingHeight));c.ChargeFx=StartHomingForm(Hand(Blue),arena.Player.Center-Hand(Blue));}
                    else{RemoveWarning(c,"homing");Fire(c,"homing",Blue,(arena.Player.Center-Hand(Blue)).normalized*(float)Tuning.HomingSpeed,new Vector2((float)Tuning.HomingWidth,(float)Tuning.HomingHeight),(float)Tuning.HomingPlayerDamage,"homing");rotation.CastingFinished();c.CastClosed=true;caster=0;}break;
                case "homing-exit":foreach(var h in hazards.Where(h=>h.Cast==c&&h.Kind=="homing")){h.Homing=false;}break;
                case "bounce":
                    if(cue.Kind==CueKind.Tell){Face(Blue,arena.Player.Center.x);c.Aim[cue.Index]=(arena.Player.Center-Hand(Blue)).normalized*(float)Tuning.BounceSpeed;Warning(c,key,Centered(Hand(Blue),1,.3f));}
                    else if(cue.Kind==CueKind.Spawn){RemoveWarning(c,key);Fire(c,"bounce",Blue,c.Aim[cue.Index],new Vector2((float)Tuning.ArrowWidth,(float)Tuning.ArrowHeight),(float)Tuning.BounceDamage,cue.Group);}
                    else if(cue.Kind==CueKind.Whiten){foreach(var h in hazards.Where(h=>h.Cast==c&&h.Group==cue.Group)){h.White=true;h.WhiteAt=arena.Now;}}
                    else if(cue.Kind==CueKind.Explode){var h=hazards.FirstOrDefault(h=>h.Cast==c&&h.Group==cue.Group);if(h!=null){var at=h.At;bool reflected=h.Reflected;RemoveHazard(h);Zone(c,"bounce-blast",Centered(at,(float)Tuning.BounceExplosionSize,(float)Tuning.BounceExplosionSize),(float)Tuning.BounceExplosionDamage,(float)Tuning.ExplosionActive,cue.Group).Reflected=reflected;}}break;
                case "fan-area":
                    var fanArea=new Rect(Stage.CameraCenter.x-(float)Tuning.FanWidth/2,Stage.Floor,(float)Tuning.FanWidth,Stage.Area.yMax-Stage.Floor);
                    if(cue.Kind==CueKind.Tell){Warning(c,"fan-area",fanArea);StartWhirl(c,new Vector2(fanArea.center.x,Stage.Floor));}
                    else{RemoveWarning(c,"fan-area");Zone(c,"fan-area",fanArea,0,(float)Tuning.FanExpandSeconds,"fan-area");}
                    break;
                case "fan-arrow":
                    float angle=(90-(float)Tuning.FanArcDegrees/2+cue.Index*(float)Tuning.FanArcDegrees/((float)Tuning.FanCount-1))*Mathf.Deg2Rad;
                    var fanAt=Blue.Position+Vector2.up*Blue.size.y+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(float)Tuning.FanRadius;
                    if(cue.Kind==CueKind.Tell)
                    {
                        var aim=arena.Player.Center-fanAt;
                        var formingArrow=ArtView("Forming fan arrow",VfxFrame("p3-arrow-flight",0),fanAt,22);
                        formingArrow.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg);
                        formingArrow.flipY=aim.x<0;
                        c.Warnings[key]=formingArrow;
                    }
                    else{
                        RemoveWarning(c,key);
                        var shot=Fire(c,"fan-arrow",Blue,(arena.Player.Center-fanAt).normalized*(float)Tuning.FanArrowSpeed,new Vector2(1,.3f),(float)Tuning.FanArrowDamage,null);
                        shot.At=fanAt;shot.Body.position=fanAt;shot.View.transform.position=fanAt;
                    }
                    break;
                case "rain-start":float start=Stage.Origin.x+(float)HazardGeometry.RainX(0,30,0,c.FromRight);Warning(c,"rain-start",new Rect(start-.15f,Stage.Floor,.3f,Stage.Area.yMax-Stage.Floor));break;
                case "rain-arrow":RemoveWarning(c,"rain-start");float x=Stage.Origin.x+(float)HazardGeometry.RainX(0,30,cue.Index,c.FromRight);var arrow=Fire(c,"rain",Blue,new Vector2(0,-(Stage.Area.yMax-Stage.Floor)/(float)Tuning.RainFall),new Vector2((float)Tuning.RainHeight,(float)Tuning.RainWidth),(float)Tuning.RainDamage,cue.Group);arrow.At=new Vector2(x,Stage.Area.yMax);break;
                case "rain-land":foreach(var h in hazards.Where(h=>h.Cast==c&&h.Group==cue.Group&&!h.Reflected)){h.Landed=true;h.Velocity=Vector2.zero;h.At=new Vector2(h.At.x,Stage.Floor+.5f);}break;
                case "rain":if(cue.Kind==CueKind.Explode){var h=hazards.FirstOrDefault(h=>h.Cast==c&&h.Group==cue.Group&&!h.Reflected);if(h!=null){float rx=h.At.x;RemoveHazard(h);Zone(c,"rain-blast",new Rect(rx-(float)Tuning.RainExplosionWidth/2,Stage.Floor,(float)Tuning.RainExplosionWidth,(float)Tuning.RainExplosionHeight),(float)Tuning.RainDamage,(float)Tuning.ExplosionActive,cue.Group);}}break;
            }
        }
        void Interrupt(RTarget? actor)
        {
            if(Model.Phase==Phase.RevivingBlue&&actor==RTarget.Rose)return;
            foreach(var c in casts)
            {
                if(Model.Phase==Phase.Duo&&actor.HasValue)
                {
                    bool rose=actor.Value==RTarget.Rose;if(rose)c.RoseCancelled=true;else c.BlueCancelled=true;
                    c.Clock.InterruptCaster(cue=>rose?cue.Channel=="stun-arrow"||cue.Channel=="duo-arrow":cue.Channel=="ink-ball"||cue.Channel=="ink-charge"||cue.Channel=="root-zone");
                    foreach(var key in c.Warnings.Keys.ToArray())if(rose?key=="stun":key=="root")RemoveWarning(c,key);
                    if(!rose&&c.Pattern==Pattern.StunAndInk){StopFx(c.ChargeFx);c.ChargeFx=null;}
                    if(c.Pattern==Pattern.Swap&&c.Clock.Age<Tuning.SwapTell)StopFx(c.PressFx[((rose?Rose.Position.x:Blue.Position.x)<Stage.CameraCenter.x)?0:1]);
                    if(!rose&&c.Pattern==Pattern.RootAndShots)ReturnBoss(c,false);
                    c.CastClosed=!c.Clock.HasPendingAttack;
                }
                else{
                    if(Model.Phase==Phase.RevivingBlue&&c.Pattern==Pattern.RootAndShots)ReturnBoss(c,false);
                    c.PresentationCancelled=true;c.Clock.InterruptCaster();ClearWarnings(c);c.CastClosed=true;StopFx(c.ChargeFx);
                    if(c.Pattern==Pattern.Swap&&c.Clock.Age<Tuning.SwapTell){StopFx(c.PressFx[0]);StopFx(c.PressFx[1]);}
                    if(c.Pattern==Pattern.ExpandingFan&&c.Clock.Age<Tuning.FanTell){StopFx(c.WhirlFx[0]);StopFx(c.WhirlFx[1]);}
                }
            }
            caster=0;rotation.InterruptCasting();
        }
        static Rect Centered(Vector2 at,float w,float h)=>new Rect(at.x-w/2,at.y-h/2,w,h);
        void Face(RoseBlueActor a,float x){a.SyncFacing(x>=a.Center.x?1:-1);a.visual.flipX=a.facing>0;}
        Vector2 Hand(RoseBlueActor a)=>a.Position+new Vector2(a.facing*a.size.x*.4f,1);
        SpriteRenderer RectView(string label,Rect rect,Color color,int order=20)
        {var go=new GameObject(label);go.transform.SetParent(transform,false);go.transform.position=rect.center;go.transform.localScale=rect.size;var v=go.AddComponent<SpriteRenderer>();v.sprite=arena.TechnicalSprite;v.color=color;v.sortingOrder=order;if(arena.spriteMaterial)v.sharedMaterial=arena.spriteMaterial;return v;}
        void Warning(Cast c,string key,Rect rect)
        {if(!c.Warnings.TryGetValue(key,out var v))c.Warnings[key]=v=RectView("Warning / "+key,rect,new Color(1,.2f,.24f,.4f),18);v.transform.position=rect.center;v.transform.localScale=rect.size;StylePhase1Warning(c,key,v,rect);}
        void RemoveWarning(Cast c,string key){if(c.Warnings.TryGetValue(key,out var v)){if(v){v.gameObject.SetActive(false);Destroy(v.gameObject);}c.Warnings.Remove(key);}}
        void ClearWarnings(Cast c)
        {foreach(var key in c.Warnings.Keys.ToArray())RemoveWarning(c,key);}
        bool Hit(Rect area,float damage)
        {
            var p=arena.Player;if(ProtectsPlayer||p.Dead||!area.Overlaps(new Rect(p.Center-p.size/2,p.size)))return false;
            // Zones, blasts and area hits parry from either facing: blocked only, no counter, no reflection.
            // A parried hit counts as delivered, so the same blast or tick is not parried again.
            // projectileHit keeps the projectile incoming-damage rules while dash/guard/shield still apply.
            bool landed=p.ReceiveWithParry(damage,Blue,p.Center.x>=area.center.x?1:-1,p.Center,false,out bool parried,false,true);
            return landed||parried;
        }
        void ClearBossEffects()
        {ClearRevivalShield();ClearPhase1Presentation();foreach(var c in casts){c.Clock.EndPhase();ClearWarnings(c);}casts.Clear();foreach(var h in hazards.ToArray())RemoveHazard(h);ClearExchange();caster=0;}
    }
}
