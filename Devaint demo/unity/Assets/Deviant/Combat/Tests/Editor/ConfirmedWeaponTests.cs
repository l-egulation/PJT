using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    [Category("RequiresArt")]
    public class ConfirmedWeaponTests
    {
        CombatArena arena;
        CombatPlayer P { get { return arena.Player; } }
        CharacterAnimationSet Set(string form) { return System.Array.Find(arena.characterAnimations.sets,s=>s.form==form); }
        [UnitySetUp] public IEnumerator Setup()
        {
            CombatTestScene.Open(requireArt: true);
            if(TestContext.CurrentContext.Test.Name.StartsWith("MeadowSword"))
                LegacySceneFixture.Open("Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity");
            yield return new EnterPlayMode(); yield return null;
            arena=Object.FindFirstObjectByType<CombatArena>(); P.UseTestInput=true; yield return new WaitForFixedUpdate();
        }
        [UnityTearDown] public IEnumerator Teardown() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }
        IEnumerator Advance(float seconds)
        {
            float end=arena.Now+seconds,timeout=Time.realtimeSinceStartup+20;
            while(arena.Now<end) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout)); yield return new WaitForFixedUpdate(); }
        }
        void Equip(string id) { P.Reconfigure(0,System.Array.FindIndex(arena.catalog.forms,f=>f.id==id),0); }
        [UnityTest] public IEnumerator LabSwordQueuedAndLateInputsDisplayDistinctApprovedHits() => SwordQueuedAndLateInputsDisplayDistinctApprovedHits();
        [UnityTest] public IEnumerator MeadowSwordQueuedAndLateInputsDisplayDistinctApprovedHits() => SwordQueuedAndLateInputsDisplayDistinctApprovedHits();
        IEnumerator SwordQueuedAndLateInputsDisplayDistinctApprovedHits()
        {
            using (var driver=arena.AcquireManualSimulation())
            {
                foreach(bool queued in new[]{true,false})
                {
                    driver.Reset(); Equip("base_sword");
                    foreach(var enemy in arena.enemies) { enemy.aggressive=false; enemy.Position=new Vector2(35,0); }
                    driver.Step(.02f,new Deviant.Combat.Contracts.CombatInput(0,Deviant.Combat.Contracts.CombatButtons.Basic));
                    var first=P.currentCast; Assert.That(first.Definition.Id,Is.EqualTo("base_sword_basic_0"));
                    while(first.Age < first.MotionDuration-.025f)
                    {
                        driver.Step(.01f,queued ? new Deviant.Combat.Contracts.CombatInput(0,Deviant.Combat.Contracts.CombatButtons.Basic) : default);
                        arena.Presentation.Draw(0);
                    }
                    Assert.That(P.visual.sprite,Is.SameAs(Resources.Load<Sprite>("CharacterApproved11/base_sword/F104")),"The first hit must end in its approved recovery pose.");
                    for(int i=0;i<100 && P.currentCast==first;i++) driver.Step(.02f);
                    if(!queued) driver.Step(.02f,new Deviant.Combat.Contracts.CombatInput(0,Deviant.Combat.Contracts.CombatButtons.Basic));
                    var second=P.currentCast; Assert.That(second,Is.Not.Null);
                    Assert.That(second.Definition.Id,Is.EqualTo("base_sword_basic_1"),"queued="+queued);
                    arena.Presentation.Draw(0);
                    Assert.That(P.visual.sprite,Is.SameAs(Resources.Load<Sprite>("CharacterApproved11/base_sword/F105")),"The second hit continues with its approved horizontal ready pose.");
                    while(second.Age<second.ImpactTime+.02f) driver.Step(.01f);
                    arena.Presentation.Draw(0);
                    Assert.That(arena.Presentation.LastAttackMotionError,Is.Null);
                    Assert.That(arena.Presentation.CurrentClip,Is.EqualTo("attack2"));
                    Assert.That(P.visual.sprite,Is.SameAs(Resources.Load<Sprite>("CharacterApproved11/base_sword/F107")),"The second hit shows its rising cut at contact.");
                    for(int hit=2;hit<6;hit++)
                    {
                        var previous=P.currentCast;
                        for(int i=0;i<100 && P.currentCast==previous;i++)
                            driver.Step(.01f,new Deviant.Combat.Contracts.CombatInput(0,Deviant.Combat.Contracts.CombatButtons.Basic));
                        Assert.That(P.currentCast,Is.Not.Null);
                        Assert.That(P.currentCast.Definition.Id,Is.EqualTo("base_sword_basic_"+(hit%2)),"Repeated input hit "+(hit+1));
                    }
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator SwordFirstCueEnclosesCharacterOnBothFacings()
        {
            arena.FeelFeedback.SetEffectsEnabled(false);
            using(var driver=arena.AcquireManualSimulation())
            foreach(int facing in new[]{-1,1})
            {
                driver.Reset(); Equip("base_sword"); P.SyncFacing(facing);
                foreach(var enemy in arena.enemies) { enemy.aggressive=false; enemy.Position=new Vector2(35,0); }
                driver.Step(.01f,new Deviant.Combat.Contracts.CombatInput(0,Deviant.Combat.Contracts.CombatButtons.Basic));
                var cast=P.currentCast;
                while(cast.Age < cast.ImpactTime+.015f) { driver.Step(.005f); arena.Presentation.Draw(0); }
                var output=GameObject.Find("BasicAttackEffect_base_sword_attack1"); Assert.That(output,Is.Not.Null);
                var renderer=output.GetComponent<SpriteRenderer>();
                Assert.That(output.transform.localScale,Is.EqualTo(new Vector3(facing*1.5f,1.5f,1)));
                Assert.That(renderer.bounds.min.y,Is.LessThanOrEqualTo(P.Position.y+.1f));
                Assert.That(renderer.bounds.max.y,Is.GreaterThan(P.Position.y+2));
                Assert.That(renderer.bounds.min.x,Is.LessThan(P.Position.x-.7f));
                Assert.That(renderer.bounds.max.x,Is.GreaterThan(P.Position.x+.7f));
                Assert.That(arena.Presentation.LastEffectError,Is.Null);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator FourSetsLoadAllModulesEffectsAndNativeFootPivots()
        {
            string[] forms={"base_sword","whip","rapier","dagger"}; int[] counts={2,2,3,4};
            Vector2[] pivots={new Vector2(80,23),new Vector2(66,16),new Vector2(64,12),new Vector2(80,23)};
            for(int i=0;i<forms.Length;i++)
            {
                var set=Set(forms[i]); Assert.That(set,Is.Not.Null);
                Assert.That(set.effects.Length,Is.EqualTo(counts[i])); Assert.That(set.Find("attack"+counts[i]),Is.Not.Null);
                Assert.That(Vector2.Distance(set.Find("idle").sprites[0].pivot,pivots[i]),Is.LessThan(.001f));
                foreach(var clip in set.clips) foreach(var sprite in clip.sprites) { Assert.That(sprite,Is.Not.Null); Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32)); }
                foreach(var effect in set.effects)
                    foreach(var sprite in effect.sprites) { Assert.That(sprite,Is.Not.Null); Assert.That(sprite.pivot.x,Is.EqualTo(0)); Assert.That(sprite.pivot.y,Is.EqualTo(sprite.rect.height)); }
            }
            Assert.That(Set("base_sword").Effect("attack1").flipX,Is.False,"team sword first-hit reversal fix must be preserved");
            yield return null;
        }
        [UnityTest] public IEnumerator VisualContactMatchesGameplayImpactForEveryBasicAttack()
        {
            foreach(var set in arena.characterAnimations.sets)
            {
                var form=System.Array.Find(arena.catalog.forms,f=>f.id==set.gameplayForm);
                Assert.That(form,Is.Not.Null,set.form);
                if(set.launcherMode>=0)
                {
                    var b=form.basics[0];var single=set.Find("attack_single");
                    Assert.That(single.SourceTime(b.impact,b.impact,b.duration),Is.EqualTo(single.sourceImpact).Within(.00001f));
                    Assert.That(single.SourceTime(b.duration,b.impact,b.duration),Is.EqualTo(single.Duration).Within(.00001f));
                    continue;
                }
                for(int i=0;i<form.basics.Length;i++)
                {
                    var b=form.basics[i]; var clip=set.Find("attack"+(i+1));
                    Assert.That(clip.SourceTime(b.impact,b.impact,b.duration),Is.EqualTo(clip.sourceImpact).Within(.00001f));
                    Assert.That(clip.SourceTime(b.duration,b.impact,b.duration),Is.EqualTo(clip.Duration).Within(.00001f));
                }
            }
            yield return null;
        }
        IEnumerator QueueFourHits()
        {
            P.QueueTestInput(basic:true); yield return Advance(.06f);
            P.QueueTestInput(basic:true); yield return Advance(.15f);
            P.QueueTestInput(basic:true); yield return Advance(.16f);
            P.QueueTestInput(basic:true);
        }
        [UnityTest] public IEnumerator DaggerFourHitsDealFiveFiveFiveNineAndShowFourthPose()
        {
            Equip("dagger"); arena.enemies[0].Position=P.Position+Vector2.right; Physics2D.SyncTransforms();
            yield return QueueFourHits(); yield return Advance(.35f);
            Assert.That(P.BasicIndex,Is.EqualTo(3)); arena.Presentation.Draw(0);
            Assert.That(arena.Presentation.CurrentClip,Is.EqualTo("attack4"));
            CollectionAssert.Contains(Set("dagger").Find("attack4").sprites,P.visual.sprite);
            yield return Advance(.4f); Assert.That(arena.enemies[0].health,Is.EqualTo(76));
        }
        [UnityTest] public IEnumerator MovingDaggerComboHasFourShortStepsTotalTwoPointSix()
        {
            Equip("dagger"); P.axis=1; float start=P.Position.x;
            yield return QueueFourHits();
            float timeout=arena.Now+2;
            while(P.currentCast) { Assert.That(arena.Now,Is.LessThan(timeout)); yield return new WaitForFixedUpdate(); }
            Assert.That(P.Position.x-start,Is.EqualTo(2.6f).Within(.03f));
        }
        [UnityTest] public IEnumerator AttackEffectMirrorsAndResetClearsPresentationWithoutDamage()
        {
            arena.FeelFeedback.SetEffectsEnabled(false);
            foreach(int direction in new[]{1,-1})
            {
                arena.ResetTest(); P.SyncFacing(direction); P.QueueTestInput(basic:true); yield return Advance(.18f); arena.Presentation.Draw(0);
                var effect=GameObject.Find("BasicAttackEffect_base_sword_attack1"); Assert.That(effect,Is.Not.Null);
                Assert.That(effect.transform.localScale.x,Is.EqualTo(direction*1.5f));
                Assert.That(effect.GetComponent<Collider2D>(),Is.Null);
                Assert.That(effect.GetComponent<Rigidbody2D>(),Is.Null);
                Assert.That(arena.enemies[0].health,Is.EqualTo(100));
                arena.ResetTest(); Assert.That(arena.Presentation.ActiveEffects,Is.EqualTo(0));
                yield return null;
            }
        }
        [UnityTest] public IEnumerator WeaponChangeSelectsItsOwnIdleAndPhysicsStillOwnsJump()
        {
            Equip("rapier"); arena.Presentation.Draw(0);
            Assert.That(arena.Presentation.CurrentForm,Is.EqualTo("rapier"));
            Assert.That(P.visual.sprite,Is.EqualTo(Set("rapier").Find("idle").sprites[0]));
            Vector2 size=P.hurtCollider.size; P.QueueTestInput(jump:true); yield return Advance(.12f); arena.Presentation.Draw(0);
            Assert.That(arena.Presentation.CurrentClip,Is.EqualTo("jump_rise")); Assert.That(P.Position.y,Is.GreaterThan(0)); Assert.That(P.hurtCollider.size,Is.EqualTo(size));
        }
    }
}
