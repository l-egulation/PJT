using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Chapter2.RoseBlue;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    public sealed partial class RoseBlueIntegrationTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static IList LiveList(RoseBlueEncounter b,string name)=>(IList)typeof(RoseBlueEncounter).GetField(name,Private).GetValue(b);
        static T Read<T>(object item,string name)=>(T)item.GetType().GetField(name).GetValue(item);
        static void Write(object item,string name,object value)=>item.GetType().GetField(name).SetValue(item,value);
        static object Orb(RoseBlueEncounter b)=>LiveList(b,"hazards").Cast<object>().Single(h=>Read<string>(h,"Kind")=="orb");
        static void EnterDuo(Chapter2World w,CombatArena.ManualSimulation sim)
        {
            w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);w.Boss.Machine.Receive(10000,w.arena.Player,1,false,null);Step(sim,RoseBlueEncounter.PhaseShiftSeconds+.05f);
            Assert.That(w.Boss.Model.Phase,Is.EqualTo(Phase.Duo));
        }
        [UnityTest] public IEnumerator RevisionNaturalFallPreservesHorizontalPositionAndHealth()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;var p=a.Player;
            using(var sim=a.AcquireManualSimulation()){
                w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);p.Position=b.Stage.Origin+new Vector2(9.5f,14);p.velocity=Vector2.zero;
                float x=p.Position.x,hp=p.health;b.Machine.Receive(10000,p,1,false,null);Step(sim,.1f);float y=p.Position.y;
                Assert.That(b.Model.Phase,Is.EqualTo(Phase.FloorTransition));Step(sim,.2f);
                Assert.That(p.Position.x,Is.EqualTo(x).Within(.001f));Assert.That(p.Position.y,Is.LessThan(y));Assert.That(p.Position.y,Is.GreaterThan(b.Stage.Floor));
                Step(sim,1);Assert.That(p.Position.y,Is.EqualTo(b.Stage.Floor).Within(.001f),"landed before the phase shift dialogue");Assert.That(b.Model.Phase,Is.EqualTo(Phase.FloorTransition));
                Step(sim,RoseBlueEncounter.PhaseShiftSeconds-1.3f+.05f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.Duo));Assert.That(p.Position.x,Is.EqualTo(x).Within(.001f));Assert.That(p.health,Is.EqualTo(hp));
                Capture(w,"revision-natural-fall-landed");
            }yield return null;
        }
        [UnityTest] public IEnumerator RevisionTrapPulseMissesAirbornePlayerAndNeverRootsOnLaterLanding()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;var p=a.Player;
            using(var sim=a.AcquireManualSimulation()){
                w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);ForcePattern(b,a,Pattern.TrapSnipe);Step(sim,.65f);
                p.Position=b.Stage.PlatformCenter(0)+Vector2.up*1.5f;p.velocity=Vector2.zero;p.grounded=false;
                Step(sim,.06f);Assert.That(p.rootUntil,Is.LessThanOrEqualTo(a.Now));
                p.Position=b.Stage.PlatformCenter(0);p.velocity=Vector2.zero;Step(sim,.3f);Assert.That(p.rootUntil,Is.LessThanOrEqualTo(a.Now),"landing after pulse must be safe");
                ForcePattern(b,a,Pattern.TrapSnipe);p.Position=b.Stage.PlatformCenter(0);p.velocity=Vector2.zero;Step(sim,.71f);
                Assert.That(p.rootUntil,Is.GreaterThan(a.Now+1.8f));Assert.That(Views(b,"trap-flash"),Is.EqualTo(7));
                Capture(w,"revision-seven-trap-pulse");
            }yield return null;
        }
        [UnityTest] public IEnumerator RevisionStunArrowAndInkFireballLeaveInPlaceAndDropFiresOneParryableArrow()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;
            using(var sim=a.AcquireManualSimulation()){
                EnterDuo(w,sim);var roseAt=b.Rose.Position;var blueAt=b.Blue.Position;
                // Keep the player well away from Blue so the fireball is still in flight when it is inspected.
                a.Player.Position=new Vector2(blueAt.x<b.Stage.CameraCenter.x?b.Stage.Area.xMax-3:b.Stage.Area.xMin+3,b.Stage.Floor);a.Player.velocity=Vector2.zero;Physics2D.SyncTransforms();
                ForcePattern(b,a,Pattern.StunAndInk);
                Assert.That(b.Rose.Position,Is.EqualTo(roseAt),"pattern 1 is cast in place");Assert.That(b.Blue.Position,Is.EqualTo(blueAt));Step(sim,.51f);
                var live=LiveList(b,"hazards").Cast<object>().ToArray();
                var stun=live.Single(h=>Read<string>(h,"Kind")=="stun");Assert.That(Read<Vector2>(stun,"Velocity").magnitude,Is.EqualTo(40).Within(.001f));
                var ink=live.Single(h=>Read<string>(h,"Kind")=="ink-ball");Assert.That(Read<Vector2>(ink,"Velocity").magnitude,Is.EqualTo(10).Within(.001f));
                Assert.That(Read<Vector2>(ink,"Size").x,Is.EqualTo(2.5f).Within(.001f));
                Step(sim,2.05f);Assert.That(LiveList(b,"hazards").Cast<object>().Any(h=>Read<string>(h,"Kind")=="ink-ball"),Is.False,"the fireball bursts by its two-second fuse");
                var blueHome=b.Blue.Position;ForcePattern(b,a,Pattern.RootAndShots);Step(sim,1.9f);Assert.That(b.Blue.PresentationVisible);
                Assert.That(b.Blue.Position.y,Is.GreaterThan(b.Stage.Floor));Step(sim,.1f);
                var arrow=LiveList(b,"hazards").Cast<object>().Single(h=>Read<string>(h,"Kind")=="duo-homing");
                Assert.That(Read<float>(arrow,"Damage"),Is.EqualTo(20));Assert.That(Read<Vector2>(arrow,"Velocity").magnitude,Is.EqualTo(20).Within(.001f));
                var p=a.Player;p.Position=new Vector2(b.Stage.CameraCenter.x,b.Stage.Floor);p.rootUntil=p.stunUntil=p.moveLockUntil=0;
                typeof(RoseBlueEncounter).GetField("bossRootUntil",Private).SetValue(b,0f);
                // Run equipment is intentionally immutable outside maintenance; invoke the real shield skill directly.
                p.CancelAction();p.SyncFacing(1);var parrySkill=a.catalog.forms.Single(f=>f.id=="shield").specials[0];
                p.currentCast=CombatCast.Create(a,p,parrySkill,p.Weapon,false);p.currentCast.BeforeMovement(.02f);p.currentCast.Step(.02f);
                Assert.That(p.currentCast,Is.Not.Null);
                float hp=p.health;typeof(RoseBlueEncounter).GetMethod("ProjectileContact",Private).Invoke(b,new object[]{arrow,p.Center+Vector2.right});
                Assert.That(Read<bool>(arrow,"Reflected"),"actual shield parry must reflect the guided arrow");Assert.That(p.health,Is.EqualTo(hp));
                Step(sim,.13f);Assert.That(b.Blue.Position,Is.EqualTo(blueHome));
                var clock=Read<PatternClock>(LiveList(b,"casts")[0],"Clock");Assert.That(clock.WillSpawn("duo-arrow"),Is.False);
                Capture(w,"revision-drop-return");
            }yield return null;
        }
        [UnityTest] public IEnumerator RevisionOrbStartsGuidingAfterHalfArenaTravel()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;
            using(var sim=a.AcquireManualSimulation()){
                w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);a.Player.maxHealth=a.Player.health=10000;ForcePattern(b,a,Pattern.LayerShots);
                Step(sim,.71f);
                var orb=Orb(b);
                Assert.That(Read<Vector2>(orb,"Velocity").magnitude,Is.EqualTo(12));
                float launchX=Read<float>(orb,"OrbLaunchX");
                Step(sim,1.15f);
                Assert.That(Read<bool>(orb,"OrbTurned"),Is.False);
                for(int i=0;i<30&&!Read<bool>(orb,"OrbTurned");i++)sim.Step(1f/120f);
                Assert.That(Read<bool>(orb,"OrbTurned"),Is.True);
                Assert.That(Mathf.Abs(Read<Vector2>(orb,"At").x-launchX),Is.EqualTo(b.Stage.Area.width/2).Within(.12f));
            }yield return null;
        }
        [UnityTest] public IEnumerator RevisionGuidedOrbDamagesMachineAndBlueDeathStartsAbsorption()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;var p=a.Player;
            using(var sim=a.AcquireManualSimulation()){
                w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);ForcePattern(b,a,Pattern.LayerShots);Step(sim,.71f);
                var orb=Orb(b);Write(orb,"OrbTurned",true);Write(orb,"GuidanceStarted",Read<float>(orb,"Age"));
                Write(orb,"At",b.Machine.Center-Vector2.right*2);Write(orb,"Velocity",Vector2.right*12);p.Position=b.Machine.Position+Vector2.right*8;
                float machine=b.Machine.health;Step(sim,.12f);Assert.That(b.Machine.health,Is.EqualTo(machine-(float)Tuning.Phase1OrbMachineDamage));
                b.Machine.Receive(10000,p,1,false,null);Step(sim,RoseBlueEncounter.PhaseShiftSeconds+.05f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.Duo));
                b.Blue.Receive(10000,p,1,false,null);Step(sim,.01f);
                Assert.That(b.Model.Phase,Is.EqualTo(Phase.AbsorptionPush));
                Assert.That(b.Actors.Any(x=>x.name.Contains("Shared revival shield")),Is.False);
                Step(sim,2.4f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.FusedBlue));
                Assert.That(b.Blue.health,Is.EqualTo(Tuning.FusedBlueHp));
            }yield return null;
        }
        [UnityTest] public IEnumerator RevisionSequencePulsesOnceAndSwapPressuresWithoutForcedPush()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;var p=a.Player;
            using(var sim=a.AcquireManualSimulation()){
                w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);p.maxHealth=p.health=10000;ForcePattern(b,a,Pattern.SequenceBlasts);
                var cast=LiveList(b,"casts")[0];var order=Read<int[]>(cast,"Platforms");Assert.That(order.Distinct().Count(),Is.EqualTo(6));
                Step(sim,.1f);Capture(w,"revision-sequence-number");Step(sim,3.95f);Capture(w,"revision-sequence-warning");
                p.Position=b.Stage.PlatformCenter(order[0]);p.velocity=Vector2.zero;float hp=p.health;Step(sim,.21f);
                Assert.That(hp-p.health,Is.InRange(9f,11f));float after=p.health;Step(sim,.06f);Assert.That(p.health,Is.EqualTo(after),"visual blast must not deal continuous damage");Capture(w,"revision-sequence-blast");
                b.Machine.Receive(20000,p,1,false,null);Step(sim,RoseBlueEncounter.PhaseShiftSeconds+.05f);ForcePattern(b,a,Pattern.Swap);
                float x=b.Stage.Area.xMin+3;p.Position=new Vector2(x,b.Stage.Floor);p.velocity=Vector2.zero;hp=p.health;Step(sim,.85f);
                Assert.That(p.health,Is.LessThan(hp));Assert.That(p.Position.x,Is.EqualTo(x).Within(.6f),"only the game's ordinary short hit reaction may remain; no forced travel to center");Assert.That(p.stunUntil,Is.LessThanOrEqualTo(a.Now));Capture(w,"revision-swap-flanks");
                // Release the test stun and verify that the normal rest replaces the old forced follow-up.
                b.Blue.stunUntil=b.Rose.stunUntil=a.Now;
                p.Position=new Vector2(b.Stage.CameraCenter.x,b.Stage.Floor);hp=p.health;Step(sim,.55f);Assert.That(p.health,Is.EqualTo(hp),"central gap must stay safe through flank growth");
                Step(sim,.06f);Assert.That(LiveList(b,"casts").Cast<object>().Any(c=>Read<Pattern>(c,"Pattern")==Pattern.StunAndInk),Is.False,"swap must enter rest instead of starting pattern 1");
                Capture(w,"revision-swap-rest");
            }yield return null;
        }
    }
}
