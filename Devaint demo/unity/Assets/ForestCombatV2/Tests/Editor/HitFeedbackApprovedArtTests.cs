using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    [Category("RequiresArt")]
    public sealed class HitFeedbackApprovedArtTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();Time.timeScale=1;}
        [UnityTest]public IEnumerator SixApprovedBodiesUseActualHitsDeathClipsAndWalkThroughDebrisFacingRight()=>Exercise(1);
        [UnityTest]public IEnumerator SixApprovedBodiesUseActualHitsDeathClipsAndWalkThroughDebrisFacingLeft()=>Exercise(-1);
        static IEnumerator Exercise(int facing)
        {
            LegacySceneFixture.Open("Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();Assert.That(world.Ready,Is.True);
            var arena=world.arena;var bridge=arena.GetComponent<HitFeedbackBridge>();Assert.That(bridge,Is.Not.Null);Assert.That(bridge.Ready,Is.True,bridge.LastError);
            var presentations=Object.FindObjectsByType<Chapter1MonsterPresentation>(FindObjectsSortMode.None).GroupBy(p=>p.species).Select(g=>g.First()).OrderBy(p=>p.species).ToArray();
            Assert.That(presentations.Length,Is.EqualTo(6));
            var catalog=Resources.Load<DebrisCatalog>("HitFeedback/Generated/Debris");Assert.That(catalog,Is.Not.Null);
            var floor=new GameObject("Approved-art debris fixture floor");floor.transform.position=new Vector3(-500,-.5f,0);floor.AddComponent<ArenaSurface>();
            var collider=floor.AddComponent<BoxCollider2D>();collider.size=new Vector2(60,1);arena.RegisterFeedbackSurface(collider);
            using(var driver=arena.AcquireManualSimulation())
            foreach(var presentation in presentations)
            {
                driver.Reset();foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(2000,0);}
                var actor=presentation.actor;actor.health=actor.maxHealth=100;actor.Position=new Vector2(-500,.003f);actor.SyncFacing(-facing);
                var player=arena.Player;player.UseTestInput=true;player.selected=0;
                Assert.That(player.TryReconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="base_sword"),0,out var error),Is.True,error);
                player.Position=actor.Position-Vector2.right*facing*1.8f;player.SyncFacing(facing);Physics2D.SyncTransforms();
                presentation.Draw(0);var idle=presentation.catalog.Find(presentation.species,"idle");var death=presentation.catalog.Find(presentation.species,"death");
                Assert.That(idle.frames[0],Is.Not.Null);Assert.That(death.frames.All(s=>s),Is.True);
                var bake=catalog.Find(idle.frames[0]);Assert.That(bake,Is.Not.Null,presentation.species+" needs its own approved body bake.");Assert.That(bake.IsCurrentSource,Is.True);
                float area=bake.pieces.Sum(p=>p.area);Assert.That(area,Is.GreaterThan(0));
                int before=actor.ReceivedHitCount;
                driver.Step(.01f,new CombatInput(0,CombatButtons.Basic));
                for(int i=0;i<100&&actor.ReceivedHitCount==before;i++)Step(driver,arena,presentation,bridge);
                Assert.That(actor.ReceivedHitCount,Is.EqualTo(before+1),presentation.species+" must be hit by the real sword action.");
                for(int i=0;i<4;i++)Step(driver,arena,presentation,bridge);
                Assert.That(bridge.StartedHits,Is.EqualTo(1));Assert.That(bridge.LostHitFacts,Is.Zero);Assert.That(bridge.LastError,Is.Null);
                var output=arena.transform.Find("Combat hit feedback output");Assert.That(output,Is.Not.Null);
                Assert.That(output.GetComponentsInChildren<ParticleSystem>(true).Sum(p=>p.particleCount),Is.GreaterThan(0));
                var legacy=actor.GetComponentsInChildren<ParticleSystem>(true);
                int nativeCount=legacy.Sum(p=>p.particleCount);
                int expected=presentation.SwordFeedback.Emissions>0?BasicSwordHitFeedback.ParticleSettings.count:18;
                Assert.That(nativeCount,Is.EqualTo(expected),"Native contact fragments and the prefab hit effect must both survive the merge.");
                var overlay=actor.visual.transform.Find("Accepted-hit white overlay").GetComponent<SpriteRenderer>();
                Assert.That(overlay.enabled,Is.True);Assert.That(overlay.sprite,Is.SameAs(actor.visual.sprite));
                Capture(arena.ViewCamera,actor.Center,presentation.species+"-"+facing+"-hit");
                long started=bridge.StartedHits;bridge.Sample();bridge.Sample();Assert.That(bridge.StartedHits,Is.EqualTo(started));
                Assert.That(legacy.Sum(p=>p.particleCount),Is.EqualTo(nativeCount),"Repeated sampling must not emit either layer twice.");
                player.CancelAction();actor.health=1;actor.shield=0;actor.guardUntil=0;
                driver.Step(.01f,new CombatInput(0,CombatButtons.Basic));
                for(int i=0;i<150&&!actor.Dead;i++)Step(driver,arena,presentation,bridge);
                Assert.That(actor.Dead,Is.True);presentation.Draw(0);bridge.Sample();
                Assert.That(presentation.State,Is.EqualTo("death"));Assert.That(bridge.ActiveDebris,Is.Zero,"A lethal hit must wait for the actual approved death clip.");
                player.CancelAction();
                for(int i=0;i<250&&actor.visual.enabled;i++)Step(driver,arena,presentation,bridge);
                Assert.That(actor.visual.enabled,Is.False,"The actual clip must complete.");
                Step(driver,arena,presentation,bridge);Assert.That(bridge.ActiveDebris,Is.EqualTo(bake.pieces.Length),bridge.LastError);
                for(int i=0;i<100;i++)Step(driver,arena,presentation,bridge);
                var meshes=output.GetComponentsInChildren<MeshRenderer>().Where(r=>r.gameObject.activeInHierarchy).ToArray();
                Assert.That(meshes.Length,Is.EqualTo(bake.pieces.Length));
                foreach(var mesh in meshes)
                {
                    Assert.That(mesh.GetComponent<Collider2D>(),Is.Null);Assert.That(mesh.GetComponent<Rigidbody2D>(),Is.Null);
                    var vertices=mesh.GetComponent<MeshFilter>().sharedMesh.vertices;
                    Assert.That(vertices.Min(v=>mesh.transform.TransformPoint(v).y),Is.GreaterThan(-.06f),"Actual mesh pixels, not a rotated bounding-box envelope, must stay above the copied floor.");
                }
                Capture(arena.ViewCamera,new Vector2(-500,1),presentation.species+"-"+facing+"-debris");
                player.Position=new Vector2(meshes.Min(r=>r.bounds.min.x)-1, .003f);player.SyncFacing(1);Physics2D.SyncTransforms();
                long kicks=bridge.DebrisKicks;float hp=player.health;
                for(int i=0;i<100;i++)Step(driver,arena,presentation,bridge,new CombatInput(1));
                Assert.That(bridge.DebrisKicks,Is.GreaterThan(kicks),"Actual continuous foot motion must wake/kick the decorative fragments.");
                Assert.That(player.health,Is.EqualTo(hp));Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1),"Ordinary approved monsters do not infer elite slow motion.");
                driver.Reset();Assert.That(bridge.ActiveDebris,Is.Zero);Assert.That(bridge.ActiveHits,Is.Zero);
            }
            Object.Destroy(floor);
        }
        static void Step(CombatArena.ManualSimulation driver,CombatArena arena,Chapter1MonsterPresentation presentation,HitFeedbackBridge bridge,CombatInput input=default)
        {driver.Step(.01f,input);arena.Presentation.Draw(0);presentation.Draw(0);bridge.Sample();}
        static void Capture(Camera camera,Vector2 center,string name)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;var position=camera.transform.position;float size=camera.orthographicSize;
            var texture=RenderTexture.GetTemporary(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);
            try
            {
                camera.transform.position=new Vector3(center.x,center.y+.4f,-10);camera.orthographicSize=2.6f;camera.targetTexture=texture;camera.Render();
                RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
                string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../approved-art-captures"));Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
            }
            finally{camera.targetTexture=previous;camera.transform.position=position;camera.orthographicSize=size;RenderTexture.active=active;RenderTexture.ReleaseTemporary(texture);Object.Destroy(image);}
        }
    }
}
