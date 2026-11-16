using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Chapter2.RoseBlue;
using UnityEngine;

namespace Deviant.Chapter2
{
    public sealed partial class RoseBlueEncounter
    {
        RoseBlueActor revivalShield;
        readonly HashSet<Guid> shieldExecutions=new HashSet<Guid>();
        float fallVelocity,landedAge=-1;

        float ClampBossX(float x)=>Mathf.Clamp(x,Stage.Area.xMin+2.5f,Stage.Area.xMax-2.5f);
        void FireLayerOrb(Cast cast)
        {
            var velocity = new Vector2(Blue.facing * (float)Tuning.Phase1OrbSpeed, 0);
            var size = Vector2.one * (float)Tuning.Phase1OrbSize;
            var orb = Fire(cast, "orb", Blue, velocity, size, (float)Tuning.Phase1OrbDamage, null);
            orb.OrbLaunchX = orb.At.x;
        }
        static Vector2 TurnToward(Vector2 velocity,Vector2 delta,float degrees,float dt)
        {
            if(delta.sqrMagnitude<.000001f)return velocity;
            float from=Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg;
            float to=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
            float angle=Mathf.MoveTowardsAngle(from,to,degrees*dt)*Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*velocity.magnitude;
        }
        void TeleportBoss(RoseBlueActor actor,Vector2 at,bool ghost=true)
        {
            if(ghost)Ghost(actor.visual.sprite,actor.visual.transform.position,actor.visual.transform.localScale,actor.visual.flipX);
            actor.Position=at;Face(actor,arena.Player.Center.x);
        }
        // Blue dissolves into ink where she stands: before her dive and right after landing.
        void VanishBlue(){StartSheetFx("blue-vanish",Blue.Position,1,false,Blue.visual.sortingOrder+1);}
        void PrepareDuo(Cast c)
        {
            c.RoseHome=Rose.Position;c.BlueHome=Blue.Position;
            float x=arena.Player.Center.x,side=x<Stage.CameraCenter.x?1:-1;
            // Pattern 1 (stun arrow and ink fireball) is cast from where both bosses already stand.
            if(c.Pattern==Pattern.RootAndShots){
                float rx=ClampBossX(x-side*7.1875f);if(Mathf.Abs(rx-x)<4.6875f)rx=ClampBossX(x+side*14.375f);
                TeleportBoss(Rose,new Vector2(rx,Stage.Floor));
                VanishBlue();TeleportBoss(Blue,new Vector2(Stage.Origin.x+(float)Stage.Field.ClampedRootCenter(x-Stage.Origin.x),Stage.Floor+(float)Tuning.BlueDropHeight),false);
            }

        }
        void ReturnBoss(Cast c,bool rose)
        {
            if(rose){if(c.RoseReturned)return;c.RoseReturned=true;TeleportBoss(Rose,c.RoseHome);}
            else{if(c.BlueReturned)return;c.BlueReturned=true;VanishBlue();TeleportBoss(Blue,c.BlueHome,false);}
        }
        void StepDuoPresentation(Cast c)
        {
            if(Model.Phase!=Phase.Duo)return;
            float age=(float)SketchMotionAge(c, false);
            if(c.Pattern==Pattern.RootAndShots&&!c.BlueReturned&&!c.BlueCancelled){
                float t=Mathf.Clamp01((age-(float)(Tuning.RootTell-Tuning.BlueDropSeconds))/(float)Tuning.BlueDropSeconds);
                float targetX = age < Tuning.RootTell-Tuning.BlueDropSeconds ? Stage.Origin.x+(float)Stage.Field.ClampedRootCenter(arena.Player.Position.x-Stage.Origin.x) : c.Lock.x;
                Blue.Position=new Vector2(targetX,Stage.Floor+(float)Tuning.BlueDropHeight*(1-t*t));
                // The falling trail rides on Blue's feet for the 0.14 s drop.
                if(!c.DiveStarted&&age>=Tuning.RootTell-Tuning.BlueDropSeconds){c.DiveStarted=true;StartSheetFx("dive-trail",Blue.Position,1,false,Blue.visual.sortingOrder-1,Blue);}
            }
        }
        // The stun arrow's aim is fixed when the tell starts: a line from Rose's hand toward where the player stood.
        void AimShotWarning(Cast c)
        {
            var from=Hand(Rose);var to=arena.Player.Center;var delta=to-from;
            Warning(c,"stun",Centered((from+to)/2,delta.magnitude,.12f));
            c.Warnings["stun"].transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        }
        void PulseTraps(Cast c)
        {
            var p=arena.Player;bool caught=false;
            foreach(int i in c.Platforms){
                RemoveWarning(c,"trap-"+i);var box=Stage.PlatformDanger(i);box.height=.12f;
                Zone(c,"trap-flash",box,0,(float)Tuning.TrapLife,"traps");
                caught|=p.grounded&&p.Position.y>=box.yMin-.05f&&p.Position.y<=box.yMin+.05f&&p.Center.x+p.size.x/2>box.xMin&&p.Center.x-p.size.x/2<box.xMax;
            }
            if(caught&&!p.FallProtected&&!ProtectsPlayer&&!p.Dead){bossRootUntil=arena.Now+(float)Tuning.TrapRoot;p.rootUntil=Mathf.Max(p.rootUntil,bossRootUntil);p.velocity=new Vector2(0,p.velocity.y);}
        }
        Rect SequenceArea(Cast c,int index){var box=Stage.PlatformDanger(c.Platforms[index]);box.height=(float)Tuning.SequenceHeight;return box;}
        void SequenceFlash(Cast c,int index)
        {
            // The order is shown by the lit platforms alone, without numbers.
            Warning(c,"sequence-"+index,SequenceArea(c,index));
        }
        void BeginRevivalShield()
        {
            ClearRevivalShield();shieldExecutions.Clear();
            positions=new DuoPositions(new Point(Rose.Position.x,Rose.Position.y),new Point(Blue.Position.x,Stage.Floor));
            positions.BeginRevival();Blue.Position=new Vector2(ClampBossX(Blue.Position.x),Stage.Floor);
            Rose.Position=Blue.Position+Vector2.right*(Blue.Position.x<Stage.CameraCenter.x?1.5f:-1.5f);
            revivalShield=new GameObject("Rose Blue / Shared revival shield").AddComponent<RoseBlueActor>();revivalShield.transform.SetParent(transform,false);
            revivalShield.Initialize(arena,Blue.Position,new Vector2((float)Tuning.RevivalShieldWidth,(float)Tuning.RevivalShieldHeight),arena.CircleSprite,new Color(.35f,.55f,1,.35f),(float)Tuning.RevivalShieldHp,false);
            revivalShield.DamageAllowed=()=>Model.Phase==Phase.RevivingBlue;
            revivalShield.ControlAllowed=()=>false;
            revivalShield.HitAccepted=origin=>{if(origin.IsSpecified)shieldExecutions.Add(origin.ExecutionId);};
        }
        bool ShieldUp=>revivalShield&&revivalShield.health>0&&Model.Phase==Phase.RevivingBlue;
        void ClearRevivalShield(){if(revivalShield){revivalShield.gameObject.SetActive(false);Destroy(revivalShield.gameObject);}revivalShield=null;}
        void StepNaturalFall(float dt)
        {
            // The cutscene owns vertical integration while ordinary player input is blocked.
            float active=Mathf.Max(0,cutAge-(float)Tuning.FloorBreakPause)-Mathf.Max(0,cutAge-dt-(float)Tuning.FloorBreakPause);
            float y=arena.Player.Position.y+fallVelocity*active-.5f*(float)Tuning.FloorFallGravity*active*active;
            fallVelocity-=(float)Tuning.FloorFallGravity*active;
            arena.Player.Position=new Vector2(cutFrom.x,Mathf.Max(Stage.Floor,y));arena.Player.velocity=Vector2.zero;
            if(y<=Stage.Floor){if(landedAge<0)landedAge=cutAge;}
            // Phase 2 starts once the landing rule holds and the phase shift dialogue (RoseBlueEncounter.PhaseShift.cs) has ended.
            if(cutAge>=Tuning.FloorTransitionMinimum&&landedAge>=0&&cutAge-landedAge>=Tuning.FloorLandingWait&&cutAge>=cutLength)CompleteCutscene(cutToken);
        }
    }
}
