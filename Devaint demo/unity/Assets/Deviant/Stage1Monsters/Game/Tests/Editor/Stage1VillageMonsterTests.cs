using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.Presentation.Unity;
using Deviant.Stage1Monsters.Game;
using Deviant.Stage1Monsters.Game.Editor;
using ForestCombatV2;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Deviant.Stage1Monsters.Game.Tests
{
    public sealed class Stage1VillageMonsterTests
    {
        [Test] public void ApprovedMonsterFramesKeepTheirNativeSpriteReferences()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<Stage1MonsterCatalog>(
                "Assets/Deviant/Stage1Monsters/Data/ApprovedStage1Monsters.asset");
            Assert.That(catalog,Is.Not.Null);
            foreach(var monster in catalog.monsters) foreach(var clip in monster.motions)
            {
                Assert.That(clip.frames,Is.Not.Empty,monster.id+"/"+clip.id);
                foreach(var sprite in clip.frames)
                {
                    Assert.That(sprite,Is.Not.Null,monster.id+"/"+clip.id+" lost its imported sprite reference");
                    Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32),monster.id+"/"+clip.id);
                }
            }
        }

        [Test] public void ApprovedHurtboxesAreIndependentOfMovementAndSurviveFacingAndReset()
        {
            var data=JsonUtility.FromJson<VillageMonsterData>(File.ReadAllText(
                "Assets/Deviant/Stage1Monsters/Game/Data/VillageMonsterContent.json"));
            foreach(var spec in data.monsters)
            {
                var node=new GameObject("Hurtbox regression");
                try
                {
                    var actor=node.AddComponent<FieldMouseEnemy>();
                    actor.InitializeSpecies(node.AddComponent<CombatArena>(),new Vector2(-900,100),spec.definition);
                    var expected=new Vector2(spec.definition.hurtSize[0],spec.definition.hurtSize[1]);
                    foreach(int direction in new[]{1,-1,1})
                    {
                        actor.SyncFacing(direction);Physics2D.SyncTransforms();
                        Assert.That(actor.hurtCollider.size,Is.EqualTo(expected),spec.id);
                        Assert.That(actor.bodyCollider.size,Is.EqualTo(new Vector2(
                            spec.definition.bodySize[0],spec.definition.bodySize[1])),spec.id);
                        Assert.That(actor.hurtCollider.offset,Is.EqualTo(new Vector2(
                            spec.definition.hurtOffset[0]*direction,spec.definition.hurtOffset[1])),spec.id);
                        // A small attack touching the new side margin must find the hurt region,
                        // while the physical movement collider remains outside that attack.
                        var point=actor.Position+actor.hurtCollider.offset+new Vector2(expected.x/2-.01f,0);
                        Assert.That(actor.hurtCollider.OverlapPoint(point),Is.True,spec.id);
                        Assert.That(actor.bodyCollider.OverlapPoint(point),Is.False,spec.id);
                    }
                    actor.ResetEnemy();
                    Assert.That(actor.hurtCollider.size,Is.EqualTo(expected),spec.id+" reset");
                    // Nonzero offsets must also mirror without borrowing the physical offset.
                    actor.ConfigureHurtbox(expected,new Vector2(.3f,1));actor.SyncFacing(-1);
                    Assert.That(actor.hurtCollider.offset,Is.EqualTo(new Vector2(-.3f,1)));
                }
                finally { Object.DestroyImmediate(node); }
            }
        }
        [Test] public void CancelledSaveKeepsDirtySceneAndDoesNotStartVillagePlay()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(scene);
            int asks=0;
            try
            {
                Assert.That(Stage1VillageMonsterInstaller.TryApply(()=>{asks++;return false;}),Is.False);
                Assert.That(Stage1VillageMonsterInstaller.TryPlay(()=>{asks++;return false;}),Is.False);
                Assert.That(asks,Is.EqualTo(2));
                Assert.That(SceneManager.GetActiveScene().handle,Is.EqualTo(scene.handle));
                Assert.That(scene.isDirty,Is.True);
                Assert.That(EditorApplication.isPlayingOrWillChangePlaymode,Is.False);
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
        }
        [UnityTest] public IEnumerator LethalHitBreaksTheApprovedBodyIntoDebrisImmediately()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();yield return null;yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();Assert.That(world.Ready,Is.True);
            var arena=world.arena;
            foreach(var enemy in arena.enemies)if(enemy){enemy.aggressive=false;enemy.CancelAttack();}
            var debris=Resources.Load<DebrisCatalog>("HitFeedback/Generated/Debris");
            var bridge=arena.GetComponent<HitFeedbackBridge>();Assert.That(bridge&&bridge.Ready,Is.True,bridge?bridge.LastError:"no bridge");
            using(var driver=arena.AcquireManualSimulation())
            {
                // One real placed monster per species, killed by an accepted player hit.
                foreach(var presentation in world.GetComponentsInChildren<Stage1VillageMonsterPresentation>()
                    .Where(p=>p.Actor&&!p.Actor.Dead).GroupBy(p=>p.Actor.Definition.id).Select(g=>g.First()).ToArray())
                {
                    var actor=presentation.Actor;var player=presentation.Player;var visual=actor.visual;
                    actor.health=1;actor.shield=0;actor.guardUntil=0;presentation.Draw();
                    int before=bridge.ActiveDebris;
                    arena.TryBindActor(arena.Player,out var handle);
                    var origin=new CombatHitOrigin(handle,default,System.Guid.NewGuid(),0,"fixture",System.Guid.Empty,CombatHitKind.Melee,
                        stamp:new CombatAttackStamp(arena.Player.weapons[0].InstanceId,"base_sword","base_sword_basic_0","fixture",true),sourceFacing:1);
                    Assert.That(actor.Receive(2,arena.Player,1,true,null,bypass:true,origin:origin,contact:new CombatHitContact(2,1,1,0,HitAnchorKind.OverlapRepresentative)),Is.True);
                    arena.FeelFeedback.TickPresentation(0);presentation.Draw();bridge.Sample();
                    Assert.That(player.Motion,Is.EqualTo("death"));
                    var bake=debris.Find(player.Definition.Find("idle").frames[0]);Assert.That(bake,Is.Not.Null,actor.Definition.id);
                    // No blank gap: the debris is out on the lethal frame, long before the death clip would end.
                    Assert.That(player.Age,Is.LessThan(player.Clip.DurationMs/1000f));
                    // Debris bodies enter the physics world on the next completed combat step.
                    for(int i=0;i<2;i++){driver.Step(.01f);arena.FeelFeedback.TickPresentation(.01f);presentation.Draw();bridge.Sample();}
                    Assert.That(bridge.ActiveDebris-before,Is.EqualTo(bake.pieces.Length),actor.Definition.id+": "+bridge.LastError);
                    for(int i=0;i<30;i++){driver.Step(.01f);arena.FeelFeedback.TickPresentation(.01f);presentation.Draw();bridge.Sample();}
                    Assert.That(visual.enabled,Is.False,"the body stays replaced by its debris");
                }
            }
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator VillageAttacksUseApprovedMotionsAndExcludeBoss()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();yield return null;yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            Assert.That(world,Is.Not.Null);Assert.That(world.Ready,Is.True);
            foreach(var presentation in world.GetComponentsInChildren<Stage1VillageMonsterPresentation>()
                .GroupBy(p=>p.Actor.Definition.id).Select(g=>g.First()))
            {
                var actor=presentation.Actor;var definition=actor.Definition;
                foreach(int facing in new[]{-1,1})
                {
                    actor.SyncFacing(facing);presentation.Player.Play("attack",true);
                    for(float time=0;time<presentation.Player.Clip.DurationMs/1000f;time+=.01f)
                    {
                        presentation.Player.SampleAt(time);
                        Assert.That(actor.hurtCollider.size,Is.EqualTo(new Vector2(
                            definition.hurtSize[0],definition.hurtSize[1])),definition.id+" attack");
                        Assert.That(actor.hurtCollider.offset.y,Is.EqualTo(definition.hurtOffset[1]));
                    }
                }
            }
            world.arena.ResetTest();
            string result=Stage1VillageMonsterReview.VerifyLogic(world);
            string output=Path.GetFullPath("../outputs/stage1-monsters/village");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"logic-validation.json"),result);
            Debug.Log("STAGE1_VILLAGE_LOGIC_PASS "+result);
            yield return new ExitPlayMode();
        }
        [UnityTearDown] public IEnumerator LeavePlayMode()
        { if(Application.isPlaying)yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator RandomStageEliteUses700HealthDirectClawAndFasterHook()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();yield return null;yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();Assert.That(world.Ready,Is.True);
            var arena=world.arena;arena.Player.UseTestInput=true;
            var enemies=arena.enemies.OfType<FieldMouseEnemy>().ToArray();
            foreach(var enemy in enemies){enemy.aggressive=false;enemy.CancelAttack();}
            var floor=new GameObject("Elite regression floor");floor.transform.position=new Vector3(-500,-.5f,0);
            floor.AddComponent<ArenaSurface>();floor.AddComponent<BoxCollider2D>().size=new Vector2(100,1);
            Physics2D.SyncTransforms();
            foreach(var index in enemies.Select(e=>e.SurfaceIndex).Distinct())
                index.Rebuild(Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None));
            try
            {
                using(var simulation=arena.AcquireManualSimulation())
                {
                    var elite=enemies.First(e=>e.Definition.id=="04_well-warden");
                    var attack=elite.GetComponent<Stage1VillageMonsterAttack>();
                    var presentation=elite.GetComponent<Stage1VillageMonsterPresentation>();
                    var original=world.GetComponent<Stage1VillageMonsterContent>().Find("04_well-warden").definition;
                    void Advance(float seconds)
                    { for(int i=0;i<Mathf.CeilToInt(seconds/.01f);i++){simulation.Step(.01f);presentation.Draw();} }
                    void Until(float age)=>Advance(Mathf.Max(0,age-elite.SpeciesAttackAge));
                    void Place(int direction,float distance)
                    {
                        arena.ResetTest();foreach(var enemy in enemies){enemy.aggressive=false;enemy.CancelAttack();}
                        elite.spawn=new Vector2(-500,.003f);elite.ResetEnemy();elite.SyncFacing(direction);
                        arena.Player.Position=elite.Position+new Vector2(direction*distance,0);
                        arena.Player.velocity=Vector2.zero;Physics2D.SyncTransforms();Advance(.12f);
                        Assert.That(elite.health,Is.EqualTo(700));Assert.That(elite.maxHealth,Is.EqualTo(700));
                        elite.aggressive=true;Advance(.03f);Assert.That(elite.Brain.Execution,Is.EqualTo(1));
                    }
                    foreach(int direction in new[]{-1,1})
                    {
                        Place(direction,1.3f);float health=arena.Player.health;
                        Assert.That(attack.DirectClaw,Is.True);Assert.That(attack.Grabbed,Is.Null);
                        Assert.That(attack.MotionOffset,Is.EqualTo(1.6f));
                        Assert.That(presentation.Player.Frame,Is.GreaterThanOrEqualTo(18));
                        int execution=elite.Brain.Execution;double began=elite.Brain.StartedAt;
                        Assert.That(elite.Receive(1,arena.Player,-direction,true,null),Is.True);
                        Advance(.01f);
                        var variance=CombatDamageVariance.Settings;
                        Assert.That(elite.health,Is.InRange(elite.maxHealth-variance.maximum-.001f,elite.maxHealth-variance.minimum+.001f));
                        Assert.That(elite.staggerUntil,Is.LessThanOrEqualTo(arena.Now));
                        Assert.That(elite.Brain.Executing,Is.True);
                        Assert.That(elite.Brain.Execution,Is.EqualTo(execution));
                        Assert.That(elite.Brain.StartedAt,Is.EqualTo(began));
                        Assert.That(presentation.Player.Motion,Is.EqualTo("attack"));
                        Until(.57f);Assert.That(arena.Player.health,Is.EqualTo(health));
                        Until(.68f);
                        var stroke=elite.GetComponent<Stage1VillageMonsterEffects>().EffectRenderer;
                        Assert.That(stroke.enabled,Is.True);var sharedStroke=stroke.sprite;
                        Until(.75f);Assert.That(arena.Player.health,Is.EqualTo(health-30).Within(.001f));
                        Assert.That(attack.HitsDelivered,Is.EqualTo(1));Assert.That(attack.HookDistance,Is.Zero);
                        Until(1.3f);Assert.That(elite.SpeciesAttackVisible,Is.False);
                        Assert.That(arena.Player.health,Is.EqualTo(health-30).Within(.001f));
                        // Starting in range does not guarantee damage if the target dodges the claw.
                        Place(direction,1.3f);health=arena.Player.health;
                        arena.Player.Position=elite.Position+new Vector2(direction*4,0);Physics2D.SyncTransforms();
                        Until(.9f);Assert.That(arena.Player.health,Is.EqualTo(health));
                        Assert.That(attack.Grabbed,Is.Null);
                        Place(direction,3.5f);health=arena.Player.health;
                        Assert.That(attack.DirectClaw,Is.False);Assert.That(attack.MotionSpeed,Is.EqualTo(1.5f));
                        Assert.That(elite.Definition.prepare,Is.EqualTo(.85f/1.5f).Within(.00001));
                        Until(.8f);Assert.That(attack.Grabbed,Is.SameAs(arena.Player));
                        Until(1.42f);Assert.That(arena.Player.health,Is.EqualTo(health));
                        Until(1.52f);
                        Assert.That(stroke.enabled,Is.True);
                        Assert.That(stroke.sprite,Is.SameAs(sharedStroke),"Direct and hook follow-up claws share the same authored effect time.");
                        Until(1.63f);Assert.That(arena.Player.health,Is.EqualTo(health-30).Within(.001f));
                        Assert.That(attack.HitsDelivered,Is.EqualTo(1));
                        Until(1.95f);Assert.That(elite.SpeciesAttackVisible,Is.False);
                        Assert.That(arena.Player.health,Is.EqualTo(health-30).Within(.001f));
                        Assert.That(original.prepare,Is.EqualTo(.85f),"Execution timing must not mutate shared data.");
                        Assert.That(original.active,Is.EqualTo(1.56f).Within(.00001));
                    }
                }
            }
            finally { Object.Destroy(floor); }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator AcceptedHitsAndFinishingBlowsKeepExistingFlashParticlesAndCleanup()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();yield return null;yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            Assert.That(world.Ready,Is.True);
            var arena=world.arena;arena.Player.UseTestInput=true;
            arena.FeelFeedback.SetEffectsEnabled(false); // Exercise the original presentation when comparison FX are off.
            foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.CancelAttack();}
            using(var simulation=arena.AcquireManualSimulation())
            {
                var presentations=world.GetComponentsInChildren<Stage1VillageMonsterPresentation>();
                void Advance(float seconds)
                {
                    for(int i=0;i<Mathf.CeilToInt(seconds/.01f);i++)
                    { simulation.Step(.01f);foreach(var p in presentations)p.Draw(); }
                }
                foreach(var presentation in presentations.GroupBy(p=>p.Actor.Definition.id).Select(g=>g.First()))
                foreach(int facing in new[]{-1,1})
                {
                    arena.ResetTest();
                    foreach(var enemy in arena.enemies)enemy.aggressive=false;
                    var actor=presentation.Actor;actor.SyncFacing(facing);presentation.Draw();
                    float health=actor.health;var position=actor.Position;
                    var body=actor.bodyCollider.size;var hurt=actor.hurtCollider.size;
                    Assert.That(actor.Receive(1,arena.Player,-facing,false,null),Is.True);
                    presentation.Draw();
                    var flashNode=actor.visual.transform.Find("White hit flash");
                    Assert.That(flashNode,Is.Not.Null,"New village presentation dropped the existing hit feedback.");
                    var flash=flashNode.GetComponent<SpriteRenderer>();
                    var particles=actor.GetComponentInChildren<ParticleSystem>();
                    Assert.That(flash.enabled,Is.True);Assert.That(flash.sprite,Is.SameAs(actor.visual.sprite));
                    Assert.That(flash.transform.localScale.x,Is.EqualTo(facing>0?-1:1));
                    Assert.That(presentation.Player.Motion,Is.EqualTo(actor.Definition.hitStaggerImmune?"idle":"hit"));
                    Assert.That(particles.particleCount,Is.EqualTo(18));
                    Assert.That(particles.main.simulationSpace,Is.EqualTo(ParticleSystemSimulationSpace.World));
                    Assert.That(actor.Position,Is.EqualTo(position));Assert.That(actor.health,Is.InRange(health-1.1f,health-.9f));
                    Assert.That(actor.bodyCollider.size,Is.EqualTo(body));Assert.That(actor.hurtCollider.size,Is.EqualTo(hurt));
                    presentation.Draw();Assert.That(particles.particleCount,Is.EqualTo(18),"Draw must not replay a received hit.");
                    using(var scope=arena.Controls.OpenScope("village feedback regression"))
                    using(var pause=scope.PauseSimulation())
                    {
                        presentation.Draw();Assert.That(particles.isPaused,Is.True);
                        float time=particles.time,now=arena.Now;
                        yield return new WaitForSecondsRealtime(.08f);
                        Assert.That(particles.time,Is.EqualTo(time));Assert.That(arena.Now,Is.EqualTo(now));
                    }
                    presentation.Draw();Assert.That(particles.isPlaying,Is.True);
                    Advance(.08f);Assert.That(flash.enabled,Is.False);
                    actor.shield=10;health=actor.health;
                    Assert.That(actor.Receive(1,arena.Player,-facing,false,null),Is.True);presentation.Draw();
                    Assert.That(actor.health,Is.EqualTo(health));Assert.That(flash.enabled,Is.True);
                    Assert.That(particles.particleCount,Is.EqualTo(36));
                    presentation.enabled=false;
                    Assert.That(particles.particleCount,Is.Zero);Assert.That(flash.enabled,Is.False);
                    presentation.enabled=true;presentation.Draw();
                    Assert.That(particles.particleCount,Is.Zero);Assert.That(flash.enabled,Is.False);
                    actor.shield=0;
                    Assert.That(actor.Receive(9999,arena.Player,-facing,false,null),Is.True);presentation.Draw();
                    Assert.That(presentation.Player.Motion,Is.EqualTo("death"));
                    Assert.That(flash.enabled,Is.True,"The finishing blow must retain the short white impact.");
                    Assert.That(flash.sprite,Is.SameAs(actor.visual.sprite));Assert.That(particles.particleCount,Is.EqualTo(18));
                    Assert.That(actor.Receive(1,arena.Player,-facing,false,null),Is.False);presentation.Draw();
                    Assert.That(particles.particleCount,Is.EqualTo(18),"Rejected hits on a corpse must not burst again.");
                    Advance(.08f);Assert.That(flash.enabled,Is.False);
                    Advance(presentation.Player.Clip.DurationMs/1000f+.03f);
                    Assert.That(actor.visual.enabled,Is.False);
                    arena.ResetTest();
                    Assert.That(particles.particleCount,Is.Zero);Assert.That(flash.enabled,Is.False);
                    Assert.That(presentation.Player.Motion,Is.EqualTo("idle"));Assert.That(actor.visual.enabled,Is.True);
                }
            }
            yield return new ExitPlayMode();
        }
    }
}
