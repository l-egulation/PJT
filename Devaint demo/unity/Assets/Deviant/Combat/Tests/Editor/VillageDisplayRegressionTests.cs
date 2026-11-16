using Deviant.Game.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using System.Collections;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class VillageDisplayRegressionTests
    {
        [UnityTest] public IEnumerator DestroyingLiveActorDoesNotReparentIntoDyingRoot()
        {
            yield return new EnterPlayMode();
            var node=new GameObject("Runtime interpolation shutdown regression");
            var actor=node.AddComponent<CombatActor>();
            var visual=new GameObject("Sprite");visual.transform.SetParent(node.transform,false);
            actor.visual=visual.AddComponent<SpriteRenderer>();
            var helper=node.AddComponent<ActorRenderInterpolation>();helper.Bind(actor);
            yield return null;
            Object.Destroy(node);yield return null;
            Assert.That(node==null,Is.True);
            yield return new ExitPlayMode();
        }
        [TestCase(0,0,1920,1080,1080,.5f,.5f,960,540)]
        [TestCase(160,90,960,540,720,.5f,.5f,640,360)]
        [TestCase(160,90,960,540,720,0,1,160,630)]
        public void WorldHudUsesActualOutputRectangle(float x,float y,float w,float h,int screenHeight,float vx,float vy,float expectedX,float expectedY)
        {
            var point=CombatDiagnostics.ProjectViewportPoint(new Rect(x,y,w,h),new Vector3(vx,vy,10),screenHeight);
            Assert.That(point.x,Is.EqualTo(expectedX).Within(.001f));Assert.That(point.y,Is.EqualTo(expectedY).Within(.001f));
            Assert.That(point.z,Is.EqualTo(10));
        }
        [Test] public void HealthAnchorUsesOpaqueTopInsteadOfTransparentSpriteCanvas()
        {
            var node=new GameObject("Health anchor test");var hudNode=new GameObject("HUD test");
            var texture=new Texture2D(16,16);var pixels=new Color32[256];pixels[10*16+8]=Color.white;
            texture.SetPixels32(pixels);texture.Apply();
            var sprite=Sprite.Create(texture,new Rect(0,0,16,16),new Vector2(.5f,.5f),8);
            try
            {
                var actor=node.AddComponent<CombatActor>();actor.Position=new Vector2(2,4);
                var visual=new GameObject("Sprite");visual.transform.SetParent(node.transform,false);
                actor.visual=visual.AddComponent<SpriteRenderer>();actor.visual.sprite=sprite;
                var hud=hudNode.AddComponent<CombatDiagnostics>();
                Assert.That(hud.EnemyHealthAnchor(actor).y,Is.EqualTo(4+.375f+.3f).Within(.001f));
                actor.Position+=Vector2.up;
                Assert.That(hud.EnemyHealthAnchor(actor).y,Is.EqualTo(5+.375f+.3f).Within(.001f));
            }
            finally { Object.DestroyImmediate(hudNode);Object.DestroyImmediate(node);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture); }
        }
        [Test] public void RenderPoseSmoothsFixedSamplesAndSnapsTeleportsOrClockResets()
        {
            var history=new ActorRenderPoseHistory();history.Push(Vector2.zero,1);history.Push(Vector2.up,1.02);
            Assert.That(history.Sample(1.025f,.02f).y,Is.EqualTo(.25f).Within(.001f));
            Assert.That(history.Sample(1.035f,.02f).y,Is.EqualTo(.75f).Within(.001f));
            history.Push(Vector2.up*100,1.04);
            Assert.That(history.Sample(1.045,.02f).y,Is.EqualTo(100));
            history.Push(Vector2.down,0);
            Assert.That(history.Sample(.005,.02f).y,Is.EqualTo(-1));
        }
        [Test] public void InterpolationParentNeverMovesLogicalActorOrSpriteLocalPivot()
        {
            var node=new GameObject("Logical actor");
            try
            {
                var actor=node.AddComponent<CombatActor>();actor.Position=new Vector2(3,5);
                var visual=new GameObject("Native sprite");visual.transform.SetParent(node.transform,false);
                actor.visual=visual.AddComponent<SpriteRenderer>();var collider=node.AddComponent<BoxCollider2D>();
                var helper=node.AddComponent<ActorRenderInterpolation>();helper.Bind(actor);
                Assert.That(actor.Position,Is.EqualTo(new Vector2(3,5)));
                Assert.That(node.transform.position,Is.EqualTo(new Vector3(3,5,0)));
                Assert.That(collider.transform,Is.EqualTo(node.transform));
                Assert.That(visual.transform.localPosition,Is.EqualTo(Vector3.zero));
                actor.Position=Vector2.one*100;
                Assert.That(helper.RenderPosition(),Is.EqualTo(actor.Position),"Warp before the next fixed tick snaps immediately.");
                helper.Unbind();Object.DestroyImmediate(helper);
                Assert.That(visual.transform.parent,Is.SameAs(node.transform));
                Assert.That(visual.transform.localPosition,Is.EqualTo(Vector3.zero));
            }
            finally { Object.DestroyImmediate(node); }
        }
    }
}
