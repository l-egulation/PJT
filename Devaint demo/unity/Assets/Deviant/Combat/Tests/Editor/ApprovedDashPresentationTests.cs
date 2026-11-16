using System;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class ApprovedDashPresentationTests
    {
        static ApprovedDashPresentation.Recipe Recipe()=>JsonUtility.FromJson<ApprovedDashPresentation.Recipe>(
            Resources.Load<TextAsset>("CharacterV2/ApprovedDashPresentation").text);
        [TestCase(.019f,0)] [TestCase(.02f,1)] [TestCase(.089f,1)]
        [TestCase(.09f,2)] [TestCase(.199f,2)] [TestCase(.2f,3)]
        [TestCase(.246f,4)] [TestCase(.301f,-1)]
        public void ApprovedPoseHoldsUntilMovementEndsThenRecovers(float age,int expected)
            =>Assert.That(ApprovedDashPresentation.PoseIndex(Recipe(),age,0,.2f),Is.EqualTo(expected));
        [Test] public void PoseAdaptsToGameplayDurationWithoutChangingDeadlines()
        {
            Assert.That(ApprovedDashPresentation.PoseIndex(Recipe(),.29f,0,.3f),Is.EqualTo(2));
            Assert.That(ApprovedDashPresentation.PoseIndex(Recipe(),.32f,0,.3f),Is.EqualTo(3));
            Assert.That(ApprovedDashPresentation.PoseIndex(Recipe(),.401f,0,.3f),Is.EqualTo(-1));
        }
        static ActorReadSnapshot Actor(float now,bool grounded=true,bool wall=false,float end=.2f)
            =>new ActorReadSnapshot(default,0,now,true,new ActorKinematicsSnapshot(0,0,15,0,1,grounded,wall),
                new ActorVitalsSnapshot(100,100,0,0,0,0),default,default,new PlayerDashSnapshot(0,end,1,1,0,0),default,Guid.Empty,"base_sword",null,default,default);
        [Test] public void AfterimageStaysAtActualWorldPoseAndResetOrWallClearsIt()
        {
            var texture=new Texture2D(8,8);var sprite=Sprite.Create(texture,new Rect(0,0,8,8),new Vector2(.5f,0),32);
            var node=new GameObject("Dash test",typeof(SpriteRenderer));var renderer=node.GetComponent<SpriteRenderer>();renderer.sprite=sprite;
            var set=new CharacterAnimationSet{form="base_sword",clips=new[]{new CharacterClip{name="dash",sprites=new[]{sprite,sprite,sprite,sprite,sprite}}}};
            using(var art=new ApprovedDashPresentation())
            {
                try
                {
                    var actor=Actor(.1f);art.Pose(in actor,set,"dash");art.Draw(in actor,renderer);
                    Assert.That(art.ActiveAfterimages,Is.EqualTo(1));
                    var echo=GameObject.Find("Approved dash afterimage");var position=echo.transform.position;
                    node.transform.position=Vector3.right*10;
                    Assert.That(echo.transform.position,Is.EqualTo(position));
                    actor=Actor(.11f,true,true);Assert.That(art.Pose(in actor,set,"dash"),Is.Null);
                    Assert.That(art.ActiveAfterimages,Is.Zero);
                    art.Reset();Assert.That(art.ActiveAfterimages,Is.Zero);
                    actor=Actor(.05f);art.Pose(in actor,set,"dash");art.Draw(in actor,renderer);
                    Assert.That(art.ActiveAfterimages,Is.EqualTo(1));
                    actor=Actor(.1f,true,false,.1f);Assert.That(art.Pose(in actor,set,"idle"),Is.Null);
                    Assert.That(art.ActiveAfterimages,Is.Zero,"Shortened gameplay deadline cancels every echo and recovery.");
                }
                finally { UnityEngine.Object.DestroyImmediate(node);UnityEngine.Object.DestroyImmediate(sprite);UnityEngine.Object.DestroyImmediate(texture); }
            }
        }
        [Test] public void SlowingDashDoesNotStackEchoesWithinTwoNativePixels()
        {
            using(var fixture=new DashDrawingFixture())
            using(var art=new ApprovedDashPresentation())
            {
                var actor=Actor(.16f,false);art.Pose(in actor,fixture.set,"dash");
                fixture.renderer.transform.position=Vector3.right*4.457333f;
                art.Draw(in actor,fixture.renderer);
                actor=Actor(.18f,false);art.Pose(in actor,fixture.set,"dash");
                fixture.renderer.transform.position=Vector3.right*4.494667f;
                art.Draw(in actor,fixture.renderer);
                actor=Actor(.19f,false);art.Pose(in actor,fixture.set,"dash");
                fixture.renderer.transform.position=Vector3.right*4.499333f;
                art.Draw(in actor,fixture.renderer);
                Assert.That(art.ActiveAfterimages,Is.EqualTo(1),"Deceleration must not pile body copies onto the final pose.");
                actor=Actor(.195f,false);art.Pose(in actor,fixture.set,"dash");
                fixture.renderer.transform.position+=Vector3.right*.1f;
                art.Draw(in actor,fixture.renderer);
                Assert.That(art.ActiveAfterimages,Is.EqualTo(2),"Actual travel must still leave a trail.");
            }
        }
        [TestCase(true)] [TestCase(false)]
        public void DashEchoesFinishFadingByTheApprovedRecoveryDeadline(bool grounded)
        {
            using(var fixture=new DashDrawingFixture())
            using(var art=new ApprovedDashPresentation())
            {
                var actor=Actor(.18f,grounded);art.Pose(in actor,fixture.set,"dash");
                art.Draw(in actor,fixture.renderer);
                var echo=GameObject.Find("Approved dash afterimage").GetComponent<SpriteRenderer>();
                actor=Actor(.25f,grounded);art.Pose(in actor,fixture.set,grounded?"idle":"fall_start");
                art.Draw(in actor,fixture.renderer);
                float age=.25f-.18f;
                var recipe=Recipe();
                float normalFade=recipe.afterimageMaximumOpacity*Mathf.Pow(1-age/(recipe.afterimageLifeMs/1000),recipe.afterimageFadeExponent);
                Assert.That(echo.color.a,Is.LessThanOrEqualTo(normalFade*.51f),"A stopped dash must release its tail instead of holding a full body echo over the fall.");
                actor=Actor(.301f,grounded);art.Pose(in actor,fixture.set,grounded?"idle":"fall_loop");
                art.Draw(in actor,fixture.renderer);
                Assert.That(art.ActiveAfterimages,Is.Zero,"No dash body may hang at the old height after recovery ends.");
            }
        }
        [Test] public void GroundDashLeavingALedgeSwitchesToItsAirbornePose()
        {
            using(var fixture=new DashDrawingFixture())
            using(var art=new ApprovedDashPresentation())
            {
                var actor=Actor(.02f);Assert.That(art.Pose(in actor,fixture.set,"dash"),Is.SameAs(fixture.crouch));
                art.Draw(in actor,fixture.renderer);
                actor=Actor(.08f,false);
                Assert.That(art.Pose(in actor,fixture.set,"dash"),Is.SameAs(fixture.airborne));
                Assert.That(art.ActiveAfterimages,Is.Zero,"Ground-crouch echoes must not overlap the airborne silhouette.");
                actor=Actor(.21f,false);Assert.That(art.Pose(in actor,fixture.set,"jump_apex"),Is.Null);
            }
        }
        sealed class DashDrawingFixture:IDisposable
        {
            readonly Texture2D texture=new Texture2D(8,8);
            public readonly Sprite crouch,airborne;
            public readonly SpriteRenderer renderer;
            public readonly CharacterAnimationSet set;
            public DashDrawingFixture()
            {
                crouch=Sprite.Create(texture,new Rect(0,0,8,4),Vector2.zero,32);
                airborne=Sprite.Create(texture,new Rect(0,0,8,8),Vector2.zero,32);
                renderer=new GameObject("Dash transition test",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                renderer.sprite=airborne;
                set=new CharacterAnimationSet{form="base_sword",clips=new[]{
                    new CharacterClip{name="dash",sprites=new[]{crouch,crouch,crouch,crouch,crouch}},
                    new CharacterClip{name="jump_apex",sprites=new[]{airborne},seconds=new[]{.07f}}}};
            }
            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                UnityEngine.Object.DestroyImmediate(crouch);UnityEngine.Object.DestroyImmediate(airborne);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        [Test] public void AirborneDashNeverForcesGroundRecovery()
        {
            using(var art=new ApprovedDashPresentation())
            {
                var set=new CharacterAnimationSet{form="base_sword",clips=new[]{new CharacterClip{name="dash",sprites=new Sprite[5]}}};
                var actor=Actor(.1f,false);art.Pose(in actor,set,"dash");
                actor=Actor(.21f,false);Assert.That(art.Pose(in actor,set,"fall_loop"),Is.Null);
            }
        }
        [Test] public void AirDashUsesAirborneSilhouetteAndGroundDashKeepsItsCrouch()
        {
            var texture=new Texture2D(8,8);
            var crouch=Sprite.Create(texture,new Rect(0,0,8,4),Vector2.zero,32);
            var airborne=Sprite.Create(texture,new Rect(0,0,8,8),Vector2.zero,32);
            try
            {
                var set=new CharacterAnimationSet{form="base_sword",clips=new[]{
                    new CharacterClip{name="dash",sprites=new[]{crouch,crouch,crouch,crouch,crouch}},
                    new CharacterClip{name="jump_apex",sprites=new[]{airborne},seconds=new[]{.07f}}}};
                using(var art=new ApprovedDashPresentation())
                {
                    var actor=Actor(.05f,false);
                    Assert.That(art.Pose(in actor,set,"dash"),Is.SameAs(airborne));
                    actor=Actor(.15f,false);
                    Assert.That(art.Pose(in actor,set,"dash"),Is.SameAs(airborne));
                    actor=Actor(.21f,true);
                    Assert.That(art.Pose(in actor,set,"idle"),Is.Null,"Landing must not force an aerial recovery pose");
                    art.Reset();actor=Actor(.05f);
                    Assert.That(art.Pose(in actor,set,"dash"),Is.SameAs(crouch));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(airborne);UnityEngine.Object.DestroyImmediate(crouch);UnityEngine.Object.DestroyImmediate(texture); }
        }
        // The village and opening bodies draw a 12-frame dash; their dashPoses pick the five approved poses, so they dash
        // and air-dash like the weapons instead of flicking through 12 frames (user 2026-11-09: the village dash smeared).
        [Test] public void LongerBodyDashPlaysItsNamedFivePosesAndAirDashHoldsTheApex()
        {
            var texture=new Texture2D(8,8);
            var frames=new Sprite[12];for(int i=0;i<12;i++)frames[i]=Sprite.Create(texture,new Rect(0,0,8,8),Vector2.zero,32);
            var apex=Sprite.Create(texture,new Rect(0,0,8,8),Vector2.zero,32);
            try
            {
                var set=new CharacterAnimationSet{form="village_unarmed",dashPoses=new[]{1,3,5,9,11},clips=new[]{
                    new CharacterClip{name="dash",sprites=frames},
                    new CharacterClip{name="jump_apex",sprites=new[]{apex},seconds=new[]{.07f}}}};
                using(var art=new ApprovedDashPresentation())
                {
                    var actor=Actor(.01f);Assert.That(art.Pose(in actor,set,"dash"),Is.SameAs(frames[1]));
                    actor=Actor(.05f);Assert.That(art.Pose(in actor,set,"dash"),Is.SameAs(frames[3]));
                    actor=Actor(.15f);Assert.That(art.Pose(in actor,set,"dash"),Is.SameAs(frames[5]));
                    art.Reset();actor=Actor(.05f,false);
                    Assert.That(art.Pose(in actor,set,"dash"),Is.SameAs(apex),"an air dash holds the body's apex pose");
                }
                set.dashPoses=new[]{1,3,5,9,12};
                using(var art=new ApprovedDashPresentation())
                {
                    var actor=Actor(.05f);Assert.That(art.Pose(in actor,set,"dash"),Is.Null,"a pose outside the clip leaves the clip's own frames");
                }
            }
            finally { foreach(var frame in frames)UnityEngine.Object.DestroyImmediate(frame);UnityEngine.Object.DestroyImmediate(apex);UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
