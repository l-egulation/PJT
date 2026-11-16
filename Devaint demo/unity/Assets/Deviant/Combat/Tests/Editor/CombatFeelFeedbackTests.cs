using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class CombatFeelFeedbackTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static TestEnemy Target(CombatArena arena)
        {
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
            var target = arena.enemies[0];
            target.health = target.maxHealth = 200;
            target.Position = arena.Player.Position + Vector2.right;
            arena.ViewCamera.transform.position = new Vector3(target.Center.x, target.Center.y, -10);
            return target;
        }
        [Test] public void SettingsRejectInvalidOrUnboundedEffects()
        {
            var settings = CombatFeelSettings.Load(); settings.Validate();
            settings.hitStopSeconds = float.NaN;
            Assert.Throws<ArgumentException>(settings.Validate);
            settings = CombatFeelSettings.Load(); settings.shakeDistance = 4;
            Assert.Throws<ArgumentException>(settings.Validate);
        }
        [Test] public void CenterUsesOpaquePixelsInsteadOfTransparentSpritePadding()
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var pixels = new Color32[256];
            for (int y = 2; y < 6; y++) for (int x = 4; x < 8; x++) pixels[y * 16 + x] = new Color32(255,255,255,255);
            texture.SetPixels32(pixels); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0,0,16,16), Vector2.one*.5f, 8);
            var go = new GameObject("padded center fixture");
            try
            {
                var visual = go.AddComponent<SpriteRenderer>(); visual.sprite = sprite; go.transform.position = new Vector3(3,4,0);
                Assert.That(CombatFeelArt.WorldCenter(visual), Is.EqualTo(new Vector3(2.75f,3.5f,0)));
                visual.flipX = true;
                Assert.That(CombatFeelArt.WorldCenter(visual), Is.EqualTo(new Vector3(3.25f,3.5f,0)));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }
        [Test] public void BladeAnchorsUseNativeFootPivotAndMirrorWithTheSword()
        {
            var profile=SwordRibbonProfile.Load();
            var sprite=Resources.Load<Sprite>("CharacterApproved11/base_sword/F048");
            Assert.That(sprite,Is.Not.Null);
            var pose=profile.Find("CharacterApproved11/base_sword/F048");
            Assert.That(pose.emit,Is.True);
            var right=SwordBladePose.Local(sprite,pose.tip,false);
            var left=SwordBladePose.Local(sprite,pose.tip,true);
            Assert.That(left.x,Is.EqualTo(-right.x));Assert.That(left.y,Is.EqualTo(right.y));
            Assert.That(right.x,Is.EqualTo((9-sprite.pivot.x)/32f));
            Assert.That(right.y,Is.EqualTo((160-87-sprite.pivot.y)/32f));
        }
        [UnityTest] public IEnumerator SkillInterruptsBasicAtEveryPhaseWithoutCompletingIt()
        {
            CombatTestScene.Open(requireArt:true);yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var player=arena.Player;
            foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(40,0);}
            using(var simulation=arena.AcquireManualSimulation()) {
                foreach(var form in new[]{"base_sword","whip"}) {
                    foreach(float fraction in new[]{.05f,.5f,.9f}) {
                        player.CancelAction();
                        player.Reconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id==form),0);
                        player.Position=Vector2.zero;player.velocity=Vector2.zero;player.grounded=true;player.SyncFacing(1);
                        simulation.Step(.001f,new CombatInput(0,CombatButtons.Basic));
                        var basic=player.currentCast;
                        Assert.That(basic,Is.Not.Null);
                        simulation.Step((basic.EndTime-arena.Now)*fraction);
                        var target=Target(arena);target.Position=player.Position+Vector2.right*2;target.velocity=Vector2.zero;
                        Physics2D.SyncTransforms();
                        simulation.Step(.001f,new CombatInput(0,CombatButtons.Special));
                        Assert.That(basic.PresentationCancelled,Is.True,form+" "+fraction);
                        Assert.That(player.currentCast,Is.Not.Null);
                        Assert.That(player.currentCast.isBasic,Is.False);
                        Assert.That(player.currentCast.ExecutionId,Is.Not.EqualTo(basic.ExecutionId));
                    }
                }
            }
        }
        [UnityTest] public IEnumerator BasicRibbonJoinsBladePosesAndDoesNotAdvanceOnRepeatedDraws()
        {
            CombatTestScene.Open(requireArt:true);yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var player=arena.Player;
            foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(40,0);}
            using(var simulation=arena.AcquireManualSimulation()) {
                player.Position=Vector2.zero;player.velocity=Vector2.zero;player.grounded=true;player.SyncFacing(1);
                player.Reconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="base_sword"),0);
                simulation.Step(.02f,new CombatInput(0,CombatButtons.Basic));
                MeshFilter ribbon=null;
                for(int i=0;i<15;i++) {
                    simulation.Step(.02f);arena.Presentation.Draw(0);
                    var node=GameObject.Find("BasicAttackEffect_base_sword_attack1");
                    ribbon=node?node.GetComponent<MeshFilter>():null;
                    if(ribbon&&ribbon.sharedMesh.vertexCount>12)break;
                }
                Assert.That(ribbon,Is.Not.Null,arena.Presentation.LastEffectError);Assert.That(ribbon.sharedMesh.vertexCount,Is.GreaterThan(12));
                var vertices=ribbon.sharedMesh.vertices;arena.Presentation.Draw(0);arena.Presentation.Draw(0);
                CollectionAssert.AreEqual(vertices,ribbon.sharedMesh.vertices);
                Assert.That(ribbon.GetComponent<Collider2D>(),Is.Null);
                arena.FeelFeedback.SetEffectsEnabled(false);arena.Presentation.Draw(0);
                Assert.That(ribbon.GetComponent<MeshRenderer>().enabled,Is.False);
            }
        }
        [UnityTest] public IEnumerator WhipTrailFollowsNativeCurveAndClearsWhenDisabled()
        {
            CombatTestScene.Open(requireArt:true);yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var player=arena.Player;
            foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(40,0);}
            using(var simulation=arena.AcquireManualSimulation()) {
                player.Position=Vector2.zero;player.velocity=Vector2.zero;player.grounded=true;player.SyncFacing(1);
                player.Reconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="whip"),0);
                simulation.Step(.02f,new CombatInput(0,CombatButtons.Basic));
                MeshFilter ribbon=null;
                for(int i=0;i<30;i++) {
                    simulation.Step(.02f);arena.Presentation.Draw(0);
                    var node=GameObject.Find("BasicAttackEffect_whip_attack1");
                    ribbon=node?node.GetComponent<MeshFilter>():null;
                    if(ribbon&&ribbon.sharedMesh.vertexCount>12)break;
                }
                Assert.That(ribbon,Is.Not.Null,arena.Presentation.LastEffectError);Assert.That(ribbon.sharedMesh.vertexCount,Is.GreaterThan(12));
                var vertices=ribbon.sharedMesh.vertices;arena.Presentation.Draw(0);arena.Presentation.Draw(0);
                CollectionAssert.AreEqual(vertices,ribbon.sharedMesh.vertices);
                Assert.That(ribbon.GetComponent<Collider2D>(),Is.Null);
                arena.FeelFeedback.SetEffectsEnabled(false);arena.Presentation.Draw(0);
                Assert.That(ribbon.GetComponent<MeshRenderer>().enabled,Is.False);
            }
        }
        [UnityTest] public IEnumerator DelayedFirstDrawStillShowsTheUnexpiredBladeTail()
        {
            CombatTestScene.Open(requireArt:true);yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var player=arena.Player;
            foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(40,0);}
            using(var simulation=arena.AcquireManualSimulation()) {
                player.Position=Vector2.zero;player.velocity=Vector2.zero;player.grounded=true;player.SyncFacing(-1);
                player.Reconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="base_sword"),0);
                simulation.Step(.02f,new CombatInput(0,CombatButtons.Basic));var cast=player.currentCast;
                while(cast.Age<cast.ImpactTime+.05f)simulation.Step(.01f);
                arena.Presentation.Draw(0);
                var node=GameObject.Find("BasicAttackEffect_base_sword_attack1");
                Assert.That(node,Is.Not.Null,arena.Presentation.LastEffectError);
                var mesh=node.GetComponent<MeshFilter>();Assert.That(mesh,Is.Not.Null);
                Assert.That(mesh.sharedMesh.vertexCount,Is.GreaterThan(12));
            }
        }
        [UnityTest] public IEnumerator ImpactsStayCenteredWithDirectionalCutAndShortCross()
        {
            CombatTestScene.Open(); yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var target = Target(arena); var feedback = arena.FeelFeedback;
            feedback.ResetFeedback(); target.Receive(10, arena.Player, 1, false, null); feedback.TickPresentation(0);
            int firstVariant=feedback.LastImpactVariant; float first = feedback.LastImpactAngle; var center = CombatFeelArt.WorldCenter(target.visual);
            Assert.That(Vector3.Distance(feedback.LastImpactCenter, center), Is.LessThan(.0001f));
            var rays = feedback.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Centered white impact ")).ToArray();
            Assert.That(rays.Length, Is.EqualTo(2));
            foreach (var ray in rays) Assert.That(Vector3.Distance(ray.transform.position, center), Is.LessThan(.0001f));
            Assert.That(feedback.ActiveParticleCount, Is.EqualTo(9));
            feedback.TickPresentation(.3f); target.Receive(10, arena.Player, 1, false, null); feedback.TickPresentation(0);
            float difference = Mathf.Abs(feedback.LastImpactAngle-first); difference = Mathf.Min(difference,180-difference);
            Assert.That(difference, Is.LessThanOrEqualTo(44)); Assert.That(feedback.LastImpactVariant,Is.Not.EqualTo(firstVariant)); Assert.That(target.health, Is.EqualTo(180));
            feedback.SetEffectsEnabled(false); Assert.That(feedback.ActiveImpactCount, Is.Zero);
        }
        [UnityTest] public IEnumerator SixDifferentImpactSilhouettesAppearBeforeRepeating()
        {
            CombatTestScene.Open();yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var target=Target(arena);var feedback=arena.FeelFeedback;
            feedback.ResetFeedback();target.health=target.maxHealth=1000;
            var seen=new System.Collections.Generic.HashSet<int>();int previous=-1;
            for(int i=0;i<12;i++) {
                feedback.TickPresentation(.3f);target.Receive(1,arena.Player,1,false,null,bypass:true);feedback.TickPresentation(0);
                Assert.That(feedback.LastImpactVariant,Is.Not.EqualTo(previous));previous=feedback.LastImpactVariant;
                if(i<6)Assert.That(seen.Add(previous),Is.True);
                Assert.That(feedback.ActiveRingCount,Is.EqualTo(previous==1||previous==4?1:0));
                Assert.That(Vector3.Distance(feedback.LastImpactCenter,CombatFeelArt.WorldCenter(target.visual)),Is.LessThan(.001f));
            }
            Assert.That(seen.Count,Is.EqualTo(6));
        }
        [Test] public void CameraOffsetDoesNotDriftOrOverrideFramingAndCanBeReleased()
        {
            var go = new GameObject("camera offset fixture");
            try
            {
                var camera = go.AddComponent<Camera>();
                var output = CameraOutputBinding.GetOrCreate(camera);
                using (var follow = output.BindBaseSource("follow fixture"))
                using (var offset = output.BindAdditiveOffset("hit fixture"))
                using (var scope = output.Requests.OpenScope("framing fixture"))
                {
                    follow.Submit(new CameraPosition(3, 4, -10)); offset.Submit(new Vector3(.1f, -.05f, 0));
                    output.ApplyNow(); output.ApplyNow();
                    Assert.That(camera.transform.position, Is.EqualTo(new Vector3(3.1f, 3.95f, -10)));
                    using (scope.Request(10, new FixedCameraPosition(new CameraPosition(9, 8, -10))))
                    { output.ApplyNow(); Assert.That(camera.transform.position, Is.EqualTo(new Vector3(9, 8, -10))); }
                    follow.Submit(new CameraPosition(5, 6, -10)); offset.Dispose(); output.ApplyNow();
                    Assert.That(camera.transform.position, Is.EqualTo(new Vector3(5, 6, -10)));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
        [UnityTest] public IEnumerator AcceptedHitsPresentOnceAndComparisonOffHasNoNewEffects()
        {
            CombatTestScene.Open(); yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var feedback = arena.FeelFeedback;
            var target = Target(arena); feedback.ResetFeedback();
            int count = feedback.PresentedHits;
            target.Receive(0, arena.Player, 1, false, null); feedback.TickPresentation(0);
            Assert.That(feedback.PresentedHits, Is.EqualTo(count), "Zero/ineffective hits add no hit-stop.");
            target.Receive(10, arena.Player, 1, false, null); feedback.TickPresentation(0);
            Assert.That(feedback.PresentedHits, Is.EqualTo(count + 1));
            Assert.That(target.health, Is.EqualTo(190));
            Assert.That(feedback.ActiveParticleCount, Is.GreaterThan(0));
            feedback.TickPresentation(0);
            Assert.That(feedback.PresentedHits, Is.EqualTo(count + 1), "Repeated drawing must not replay hits.");
            feedback.SetEffectsEnabled(false);
            target.Receive(10, arena.Player, 1, false, null); feedback.TickPresentation(0);
            Assert.That(target.health, Is.EqualTo(180)); Assert.That(feedback.HitStopped, Is.False);
            Assert.That(feedback.ActiveParticleCount, Is.Zero);
            feedback.SetEffectsEnabled(true); feedback.TickPresentation(0);
            Assert.That(feedback.PresentedHits, Is.EqualTo(count + 1), "Re-enabling must not replay disabled hits.");
        }
        [UnityTest] public IEnumerator HitStopPreservesInputAndDoesNotOwnAnotherPause()
        {
            CombatTestScene.Open(); yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var feedback = arena.FeelFeedback;
            var target = Target(arena); feedback.ResetFeedback();
            feedback.SetHitStopEnabled(true);
            float scale = Time.timeScale;
            arena.Player.SubmitInput(new CombatInput(0, CombatButtons.Basic));
            target.Receive(10, arena.Player, 1, false, null); feedback.TickPresentation(0);
            Assert.That(feedback.HitStopped, Is.True);
            float clock = arena.Now; arena.SendMessage("FixedUpdate");
            Assert.That(arena.Now, Is.EqualTo(clock));
            Assert.That(arena.Player.CurrentInput.Pressed & CombatButtons.Basic, Is.EqualTo(CombatButtons.Basic));
            Assert.That(Time.timeScale, Is.EqualTo(scale));
            using (var scope = arena.Controls.OpenScope("other owner"))
            {
                using (var lease = scope.PauseSimulation())
                {
                    feedback.SetEffectsEnabled(false);
                    Assert.That(lease.IsActive, Is.True);
                    Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                }
            }
            feedback.SetEffectsEnabled(true); target.Receive(10, arena.Player, 1, false, null);
            feedback.TickPresentation(0); feedback.TickPresentation(.3f);
            Assert.That(feedback.HitStopped, Is.False); Assert.That(feedback.CurrentCameraOffset, Is.EqualTo(Vector3.zero));
            arena.SendMessage("FixedUpdate"); Assert.That(arena.Now, Is.GreaterThan(clock));
        }
        // 독·전기장 같은 잉크 파츠 지속 피해(mvp_effect)는 틱마다 판을 멈추지 않는다
        [UnityTest] public IEnumerator InkPartEffectDamageDoesNotHitStop()
        {
            CombatTestScene.Open(); yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var feedback = arena.FeelFeedback;
            var target = Target(arena); feedback.ResetFeedback();
            feedback.SetHitStopEnabled(true);
            Assert.That(arena.TryBindActor(arena.Player, out var player), Is.True);
            int count = feedback.PresentedHits;
            var effect = new CombatHitOrigin(player, default, Guid.NewGuid(), 0, null, Guid.NewGuid(), CombatHitKind.InkPart, actionId: "mvp_effect");
            target.Receive(10, arena.Player, 1, false, null, origin: effect); feedback.TickPresentation(0);
            Assert.That(feedback.PresentedHits, Is.EqualTo(count + 1), "The tick still presents its hit.");
            Assert.That(feedback.HitStopped, Is.False);
            target.Receive(10, arena.Player, 1, false, null); feedback.TickPresentation(0);
            Assert.That(feedback.HitStopped, Is.True, "Unlisted direct hits keep the global hit-stop.");
        }
        [UnityTest] public IEnumerator ResetAndDisableClearTransientFeedbackWithoutReplayingOldHits()
        {
            CombatTestScene.Open(); yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var target = Target(arena); var feedback = arena.FeelFeedback;
            feedback.SetHitStopEnabled(true);
            feedback.ResetFeedback(); target.Receive(30, arena.Player, 1, false, null); feedback.TickPresentation(0);
            Assert.That(feedback.HitStopRemaining, Is.GreaterThan(.06f));
            int count = feedback.PresentedHits;
            arena.ResetTest(); feedback.TickPresentation(0);
            Assert.That(feedback.HitStopped, Is.False); Assert.That(feedback.ActiveParticleCount, Is.Zero);
            Assert.That(feedback.PresentedHits, Is.EqualTo(count));
            target = Target(arena); target.Receive(1, arena.Player, 1, false, null); feedback.TickPresentation(0);
            feedback.enabled = false;
            Assert.That(feedback.HitStopped, Is.False); Assert.That(feedback.CurrentCameraOffset, Is.EqualTo(Vector3.zero));
        }
        [UnityTest] public IEnumerator DefaultHitsKeepSimulationMovingWhileTheVisibleImpactPlays()
        {
            CombatTestScene.Open(); yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var feedback = arena.FeelFeedback; var target = Target(arena);
            Assert.That(feedback.HitStopEnabled, Is.False);
            feedback.ResetFeedback(); target.Receive(10,arena.Player,1,false,null); feedback.TickPresentation(0);
            Assert.That(feedback.HitStopped, Is.False); Assert.That(feedback.HitStopRemaining, Is.Zero);
            Assert.That(feedback.ActiveImpactCount, Is.GreaterThan(0)); Assert.That(feedback.ActiveParticleCount, Is.EqualTo(9));
            float before=arena.Now;arena.SendMessage("FixedUpdate");Assert.That(arena.Now,Is.GreaterThan(before));
        }
        [UnityTest] public IEnumerator WhiteParticlesSurviveBroadTerrainVolumesAndBounceAtTheContactFloor()
        {
            CombatTestScene.Open(); yield return new EnterPlayMode(); yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var target=Target(arena);var feedback=arena.FeelFeedback;
            var solid=new GameObject("broad village terrain fixture");solid.transform.position=target.Center;
            solid.AddComponent<BoxCollider2D>().size=Vector2.one*20;Physics2D.SyncTransforms();
            feedback.ResetFeedback();target.Receive(10,arena.Player,1,false,null);feedback.TickPresentation(0);
            var emitter=feedback.GetComponentsInChildren<ParticleSystem>().Single(p=>p.name=="Combat feel ink impacts");
            var positions=new ParticleSystem.Particle[32];
            emitter.Simulate(.18f,false,false,true);feedback.TickPresentation(0);
            int count=emitter.GetParticles(positions);Assert.That(count,Is.EqualTo(9));
            Assert.That(positions.Take(count).Max(p=>Mathf.Abs(p.position.x-feedback.LastImpactCenter.x)),Is.GreaterThan(1));
            for(int i=0;i<count;i++){positions[i].position=new Vector3(positions[i].position.x,target.Position.y-.1f,0);positions[i].velocity=new Vector3(positions[i].velocity.x,-6,0);}
            emitter.SetParticles(positions,count);feedback.TickPresentation(0);count=emitter.GetParticles(positions);
            Assert.That(count,Is.EqualTo(9));
            Assert.That(positions.Take(count).Min(p=>p.position.y),Is.GreaterThanOrEqualTo(target.Position.y+.062f));
            Assert.That(positions.Take(count).Any(p=>p.velocity.y>0),Is.True);
            Object.Destroy(solid);
        }
        [UnityTest] public IEnumerator FinishingBlowShowsActualDamageRingAndPopThenCleansUp()
        {
            CombatTestScene.Open();yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var target=Target(arena);var feedback=arena.FeelFeedback;
            target.health=20;feedback.ResetFeedback();Assert.That(target.Receive(30,arena.Player,1,false,null),Is.True);feedback.TickPresentation(0);
            Assert.That(target.health,Is.Zero);Assert.That(feedback.LastDamageValue,Is.EqualTo(30));Assert.That(feedback.LastDamageWasHeavy,Is.True);
            Assert.That(feedback.ActiveDamageNumberCount,Is.EqualTo(1));Assert.That(feedback.ActiveDeathPopCount,Is.EqualTo(1));Assert.That(feedback.ActiveRingCount,Is.EqualTo(feedback.LastImpactVariant==1||feedback.LastImpactVariant==4?1:0));
            Assert.That(feedback.OwnsDeathVisual(target.visual),Is.True);Assert.That(target.visual.enabled,Is.False);
            feedback.TickPresentation(.5f);Assert.That(feedback.ActiveDeathPopCount,Is.Zero);Assert.That(feedback.ActiveRingCount,Is.Zero);
            feedback.TickPresentation(.3f);Assert.That(feedback.ActiveDamageNumberCount,Is.Zero);
            feedback.SetEffectsEnabled(false);Assert.That(feedback.OwnsDeathVisual(target.visual),Is.False);
        }
        [UnityTest] public IEnumerator SimultaneousHitsStackDamageNumbersOnSeparateLanesAndPopInTurn()
        {
            CombatTestScene.Open();yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var target=Target(arena);var feedback=arena.FeelFeedback;
            target.health=1000;feedback.ResetFeedback();
            for(int i=0;i<3;i++)Assert.That(target.Receive(5,arena.Player,1,false,null),Is.True);
            feedback.TickPresentation(0);
            Assert.That(feedback.ActiveDamageNumberCount,Is.EqualTo(3));
            Assert.That(new[]{feedback.DamageNumberLane(0),feedback.DamageNumberLane(1),feedback.DamageNumberLane(2)},Is.EqualTo(new[]{0,1,2}));
            Assert.That(feedback.DamageNumberAge(1),Is.EqualTo(-CombatFeelFeedback.DamageNumberStagger).Within(1e-4f));
            Assert.That(feedback.DamageNumberAge(2),Is.EqualTo(-2*CombatFeelFeedback.DamageNumberStagger).Within(1e-4f));
            Assert.That(feedback.VisibleDamageNumberCount,Is.EqualTo(1));
            feedback.TickPresentation(CombatFeelFeedback.DamageNumberStagger+.001f);Assert.That(feedback.VisibleDamageNumberCount,Is.EqualTo(2));
            feedback.TickPresentation(CombatFeelFeedback.DamageNumberStagger);Assert.That(feedback.VisibleDamageNumberCount,Is.EqualTo(3));
            feedback.TickPresentation(CombatFeelFeedback.DamageNumberStackWindow+.01f);
            target.Receive(5,arena.Player,1,false,null);feedback.TickPresentation(0);
            Assert.That(feedback.DamageNumberLane(3),Is.EqualTo(0),"older numbers outside the stack window free their lane");
            Assert.That(feedback.DamageNumberAge(3),Is.Zero,"a hit after the burst shows at once");
            feedback.TickPresentation(CombatFeelFeedback.DamageNumberSeconds);Assert.That(feedback.ActiveDamageNumberCount,Is.Zero);
        }
        [UnityTest] public IEnumerator PlayerDamageShowsRedEdgesWithoutTintingAttacksAgainstEnemies()
        {
            CombatTestScene.Open();yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var enemy=Target(arena);var feedback=arena.FeelFeedback;
            feedback.ResetFeedback();enemy.Receive(10,arena.Player,1,false,null);feedback.TickPresentation(0);
            Assert.That(feedback.PlayerHurtVignetteActive,Is.False);
            feedback.ResetFeedback();
            Assert.That(arena.Player.Receive(5,enemy,-1,false,null,bypass:true),Is.True);feedback.TickPresentation(0);
            Assert.That(feedback.ActiveImpactCount,Is.Zero);Assert.That(feedback.ActiveRingCount,Is.Zero);
            Assert.That(feedback.ActiveParticleCount,Is.Zero);Assert.That(feedback.HitStopped,Is.False);
            Assert.That(arena.Player.visual.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.name=="Combat feel contact flash"),Is.False);
            Assert.That(feedback.PlayerHurtVignetteActive,Is.True);Assert.That(arena.LastPlayerKnockDistance,Is.EqualTo(.9f));
            Assert.That(arena.Player.knockUntil-arena.Now,Is.EqualTo(.18f).Within(.001f));
            feedback.TickPresentation(.15f);Assert.That(feedback.PlayerHurtOpacity,Is.EqualTo(.5f).Within(.001f));
            feedback.TickPresentation(.75f);Assert.That(feedback.PlayerHurtVignetteActive,Is.False);
        }
        [UnityTest] public IEnumerator HeavyCleaveUsesItsRealAttackBoxAndShowsNumericHeavyDamage()
        {
            CombatTestScene.Open(requireArt:true);yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var target=Target(arena);var player=arena.Player;var feedback=arena.FeelFeedback;
            using(var simulation=arena.AcquireManualSimulation()) {
                player.Position=Vector2.zero;player.velocity=Vector2.zero;player.grounded=true;player.SyncFacing(1);
                player.Reconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="base_sword"),0);player.selected=0;
                target.Position=new Vector2(2,0);target.velocity=Vector2.zero;target.health=target.maxHealth=200;
                foreach(var other in arena.enemies)if(other!=target)other.Position=new Vector2(40,0);
                Physics2D.SyncTransforms();feedback.ResetFeedback();simulation.Step(.02f,new CombatInput(0,CombatButtons.Special));
                for(int i=0;i<30&&feedback.PresentedHits==0;i++){simulation.Step(.02f);feedback.TickPresentation(.02f);}
                Assert.That(feedback.PresentedHits,Is.GreaterThan(0));Assert.That(feedback.LastDamageWasHeavy,Is.True);
                Assert.That(feedback.LastDamageValue,Is.EqualTo(200-target.health).Within(.001f));Assert.That(feedback.ActiveCleaveCount,Is.EqualTo(1));
                var view=GameObject.Find("Heavy cleave blade ribbon").GetComponent<MeshFilter>();
                Assert.That(view.sharedMesh.vertexCount,Is.GreaterThan(12));
                Assert.That(feedback.LastCleaveBounds.size.x,Is.GreaterThan(2.5f));
                Assert.That(feedback.HitStopped,Is.False);
            }
        }
        [UnityTest] public IEnumerator WhipSkillWrapsItsRangeAndClearsWithoutChangingDamage()
        {
            CombatTestScene.Open(requireArt:true);yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var target=Target(arena);var player=arena.Player;var feedback=arena.FeelFeedback;
            using(var simulation=arena.AcquireManualSimulation()) {
                player.Position=Vector2.zero;player.velocity=Vector2.zero;player.grounded=true;player.SyncFacing(1);
                player.Reconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="whip"),0);player.selected=0;
                target.Position=new Vector2(2,0);target.velocity=Vector2.zero;target.health=target.maxHealth=200;
                foreach(var other in arena.enemies)if(other!=target)other.Position=new Vector2(40,0);
                Physics2D.SyncTransforms();feedback.ResetFeedback();simulation.Step(.02f,new CombatInput(0,CombatButtons.Special));
                for(int i=0;i<30&&feedback.PresentedHits==0;i++){simulation.Step(.02f);feedback.TickPresentation(.02f);}
                Assert.That(feedback.PresentedHits,Is.GreaterThan(0));Assert.That(feedback.LastDamageWasHeavy,Is.False);
                Assert.That(feedback.LastDamageValue,Is.EqualTo(200-target.health).Within(.001f));Assert.That(feedback.ActiveCleaveCount,Is.EqualTo(1));
                var view=GameObject.Find("Whip skill wrapping sweep").GetComponent<MeshFilter>();
                Assert.That(view.sharedMesh.vertexCount,Is.GreaterThan(12));
                Assert.That(feedback.LastCleaveBounds.size.x,Is.GreaterThan(2.5f));
                Assert.That(feedback.HitStopped,Is.False);
                Assert.That(feedback.LastDamageValue,Is.EqualTo(15));
                feedback.SetEffectsEnabled(false);Assert.That(feedback.ActiveCleaveCount,Is.Zero);
                Assert.That(view.GetComponent<MeshRenderer>().enabled,Is.False);
            }
        }
        [UnityTest] public IEnumerator StrongerPlayerKnockbackTravelsNormallyAndStillStopsAtSolidWalls()
        {
            CombatTestScene.Open();yield return new EnterPlayMode();yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var enemy=Target(arena);var player=arena.Player;
            using(var simulation=arena.AcquireManualSimulation()) {
                player.Position=new Vector2(0,10);player.velocity=Vector2.zero;player.SyncFacing(1);enemy.Position=new Vector2(2,10);Physics2D.SyncTransforms();
                player.Receive(5,enemy,-1,false,null,bypass:true);
                for(int i=0;i<12;i++)simulation.Step(.02f);
                Assert.That(player.Position.x,Is.LessThan(-.5f));
                player.ResetPlayer();player.Position=new Vector2(0,10);player.velocity=Vector2.zero;player.SyncFacing(1);
                var wall=new GameObject("Knockback solid wall");wall.transform.position=new Vector3(-.85f,10,0);
                wall.AddComponent<ArenaSurface>();var solid=wall.AddComponent<BoxCollider2D>();solid.size=new Vector2(.2f,8);Physics2D.SyncTransforms();
                player.Receive(5,enemy,-1,false,null,bypass:true);
                for(int i=0;i<12;i++)simulation.Step(.02f);
                Physics2D.SyncTransforms();
                Assert.That(player.Position.x,Is.LessThan(-.2f));Assert.That(player.bodyCollider.bounds.min.x,Is.GreaterThanOrEqualTo(solid.bounds.max.x-.01f));
                Object.Destroy(wall);
            }
        }
    }
}
