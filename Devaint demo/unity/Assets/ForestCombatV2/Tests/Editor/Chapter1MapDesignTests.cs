using System.Collections;
using System.Linq;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1MapDesignTests
    {
        ForestCombatWorld world; CombatArena arena; CombatArena.ManualSimulation sim;
        [UnitySetUp] public IEnumerator Setup(){
            LegacySceneFixture.Open(Chapter1MapSelector.Ver2Scene);
            yield return new EnterPlayMode();yield return null;yield return null;
            world=Object.FindFirstObjectByType<ForestCombatWorld>();arena=world.arena;
            arena.Player.UseTestInput=true;sim=arena.AcquireManualSimulation();
        }
        [UnityTearDown] public IEnumerator Teardown(){sim?.Dispose();if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();}
        void Advance(float seconds){for(int i=0;i<Mathf.RoundToInt(seconds*120);i++)sim.StepPending(1f/120);}
        [UnityTest] public IEnumerator EightEncountersHaveTenSupportedDistinctSpawnsAndNamedSafeRooms(){
            Assert.That(arena.enemies.Count,Is.EqualTo(80));
            Assert.That(world.Data.actors.GroupBy(p=>p.zone).Count(),Is.EqualTo(8));
            foreach(var group in world.Data.actors.GroupBy(p=>p.zone)){
                Assert.That(group.Count(),Is.GreaterThanOrEqualTo(10));
                Assert.That(group.Select(p=>new Vector2(p.x,p.y)).Distinct().Count(),Is.EqualTo(group.Count()));
            }
            foreach(var e in arena.enemies){
                Assert.That(e.ProbeGround(),Is.True,e.name+" supported spawn");
                Assert.That(((FieldMouseEnemy)e).IdleRoamingEnabled,Is.True);
            }
            Assert.That(world.Data.landmarks.Count(p=>p.kind=="maintenance"),Is.EqualTo(3));
            Assert.That(world.Data.landmarks.Count(p=>p.kind=="shop"),Is.EqualTo(2));
            Assert.That(world.Data.landmarks.Count(p=>p.kind=="elite"),Is.EqualTo(1));
            Assert.That(world.Data.landmarks.Count(p=>p.kind=="boss"),Is.EqualTo(1));
            foreach(var p in world.Data.landmarks)Assert.That(world.Data.actors.Any(a=>a.zone==p.zone),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator RealPlayerReachesEverySolidIslandFromItsApproachDeck(){
            foreach(var enemy in arena.enemies)enemy.aggressive=false;
            var player=arena.Player;var data=world.Data;
            foreach(var group in data.actors.GroupBy(a=>a.zone)){
                float top=group.Min(a=>a.y)/32f;
                Assert.That(group.Count(a=>a.y/32f==top),Is.EqualTo(4),"four upper defenders");
                Assert.That(group.Count(a=>a.y/32f>=top+6),Is.EqualTo(6),"six lower defenders");
                int left=Mathf.FloorToInt(group.First(a=>a.y/32f==top).x/32f);
                while(data.ground[(int)top*data.cols+left-1]!=0)left--;
                player.ResetPlayer();player.Position=new Vector2(left-4.5f,-top-4+.003f);
                player.velocity=Vector2.zero;player.axis=0;Physics2D.SyncTransforms();Advance(.05f);
                Assert.That(player.ProbeGround(),Is.True,"approach deck zone "+group.Key);
                player.axis=1;player.QueueTestInput(jump:true);
                for(int frame=0;frame<180;frame++){
                    if(frame==30)player.QueueTestInput(jump:true);
                    if(player.Position.x>=left+2)player.axis=0;
                    sim.StepPending(1f/120);
                }
                Assert.That(player.Position.x,Is.GreaterThan(left+1),"cross island side zone "+group.Key);
                Assert.That(player.Position.y,Is.EqualTo(-top).Within(.04f),"land on solid island zone "+group.Key);
                Assert.That(player.ProbeGround(),Is.True);
                var supports=Physics2D.RaycastAll(player.Position+Vector2.up*.2f,Vector2.down,.5f);
                Assert.That(supports.Any(hit=>hit.collider.GetComponent<ArenaSurface>() is ArenaSurface surface&&!surface.oneWay),Is.True,"island must have solid terrain collision");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator SixSpeciesShowTheirRealAttackFootprintDuringPreparation(){
            foreach(var enemy in arena.enemies)enemy.aggressive=false;
            var player=arena.Player;player.maxHealth=1000;player.health=1000;
            foreach(var enemy in arena.enemies.Cast<FieldMouseEnemy>().Take(6)){
                enemy.ResetEnemy();enemy.aggressive=true;
                float distance=enemy.Definition.chargeDistance>0?4:Mathf.Min(1.1f,enemy.Definition.startGap-.1f);
                player.Position=enemy.Position+Vector2.right*distance;player.velocity=Vector2.zero;
                Physics2D.SyncTransforms();
                var warning=enemy.GetComponent<Chapter1AttackWarning>();
                Assert.That(warning,Is.Not.Null,enemy.name);
                bool shown=false;
                for(int frame=0;frame<180;frame++){
                    sim.StepPending(1f/120);warning.Sample();shown|=warning.Visible;
                    if(shown)break;
                }
                Assert.That(shown,Is.True,enemy.name+" must warn before its active hit");
                Assert.That(enemy.GetComponentsInChildren<Collider2D>().Length,Is.EqualTo(2),"warning is visual only");
                enemy.aggressive=false;warning.Sample();Assert.That(warning.Visible,Is.False);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator AcceptedPlayerHitOnlyGlowsScreenAndResetClearsFeedback(){
            foreach(var enemy in arena.enemies)enemy.aggressive=false;
            var player=arena.Player;var feedback=player.GetComponent<Chapter1PlayerHitFeedback>();
            Assert.That(feedback,Is.Not.Null);
            var color=player.visual.color;arena.FeelFeedback.ResetFeedback();
            Assert.That(player.Receive(10,arena.enemies[0],-1,false,null,bypass:true),Is.True);
            arena.FeelFeedback.TickPresentation(0);arena.FeelFeedback.TickPresentation(.15f);
            feedback.Sample();Assert.That(feedback.FlashStrength,Is.GreaterThan(.9f));
            Assert.That(player.visual.color,Is.EqualTo(color));
            arena.FeelFeedback.TickPresentation(.75f);feedback.Sample();Assert.That(feedback.FlashStrength,Is.Zero);
            player.ResetPlayer();arena.FeelFeedback.ResetFeedback();feedback.Sample();Assert.That(feedback.FlashStrength,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator RoamingWalksAndStopsWithinRadiusRespectsPauseAndDetection(){
            arena.Player.Position=new Vector2(-500,0);Physics2D.SyncTransforms();
            var actors=arena.enemies.Cast<FieldMouseEnemy>().ToArray();
            var starts=actors.Select(e=>e.Position).ToArray();int moved=0,waiting=0;
            for(int frame=0;frame<960;frame++){
                sim.StepPending(1f/120);
                for(int i=0;i<actors.Length;i++){
                    var e=actors[i];Assert.That(Mathf.Abs(e.Position.x-e.spawn.x),Is.LessThanOrEqualTo(1.15f));
                    Assert.That(Mathf.Abs(e.Position.y-e.spawn.y),Is.LessThan(.08f),e.name+" must not fall off its support");
                    if(Mathf.Abs(e.Position.x-starts[i].x)>.05f)moved++;
                    if(!e.IsIdleRoaming)waiting++;
                }
            }
            Assert.That(moved,Is.GreaterThan(80));Assert.That(waiting,Is.GreaterThan(80));
            var actor=actors[0];actor.aggressive=false;var stopped=actor.Position;Advance(2);
            Assert.That(Vector2.Distance(stopped,actor.Position),Is.LessThan(.01f));
            actor.ResetEnemy();actor.aggressive=true;
            arena.Player.Position=actor.Position+Vector2.right*2;Physics2D.SyncTransforms();Advance(.25f);
            Assert.That(actor.IsIdleRoaming,Is.False);Assert.That(actor.Brain.State,Is.Not.EqualTo(FieldMouseState.Idle));
            yield return null;
        }
        [UnityTest] public IEnumerator RealPlayerSingleJumpClears64PixelsAndDoubleJumpClears128Pixels(){
            foreach(var enemy in arena.enemies)enemy.aggressive=false;
            var floor=new GameObject("jump measurement ground");floor.transform.position=new Vector3(-500,-.5f,0);
            floor.AddComponent<ArenaSurface>();floor.AddComponent<BoxCollider2D>().size=new Vector2(40,1);
            Physics2D.SyncTransforms();var player=arena.Player;
            foreach(bool second in new[]{false,true}){
                player.ResetPlayer();player.Position=new Vector2(-500,.003f);player.velocity=Vector2.zero;player.axis=0;
                var landing=new GameObject("jump landing");landing.transform.position=new Vector3(-500,(second?4:2)-.0625f,0);
                landing.AddComponent<ArenaSurface>().oneWay=true;landing.AddComponent<BoxCollider2D>().size=new Vector2(4,.125f);
                Physics2D.SyncTransforms();Advance(.05f);player.QueueTestInput(jump:true);
                float highest=player.Position.y;
                for(int frame=0;frame<120;frame++){
                    if(second&&frame==30)player.QueueTestInput(jump:true);
                    sim.StepPending(1f/120);highest=Mathf.Max(highest,player.Position.y);
                }
                Assert.That(highest,Is.GreaterThan(second?4.3f:2.3f));
                Assert.That(highest,Is.LessThan(second?6.1f:3.1f));
                Assert.That(player.Position.y,Is.EqualTo(second?4:2).Within(.03f),"real player must land on the shelf");
                Object.DestroyImmediate(landing);
            }
            yield return null;
        }
    }
}
