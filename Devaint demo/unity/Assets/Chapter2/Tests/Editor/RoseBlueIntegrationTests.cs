using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Chapter2.Editor;
using Deviant.Chapter2.RoseBlue;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    public sealed partial class RoseBlueIntegrationTests
    {
        [UnitySetUp] public IEnumerator OpenScene()
        {
            if(Application.isPlaying)yield return new ExitPlayMode();
            EditorSceneManager.playModeStartScene=null;EditorSceneManager.OpenScene(TestContext.CurrentContext.Test.MethodName.StartsWith("DedicatedBossScene")?RoseBluePhase1Playtest.ScenePath:Chapter2Tools.ScenePath);
            var w=Object.FindAnyObjectByType<Chapter2World>();w.seed=17;w.family=TestContext.CurrentContext.Test.MethodName.StartsWith("BottomEntrance")?"v01":"current";
            yield return new EnterPlayMode();yield return Ready();
        }
        static IEnumerator Ready()
        {float end=Time.realtimeSinceStartup+45;while(Time.realtimeSinceStartup<end){var w=Object.FindAnyObjectByType<Chapter2World>();if(w&&w.Ready){yield return null;yield break;}yield return null;}Assert.Fail("Chapter2 did not initialize");}
        [UnityTearDown] public IEnumerator LeavePlayMode()
        {if(Application.isPlaying)yield return new ExitPlayMode();}
        static void Step(CombatArena.ManualSimulation sim,float seconds)
        {for(int i=0;i<Mathf.CeilToInt(seconds*120);i++){Physics2D.SyncTransforms();sim.Step(1f/120);}}
        static void ForcePattern(RoseBlueEncounter b,CombatArena a,Pattern pattern)
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(RoseBlueEncounter).GetMethod("ClearBossEffects",flags).Invoke(b,null);
            ((AttackRotation)typeof(RoseBlueEncounter).GetField("rotation",flags).GetValue(b)).SetPhase(b.Model.Phase);
            b.Blue.stunUntil=b.Rose.stunUntil=a.Now+100;
            typeof(RoseBlueEncounter).GetField("previousBlueStun",flags).SetValue(b,b.Blue.stunUntil);
            typeof(RoseBlueEncounter).GetField("previousRoseStun",flags).SetValue(b,b.Rose.stunUntil);
            b.BeginPattern(pattern);
        }
        static void AssertInkControlRejected(CombatArena arena,RoseBlueActor target)
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;float root=target.rootUntil,stun=target.stunUntil;
            foreach(bool freeze in new[]{false,true})typeof(CombatArena).GetMethod("AddInkControl",flags).Invoke(arena,new object[]{target,2f,freeze});
            var type=typeof(CombatArena).GetNestedType("InkHit",BindingFlags.NonPublic);var ctor=type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Single();
            var hit=ctor.Invoke(new object[]{System.Activator.CreateInstance(ctor.GetParameters()[0].ParameterType),target,arena.Player});
            foreach(bool freeze in new[]{false,true})Assert.That(typeof(CombatArena).GetMethod("MvpControl",flags).Invoke(arena,new object[]{hit,null,2f,freeze}),Is.False);
            Assert.That(target.rootUntil,Is.EqualTo(root));Assert.That(target.stunUntil,Is.EqualTo(stun));
        }
        static int Views(RoseBlueEncounter b,string prefix)=>b.GetComponentsInChildren<SpriteRenderer>().Count(v=>v.gameObject.name.StartsWith(prefix));
        [UnityTest] public IEnumerator DedicatedBossSceneStartsPhase1WithoutOrdinaryMonsters()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();float end=Time.realtimeSinceStartup+5;
            while(!w.Boss.Started&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(w.gameObject.scene.name,Is.EqualTo("RoseBlueBoss"));Assert.That(w.Boss.Started,Is.True);
            Assert.That(w.CurrentRoom,Is.EqualTo("B"));
            Assert.That(w.Actors.Values.Where(x=>x&&!(x is RoseBlueActor)).All(x=>!x.gameObject.activeSelf),Is.True);
            using(var sim=w.arena.AcquireManualSimulation()){Step(sim,RoseBlueEncounter.IntroSeconds+.1f);Assert.That(w.Boss.Model.Phase,Is.EqualTo(Phase.Machine));Capture(w,"dedicated-boss-scene-phase1");}
        }
        [UnityTest] public IEnumerator DedicatedBossSceneInkStunKeepsAttackAnimationAndCounterMechanicStun()
        {
            Object.FindAnyObjectByType<RoseBlueBossScene>().SelectPhase(3);
            yield return null; yield return Ready();
            var w=Object.FindAnyObjectByType<Chapter2World>(); var b=w.Boss; var a=w.arena;
            a.EnableSketchEquipment(42,2);
            using(var sim=a.AcquireManualSimulation())
            {
                b.Blue.stunUntil=a.Now+3;
                ForcePattern(b,a,Pattern.ArrowRain); Step(sim,.1f);
                Assert.That(b.Blue.stunUntil,Is.GreaterThan(a.Now));
                Assert.That(b.BlueMotion,Is.Not.EqualTo("p3-stun"),"ink status must not hide the active attack animation");
                b.Model.HomingArrowHitsBlue();
                Step(sim,.01f);
                Assert.That(b.BlueMotion,Is.EqualTo("p3-stun"),"the boss's own counter-mechanic keeps its reaction");
            }
        }
        [UnityTest] public IEnumerator DedicatedBossSceneButtonsRestartEveryPhase()
        {
            foreach (int phase in new[] { 2, 3, 1, 3 })
            {
                var selector = Object.FindAnyObjectByType<RoseBlueBossScene>();
                Assert.That(selector, Is.Not.Null);
                selector.SelectPhase(phase);
                yield return null;
                yield return Ready();
                var world = Object.FindAnyObjectByType<Chapter2World>();
                var boss = world.Boss;
                var expected = phase == 1 ? Phase.Machine : phase == 2 ? Phase.Duo : Phase.FusedBlue;
                Assert.That(boss.Model.Phase, Is.EqualTo(expected));
                Assert.That(boss.ProtectsPlayer, Is.False);
                Assert.That(boss.BlocksTraversal, Is.False);
                Assert.That(world.arena.Player.health, Is.EqualTo(world.arena.Player.maxHealth));
                Assert.That(boss.Stage.Surfaces.Count(s => s.oneWay && s.gameObject.activeInHierarchy),
                    Is.EqualTo(phase == 1 ? 7 : 0));
                Assert.That(boss.Blue.health, Is.EqualTo(phase == 3 ? 1300 : 700));
                // 보스 체력바는 표시 이름(베르소·렉토)과 합친 체력 한 줄만 쓴다.
                Assert.That(boss.HudText, Is.EqualTo(phase == 1 ? "기계장치 900 / 900" : phase == 2 ? "베르소 렉토 1400 / 1400" : "렉토 1300 / 1300"));
                using (var sim = world.arena.AcquireManualSimulation())
                {
                    Step(sim, .1f);
                    Assert.That(world.arena.SimulationFaulted, Is.False);
                    Assert.That(boss.Model.Phase, Is.EqualTo(expected));
                }
            }
        }
        [UnityTest] public IEnumerator Phase1ApprovedArtKeepsScaleAndPhysicsAndCleansUpOnMachineDestruction()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;
            using(var sim=a.AcquireManualSimulation())
            {
                w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);ForcePattern(b,a,Pattern.LayerShots);Step(sim,.4f);
                Assert.That(b.Machine.visual.sprite.texture.name,Is.EqualTo("machine-crystal-idle"));
                Assert.That(b.Machine.visual.transform.localScale,Is.EqualTo(Vector3.one),"the crystal cell is drawn 1:1");
                b.Machine.Receive((float)Tuning.MachineHp*.4f,a.Player,1,false,null);Step(sim,.02f);
                Assert.That(b.Machine.visual.sprite.texture.name,Is.EqualTo("machine-crystal-hit1"),"a hit flashes the crystal in its new wear step");
                Step(sim,.2f);Assert.That(b.Machine.visual.sprite.texture.name,Is.EqualTo("machine-crystal-damage1"),"under two thirds the crystal stays split");
                Assert.That(b.Rose.visual.sprite.texture.name,Is.EqualTo("rose-triple-shot"));Assert.That(b.RoseMotion,Is.EqualTo("rose-triple-shot"));
                Assert.That(b.Blue.visual.sprite.texture.name,Is.EqualTo("blue-palm"));
                var blueScale=b.Blue.visual.transform.localScale;var roseScale=b.Rose.visual.transform.localScale;
                Capture(w,"phase1-approved-art-charge");
                Assert.That(b.Blue.visual.sprite.rect.size,Is.EqualTo(new Vector2(320,200)));
                Step(sim,.32f);
                Assert.That(b.Blue.visual.transform.localScale,Is.EqualTo(blueScale));Assert.That(b.Rose.visual.transform.localScale,Is.EqualTo(roseScale));
                Capture(w,"phase1-approved-art-release");
                var orb=b.GetComponentsInChildren<Rigidbody2D>().Single(x=>x.name=="orb");
                Assert.That(orb.GetComponent<BoxCollider2D>().size,Is.EqualTo(Vector2.one*(float)Tuning.Phase1OrbSize));
                var orbArt=orb.GetComponentsInChildren<SpriteRenderer>().Where(v=>v.sprite).Select(v=>v.sprite.texture.name).ToArray();
                Assert.That(orbArt,Does.Contain("blue-orb-trail").And.Contain("blue-orb-body"));
                var arrow=b.GetComponentsInChildren<Rigidbody2D>().First(x=>x.name=="straight");
                Assert.That(arrow.GetComponentsInChildren<SpriteRenderer>().Any(v=>v.sprite&&v.sprite.texture.name=="rose-arrow-flight"));
                ForcePattern(b,a,Pattern.MachineAndPlatforms);Step(sim,.75f);
                var machineTell=b.GetComponentsInChildren<SpriteRenderer>().Single(v=>v.name=="Warning / machine");
                Assert.That(machineTell.sprite.texture.name,Is.EqualTo("machine-warn"));Assert.That(machineTell.transform.localScale,Is.EqualTo(Vector3.one));
                Step(sim,.3f);
                var burst=b.GetComponentsInChildren<SpriteRenderer>().Single(v=>v.name=="Phase1 art / machine-ink-blast");
                Assert.That(burst.sprite.rect.size,Is.EqualTo(new Vector2(288,160)));Assert.That(burst.transform.localScale,Is.EqualTo(Vector3.one));
                Assert.That(burst.transform.position.y,Is.EqualTo(b.Stage.PlatformDanger(b.Stage.Field.MachinePlatform).yMin).Within(.001f),"the zone burst is anchored on the machine platform top");
                Step(sim,.1f);Capture(w,"phase1-approved-art-ink-blast");
                var crystalCentre=b.Machine.Center;
                b.Machine.Receive(10000,a.Player,1,false,null);Step(sim,.02f);
                Assert.That(b.Model.Phase,Is.EqualTo(Phase.FloorTransition));
                Assert.That(b.GetComponentsInChildren<SpriteRenderer>().Count(v=>v.name=="Phase1 art / machine-ink-blast"&&v.gameObject.activeSelf),Is.Zero);
                var shatter=b.GetComponentsInChildren<SpriteRenderer>().Single(v=>v.name=="Phase1 art / machine break");
                Assert.That(shatter.sprite.texture.name,Is.EqualTo("machine-crystal-break"));
                Assert.That(Vector2.Distance(shatter.transform.position,crystalCentre),Is.LessThan(.001f),"the shatter starts where the crystal stood");
                Step(sim,.3f);Capture(w,"phase1-machine-crystal-break");
                Step(sim,RoseBlueEncounter.PhaseShiftSeconds+.05f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.Duo));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator DestroyingEitherExchangeProjectileNeverDropsItems()
        {
            var world=Object.FindAnyObjectByType<Chapter2World>();
            var arena=world.arena;var boss=world.Boss;var player=arena.Player;
            var bag=arena.GetComponent<Deviant.Inventory.Unity.InventoryRuntimeHost>();
            Assert.That(bag,Is.Not.Null);
            Assert.That(bag.Content.Table("melee").entries,Is.Not.Empty,"the ordinary fallback must pay loot for this regression to detect it");
            using(var sim=arena.AcquireManualSimulation())
            {
                player.maxHealth=player.health=10000;
                world.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);
                boss.Machine.Receive(20000,player,1,false,null);Step(sim,RoseBlueEncounter.PhaseShiftSeconds+.05f);
                Assert.That(boss.Model.Phase,Is.EqualTo(Phase.Duo));
                int drops=bag.Drops.Count,kills=world.Kills;
                int rewards=Object.FindObjectsByType<ForestCombatV2.Chapter1RewardPickup>(FindObjectsSortMode.None).Length;
                // Exercise both destruction orders, including the accelerated survivor.
                foreach(bool roseFirst in new[]{true,false})
                {
                    ForcePattern(boss,arena,Pattern.Exchange);Step(sim,1.01f);
                    var targets=boss.Actors.Where(e=>e.name.StartsWith("Destructible")).OrderBy(e=>e.name).ToArray();
                    Assert.That(targets.Length,Is.EqualTo(2));
                    if(roseFirst)System.Array.Reverse(targets);
                    foreach(var target in targets)
                    {
                        Assert.That(arena.enemies,Does.Contain(target));
                        Assert.That(target.Receive(10000,player,1,false,null),Is.True,target.name);
                        // Observe lethal damage before the next boss step removes the target.
                        bag.Advance();
                        Assert.That(bag.Drops.Count,Is.EqualTo(drops),target.name+" must not use ordinary monster loot");
                        Step(sim,.01f);
                    }
                    Assert.That(boss.Model.ExchangeActive,Is.False);
                    Assert.That(boss.Model.CanDamage(Target.Rose)&&boss.Model.CanDamage(Target.Blue),Is.True);
                    Assert.That(world.Kills,Is.EqualTo(kills));
                    Assert.That(Object.FindObjectsByType<ForestCombatV2.Chapter1RewardPickup>(FindObjectsSortMode.None).Length,Is.EqualTo(rewards));
                }
                Assert.That(arena.SimulationFaulted,Is.False);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ExchangeCollisionOnlyDamagesInsideItsVisualBounds()
        {
            var world=Object.FindAnyObjectByType<Chapter2World>();
            var arena=world.arena;var boss=world.Boss;var player=arena.Player;
            using(var sim=arena.AcquireManualSimulation())
            {
                world.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);
                player.maxHealth=player.health=10000;
                boss.Machine.Receive(20000,player,1,false,null);Step(sim,RoseBlueEncounter.PhaseShiftSeconds+.05f);
                foreach(bool inside in new[]{false,true})
                {
                    ForcePattern(boss,arena,Pattern.Exchange);
                    player.Position=new Vector2(inside?boss.Stage.CameraCenter.x:boss.Stage.Area.xMin+.5f,boss.Stage.Floor);
                    player.velocity=Vector2.zero;
                    float before=player.health;
                    Step(sim,3.85f);
                    Assert.That(before-player.health,Is.EqualTo(inside?20:0).Within(.01f));
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator Phase1MachineMovesBetweenPatternsWithHealthAndHazardAnchorPreserved()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            using(var sim=a.AcquireManualSimulation())
            {
                w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);ForcePattern(b,a,Pattern.LayerShots);
                var old=b.Machine.Position;var hp=b.Machine.health;
                typeof(RoseBlueEncounter).GetField("machineMoveElapsed",flags).SetValue(b,(float)Tuning.MachineMoveInterval);
                Step(sim,.2f);Assert.That(b.Machine.Position,Is.EqualTo(old),"active pattern must keep its machine anchor");
                typeof(RoseBlueEncounter).GetMethod("ClearBossEffects",flags).Invoke(b,null);
                ((AttackRotation)typeof(RoseBlueEncounter).GetField("rotation",flags).GetValue(b)).SetPhase(Phase.Machine);
                b.Blue.stunUntil=b.Rose.stunUntil=a.Now;
                Step(sim,.02f);
                Assert.That(b.Machine.Position,Is.EqualTo(old),"one additional full pattern must start before relocation");
                typeof(RoseBlueEncounter).GetMethod("ClearBossEffects",flags).Invoke(b,null);
                ((AttackRotation)typeof(RoseBlueEncounter).GetField("rotation",flags).GetValue(b)).SetPhase(Phase.Machine);
                Step(sim,.02f);
                Assert.That(b.Stage.Field.MachinePlatform,Is.Not.EqualTo(3));
                Assert.That(b.Machine.Position,Is.EqualTo(b.Stage.PlatformCenter(b.Stage.Field.MachinePlatform)));
                Assert.That(b.Machine.health,Is.EqualTo(hp));
                ForcePattern(b,a,Pattern.MachineAndPlatforms);
                Assert.That(b.Blue.Position,Is.EqualTo(b.Machine.Position),"eruption caster follows relocated device");
                Physics2D.SyncTransforms();Assert.That(b.Machine.hurtCollider.enabled,Is.True);
                Assert.That(Vector2.Distance(b.Machine.hurtCollider.bounds.center,b.Machine.Center),Is.LessThan(.05f));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator Phase1PlayerStaysInFrontOfMachineAndTrapBubblesKeepTheirHitbox()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;var p=a.Player;
            using(var sim=a.AcquireManualSimulation())
            {
                w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);ForcePattern(b,a,Pattern.LayerShots);
                p.Position=b.Machine.Position;p.velocity=Vector2.zero;Physics2D.SyncTransforms();
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    w.CameraStep(0,true);var cam=w.sceneCamera;var old=cam.targetTexture;var active=RenderTexture.active;
                    var target=new RenderTexture(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);
                    Color Sample()
                    {
                        cam.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
                        var point=cam.WorldToViewportPoint(p.visual.bounds.center);return image.GetPixel(Mathf.RoundToInt(point.x*960),Mathf.RoundToInt(point.y*540));
                    }
                    try
                    {
                        cam.targetTexture=target;b.Machine.visual.enabled=false;var player=Sample();
                        p.visual.enabled=false;var background=Sample();p.visual.enabled=true;
                        Assert.That(Vector3.Distance(new Vector3(player.r,player.g,player.b),new Vector3(background.r,background.g,background.b)),Is.GreaterThan(.08f),"sample must contain the visible player");
                        b.Machine.visual.enabled=true;var overlap=Sample();
                        Assert.That(Vector3.Distance(new Vector3(player.r,player.g,player.b),new Vector3(overlap.r,overlap.g,overlap.b)),Is.LessThan(.025f),"machine must not cover the player's body");
                    }
                    finally{b.Machine.visual.enabled=true;p.visual.enabled=true;cam.targetTexture=old;RenderTexture.active=active;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);}
                }
                // One chain binding per platform at its top centre; the 0.12u contact strip underneath keeps its size.
                ForcePattern(b,a,Pattern.TrapSnipe);Step(sim,.75f);
                var chains=b.GetComponentsInChildren<SpriteRenderer>().Where(v=>v.name=="Phase1 art / trap-snap").ToArray();
                Assert.That(chains.Length,Is.EqualTo(7));
                foreach(var chain in chains){Assert.That(chain.sprite.rect.size,Is.EqualTo(new Vector2(224,144)));Assert.That(chain.transform.localScale,Is.EqualTo(Vector3.one));}
                var strips=b.GetComponentsInChildren<SpriteRenderer>(true).Where(v=>v.name=="trap-flash").ToArray();
                Assert.That(strips.Length,Is.EqualTo(7));foreach(var strip in strips)Assert.That(strip.transform.localScale.y,Is.EqualTo(.12f).Within(.001f),"the chain art must not change root contact height");
                Capture(w,"phase1-player-front-and-visible-trap");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator IntroductionLiftBubbleEnclosesPlayerAndClearsBeforeCombat()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;
            using(var sim=a.AcquireManualSimulation())
            {
                w.WarpToRoom("B");Step(sim,.1f);var start=a.Player.Position;
                // The bubble forms around the standing player, then lifts them (data/combat/chapter2_rose_blue_intro.json).
                Step(sim,RoseBlueEncounter.IntroAt("bubbleFormed")-.15f);
                Assert.That(a.Player.Position.x,Is.EqualTo(start.x).Within(.001f),"form the bubble before lifting");
                Step(sim,.6f);
                var bubble=b.GetComponentsInChildren<SpriteRenderer>().Single(v=>v.name=="Phase1 art / player lift bubble");
                Assert.That(bubble.enabled,Is.True);Assert.That(bubble.bounds.Contains(a.Player.Center),Is.True);
                Assert.That(bubble.bounds.size.y,Is.GreaterThan(a.Player.size.y));
                Assert.That(bubble.transform.position.y,Is.EqualTo(a.Player.Center.y).Within(.001f));
                Assert.That(a.Player.Position.y,Is.GreaterThan(start.y));Capture(w,"phase1-magic-bubble-lift");
                Step(sim,RoseBlueEncounter.IntroSeconds-RoseBlueEncounter.IntroAt("bubbleFormed")+.1f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.Machine));Assert.That(bubble.enabled,Is.False);
                Assert.That(b.ProtectsPlayer,Is.False,"cutscene control and protection must end with the bubble");
            }
            yield return null;
        }
        // Entrance cutscene follows data/combat/chapter2_rose_blue_intro.json: the siblings' approved motions, the speech
        // lines (Blue cuts Rose off and her line stays under the cut-in), the letterbox, and the hand-off to phase 1.
        [UnityTest] public IEnumerator IntroductionPlaysApprovedMotionsAndLines()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;float elapsed=0;
            using(var sim=a.AcquireManualSimulation())
            {
                void To(float at){Step(sim,at-elapsed);elapsed=at;}
                w.WarpToRoom("B");Assert.That(b.InIntroduction,Is.True);
                To(RoseBlueEncounter.IntroLineAt("rose-laugh")+.3f);
                Assert.That(b.Rose.PresentationVisible&&b.Blue.PresentationVisible,Is.True);Assert.That(b.RoseMotion,Is.EqualTo("R1"));Assert.That(b.CutsceneBars,Is.True);
                Assert.That(b.CutsceneLines.Select(l=>l.Id),Is.EqualTo(new[]{"rose-laugh"}));Assert.That(b.CutsceneLines[0].Speaker,Is.EqualTo(RoseBlueEncounter.RoseDisplayName));
                To(RoseBlueEncounter.IntroLineAt("rose-block")+.5f);Assert.That(b.RoseMotion,Is.EqualTo("R2"));
                To(RoseBlueEncounter.IntroLineAt("blue-start")+.2f);
                Assert.That(b.CutsceneLines.Select(l=>l.Id),Is.EqualTo(new[]{"rose-mood","blue-start"}),"the cut-in keeps the cut line under it");
                Assert.That(b.CutsceneLines[1].Visible,Is.EqualTo(b.CutsceneLines[1].Text.Length),"Blue speaks at once");
                Assert.That(b.CutsceneLines[1].Speaker,Is.EqualTo(RoseBlueEncounter.BlueDisplayName));Capture(w,"intro-blue-cuts-in");
                To(RoseBlueEncounter.IntroAt("castStart")+.3f);Assert.That(b.BlueMotion,Is.EqualTo("B3"));
                To(RoseBlueEncounter.IntroAt("vanish")+.1f);Assert.That(b.Rose.PresentationVisible||b.Blue.PresentationVisible,Is.False);
                To(RoseBlueEncounter.IntroSeconds+.1f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.Machine));
                Assert.That(b.CutsceneLines,Is.Empty);Assert.That(b.CutsceneBars,Is.False);Assert.That(b.Rose.visual.transform.localPosition,Is.EqualTo(Vector3.zero));
            }
            yield return null;
        }
        // Phase 1 to 2 follows data/combat/chapter2_rose_blue_phase_shift.json on top of the natural fall: the siblings at the
        // floor ends with R4/R7, the argument lines and the letterbox, then phase 2 once the clock ends.
        [UnityTest] public IEnumerator PhaseShiftPlaysApprovedMotionsAndLines()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;float elapsed=0;
            using(var sim=a.AcquireManualSimulation())
            {
                void To(float at){Step(sim,at-elapsed);elapsed=at;}
                w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);
                a.Player.maxHealth=a.Player.health=10000;b.Machine.Receive(10000,a.Player,1,false,null);Step(sim,.01f);
                Assert.That(b.Model.Phase,Is.EqualTo(Phase.FloorTransition));Assert.That(b.InBossCutscene,Is.True);
                Assert.That(b.Rose.PresentationVisible||b.Blue.PresentationVisible,Is.False,"the siblings come in after the fall");
                To(RoseBlueEncounter.PhaseShiftLineAt("rose-huh")+.3f);
                Assert.That(b.CutsceneBars,Is.True);Assert.That(b.Rose.PresentationVisible&&b.Blue.PresentationVisible,Is.True);Assert.That(b.RoseMotion,Is.EqualTo("R4"));
                Assert.That(b.Rose.Position,Is.EqualTo(b.Stage.World(b.Stage.Data.Marker("rose-phase2").Feet)));
                Assert.That(b.CutsceneLines.Select(l=>l.Id),Is.EqualTo(new[]{"rose-huh"}));
                To(RoseBlueEncounter.PhaseShiftLineAt("blue-quiet")+.1f);
                Assert.That(b.CutsceneLines.Single().Speaker,Is.EqualTo(RoseBlueEncounter.BlueDisplayName));Assert.That(b.CutsceneLines.Single().Visible,Is.EqualTo(b.CutsceneLines.Single().Text.Length));
                Assert.That(a.Player.facing,Is.EqualTo(b.Blue.Position.x<a.Player.Position.x?-1:1),"the player looks at the speaker");Capture(w,"phase-shift-blue-quiet");
                To(RoseBlueEncounter.PhaseShiftLineAt("rose-greet")+.5f);Assert.That(b.RoseMotion,Is.EqualTo("R7"));
                To(RoseBlueEncounter.PhaseShiftSeconds+.1f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.Duo));
                Assert.That(b.CutsceneLines,Is.Empty);Assert.That(b.CutsceneBars,Is.False);Assert.That(b.InBossCutscene,Is.False);
            }
            yield return null;
        }
        static void Capture(Chapter2World w,string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            // 배치 모드 화면 비율은 16:9 가 아니어서 그대로 찍으면 실제보다 넓게 나온다. 게임 화면과 같은 16:9 로 맞춰 찍는다.
            var cam=w.sceneCamera;cam.aspect=16f/9;w.CameraStep(0,true);var target=new RenderTexture(1280,720,24);var old=cam.targetTexture;var active=RenderTexture.active;
            try{cam.targetTexture=target;cam.Render();RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();if(name=="phase1-approved-layout"){var point=cam.WorldToViewportPoint(w.Boss.Machine.Center);var pixel=image.GetPixel(Mathf.RoundToInt(point.x*1280),Mathf.RoundToInt(point.y*720));w.Boss.Machine.visual.enabled=false;cam.Render();image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();var behind=image.GetPixel(Mathf.RoundToInt(point.x*1280),Mathf.RoundToInt(point.y*720));w.Boss.Machine.visual.enabled=true;cam.Render();image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Assert.That(Vector3.Distance(new Vector3(pixel.r,pixel.g,pixel.b),new Vector3(behind.r,behind.g,behind.b)),Is.GreaterThan(.08f),"approved machine art must remain visible over the background");}var dir=System.IO.Path.GetFullPath(Application.dataPath+"/../../outputs/rose-blue-unity");System.IO.Directory.CreateDirectory(dir);System.IO.File.WriteAllBytes(dir+"/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);}
            finally{cam.ResetAspect();cam.targetTexture=old;RenderTexture.active=active;target.Release();Object.DestroyImmediate(target);}
        }
        [UnityTest] public IEnumerator ApprovedRoomEntryRemovesEliteWithoutRewardAndRestoresFloorAcrossPhases()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;var p=a.Player;
            ForestCombatV2.Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore(new Deviant.Run.ProfileData {
                profileId=System.Guid.NewGuid().ToString(),chaptersCleared=1,chapter1Clears=1,chapter1BossFirstClear=true,
                seenStories=new[]{"opening","first-return","artisan-first-run","artisan-visit-1"} }));
            var plan=ForestCombatV2.Chapter1ProfileHost.Service.PlanRun(new[]{"sword","blunt"});
            var inkRun=new Deviant.Run.RunFlow(()=>17).StartNewRun(a.Now,plan:plan);
            typeof(Chapter2World).GetProperty("SourceRun").SetValue(w,inkRun);
            try
            {
            using(var sim=a.AcquireManualSimulation())
            {
                int kills=w.Kills;int rewards=Object.FindObjectsByType<ForestCombatV2.Chapter1RewardPickup>(FindObjectsSortMode.None).Length;
                w.WarpToRoom("F01");var normal=w.Actors.Values.OfType<Chapter2NormalEnemy>().First(n=>!n.Dead);Assert.That(normal.aggressive,Is.True);
                w.WarpToRoom("B");Assert.That(b.Started);Assert.That(w.ElitePursuing,Is.False);Assert.That(w.Elite.gameObject.activeSelf,Is.False);
                Assert.That(normal.aggressive,Is.False,"normal enemies stop attacking only in the boss room");Assert.That(normal.Attacking,Is.False);Assert.That(a.enemies,Has.No.Member(normal));
                Assert.That(w.Kills,Is.EqualTo(kills));Assert.That(Object.FindObjectsByType<ForestCombatV2.Chapter1RewardPickup>(FindObjectsSortMode.None).Length,Is.EqualTo(rewards));
                Assert.That(b.Model.Phase,Is.EqualTo(Phase.Introduction));Assert.That(b.Machine.Receive(50,p,1,false,null),Is.False);
                float hp=p.health;Assert.That(p.Receive(20,b.Blue,-1,false,null,false,true),Is.False);Assert.That(p.health,Is.EqualTo(hp));
                Step(sim,RoseBlueEncounter.IntroSeconds+.05f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.Machine));AssertInkControlRejected(a,b.Machine);
                Assert.That(b.Stage.Field[0].Left,Is.EqualTo(6.5));Assert.That(b.Stage.Field[1].Left,Is.EqualTo(17.5));Assert.That(b.Stage.Field[2].Left,Is.EqualTo(2));
                Assert.That(b.Stage.Surfaces.Count(s=>s.oneWay&&s.gameObject.activeInHierarchy),Is.EqualTo(7));
                Assert.That(b.Rose.Receive(50,p,1,false,null),Is.False);Assert.That(b.Machine.Receive(10000,p,1,false,null),Is.True);
                Step(sim,.01f);b.Blue.stunUntil=b.Rose.stunUntil=a.Now+100;Assert.That(b.Model.Phase,Is.EqualTo(Phase.FloorTransition));Step(sim,RoseBlueEncounter.PhaseShiftSeconds+.05f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.Duo));
                Assert.That(b.Machine.PresentationVisible,Is.False);Assert.That(b.Stage.Surfaces.Count(s=>s.oneWay&&s.gameObject.activeInHierarchy),Is.Zero);
                Assert.That(p.Position.y,Is.EqualTo(b.Stage.Floor).Within(.05f));Assert.That(b.Blue.Receive(10000,p,1,false,null),Is.True);Step(sim,.01f);
                Assert.That(b.Model.Phase,Is.EqualTo(Phase.AbsorptionPush));
                Assert.That(b.Rose.health,Is.EqualTo(Tuning.RoseHp));
                Step(sim,.35f);
                Assert.That(b.Model.Phase,Is.EqualTo(Phase.Absorption));Step(sim,2.05f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.FusedBlue));Assert.That(b.Blue.health,Is.EqualTo(Tuning.FusedBlueHp));
                b.Blue.Receive(10000,p,1,false,null);Step(sim,.02f);Assert.That(b.Dead);Assert.That(w.Rooms.IsCleared("B"));Assert.That(w.Elite.gameObject.activeSelf,Is.False);
                // The boss weapon choice waits for this defeat pose (QA 2026-11-13); it ends after the pose and its hold.
                Assert.That(b.DefeatPresentationPlaying,Is.True,"Blue's defeat pose plays once the fight ends");
            }
            float deadline=Time.realtimeSinceStartup+3;
            while(!w.Exit.VillageInkPickup.Landed && Time.realtimeSinceStartup<deadline) yield return null;
            var ink=w.Exit.VillageInkPickup;
            Assert.That(ink.Landed,Is.True,"the first chapter-two clear drops its village ink");
            Assert.That(ink.Body.sprite.name,Does.EndWith("drops/yellow"));
            p.Position=w.Exit.Position;p.velocity=Vector2.zero;
            Assert.That(w.TryUseExit(),Is.False,"uncollected village ink blocks the exit");
            p.Position=ink.Position+Vector2.left;
            Assert.That(ink.TryCollect(),Is.True);
            Assert.That(ink.BlocksExit,Is.True,"pickup F cannot also open the exit in the same frame");
            Assert.That(p.GetComponent<ForestCombatV2.VillageInkPocketGlow>().Piece,Is.EqualTo(2));
            yield return null;
            Assert.That(ink.BlocksExit,Is.False);
            Assert.That(ink.TryCollect(),Is.False,"the drop is consumed once");
            }
            finally
            {
                typeof(Chapter2World).GetProperty("SourceRun").SetValue(w,null);
                ForestCombatV2.Chapter1ProfileHost.UseStore(null);
            }
        }
        // 보스방은 1챕터처럼 화면 하나(폭 30u)에 딱 맞고 양옆 벽은 화면 밖이다(사용자 2026-11-13). 뒷배경은 v26 그림(30×15u),
        // 1페이즈 공중전엔 바닥 없이 벽·수문·물줄기가 전장 아래 끝까지 이어진 판(30×18u)이 덮고 2페이즈에 그림 바닥이 돌아온다. 출구 문은 보스방 가운데다.
        [UnityTest] public IEnumerator BossRoomFillsOneThirtyUnitViewWithPaintedRoomAndPhase1AirBackdrop()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;var p=a.Player;
            p.maxHealth=p.health=10000;
            Assert.That(w.Exit.Position.x,Is.EqualTo(b.Stage.Origin.x+RoseBlueStage.Width/2).Within(.001f),"the exit door stands in the middle of the boss room");
            using(var sim=a.AcquireManualSimulation())
            {
                w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.Machine));
                var cam=w.sceneCamera;cam.aspect=16f/9;w.CameraStep(0,true);
                Assert.That(cam.orthographicSize*2*cam.aspect,Is.EqualTo(RoseBlueStage.Width).Within(.001f),"the view is exactly the 30u arena");
                Assert.That(cam.transform.position.x,Is.EqualTo(b.Stage.Origin.x+15).Within(.001f));
                if(a.FeelFeedback&&a.FeelFeedback.CombatCamera)Assert.That(a.FeelFeedback.CombatCamera.HoldBaseFraming,Is.True,"no combat zoom inside the boss room");
                cam.ResetAspect();
                var back=b.Stage.Backdrop;Assert.That(back,Is.Not.Null,"the painted v26 boss room is deployed");
                Assert.That((Vector2)back.bounds.min,Is.EqualTo(b.Stage.Origin+new Vector2(0,3)));Assert.That((Vector2)back.bounds.size,Is.EqualTo(new Vector2(30,15)));
                Assert.That(back.sortingOrder,Is.LessThan(0));Assert.That(back.GetComponent<Collider2D>(),Is.Null);
                Assert.That(b.Stage.AirBackdropShown,Is.True,"phase 1 is fought in the air: the room continues down with no floor");
                var air=b.Stage.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.name=="Rose Blue phase1 air backdrop");
                Assert.That((Vector2)air.bounds.min,Is.EqualTo(b.Stage.Origin));Assert.That((Vector2)air.bounds.size,Is.EqualTo(new Vector2(30,18)));Assert.That(air.sortingOrder,Is.GreaterThan(back.sortingOrder));
                var floor=b.Stage.Surfaces.First(s=>!s.oneWay&&s.name=="floor");
                Assert.That(floor.gameObject.activeInHierarchy,Is.False);
                Capture(w,"boss-room-30u-phase1");
                b.Machine.Receive(10000,p,1,false,null);Step(sim,.01f);b.Blue.stunUntil=b.Rose.stunUntil=a.Now+100;Step(sim,RoseBlueEncounter.PhaseShiftSeconds+.05f);
                Assert.That(b.Model.Phase,Is.EqualTo(Phase.Duo));
                Assert.That(b.Stage.AirBackdropShown,Is.False);Assert.That(floor.gameObject.activeInHierarchy,Is.True);
                var art=floor.GetComponent<SpriteRenderer>();Assert.That(art.sprite.texture.name,Is.EqualTo("boss-floor"),"the restored floor is the painted sewer stone");
                Assert.That(art.bounds.min.y,Is.EqualTo(b.Stage.Origin.y).Within(.001f));Assert.That(art.bounds.size.x,Is.EqualTo(30).Within(.001f));
                Capture(w,"boss-room-30u-phase2");
            }
            yield return null;
        }
        // The chapter 2 exit is the chapter 3 great-library door (user decision 2026-11-13): hidden until the boss falls, then a
        // 6x7u bookcase double door (bottom-centre pivot) whose eight opening frames end on the open sprite.
        [UnityTest] public IEnumerator ExitIsTheGreatLibraryDoorAndOpensThroughItsEightFrames()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;var p=a.Player;
            var door=w.Exit.transform.Find("Chapter2 exit").GetComponent<SpriteRenderer>();
            Assert.That(door.sprite.name,Is.EqualTo("door-closed"));Assert.That(w.Exit.Visible,Is.False,"hidden before the boss falls");
            Assert.That(door.sprite.rect.size,Is.EqualTo(new Vector2(192,224)));Assert.That(door.sprite.pivot,Is.EqualTo(new Vector2(96,0)));
            var frames=ForestCombatV2.SpriteFlipbook.Load(Chapter2BossExit.DoorRoot+"opening");Assert.That(frames.Length,Is.EqualTo(8));
            Assert.That(frames.All(f=>f.rect.size==new Vector2(192,224)),Is.True);
            p.maxHealth=p.health=10000;
            using(var sim=a.AcquireManualSimulation())
            {
                w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);
                b.Machine.Receive(10000,p,1,false,null);Step(sim,.01f);b.Blue.stunUntil=b.Rose.stunUntil=a.Now+100;Step(sim,RoseBlueEncounter.PhaseShiftSeconds+.05f);
                b.Blue.Receive(10000,p,1,false,null);Step(sim,2.45f);b.Blue.Receive(10000,p,1,false,null);Step(sim,.02f);
                Assert.That(b.Dead,Is.True);
            }
            yield return null;
            Assert.That(w.Exit.Visible,Is.True,"the door appears once the boss falls");Assert.That(door.sprite.name,Is.EqualTo("door-closed"));
            p.Position=w.Exit.Position+Vector2.left*3.5f;p.velocity=Vector2.zero;yield return null;Capture(w,"chapter2-library-exit-closed");
            // Play the door's own opening (TryUse also waits for the boss reward choice, which other tests cover).
            typeof(Chapter2BossExit).GetProperty("Opening").SetValue(w.Exit,true);
            w.Exit.StartCoroutine((System.Collections.IEnumerator)typeof(Chapter2BossExit).GetMethod("OpenPortcullis",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(w.Exit,null));
            // In batch mode one frame of this scene outlasts the whole 0.64 s opening, so the run is checked by its end state;
            // the mid frame is captured by showing that sprite directly.
            float openAt=Time.realtimeSinceStartup;
            while(!w.Exit.Opened&&Time.realtimeSinceStartup-openAt<5)yield return null;
            Assert.That(w.Exit.Opened,Is.True);yield return null;Assert.That(door.sprite.name,Is.EqualTo("door-open"));Capture(w,"chapter2-library-exit-open");
            door.sprite=frames[4];Capture(w,"chapter2-library-exit-opening");
        }
        [UnityTest] public IEnumerator FallingUsesApprovedPlatformsAndExistingTenPercentPenalty()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;var p=a.Player;
            using(var sim=a.AcquireManualSimulation())
            {
                w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);p.health=p.maxHealth;
                // Keep the machine alive but freeze casting while testing repeated recovery destinations.
                b.Blue.stunUntil=b.Rose.stunUntil=a.Now+100;float hp=p.health;p.Position=new Vector2(b.Stage.CameraCenter.x,b.Stage.Origin.y-1);
                Step(sim,.01f);Assert.That(p.health,Is.EqualTo(hp-p.maxHealth*.1f).Within(.001f));
                Assert.That(Enumerable.Range(0,7).Any(i=>Vector2.Distance(p.Position,b.Stage.PlatformCenter(i))<.05f));
                Assert.That(p.Invulnerable);
                var flag=BindingFlags.Instance|BindingFlags.NonPublic;typeof(RoseBlueEncounter).GetMethod("Zone",flag).Invoke(b,new object[]{null,"root",new Rect(p.Position.x-2,p.Position.y,4,.12f),0f,2f,"traps",false});
                Step(sim,.1f);Assert.That(p.rootUntil,Is.LessThanOrEqualTo(a.Now),"recovery protection also blocks roots");Step(sim,.55f);Assert.That(p.rootUntil,Is.GreaterThan(a.Now),"a trap still occupied after protection expires becomes active");
                w.CameraStep(0,true);var camera=w.sceneCamera.transform.position;
                Assert.That(camera.x,Is.EqualTo(b.Stage.CameraCenter.x));Assert.That(camera.y,Is.EqualTo(b.Stage.CameraCenter.y));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator Phase3BounceExplosionsAtEitherWallKeepTheSimulationRunning()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;var p=a.Player;
            using(var sim=a.AcquireManualSimulation())
            {
                p.maxHealth=p.health=100000;w.WarpToRoom("B");b.StartTestPhase(3);
                Assert.That(b.Model.Phase,Is.EqualTo(Phase.FusedBlue));
                foreach(bool left in new[]{true,false})
                {
                    double bound=left?(double)b.Stage.Area.xMin+b.Blue.size.x/2d:(double)b.Stage.Area.xMax-b.Blue.size.x/2d;
                    b.Blue.Position=new Vector2((float)bound,b.Stage.Floor);
                    p.Position=new Vector2(b.Stage.CameraCenter.x,b.Stage.Floor);
                    ForcePattern(b,a,Pattern.Bounce);
                    b.Blue.stunUntil=b.Rose.stunUntil=b.Blue.rootUntil=a.Now;
                    typeof(RoseBlueEncounter).GetField("wasResting",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(b,false);
                    Step(sim,2.51f);Assert.That(Views(b,"bounce"),Is.EqualTo(5));
                    Step(sim,2.5f);Assert.That(Views(b,"bounce-blast"),Is.EqualTo(5));
                    float before=a.Now;Step(sim,(float)Tuning.ExplosionActive+.25f);
                    Assert.That(a.Now,Is.GreaterThan(before));Assert.That(a.SimulationFaulted,Is.False);
                    Assert.That(b.Blue.Position.x,Is.EqualTo((float)bound));
                    var rotation=(AttackRotation)typeof(RoseBlueEncounter).GetField("rotation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(b);
                    Assert.That(rotation.CanMove,Is.True,"The regression must reach the rest after all five explosions.");
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator AttacksUseNativeDamageExchangeTargetsAndPhase3Patterns()
        {
            var w=Object.FindAnyObjectByType<Chapter2World>();var a=w.arena;var b=w.Boss;var p=a.Player;
            using(var sim=a.AcquireManualSimulation())
            {
                p.maxHealth=p.health=10000;w.WarpToRoom("B");Step(sim,RoseBlueEncounter.IntroSeconds+.05f);
                ForcePattern(b,a,Pattern.LayerShots);p.Position=b.Rose.Position+Vector2.right*b.Rose.facing*6;
                float hp=p.health;Step(sim,1.3f);Assert.That(p.health,Is.LessThan(hp),"swept arrow must hit the native player hurtbox");
                Assert.That(b.Machine.PresentationVisible);Assert.That(b.Machine.visual.color.a,Is.EqualTo(1));
                // The 96x128 crystal cell (3x4u) is pivoted on the 2x3u collider centre, so it covers the whole hit area.
                Assert.That(b.Machine.visual.bounds.size.x,Is.EqualTo(3).Within(.01f));Assert.That(b.Machine.visual.bounds.size.y,Is.EqualTo(4).Within(.01f));
                Assert.That(Vector2.Distance(b.Machine.visual.transform.position,b.Machine.hurtCollider.bounds.center),Is.LessThan(.01f));
                var vb=b.Machine.visual.bounds;var hb=b.Machine.hurtCollider.bounds;
                Assert.That(vb.min.x<=hb.min.x+.001f&&vb.min.y<=hb.min.y+.001f&&vb.max.x>=hb.max.x-.001f&&vb.max.y>=hb.max.y-.001f,"the art covers the whole hit area");
                yield return null;Capture(w,"phase1-approved-layout");
                ForcePattern(b,a,Pattern.TrapSnipe);Step(sim,.75f);Assert.That(Views(b,"trap-flash"),Is.EqualTo(7));
                ForcePattern(b,a,Pattern.MachineAndPlatforms);p.Position=b.Stage.PlatformCenter(b.Stage.Field.MachinePlatform);hp=p.health;
                Step(sim,1.01f);Assert.That(p.health,Is.LessThan(hp),"machine platform impact");
                b.Machine.Receive(10000,p,1,false,null);Step(sim,RoseBlueEncounter.PhaseShiftSeconds+.05f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.Duo));
                ForcePattern(b,a,Pattern.StunAndInk);Step(sim,2.1f);
                ForcePattern(b,a,Pattern.RootAndShots);Step(sim,2.01f);Assert.That(Views(b,"root"),Is.Zero);
                ForcePattern(b,a,Pattern.RootAndShots);b.Blue.Receive(1,p,1,false,new HitData{stun=2});
                var activeCasts=(System.Collections.IList)typeof(RoseBlueEncounter).GetField("casts",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(b);var activeClock=(PatternClock)activeCasts[0].GetType().GetField("Clock").GetValue(activeCasts[0]);Assert.That(activeClock.WillSpawn("root-zone"),Is.True,"ink stun keeps Blue's pending pattern");Assert.That(activeClock.WillSpawn("duo-arrow"),Is.True,"Rose also keeps her pending arrows");
                ForcePattern(b,a,Pattern.Exchange);Step(sim,1.01f);Assert.That(b.Model.ExchangeActive);var targets=b.Actors.Where(e=>e.name.StartsWith("Destructible")).ToArray();Assert.That(targets.Length,Is.EqualTo(2));
                foreach(var target in targets)Assert.That(target.Receive(10000,p,1,false,null));Step(sim,.01f);Assert.That(b.Model.ExchangeActive,Is.False);Assert.That(b.Model.CanDamage(Target.Rose)&&b.Model.CanDamage(Target.Blue));
                float before=b.Rose.Position.x;ForcePattern(b,a,Pattern.Swap);Step(sim,.51f);Assert.That(b.Rose.Position.x,Is.Not.EqualTo(before));
                b.Rose.Receive(10000,p,1,false,null);Step(sim,2.4f);Assert.That(b.Model.Phase,Is.EqualTo(Phase.FusedBlue));
                Assert.That(b.Blue.hurtCollider.size,Is.EqualTo(new Vector2(1.2f,2.6f)));
                ForcePattern(b,a,Pattern.Bounce);Step(sim,.51f);
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var reflectedHazards=(System.Collections.IList)typeof(RoseBlueEncounter).GetField("hazards",flags).GetValue(b);
                var reflected=reflectedHazards[0];var ht=reflected.GetType();
                var reflectedCast=ht.GetField("Cast").GetValue(reflected);var clock=(PatternClock)reflectedCast.GetType().GetField("Clock").GetValue(reflectedCast);
                clock.Advance(3.3);ht.GetField("At").SetValue(reflected,b.Blue.Center+new Vector2(-20,4));
                typeof(RoseBlueEncounter).GetMethod("ReflectProjectile",flags).Invoke(b,new[]{reflected});
                float beforeReflect=b.Blue.health;Step(sim,.6f);Assert.That((bool)ht.GetField("Consumed").GetValue(reflected),Is.False,"reflected arrow must survive the original five-second deadline");
                Step(sim,1f);Assert.That((bool)ht.GetField("Consumed").GetValue(reflected));Assert.That(beforeReflect-b.Blue.health,Is.InRange(12f,18f),"arrival deals explosion damage rather than contact plus explosion");
                ForcePattern(b,a,Pattern.Bounce);Step(sim,.51f);
                var edgeHazards=(System.Collections.IList)typeof(RoseBlueEncounter).GetField("hazards",flags).GetValue(b);var edgeArrow=edgeHazards[0];var edgeType=edgeArrow.GetType();edgeType.GetField("At").SetValue(edgeArrow,new Vector2(b.Stage.Area.xMax-.42f,b.Stage.Floor+2));edgeType.GetField("Velocity").SetValue(edgeArrow,Vector2.right*10);Step(sim,.02f);Assert.That(a.SimulationFaulted,Is.False,"edge spawn is clamped before reflection");
                ForcePattern(b,a,Pattern.Bounce);Step(sim,2.51f);Assert.That(Views(b,"bounce"),Is.EqualTo(5));Step(sim,2.5f);Assert.That(Views(b,"bounce-blast"),Is.EqualTo(5));
                ForcePattern(b,a,Pattern.ArrowRain);Step(sim,2.26f);Assert.That(Views(b,"rain"),Is.EqualTo(12));
                var rainHazards=(System.Collections.IList)typeof(RoseBlueEncounter).GetField("hazards",flags).GetValue(b);var rain=rainHazards.Cast<object>().First(h=>(string)h.GetType().GetField("Kind").GetValue(h)=="rain");var rainSize=(Vector2)rain.GetType().GetField("Size").GetValue(rain);
                var rainExtent=(Vector2)typeof(RoseBlueEncounter).GetMethod("RotatedExtent",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{rainSize,Vector2.down});Assert.That(rainExtent.x*2,Is.EqualTo(.3f).Within(.001f));Assert.That(rainExtent.y*2,Is.EqualTo(1).Within(.001f));Step(sim,1.9f);
                ForcePattern(b,a,Pattern.ExpandingFan);
                Step(sim,1.5f);Assert.That(Views(b,"fan-area"),Is.EqualTo(1));
                Step(sim,1.1f);Assert.That(Views(b,"fan-area"),Is.Zero);
                var forming=b.GetComponentsInChildren<SpriteRenderer>().Where(v=>v.name.EndsWith("/ Forming fan arrow")).ToArray();
                Assert.That(forming,Is.Not.Empty);
                Assert.That(forming.All(v=>v.enabled&&v.sprite&&v.sprite.texture.name=="p3-arrow-flight"),Is.True);
                Step(sim,.41f);Assert.That(Views(b,"fan-arrow"),Is.GreaterThanOrEqualTo(1));
                Assert.That(a.SimulationFaulted,Is.False);
                p.Position=new Vector2(b.Stage.Area.xMax-2,b.Stage.Floor);p.velocity=Vector2.zero;
                ForcePattern(b,a,Pattern.Homing);Step(sim,1.01f);Assert.That(Views(b,"homing"),Is.EqualTo(1));
                b.Blue.stunUntil=a.Now;var homing=b.GetComponentsInChildren<Rigidbody2D>().First(x=>x.name=="homing");
                // Put Blue in the swept path after the owner grace; native physics must deliver the special self-hit.
                Step(sim,.51f);var hazardList=(System.Collections.IList)typeof(RoseBlueEncounter).GetField("hazards",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(b);
                var hazard=hazardList.Cast<object>().First(x=>(string)x.GetType().GetField("Kind").GetValue(x)=="homing");var at=(Vector2)hazard.GetType().GetField("At").GetValue(hazard);
                b.Blue.Position=at-Vector2.up*b.Blue.size.y/2;Physics2D.SyncTransforms();float blueHp=b.Blue.health;Step(sim,.02f);Assert.That(b.Blue.health,Is.EqualTo(blueHp-100).Within(.01f));Assert.That(b.Blue.stunUntil,Is.GreaterThan(a.Now));
            }
            yield return null;
        }
    }
}
