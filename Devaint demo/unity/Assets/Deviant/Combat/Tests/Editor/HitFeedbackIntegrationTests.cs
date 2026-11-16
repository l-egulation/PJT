using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class HitFeedbackIntegrationTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();Time.timeScale=1;}
        [UnityTest]public IEnumerator LabAcceptedHitsUseCapturedGeometryAndReleaseOnReset()=>Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTest]public IEnumerator GameAcceptedHitsUseTheSameOutputAndTimeOwners()=>Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        static IEnumerator Exercise(string path)
        {
            LegacySceneFixture.Open(path);Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=false;
            yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var player=arena.Player;var target=arena.enemies[0];
            var floor=new GameObject("Feedback explicit fixture floor");floor.transform.position=new Vector3(0,999.5f,0);
            floor.AddComponent<ArenaSurface>();var collider=floor.AddComponent<BoxCollider2D>();collider.size=new Vector2(80,1);arena.RegisterFeedbackSurface(collider);
            using(var driver=arena.AcquireManualSimulation())
            {
                driver.Reset();foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(35,1000);}
                target.maxHealth=target.health=100;target.Position=new Vector2(2.3f,1000);player.Position=new Vector2(0,1000);player.SyncFacing(1);
                Assert.That(player.TryReconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="base_sword"),0,out var error),Is.True,error);player.selected=0;Physics2D.SyncTransforms();
                var preplaced=arena.gameObject.AddComponent<HitFeedbackBridge>();
                var bridge=HitFeedbackBridge.Attach(arena);Assert.That(bridge,Is.SameAs(preplaced));Assert.That(bridge.Ready,Is.True,bridge.LastError);
                target.Position=new Vector2(35,1000);Physics2D.SyncTransforms();
                driver.Step(.02f,new CombatInput(0,CombatButtons.Basic));
                for(int i=0;i<35;i++){driver.Step(.02f);bridge.Sample();}
                Assert.That(bridge.StartedHits,Is.Zero);Assert.That(bridge.Distortion.ActiveCount,Is.Zero,"A real missed attack must not request distortion.");
                driver.Reset();foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(35,1000);}
                player.Position=new Vector2(0,1000);player.SyncFacing(1);player.selected=0;
                Assert.That(player.TryReconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="base_sword"),0,out error),Is.True,error);
                target.health=100;target.Position=new Vector2(2.3f,1000);Physics2D.SyncTransforms();
                arena.TryGetCursor(out var cursor);
                driver.Step(.02f,new CombatInput(0,CombatButtons.Basic));
                for(int i=0;i<35;i++){driver.Step(.02f);bridge.Sample();}
                var buffer=new CombatAcceptedHit[32];Assert.That(arena.TryRead(cursor,buffer,out var page),Is.True);
                var hit=buffer.Take(page.Count).Single(h=>h.Target.ActorId==target.ActorId);
                Assert.That(hit.Origin.Stamp.FormId,Is.EqualTo("base_sword"));Assert.That(hit.Origin.Stamp.ActionId,Is.EqualTo("base_sword_basic_0"));
                Assert.That(hit.Origin.Stamp.WeaponId,Is.EqualTo(player.weapons[0].InstanceId));Assert.That(hit.Contact.Kind,Is.EqualTo(HitAnchorKind.OverlapRepresentative));Assert.That(hit.Contact.IncomingX,Is.EqualTo(1));
                Assert.That(bridge.StartedHits,Is.EqualTo(1));Assert.That(bridge.Distortion.ActiveCount,Is.EqualTo(1));long started=bridge.StartedHits;float health=target.health,now=arena.Now;
                bridge.Sample();bridge.Sample();Assert.That(bridge.StartedHits,Is.EqualTo(started));Assert.That(bridge.Distortion.ActiveCount,Is.EqualTo(1));Assert.That(target.health,Is.EqualTo(health));Assert.That(arena.Now,Is.EqualTo(now));
                bridge.SetMotionIntensity(0);Assert.That(target.Receive(0,player,1,false,null),Is.True);bridge.Sample();Assert.That(bridge.StartedHits,Is.EqualTo(started),"Unstamped direct damage is not silently mapped to the current weapon.");
                using(var owner=arena.Controls.OpenScope("external pause"))
                {
                    var pause=owner.PauseSimulation();driver.Reset();Assert.That(bridge.ActiveHits,Is.Zero);Assert.That(bridge.Distortion.ActiveCount,Is.Zero);Assert.That(arena.Controls.Current.SimulationPaused,Is.True);pause.Dispose();
                }
                // A real emitted projectile retains its original stamp after weapon switching.
                foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(35,1000);}
                player.Position=new Vector2(0,1000);target.Position=new Vector2(5,1000);target.health=100;
                player.TryReconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="shuriken"),0,out _);player.selected=0;Physics2D.SyncTransforms();arena.TryGetCursor(out cursor);
                driver.Step(.02f,new CombatInput(0,CombatButtons.Basic));for(int i=0;i<40&&arena.playerShots.Count==0;i++)driver.Step(.02f);
                Assert.That(arena.playerShots,Is.Not.Empty);var shot=arena.playerShots[0];player.CancelAction();player.selected=1;shot.SweepTo(new Vector2(7,shot.Position.y));
                Assert.That(arena.TryRead(cursor,buffer,out page),Is.True);hit=buffer.Take(page.Count).First(h=>h.Target.ActorId==target.ActorId);
                Assert.That(hit.Origin.Stamp.FormId,Is.EqualTo("shuriken"));Assert.That(hit.Contact.Kind,Is.EqualTo(HitAnchorKind.SweepContact));bridge.Sample();
                driver.Reset();Assert.That(bridge.ActiveHits,Is.Zero);bridge.enabled=false;Assert.That(bridge.ActiveDebris,Is.Zero);
            }
            Object.Destroy(floor);
        }
        [UnityTest]public IEnumerator BasicAndSpecialDistortionUsesAcceptedHitsAndKeepsTheWaveBudgetAndPauseBoundary()
        {
            CombatTestScene.Open();yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var target=arena.enemies[0];var player=arena.Player;
            var settings=new HitFeedbackSettings(JsonUtility.FromJson<HitFeedbackData>(Resources.Load<TextAsset>("HitFeedback/HitFeedback").text));
            using(var driver=arena.AcquireManualSimulation())
            {
                driver.Reset();foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(40,1000);}
                player.Position=new Vector2(0,1000);target.Position=new Vector2(2,1000);target.maxHealth=target.health=1000;target.shield=0;
                var bridge=HitFeedbackBridge.Attach(arena);Assert.That(bridge.Ready,Is.True,bridge.LastError);
                Assert.That(arena.TryBindActor(player,out var source),Is.True);
                foreach(bool basic in new[]{true,false})
                {
                    var origin=new CombatHitOrigin(source,default,Guid.NewGuid(),0,"fixture",Guid.Empty,CombatHitKind.Melee,
                        stamp:new CombatAttackStamp(player.weapons[0].InstanceId,"base_sword",basic?"fixture-basic":"fixture-special","fixture",basic));
                    target.health=0;
                    Assert.That(target.Receive(1,player,1,basic,null,origin:origin),Is.False);bridge.Sample();
                    Assert.That(bridge.Distortion.ActiveCount,Is.Zero,"A rejected hit must not create a wave.");
                    target.health=1000;target.guardUntil=arena.Now+1;target.guardReduction=1;
                    Assert.That(target.Receive(1,player,1,basic,null,origin:origin),Is.True);bridge.Sample();
                    Assert.That(target.health,Is.EqualTo(1000),"A fully guarded but accepted hit still requests distortion.");
                    Assert.That(bridge.Distortion.ActiveCount,Is.EqualTo(1),basic?"basic":"special");
                    bridge.Sample();Assert.That(bridge.Distortion.ActiveCount,Is.EqualTo(1),"Resampling must not duplicate a wave.");
                    using(var menu=arena.Controls.OpenScope("distortion pause"))
                    {
                        var pause=menu.PauseSimulation();arena.AdvanceFeedbackAccent(1);
                        Assert.That(bridge.Distortion.ActiveCount,Is.EqualTo(1));pause.Dispose();
                    }
                    arena.AdvanceFeedbackAccent(settings.WaveSeconds*.9f);Assert.That(bridge.Distortion.ActiveCount,Is.EqualTo(1),"The wave must survive until its configured lifetime.");
                    arena.AdvanceFeedbackAccent(settings.WaveSeconds*.2f);Assert.That(bridge.Distortion.ActiveCount,Is.Zero);
                }
                target.guardUntil=0;target.guardReduction=0;
                for(int i=0;i<8;i++)
                {
                    var origin=new CombatHitOrigin(source,default,Guid.NewGuid(),0,"fixture",Guid.Empty,CombatHitKind.Melee,
                        stamp:new CombatAttackStamp(player.weapons[0].InstanceId,"base_sword","fixture-burst","fixture",true));
                    Assert.That(target.Receive(1,player,1,true,null,origin:origin),Is.True);bridge.Sample();
                }
                Assert.That(bridge.Distortion.ActiveCount,Is.EqualTo(4),"All-hit requests retain the canonical simultaneous-wave limit.");
                bridge.SetMotionIntensity(0);Assert.That(bridge.Distortion.Intensity,Is.Zero);
                Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1));
                driver.Reset();Assert.That(bridge.Distortion.ActiveCount,Is.Zero);bridge.enabled=false;
            }
        }
        [UnityTest]public IEnumerator OrdinaryHitsDrawRandomPrefabsCriticalHitsTheBigOneAndOtherOutputsSurviveReset()
        {
            CombatTestScene.Open();yield return new EnterPlayMode();
            CombatArena arena=null;
            for(int frame=0;frame<120&&(!arena||!arena.Player);frame++)
            {yield return null;arena=Object.FindAnyObjectByType<CombatArena>();}
            Assert.That(arena&&arena.Player,Is.True,"The legacy combat fixture must initialize before testing the hit VFX.");
            CheckHitVfx(arena);
        }
        // Create captured callbacks only after EnterPlayMode has finished reloading the domain.
        static void CheckHitVfx(CombatArena arena)
        {
            var player=arena.Player;var target=arena.enemies[0];
            var settings=new HitFeedbackSettings(JsonUtility.FromJson<HitFeedbackData>(Resources.Load<TextAsset>("HitFeedback/HitFeedback").text));
            using(var driver=arena.AcquireManualSimulation())
            {
                driver.Reset();foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(40,1000);}
                player.Position=new Vector2(0,1000);target.Position=new Vector2(2,1000);target.maxHealth=target.health=1000;
                var bridge=HitFeedbackBridge.Attach(arena);Assert.That(bridge.Ready,Is.True,bridge.LastError);
                Action<string> hit=form=>{
                    arena.TryBindActor(player,out var source);
                    var origin=new CombatHitOrigin(source,default,Guid.NewGuid(),0,"fixture",Guid.Empty,CombatHitKind.Melee,
                        stamp:new CombatAttackStamp(player.weapons[0].InstanceId,form,"fixture-vfx","fixture",true));
                    Assert.That(target.Receive(1,player,1,true,null,bypass:true,origin:origin,
                        contact:new CombatHitContact(2,1001,1,0,HitAnchorKind.OverlapRepresentative)),Is.True);
                    bridge.Sample();
                };
                // F5 between rounds frees the live prefabs, so the shared particle budget never hides a later picture.
                Action round=()=>{driver.Reset();foreach(var enemy in arena.enemies)enemy.aggressive=false;target.health=1000;Assert.That(bridge.Ready,Is.True,bridge.LastError);};
                var forms=new[]{"base_sword","greatsword","whip","rapier","dagger","hammer","shield","bomb","shuriken","launcher"};
                var seen=new System.Collections.Generic.HashSet<string>();
                arena.PlayerCritChance=0;
                foreach(var form in forms)
                {
                    round();
                    for(int i=0;i<4;i++)
                    {
                        target.health=1000;hit(form);
                        Assert.That(bridge.StartedHits,Is.EqualTo(i+1),form);
                        Assert.That(settings.OrdinaryRecipeKeys,Does.Contain(bridge.LastHitVfxKey),form);
                        Assert.That(bridge.LastHitVfxKey,Is.Not.EqualTo("hit_blunt"),"The big prefab is kept for critical hits.");
                        seen.Add(bridge.LastHitVfxKey);
                    }
                }
                Assert.That(seen.Count,Is.GreaterThan(4),"Ordinary hits draw from the whole pool, not one prefab per weapon.");
                round();arena.PlayerCritChance=1;
                foreach(var form in new[]{"base_sword","hammer","shuriken"})
                {
                    target.health=1000;hit(form);
                    Assert.That(bridge.LastHitVfxKey,Is.EqualTo("hit_blunt"),form+" critical");
                }
                arena.PlayerCritChance=0;
                // A new input session without a combat reset (stage entry, respawn) must not leave the bridge on the old cursor.
                player.ResetPlayer();bridge.Sample();target.health=1000;hit("base_sword");
                Assert.That(bridge.Ready,Is.True,bridge.LastError);
                Assert.That(bridge.StartedHits,Is.EqualTo(1),"The bridge follows the player's new session and keeps playing hits.");
                long started=bridge.StartedHits;
                string first=bridge.LastHitVfxKey;
                var firstOutput=bridge.transform.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Hit feedback · "+first&&t.gameObject.activeSelf);
                float styleNow=arena.Now,styleHealth=target.health;
                bridge.SetPaperOutlineColor(Color.blue);bridge.Sample();
                Assert.That(arena.Now,Is.EqualTo(styleNow));Assert.That(target.health,Is.EqualTo(styleHealth));
                Assert.That(bridge.PaperOutlineColor,Is.EqualTo(Color.blue));
                Assert.That(firstOutput.gameObject.activeSelf,Is.True,"Changing the outline must not replace an already captured VFX.");
                bridge.SetEffectEnabled(HitFeedbackEffect.HitVfx,false);Assert.That(bridge.ActiveHits,Is.Zero);
                hit("base_sword");Assert.That(bridge.StartedHits,Is.EqualTo(started));
                Assert.That(bridge.Distortion.ActiveCount,Is.GreaterThan(0),"The VFX toggle must not turn off distortion.");
                Assert.That(target.visual.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.name=="Accepted-hit white overlay"&&r.enabled),Is.True);
                bridge.SetEffectEnabled(HitFeedbackEffect.HitVfx,true);bridge.Sample();Assert.That(bridge.ActiveHits,Is.Zero,"Enabling must not replay old hits.");
                hit("base_sword");Assert.That(settings.OrdinaryRecipeKeys,Does.Contain(bridge.LastHitVfxKey));Assert.That(bridge.StartedHits,Is.EqualTo(started+1));
                driver.Reset();Assert.That(bridge.PaperOutlineColor,Is.EqualTo(Color.blue),"F5 retains the outline color.");
                foreach(var enemy in arena.enemies)enemy.aggressive=false;
                arena.PlayerCritChance=1;target.health=1000;hit("hammer");arena.PlayerCritChance=0;
                Assert.That(bridge.LastHitVfxKey,Is.EqualTo("hit_blunt"),"F5 keeps the critical picture.");
                bridge.enabled=false;
            }
        }
        [UnityTest]public IEnumerator MonsterDeathNeverSlowsEvenWithTheFormerMarkerAndWaitsForMotionCompletion()
        {
            CombatTestScene.Open();yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var target=arena.enemies[0];var player=arena.Player;
            using(var driver=arena.AcquireManualSimulation())
            {
                driver.Reset();foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(40,1000);}
                player.Position=new Vector2(0,1000);target.Position=new Vector2(2,1000);target.health=1;
                // Explicit synthetic fixture, not an unrelated monster art fallback.
                var catalog=MakeDebrisFixture();var bridge=HitFeedbackBridge.Attach(arena,catalog);
                target.gameObject.AddComponent<HitFeedbackTarget>().importantDeath=true;
                arena.TryBindActor(player,out var source);var origin=new CombatHitOrigin(source,default,Guid.NewGuid(),0,"fixture",Guid.Empty,CombatHitKind.Melee,
                    stamp:new CombatAttackStamp(player.weapons[0].InstanceId,"base_sword","base_sword_basic_0","fixture",true));
                Assert.That(target.Receive(2,player,1,true,null,origin:origin),Is.True);bridge.Sample();
                long token=bridge.BeginDeath(target,catalog.bakes[0].source);Assert.That(token,Is.GreaterThan(0));Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1));Assert.That(bridge.ActiveDebris,Is.Zero);
                using(var external=arena.Controls.OpenScope("external"))
                {
                    var pause=external.PauseSimulation();arena.AdvanceFeedbackAccent(1);Assert.That(arena.Controls.Current.SimulationScale,Is.Zero);
                    Assert.That(bridge.CompleteDeath(target,token),Is.True);Assert.That(bridge.CompleteDeath(target,token),Is.False);
                    pause.Dispose();driver.Step(.02f);bridge.Sample();Assert.That(bridge.ActiveDebris,Is.GreaterThan(0));
                    pause=external.PauseSimulation();driver.Reset();Assert.That(bridge.ActiveDebris,Is.Zero);Assert.That(arena.Controls.Current.SimulationPaused,Is.True);
                    Assert.That(bridge.CompleteDeath(target,token),Is.False);pause.Dispose();Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1));
                }
                bridge.enabled=false;foreach(var bake in catalog.bakes){Object.Destroy(bake.source);Object.Destroy(bake.pieces[0].mesh);Object.Destroy(bake);}Object.Destroy(catalog);
            }
        }
        [Test]public void CameraComposesBothFeedbackOffsetsWithoutDriftOrSharedRelease()
        {
            var root=new GameObject("Merged camera output fixture");
            try
            {
                var camera=root.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5;
                var output=CameraOutputBinding.GetOrCreate(camera);var mixer=new CameraKickMixer(4,4);
                using(var follow=output.BindBaseSource("test follow"))
                using(var additive=output.BindAdditiveOffset("main feedback"))
                using(var kick=output.BindKickSource(mixer,100))
                {
                    follow.Submit(new CameraPosition(3,4,-10));additive.Submit(new Vector3(.1f,.05f,0));mixer.Add(1,0,2,1);
                    output.ApplyNow();output.ApplyNow();Assert.That(Vector3.Distance(camera.transform.position,new Vector3(3.3f,4.05f,-10)),Is.LessThan(.0001f));
                    kick.Dispose();output.ApplyNow();Assert.That(Vector3.Distance(camera.transform.position,new Vector3(3.1f,4.05f,-10)),Is.LessThan(.0001f));
                    additive.Dispose();output.ApplyNow();Assert.That(camera.transform.position,Is.EqualTo(new Vector3(3,4,-10)));
                }
            }
            finally{Object.DestroyImmediate(root);}
        }
        [UnityTest]public IEnumerator IndependentTogglesPreserveTheOtherFeedbackLayerAndForeignTimeOwners()
        {
            CombatTestScene.Open();yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var player=arena.Player;var target=arena.enemies[0];
            using(var driver=arena.AcquireManualSimulation())
            {
                driver.Reset();foreach(var enemy in arena.enemies)enemy.aggressive=false;
                player.Position=Vector2.zero;target.Position=new Vector2(2,0);target.maxHealth=target.health=1000;
                var main=arena.FeelFeedback;main.ResetFeedback();int previous=main.PresentedHits;
                var bridge=HitFeedbackBridge.Attach(arena);Assert.That(bridge.Ready,Is.True,bridge.LastError);
                arena.TryBindActor(player,out var source);
                Action hit=()=>{
                    var origin=new CombatHitOrigin(source,default,Guid.NewGuid(),0,"fixture",Guid.Empty,CombatHitKind.Melee,
                        stamp:new CombatAttackStamp(player.weapons[0].InstanceId,"base_sword","base_sword_basic_0","fixture",true),sourceFacing:1);
                    Assert.That(target.Receive(2,player,1,true,null,bypass:true,origin:origin,contact:new CombatHitContact(2,1,1,0,HitAnchorKind.OverlapRepresentative)),Is.True);
                    main.TickPresentation(0);bridge.Sample();
                };
                hit();Assert.That(main.PresentedHits,Is.EqualTo(previous+1));Assert.That(bridge.StartedHits,Is.EqualTo(1));
                Assert.That(bridge.ActiveHits,Is.GreaterThan(0));Assert.That(bridge.Distortion.ActiveCount,Is.EqualTo(1));
                bridge.SetEffectEnabled(HitFeedbackEffect.HitVfx,false);Assert.That(bridge.ActiveHits,Is.Zero);
                Assert.That(main.UsePixelHits?main.PixelFx.ActiveCount:main.ActiveImpactCount,Is.GreaterThan(0));Assert.That(bridge.Distortion.ActiveCount,Is.EqualTo(1));
                bridge.SetEffectEnabled(HitFeedbackEffect.WorldDistortion,false);Assert.That(bridge.Distortion.ActiveCount,Is.Zero);
                bridge.SetEffectEnabled(HitFeedbackEffect.HitFlash,false);
                Assert.That(bridge.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="Accepted-hit white overlay").All(r=>!r.enabled),Is.True);
                bridge.SetEffectEnabled(HitFeedbackEffect.WorldDistortion,true);hit();
                Assert.That(bridge.StartedHits,Is.EqualTo(1));Assert.That(bridge.Distortion.ActiveCount,Is.EqualTo(1));Assert.That(main.PresentedHits,Is.EqualTo(previous+2));
                bridge.SetEffectEnabled(HitFeedbackEffect.HitVfx,true);bridge.Sample();Assert.That(bridge.ActiveHits,Is.Zero,"Re-enabling must not replay old hits.");
                main.SetEffectsEnabled(false);hit();Assert.That(bridge.StartedHits,Is.EqualTo(2));Assert.That(main.PresentedHits,Is.EqualTo(previous+2));
                using(var owner=arena.Controls.OpenScope("foreign slow"))using(var menu=arena.Controls.OpenScope("foreign pause"))
                {
                    var slow=((IFeedbackTimeScope)owner).RequestSlow(.5f,2);
                    player.health=0;player.Die();Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(.35f));
                    bridge.SetEffectEnabled(HitFeedbackEffect.PlayerDeathSlow,false);Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(.5f));
                    var pause=menu.PauseSimulation();bridge.SetEffectEnabled(HitFeedbackEffect.PlayerDeathSlow,true);
                    Assert.That(arena.Controls.Current.SimulationPaused,Is.True);pause.Dispose();Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(.5f));
                    driver.Reset();Assert.That(bridge.IsEffectEnabled(HitFeedbackEffect.HitFlash),Is.False,"Combat reset retains the selected effect switches.");
                    Assert.That(main.EffectsEnabled,Is.False);player.health=0;player.Die();Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(.35f));
                    arena.AdvanceFeedbackAccent(.2f);Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(.5f));slow.Dispose();
                }
            }
        }
        [UnityTest]public IEnumerator DebrisSwitchClearsLiveAndPendingBodiesWithoutReplayingADeath()
        {
            CombatTestScene.Open();yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var target=arena.enemies[0];var catalog=MakeDebrisFixture();
            using(var driver=arena.AcquireManualSimulation())
            {
                driver.Reset();foreach(var enemy in arena.enemies)enemy.aggressive=false;
                var bridge=HitFeedbackBridge.Attach(arena,catalog);Assert.That(bridge.Ready,Is.True,bridge.LastError);
                target.health=0;long token=bridge.BeginDeath(target,catalog.bakes[0].source);
                bridge.SetEffectEnabled(HitFeedbackEffect.CorpseDebris,false);bridge.SetEffectEnabled(HitFeedbackEffect.CorpseDebris,true);
                Assert.That(bridge.CompleteDeath(target,token),Is.False);driver.Step(.02f);Assert.That(bridge.ActiveDebris,Is.Zero);
                driver.Reset();target.health=0;token=bridge.BeginDeath(target,catalog.bakes[0].source);
                Assert.That(bridge.CompleteDeath(target,token),Is.True);driver.Step(.02f);Assert.That(bridge.ActiveDebris,Is.GreaterThan(0));
                bridge.SetEffectEnabled(HitFeedbackEffect.CorpseDebris,false);Assert.That(bridge.ActiveDebris,Is.Zero);
                bridge.SetEffectEnabled(HitFeedbackEffect.CorpseDebris,true);driver.Step(.02f);Assert.That(bridge.ActiveDebris,Is.Zero);
                bridge.enabled=false;
            }
            foreach(var bake in catalog.bakes){Object.Destroy(bake.source);Object.Destroy(bake.pieces[0].mesh);Object.Destroy(bake);}Object.Destroy(catalog);
        }
        static DebrisCatalog MakeDebrisFixture()
        {
            var sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),Vector2.one*.5f,Texture2D.whiteTexture.width);
            var mesh=new Mesh{vertices=new[]{new Vector3(-.1f,-.1f),new Vector3(.1f,-.1f),new Vector3(.1f,.1f),new Vector3(-.1f,.1f)},
                uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},triangles=new[]{0,2,1,0,3,2}};mesh.RecalculateBounds();
            var bake=ScriptableObject.CreateInstance<DebrisBakeAsset>();bake.source=sprite;bake.sourceRect=sprite.rect;bake.sourcePivot=sprite.pivot;bake.pixelsPerUnit=sprite.pixelsPerUnit;
            bake.pieces=new[]{new DebrisPiece{center=Vector2.zero,mesh=mesh,mirroredMesh=mesh,area=.04f,
                hull=new[]{new Vector2(-.1f,-.1f),new Vector2(.1f,-.1f),new Vector2(.1f,.1f),new Vector2(-.1f,.1f)}}};
            var catalog=ScriptableObject.CreateInstance<DebrisCatalog>();catalog.bakes=new[]{bake};return catalog;
        }
    }
}
