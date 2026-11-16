using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Chapter2.RoseBlue;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using Object=UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    // Motion timing math: cue keys stretch a clip so its key pose lands on the Patterns.cs cue.
    public sealed class RoseBlueMotionMathTests
    {
        [Test] public void WarpAgeIsPiecewiseLinearAndOneToOneAfterTheLastKey()
        {
            var keys=new[]{0f,0f,1f,.63f,2.365f,1.995f};   // p3-homing: arm out at 0.63 s -> HomingTell 1.0 s
            Assert.That(RoseBlueEncounter.WarpAge(keys,0),Is.EqualTo(0).Within(1e-6));
            Assert.That(RoseBlueEncounter.WarpAge(keys,.5),Is.EqualTo(.315).Within(1e-6));
            Assert.That(RoseBlueEncounter.WarpAge(keys,1),Is.EqualTo(.63).Within(1e-6));
            Assert.That(RoseBlueEncounter.WarpAge(keys,3),Is.EqualTo(1.995+.635).Within(1e-6));
            Assert.That(RoseBlueEncounter.WarpAge(null,1.25),Is.EqualTo(1.25));
        }
        [Test] public void MotionStepHoldsTheLastStepOrWrapsWhenLooping()
        {
            var ms=new[]{100,200,300};
            Assert.That(RoseBlueEncounter.MotionStepAt(ms,0,false),Is.EqualTo(0));
            Assert.That(RoseBlueEncounter.MotionStepAt(ms,.15,false),Is.EqualTo(1));
            Assert.That(RoseBlueEncounter.MotionStepAt(ms,5,false),Is.EqualTo(2));
            Assert.That(RoseBlueEncounter.MotionStepAt(ms,.65,true),Is.EqualTo(0));
            Assert.That(RoseBlueEncounter.MotionStepAt(ms,.75,true),Is.EqualTo(1));
        }
        [Test] public void EveryBoundMotionHasItsDeployedSheet()
        {
            var config=Resources.Load<TextAsset>("RoseBlueMotions");Assert.That(config,Is.Not.Null);
            foreach(var id in new[]{"rose-triple-shot","blue-ink-burst","rose-stun-shot","blue-ink-throw","rose-sky-volley","blue-ink-whirl","rose-stun","blue-stun",
                "p3-homing","p3-bounce","p3-rain","p3-fan","p3-hover","p3-stun","p3-defeat","rose-breath","blue-breath","fused-breath"})
            {
                Assert.That(config.text,Does.Contain("\"id\": \""+id+"\""),id);
                var tex=Resources.Load<Texture2D>("RoseBlueMotions/"+id);Assert.That(tex,Is.Not.Null,id);
                Assert.That(tex.width%384,Is.Zero,id);Assert.That(tex.height%256,Is.Zero,id);Assert.That(Mathf.Max(tex.width,tex.height),Is.LessThanOrEqualTo(4096),id);
                Assert.That(tex.filterMode,Is.EqualTo(FilterMode.Point),id);
            }
        }
    }

    public sealed partial class RoseBlueIntegrationTests
    {
        // ForcePattern parks both bosses in a long stun so rotation cannot start another cast; lift it to see the cast's own body motion.
        static void Unstun(RoseBlueEncounter b,CombatArena a)
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;b.Blue.stunUntil=b.Rose.stunUntil=0;
            typeof(RoseBlueEncounter).GetField("previousBlueStun",flags).SetValue(b,0f);typeof(RoseBlueEncounter).GetField("previousRoseStun",flags).SetValue(b,0f);
        }
        static void AssertDrawnAtFeet(RoseBlueActor actor,string motion)
        {
            Assert.That(actor.visual.sprite.texture.name,Is.EqualTo(motion));
            Assert.That(actor.visual.sprite.rect.size,Is.EqualTo(new Vector2(384,256)));
            Assert.That(actor.visual.sprite.pixelsPerUnit,Is.EqualTo(32));
            Assert.That(actor.visual.sprite.pivot,Is.EqualTo(new Vector2(192,24)));
            Assert.That(actor.visual.transform.localScale,Is.EqualTo(Vector3.one));
            Assert.That((Vector2)actor.visual.transform.position,Is.EqualTo(actor.Position).Using(Vector2EqualityComparer.Instance));
        }
        static IEnumerator SelectBossPhase(int phase)
        {
            Object.FindAnyObjectByType<RoseBlueBossScene>().SelectPhase(phase);yield return null;yield return Ready();
        }
        [UnityTest] public IEnumerator DedicatedBossSceneDuoBodyMotionsFollowTheirPatterns()
        {
            yield return SelectBossPhase(2);
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;
            using(var sim=a.AcquireManualSimulation())
            {
                Step(sim,.1f);Assert.That(b.RoseMotion,Is.EqualTo("rose-breath"));Assert.That(b.BlueMotion,Is.EqualTo("blue-breath"));
                AssertDrawnAtFeet(b.Rose,"rose-breath");AssertDrawnAtFeet(b.Blue,"blue-breath");
                ForcePattern(b,a,Pattern.StunAndInk);Unstun(b,a);Step(sim,.45f);
                Assert.That(b.RoseMotion,Is.EqualTo("rose-stun-shot"));Assert.That(b.BlueMotion,Is.EqualTo("blue-ink-throw"));
                AssertDrawnAtFeet(b.Rose,"rose-stun-shot");AssertDrawnAtFeet(b.Blue,"blue-ink-throw");Capture(w,"motion-duo-stun-and-ink");
                ForcePattern(b,a,Pattern.Swap);Unstun(b,a);Step(sim,.4f);
                Assert.That(b.RoseMotion,Is.EqualTo("rose-sky-volley"));Assert.That(b.BlueMotion,Is.EqualTo("blue-ink-whirl"));Capture(w,"motion-duo-swap");
                b.Rose.stunUntil=a.Now+(float)Tuning.ExchangeStun;Step(sim,.3f);
                Assert.That(b.RoseMotion,Is.EqualTo("rose-stun"));AssertDrawnAtFeet(b.Rose,"rose-stun");Capture(w,"motion-duo-rose-stun");
                Assert.That(a.SimulationFaulted,Is.False);
            }
        }
        [UnityTest] public IEnumerator DedicatedBossSceneFusedBlueBodyMotionsFollowTheirPatterns()
        {
            yield return SelectBossPhase(3);
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;
            using(var sim=a.AcquireManualSimulation())
            {
                Step(sim,.1f);
                foreach(var (pattern,motion) in new[]{(Pattern.Homing,"p3-homing"),(Pattern.Bounce,"p3-bounce"),(Pattern.ArrowRain,"p3-rain"),(Pattern.ExpandingFan,"p3-fan")})
                {
                    ForcePattern(b,a,pattern);Unstun(b,a);Step(sim,.6f);
                    Assert.That(b.BlueMotion,Is.EqualTo(motion),pattern.ToString());AssertDrawnAtFeet(b.Blue,motion);Capture(w,"motion-fused-"+motion);
                }
                b.Blue.stunUntil=a.Now+2;Step(sim,.2f);Assert.That(b.BlueMotion,Is.EqualTo("p3-stun"));
                Unstun(b,a);typeof(RoseBlueEncounter).GetMethod("ClearBossEffects",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(b,null);Step(sim,.05f);
                b.Blue.health=0;Step(sim,.3f);
                Assert.That(b.Model.Phase,Is.EqualTo(Phase.Complete));Assert.That(b.BlueMotion,Is.EqualTo("p3-defeat"));Assert.That(b.Blue.PresentationVisible);
                AssertDrawnAtFeet(b.Blue,"p3-defeat");Capture(w,"motion-fused-defeat");
                Step(sim,5f);Assert.That(b.Blue.PresentationVisible,Is.False,"the defeat pose leaves after its length and a short hold");
            }
        }
    }
}
