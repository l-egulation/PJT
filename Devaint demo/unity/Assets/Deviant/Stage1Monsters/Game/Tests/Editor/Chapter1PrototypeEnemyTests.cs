using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using ForestCombatV2;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Stage1Monsters.Game.Tests
{
    public sealed class Chapter1PrototypeEnemyTests
    {
        ForestCombatWorld world;
        Vector2 origin;
        [UnitySetUp] public IEnumerator StartWorld()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            UnityEngine.SceneManagement.SceneManager.LoadScene(Chapter1MapSelector.ProceduralScene);
            float deadline=Time.realtimeSinceStartup+45;
            do { world=Object.FindAnyObjectByType<ForestCombatWorld>();if(world&&world.Ready)break;yield return null; }
            while(Time.realtimeSinceStartup<deadline);
            Assert.That(world&&world.Ready,Is.True,"Chapter1 must finish its staged composition before testing enemies");
            foreach(var e in world.arena.enemies){e.aggressive=false;e.CancelAttack();}
            var r=world.Data.rooms.First(r=>r.kind=="start");origin=new Vector2(r.x+6,-(r.y+r.exitY));
            world.arena.Player.maxHealth=10000;world.arena.Player.health=10000;world.arena.Player.shield=0;
            // These cases time the charge from detection; the "!" reaction pause is covered by its own rules.
            var pursuit=EnemyPursuitSettings.Load();reaction=new Vector2(pursuit.reactionDelayMin,pursuit.reactionDelayMax);
            pursuit.reactionDelayMin=pursuit.reactionDelayMax=0;
        }
        Vector2 reaction;
        [UnityTearDown] public IEnumerator StopWorld()
        {
            var pursuit=EnemyPursuitSettings.Load();pursuit.reactionDelayMin=reaction.x;pursuit.reactionDelayMax=reaction.y;
            yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }
        Chapter1PrototypeEnemy Spawn(bool ranged)
        {
            var spec=Chapter1NewMonsterCatalog.Load().monsters.Single(s=>s.ranged==ranged);
            var e=(Chapter1PrototypeEnemy)world.SpawnMonster(spec.id,origin,true);
            e.Spec.firstDelayMin=e.Spec.firstDelayMax=0;e.ResetEnemy();return e;
        }
        void PlayerAt(Vector2 at){world.arena.Player.Position=at;world.arena.Player.velocity=Vector2.zero;Physics2D.SyncTransforms();}
        [UnityTest] public IEnumerator MillstoneKeepsFacingWhenPlayerIsVerticallyAlignedAndLocksItsChargeDirection()
        {
            var enemy=Spawn(false);
            using(var sim=world.arena.AcquireManualSimulation())
            foreach(int side in new[]{-1,1})
            {
                world.arena.Player.ResetActor(enemy.spawn+Vector2.up*8);
                enemy.ResetEnemy();enemy.SyncFacing(side);float x=enemy.Position.x;
                for(int tick=0;tick<120;tick++)
                {
                    float jitter=(tick%3-1)*3f/32;
                    PlayerAt(new Vector2(x+jitter,enemy.spawn.y+8));sim.Step(1f/120);
                    Assert.That(enemy.facing,Is.EqualTo(side),"vertical target jitter tick="+tick+" player="+world.arena.Player.Position+" enemy="+enemy.Position+" state="+enemy.State+" path="+enemy.Pursuit.Status);
                    Assert.That(enemy.Position.x,Is.EqualTo(x).Within(.001f),"asymmetric body offset must not cause shuffling");
                }
                Assert.That(enemy.Hunting,Is.True);
                PlayerAt(new Vector2(x-side,enemy.spawn.y+8));sim.Step(1f/120);
                Assert.That(enemy.facing,Is.EqualTo(-side),"a target outside the band still changes approach direction");
                Assert.That((enemy.Position.x-x)*-side,Is.GreaterThan(0));

                enemy.ResetEnemy();enemy.SyncFacing(side);
                PlayerAt(enemy.Position+new Vector2(-side*3f/32,0));sim.Step(1f/120);
                Assert.That(enemy.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Telegraph));
                Assert.That(enemy.facing,Is.EqualTo(side),"starting an attack in the band keeps the existing direction");
                for(int tick=0;tick<250;tick++)
                {
                    PlayerAt(enemy.Position+new Vector2(-side*3,0));sim.Step(1f/120);
                    Assert.That(enemy.facing,Is.EqualTo(side),"telegraph and charge keep their captured direction");
                }
            }
            Assert.That(world.arena.SimulationFaulted,Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator MillstoneChargeKeepsCyclingNativeFramesAfterLateUpdateInBothDirections()
        {
            var room=world.Data.rooms.First(r=>r.kind=="start");
            origin=new Vector2(room.x+room.w*.5f,-(room.y+room.exitY));
            var rows=new System.Text.StringBuilder("direction,sample,age,x,sprite\n");
            var clip=Chapter1NewMonsterArt.Load().Find("millstone-roll");
            using(var simulation=world.arena.AcquireManualSimulation())
            {
                foreach(int facing in new[]{1,-1})
                {
                    var actor=Spawn(false);PlayerAt(origin+Vector2.right*(facing*3));
                    for(int n=0;n<260&&actor.State!=Chapter1PrototypeEnemy.Phase.Charge;n++)simulation.Step(.01f);
                    Assert.That(actor.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Charge));
                    var sprites=new System.Collections.Generic.HashSet<Sprite>();
                    for(int n=0;n<24;n++)
                    {
                        simulation.Step(.04f);
                        yield return null; // Inspect the final sprite after the real presentation LateUpdate.
                        Assert.That(actor.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Charge));
                        var expected=clip.SpriteAt(actor.AttackAge-actor.Definition.prepare,true);
                        Assert.That(actor.visual.sprite,Is.SameAs(expected),"The charge roll was overwritten after presentation.");
                        Assert.That(actor.visual.flipX,Is.EqualTo(facing>0));sprites.Add(actor.visual.sprite);
                        rows.AppendLine(string.Join(",",facing,n,actor.AttackAge,actor.Position.x,actor.visual.sprite.name));
                        string captureRoot=System.IO.Path.GetFullPath("../outputs/stage2-fx-review/unity-millstone");System.IO.Directory.CreateDirectory(captureRoot);
                        var camera=world.sceneCamera;camera.transform.position=new Vector3(actor.Position.x+2,actor.Position.y+3,-10);
                        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var target=RenderTexture.GetTemporary(960,540,24);
                        camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                        var image=new Texture2D(960,540,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(captureRoot,"roll-"+facing+"-"+n.ToString("00")+".png"),image.EncodeToPNG());
                        Object.Destroy(image);camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(target);

                    }
                    Assert.That(sprites.Count,Is.EqualTo(8),"A moving charge must display all eight native roll frames repeatedly.");
                    actor.aggressive=false;actor.CancelAttack();
                }
            }
            string output=System.IO.Path.GetFullPath("../outputs/stage2-fx-review/all-attacks");
            System.IO.Directory.CreateDirectory(output);System.IO.File.WriteAllText(System.IO.Path.Combine(output,"millstone-runtime-frames.csv"),rows.ToString());
            Debug.Log("MILLSTONE_RUNTIME_ROLL_PASS: 8 unique sprites / 24 charge samples / both directions, after LateUpdate.");
        }
        // Run through the actual common simulation, not a second implementation of the attack clock.
        [UnityTest] public IEnumerator RandomStageCanSpawnBothNewActorsWithProvidedSprites()
        {
            foreach(var spec in Chapter1NewMonsterCatalog.Load().monsters)
            {
                var actor=Spawn(spec.ranged);
                Assert.That(actor.Spec.id,Is.EqualTo(spec.id));
                Assert.That(actor.maxHealth,Is.EqualTo(spec.ranged?60:140));
                Assert.That(actor.health,Is.EqualTo(actor.maxHealth));
                actor.health=1;actor.ResetEnemy();
                Assert.That(actor.health,Is.EqualTo(actor.maxHealth),"Reset restores the configured maximum health.");
                var sprite=Resources.Load<Sprite>(spec.resource);Assert.That(sprite,Is.Not.Null);
                Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
                Assert.That(sprite.pivot.y,Is.EqualTo(8).Within(.01));
            }
            Assert.That(Chapter1NewMonsterCatalog.Load().monsters.Single(s=>s.ranged).definition.maxHP,Is.EqualTo(60));
            Assert.That(Chapter1NewMonsterCatalog.Load().monsters.Single(s=>!s.ranged).definition.maxHP,Is.EqualTo(140));
            yield return null;
        }
        [UnityTest] public IEnumerator BombardmentHasFixedGroundTimingOnePulseAndDeathCancellation()
        {
            var e=Spawn(true);var player=world.arena.Player;var target=origin+Vector2.right*7;
            using(var driver=world.arena.AcquireManualSimulation())
            {
                PlayerAt(target);driver.Step(.01f);Assert.That(e.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Telegraph));
                Assert.That(e.ShotLaunched,Is.False);
                e.DrawPrototype();Assert.That(e.Presentation.Warning.enabled,Is.False);
                for(int i=0;i<145;i++)driver.Step(.01f);
                Assert.That(e.ShotLaunched,Is.False,"Inflation alone has no landing marker.");
                for(int i=0;i<6;i++)driver.Step(.01f);
                e.DrawPrototype();Assert.That(e.ShotLaunched,Is.True);
                var locked=e.LockedGround;Assert.That(e.BlastBounds.size.x,Is.EqualTo(2));Assert.That(e.BlastBounds.min.y,Is.EqualTo(locked.y));
                Assert.That(e.Presentation.Warning.enabled,Is.True);
                Assert.That(e.Presentation.Warning.bounds.size.x,Is.EqualTo(e.BlastBounds.size.x).Within(.001));
                Assert.That(e.Presentation.Warning.bounds.size.y,Is.EqualTo(e.BlastBounds.size.y).Within(.001));
                PlayerAt(target+Vector2.right*4);
                for(int i=0;i<120;i++)driver.Step(.01f);
                Assert.That(e.HitsDelivered,Is.Zero);Assert.That(e.LockedGround,Is.EqualTo(locked));
                for(int i=0;i<10;i++)driver.Step(.01f);Assert.That(e.HitsDelivered,Is.Zero,"moving away from the marker must dodge");
                e.ResetEnemy();PlayerAt(target);driver.Step(.01f);float hp=player.health;
                for(int i=0;i<285;i++)driver.Step(.01f);
                Assert.That(e.HitsDelivered,Is.EqualTo(1));Assert.That(hp-player.health,Is.EqualTo(15).Within(.001));
                for(int i=0;i<100;i++)driver.Step(.01f);Assert.That(e.HitsDelivered,Is.EqualTo(1),"no lingering damage");
                e.ResetEnemy();PlayerAt(target);driver.Step(.01f);hp=player.health;e.health=0;
                for(int i=0;i<300;i++)driver.Step(.01f);Assert.That(player.health,Is.EqualTo(hp),"death cancels the pending shell");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ChargeLocksDirectionTravelsTwelveAndHitsEachTargetOnce()
        {
            var e=Spawn(false);var target=origin+Vector2.right*8;
            using(var driver=world.arena.AcquireManualSimulation())
            {
                PlayerAt(target);driver.Step(.01f);Assert.That(e.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Telegraph));
                int facing=e.facing;PlayerAt(origin+Vector2.left*3);
                for(int i=0;i<185;i++)driver.Step(.01f);
                Assert.That(e.facing,Is.EqualTo(facing));Assert.That(e.Position.x,Is.EqualTo(origin.x).Within(.02));
                PlayerAt(target);float hp=world.arena.Player.health;
                for(int i=0;i<130;i++)driver.Step(.01f);
                Assert.That(e.Travelled,Is.EqualTo(12).Within(.03));Assert.That(e.HitsDelivered,Is.EqualTo(1));
                Assert.That(hp-world.arena.Player.health,Is.EqualTo(20).Within(.001));
                Assert.That(e.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Cooldown));
                float ready=e.ReadyAt;Assert.That(ready-world.arena.Now,Is.GreaterThan(1));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ApprovedChargeShakesThenHoldsStillAndUsesNativeRollAndWave()
        {
            var e=Spawn(false);PlayerAt(origin+Vector2.right*8);
            using(var driver=world.arena.AcquireManualSimulation())
            {
                driver.Step(.01f);var at=e.Position;
                // The body reddens with the shake: faint early, deep by the end of the telegraph.
                for(int i=0;i<40;i++)driver.Step(.01f);
                e.DrawPrototype();float early=e.Presentation.Redness;
                Assert.That(e.Presentation.Red.enabled,Is.True);Assert.That(early,Is.GreaterThan(0).And.LessThan(.1f));
                for(int i=0;i<144;i++)driver.Step(.01f);
                Assert.That(e.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Telegraph));
                e.DrawPrototype();Assert.That(e.Presentation.Redness,Is.GreaterThan(.4f),"red by the end of the wind-up");
                for(int i=0;i<12;i++)
                {
                    e.DrawPrototype();Assert.That(e.Presentation.ShakeOffset,Is.EqualTo(Vector2.zero));
                    Assert.That(e.Position.x,Is.EqualTo(at.x).Within(.001),"The final hold cannot move the body.");
                    Assert.That(e.Presentation.Wave.enabled,Is.False);driver.Step(.01f);
                }
                for(int i=0;i<8;i++)driver.Step(.01f);
                e.DrawPrototype();Assert.That(e.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Charge));
                Assert.That(e.Presentation.Wave.enabled,Is.True);Assert.That(e.Presentation.Red.enabled,Is.False,"no red once it rolls");
                Assert.That(e.visual.sprite.name,Does.Not.Contain("base"));
                float clock=world.arena.Now;var sprite=e.visual.sprite;var position=e.Presentation.Wave.transform.position;
                e.DrawPrototype();Assert.That(world.arena.Now,Is.EqualTo(clock));
                Assert.That(e.visual.sprite,Is.EqualTo(sprite));Assert.That(e.Presentation.Wave.transform.position,Is.EqualTo(position),"Draw has no independent wall clock.");
                e.CancelAttack();e.DrawPrototype();Assert.That(e.Presentation.Wave.enabled,Is.False);
                e.ResetEnemy();e.DrawPrototype();Assert.That(e.Presentation.ShakeOffset,Is.EqualTo(Vector2.zero));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator OrdinaryKnockKeepsTelegraphButStunCancelsAndDelaysRestart()
        {
            var e=Spawn(false);
            using(var driver=world.arena.AcquireManualSimulation())
            {
                PlayerAt(origin+Vector2.right*8);driver.Step(.01f);
                Assert.That(e.Receive(1,world.arena.Player,1,true,null,bypass:true),Is.True);
                Assert.That(e.AttackInProgress,Is.True);Assert.That(e.knockUntil,Is.GreaterThan(world.arena.Now));
                Assert.That(e.Receive(0,world.arena.Player,1,false,new HitData{stun=.5f},bypass:true),Is.True);
                Assert.That(e.AttackInProgress,Is.False);Assert.That(e.ReadyAt,Is.GreaterThanOrEqualTo(e.stunUntil+3f-.01f));
                for(int i=0;i<100;i++)driver.Step(.01f);Assert.That(e.AttackInProgress,Is.False);
                e.ResetEnemy();Assert.That(e.AttacksStarted,Is.Zero);Assert.That(e.Travelled,Is.Zero);
            }
            yield return null;
        }
        // The barrel bombard is a fixed emplacement; the millstone keeps the ordinary knockback.
        [UnityTest] public IEnumerator BarrelBombardStaysPutWhenHitButMillstoneIsPushed()
        {
            var catalog=Chapter1NewMonsterCatalog.Load().monsters;
            Assert.That(catalog.Single(s=>s.ranged).definition.immovable,Is.True);
            Assert.That(catalog.Single(s=>!s.ranged).definition.immovable,Is.False);
            using(var driver=world.arena.AcquireManualSimulation())
            {
                foreach(bool ranged in new[]{true,false})
                {
                    var e=Spawn(ranged);
                    for(int i=0;i<30;i++)driver.Step(.01f); // settle on the floor before measuring
                    // Passive brains still run the motor, so only the hits can move the body.
                    e.aggressive=false;float x=e.Position.x,hp=e.health;
                    PlayerAt(e.Position+Vector2.left*1.5f);
                    Assert.That(e.Receive(1,world.arena.Player,1,true,null,bypass:true),Is.True);
                    Assert.That(e.Receive(1,world.arena.Player,1,false,new HitData{push=2,pushSeconds=.2f},bypass:true),Is.True);
                    for(int i=0;i<60;i++)driver.Step(.01f);
                    Assert.That(e.health,Is.LessThan(hp),"Both hits are still accepted.");
                    if(ranged)Assert.That(e.Position.x,Is.EqualTo(x).Within(.001),"The barrel bombard never leaves its spot.");
                    else Assert.That(e.Position.x-x,Is.GreaterThan(.5f),"The millstone keeps its knockback.");
                    e.CancelAttack();e.gameObject.SetActive(false);
                }
            }
            yield return null;
        }
        // The species presenter rewrites flipX after the shared hit bridge; the white flash must
        // still match the body's final frame, or the millstone looks like two mirrored copies.
        [UnityTest] public IEnumerator HitFlashMatchesBodyFlipAndFrameInBothDirections()
        {
            var bridge=Deviant.Integration.HitFeedbackBridge.Attach(world.arena);Assert.That(bridge.Ready,Is.True,bridge.LastError);
            bridge.SetEffectEnabled(Deviant.Integration.HitFeedbackEffect.HitFlash,true);
            foreach(bool ranged in new[]{false,true})
                foreach(int side in new[]{1,-1})
                {
                    Chapter1PrototypeEnemy e;
                    // The manual driver holds combat time, so the 65 ms flash is still up when the frame is checked.
                    var driver=world.arena.AcquireManualSimulation();
                    e=Spawn(ranged);e.maxHealth=e.health=10000;
                    PlayerAt(origin+Vector2.right*(side*6));driver.Step(.01f);
                    Assert.That(e.facing,Is.EqualTo(side));
                    Assert.That(e.Receive(1,world.arena.Player,side,true,null,bypass:true),Is.True);
                    yield return null; // bridge (750) -> species presenter (950) -> overlay follow, in a real frame
                    var overlay=e.visual.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Accepted-hit white overlay");
                    string label=(ranged?"barrel":"millstone")+" facing "+side;
                    Assert.That(overlay.enabled,Is.True,label);
                    Assert.That(overlay.flipX,Is.EqualTo(e.visual.flipX),label+": the flash must not be mirrored against the body.");
                    Assert.That(overlay.sprite,Is.SameAs(e.visual.sprite),label);
                    Assert.That(overlay.transform.localPosition,Is.EqualTo(Vector3.zero),label);
                    // What is drawn, not just the flags: the flash covers the body's own pixels (the shader applies flipX).
                    Assert.That(DrawnOverlap(e.visual,overlay),Is.GreaterThan(.95f),label+": the flash is drawn over the body, not beside it.");
                    e.aggressive=false;e.CancelAttack();e.gameObject.SetActive(false);driver.Dispose();
                }
        }
        // Renders the body alone and the flash alone from the same camera and returns the IoU of their opaque pixels.
        static float DrawnOverlap(SpriteRenderer body,SpriteRenderer flash)
        {
            const int layer=30,size=256;
            var go=new GameObject("Flash overlap camera");var cam=go.AddComponent<Camera>();
            cam.orthographic=true;cam.orthographicSize=Mathf.Max(body.bounds.extents.x,body.bounds.extents.y)*1.4f+.1f;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(0,0,0,0);cam.cullingMask=1<<layer;
            go.transform.position=new Vector3(body.bounds.center.x,body.bounds.center.y,-10);
            var rt=new RenderTexture(size,size,24,RenderTextureFormat.ARGB32);cam.targetTexture=rt;
            int bodyLayer=body.gameObject.layer,flashLayer=flash.gameObject.layer;bool flashOn=flash.enabled;
            bool[] Mask(SpriteRenderer shown,SpriteRenderer hidden)
            {
                shown.gameObject.layer=layer;hidden.gameObject.layer=0;shown.enabled=true;
                cam.Render();RenderTexture.active=rt;var t=new Texture2D(size,size,TextureFormat.RGBA32,false);t.ReadPixels(new Rect(0,0,size,size),0,0);t.Apply();RenderTexture.active=null;
                var m=t.GetPixels32().Select(c=>c.a>40).ToArray();Object.DestroyImmediate(t);return m;
            }
            var a=Mask(body,flash);var b=Mask(flash,body);
            body.gameObject.layer=bodyLayer;flash.gameObject.layer=flashLayer;flash.enabled=flashOn;
            cam.targetTexture=null;Object.DestroyImmediate(go);rt.Release();Object.DestroyImmediate(rt);
            int both=0,any=0;for(int i=0;i<a.Length;i++){if(a[i]&&b[i])both++;if(a[i]||b[i])any++;}
            return any==0?0:(float)both/any;
        }
        [UnityTest] public IEnumerator HalfHealthPreservesTelegraph()
            => VerifyHealthBreak(false);
        [UnityTest] public IEnumerator HalfHealthPreservesCharge()
            => VerifyHealthBreak(true);
        IEnumerator VerifyHealthBreak(bool duringCharge)
        {
            var e=Spawn(false);
            using(var driver=world.arena.AcquireManualSimulation())
            {
                PlayerAt(origin+Vector2.right*8);driver.Step(.01f);
                if(duringCharge)for(int i=0;i<210;i++)driver.Step(.01f);
                Assert.That(e.State,Is.EqualTo(duringCharge?Chapter1PrototypeEnemy.Phase.Charge:Chapter1PrototypeEnemy.Phase.Telegraph));
                var state=e.State;
                Assert.That(e.Receive(e.maxHealth*.55f,world.arena.Player,1,false,null,bypass:true),Is.True);
                Assert.That(e.State,Is.EqualTo(state));
                Assert.That(e.stunUntil,Is.LessThanOrEqualTo(world.arena.Now));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ChargeRecoveryLastsThreeSecondsBeforeNextTelegraph()
        {
            var e=Spawn(false);
            using(var driver=world.arena.AcquireManualSimulation())
            {
                PlayerAt(origin+Vector2.right*8);
                for(int i=0;i<400&&e.State!=Chapter1PrototypeEnemy.Phase.Cooldown;i++)driver.Step(.01f);
                Assert.That(e.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Cooldown));
                Assert.That(e.ReadyAt-world.arena.Now,Is.EqualTo(3).Within(.02));
                int attacks=e.AttacksStarted;PlayerAt(e.Position+Vector2.left*8);
                for(int i=0;i<290;i++)driver.Step(.01f);
                Assert.That(e.AttacksStarted,Is.EqualTo(attacks));
                Assert.That(e.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Cooldown));
                for(int i=0;i<15;i++)driver.Step(.01f);
                Assert.That(e.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Telegraph));
                Assert.That(e.AttacksStarted,Is.EqualTo(attacks+1));
            }
            yield return null;
        }
        // 2026-11-11: once it has detected the target the charger hunts it off ledges (data/combat/enemy_pursuit.json),
        // but a charge stops at an edge whose floor below has no route back up (5 units, held above a 3.2-unit jump ceiling so the room's other floors cannot route back).
        [UnityTest] public IEnumerator HuntingApproachLeavesTheLedgeButChargeStopsAtAnEdgeWithNoWayBack()
        {
            var platform=new GameObject("test ledge");platform.AddComponent<ArenaSurface>().oneWay=true;
            platform.transform.position=origin+new Vector2(1,5-.25f);platform.AddComponent<BoxCollider2D>().size=new Vector2(8,.5f);
            var e=Spawn(false);e.spawn=origin+new Vector2(0,5);e.ResetEnemy();Physics2D.SyncTransforms();
            float jumpCeiling=e.Pursuit.JumpHeight;e.Pursuit.JumpHeight=3.2f;
            try
            {
            using(var driver=world.arena.AcquireManualSimulation())
            {
                PlayerAt(e.spawn+new Vector2(8,3));
                for(int i=0;i<300;i++){PlayerAt(e.spawn+new Vector2(8,3));driver.Step(.01f);}
                Assert.That(e.Hunting,Is.True);
                Assert.That(e.Position.y,Is.LessThan(e.spawn.y-.1f),"the hunting approach follows the target off the edge");
                e.ResetEnemy();PlayerAt(e.spawn+Vector2.right*8);
                for(int i=0;i<275;i++)driver.Step(.01f);
                Assert.That(e.Travelled,Is.GreaterThan(1).And.LessThan(11));
                Assert.That(e.Position.y,Is.EqualTo(e.spawn.y).Within(.05),"the charge ends on the edge instead of dropping");
                Assert.That(e.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Cooldown));
            }
            }
            finally{e.Pursuit.JumpHeight=jumpCeiling;}
            yield return null;
        }
        // 2026-11-12: a hunter whose body stays fully off screen for leaveScreenSeconds gives up and returns to its spawn
        // (data/combat/enemy_pursuit.json screenLeash); seen on screen again it detects and hunts again.
        [UnityTest] public IEnumerator HunterOffScreenGivesUpReturnsHomeAndHuntsAgainOnScreen()
        {
            var e=Spawn(false);var cam=world.arena.ViewCamera;Assert.That(cam&&cam.orthographic,Is.True);
            var oldCamera=cam.transform.position;
            void Look(Vector2 at)=>cam.transform.position=new Vector3(at.x,at.y,-10);
            try
            {
            using(var driver=world.arena.AcquireManualSimulation())
            {
                Look(e.Position);PlayerAt(origin+Vector2.right*12);
                for(int i=0;i<200&&!e.Hunting;i++)driver.Step(.01f);
                Assert.That(e.Hunting,Is.True);driver.Step(.01f);
                Look(e.Position+Vector2.right*200);
                float offAt=world.arena.Now;
                // The give-up waits for the current telegraph/charge to end.
                for(int i=0;i<1200&&!e.Pursuit.Returning;i++)driver.Step(.01f);
                Assert.That(e.Pursuit.Returning,Is.True,"a hunter off screen gives up");
                Assert.That(world.arena.Now-offAt,Is.GreaterThanOrEqualTo(e.Pursuit.settings.leaveScreenSeconds-.02f));
                Assert.That(e.Hunting,Is.False);Assert.That(e.Alert.Muted,Is.True,"returning walkers ignore chain alerts");
                float health=e.health;
                for(int i=0;i<1300&&e.Pursuit.Returning;i++)driver.Step(.01f);
                Assert.That(e.Pursuit.Returning,Is.False);
                Assert.That(Vector2.Distance(e.Position,e.spawn),Is.LessThan(.6f),"it ends at its spawn point");
                Assert.That(e.health,Is.EqualTo(health),"health is kept");
                Assert.That(e.Alert.Alerted,Is.False,"the reaction shows again on the next detection");
                Look(e.Position);
                for(int i=0;i<200&&!e.Hunting;i++)driver.Step(.01f);
                Assert.That(e.Hunting,Is.True,"on screen it detects and hunts again");
            }
            }
            finally{cam.transform.position=oldCamera;}
            yield return null;
        }
        [UnityTest] public IEnumerator InitialBombardmentDelayBeginsAtDetectionRatherThanWorldSpawn()
        {
            var e=Spawn(true);e.Spec.firstDelayMin=e.Spec.firstDelayMax=.5f;e.ResetEnemy();
            using(var driver=world.arena.AcquireManualSimulation())
            {
                // Outside detection range, the first-attack timer does not start.
                PlayerAt(origin+Vector2.left*30);
                for(int i=0;i<100;i++)driver.Step(.01f);
                Assert.That(e.AttacksStarted,Is.Zero);
                PlayerAt(origin+Vector2.right*25);driver.Step(.01f);
                Assert.That(e.AttacksStarted,Is.Zero,"same room but beyond the 24-unit sense radius");
                PlayerAt(origin+Vector2.right*7);driver.Step(.01f);
                for(int i=0;i<46;i++)driver.Step(.01f);
                Assert.That(e.AttacksStarted,Is.Zero,"first delay must still apply after a late room entry");
                for(int i=0;i<8;i++)driver.Step(.01f);
                Assert.That(e.AttacksStarted,Is.EqualTo(1));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator MillstoneChargesThroughTheStartingRoomExit()
        {
            var room=world.Data.rooms.First(r=>r.kind=="start");
            var blueprints=world.GetComponent<Chapter1StartingBlueprints>();
            if(blueprints.ExitLocked)Assert.That(blueprints.ApplyPreselected(new[]{"sword","blunt"}),Is.True);
            Assert.That(blueprints.ExitLocked,Is.False);
            float exit=room.x+room.w;
            origin=new Vector2(exit-4,-(room.y+room.exitY));
            var roller=Spawn(false);
            using(var sim=world.arena.AcquireManualSimulation())
            {
                PlayerAt(new Vector2(exit+2,origin.y));
                sim.Step(.01f);
                Assert.That(roller.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Telegraph));
                for(int i=0;i<320&&roller.Position.x<=exit+.1f;i++)sim.Step(.01f);
                Assert.That(roller.Position.x,Is.GreaterThan(exit+.1f));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ChargeStopsAtWallWithoutDamagingPlayerBehindIt()
        {
            var wall=new GameObject("test solid wall");wall.AddComponent<ArenaSurface>();
            wall.transform.position=origin+new Vector2(6,2);wall.AddComponent<BoxCollider2D>().size=new Vector2(.5f,4);
            var e=Spawn(false);Physics2D.SyncTransforms();
            using(var driver=world.arena.AcquireManualSimulation())
            {
                PlayerAt(origin+Vector2.right*8);float hp=world.arena.Player.health;
                for(int i=0;i<310;i++)driver.Step(.01f);
                Assert.That(e.AttacksStarted,Is.EqualTo(1));
                Assert.That(e.Travelled,Is.InRange(3f,6f));
                Assert.That(e.State,Is.EqualTo(Chapter1PrototypeEnemy.Phase.Cooldown));
                Assert.That(e.HitsDelivered,Is.Zero);Assert.That(world.arena.Player.health,Is.EqualTo(hp));
                Assert.That(e.bodyCollider.bounds.max.x,Is.LessThanOrEqualTo(wall.GetComponent<Collider2D>().bounds.min.x+.02f));
            }
            yield return null;
        }
    }
}
