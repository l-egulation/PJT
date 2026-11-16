using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    [Category("RequiresArt")]
    public sealed class HitFeedbackForestArtTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();Time.timeScale=1;}
        [UnityTest]public IEnumerator GameSceneSuppliedForestBodiesUseTheirOwnDeathAndDebrisBindings()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=true;
            yield return new EnterPlayMode();yield return null;yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var bridge=arena.GetComponent<HitFeedbackBridge>();Assert.That(bridge.Ready,Is.True,bridge.LastError);
            var catalog=Resources.Load<DebrisCatalog>("HitFeedback/Generated/Debris");
            Capture(arena.ViewCamera,arena.Player.Center+Vector2.right*2,"scene-before-combat");
            var candidates=arena.enemies.Where(e=>e&&e.GetComponent("ForestEnemyPresentation"))
                .GroupBy(e=>e.GetComponent("ForestEnemyPresentation").GetType().GetField("data").GetValue(e.GetComponent("ForestEnemyPresentation")))
                .Select(g=>g.First()).ToArray();Assert.That(candidates.Length,Is.EqualTo(2));
            var floor=new GameObject("Forest approved-art fixture floor");floor.transform.position=new Vector3(-500,-.5f,0);floor.AddComponent<ArenaSurface>();var surface=floor.AddComponent<BoxCollider2D>();surface.size=new Vector2(60,1);arena.RegisterFeedbackSurface(surface);
            using(var driver=arena.AcquireManualSimulation())foreach(var actor in candidates)
            {
                driver.Reset();foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(2000,0);}
                var view=actor.GetComponent("ForestEnemyPresentation");var data=view.GetType().GetField("data").GetValue(view);
                var move=data.GetType().GetField("move").GetValue(data);var source=((Sprite[])move.GetType().GetField("frames").GetValue(move))[0];
                var death=data.GetType().GetField("death").GetValue(data);
                bool hasDeath=death!=null&&((Sprite[])death.GetType().GetField("frames").GetValue(death))?.Length>0;
                string species=(string)data.GetType().GetField("species").GetValue(data);
                var bake=catalog.Find(source);Assert.That(bake,Is.Not.Null,species);Assert.That(bake.IsCurrentSource,Is.True);
                actor.Position=new Vector2(-500,.003f);actor.health=1;actor.shield=0;actor.guardUntil=0;
                var player=arena.Player;player.UseTestInput=true;player.selected=0;player.TryReconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="base_sword"),0,out _);
                player.Position=actor.Position-Vector2.right*1.8f;player.SyncFacing(1);Physics2D.SyncTransforms();view.SendMessage("LateUpdate");
                driver.Step(.01f,new CombatInput(0,CombatButtons.Basic));
                for(int i=0;i<100&&!actor.Dead;i++)Step(driver,arena,view,bridge);
                Assert.That(actor.Dead,Is.True);view.SendMessage("LateUpdate");bridge.Sample();
                Assert.That(bridge.StartedHits,Is.EqualTo(1));Assert.That(bridge.ActiveDebris,Is.Zero,"The actual forest death clip still owns its end.");player.CancelAction();
                if(!hasDeath)
                {
                    for(int i=0;i<100;i++)Step(driver,arena,view,bridge);
                    Assert.That(bridge.ActiveDebris,Is.Zero,"Legacy forest data has no death clip; do not invent a completion or borrow another species' clip.");
                    Assert.That(actor.visual.color.a,Is.Zero);StringAssert.Contains("No death clip",bridge.LastError);
                    Capture(arena.ViewCamera,new Vector2(-500,1),species+"-legacy-fade");driver.Reset();continue;
                }
                for(int i=0;i<300&&actor.visual.enabled;i++)Step(driver,arena,view,bridge);
                Assert.That(actor.visual.enabled,Is.False);Step(driver,arena,view,bridge);
                Assert.That(bridge.ActiveDebris,Is.EqualTo(bake.pieces.Length),bridge.LastError);
                for(int i=0;i<100;i++)Step(driver,arena,view,bridge);
                var displays=arena.transform.Find("Combat hit feedback output").GetComponentsInChildren<MeshRenderer>().Where(r=>r.gameObject.activeInHierarchy).ToArray();
                Assert.That(displays.Length,Is.EqualTo(bake.pieces.Length));
                foreach(var display in displays)Assert.That(display.GetComponent<Collider2D>(),Is.Null);
                Capture(arena.ViewCamera,new Vector2(-500,1),species);
                driver.Reset();Assert.That(bridge.ActiveDebris,Is.Zero);Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1));
            }
            Object.Destroy(floor);
        }
        static void Step(CombatArena.ManualSimulation driver,CombatArena arena,Component view,HitFeedbackBridge bridge)
        {driver.Step(.01f);arena.Presentation.Draw(0);view.SendMessage("LateUpdate");bridge.Sample();}
        static void Capture(Camera camera,Vector2 center,string species)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;var position=camera.transform.position;float size=camera.orthographicSize;
            var texture=RenderTexture.GetTemporary(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);
            try
            {
                camera.transform.position=new Vector3(center.x,center.y+.4f,-10);camera.orthographicSize=2.6f;camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../approved-art-captures"));Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"forest-"+species+"-debris.png"),image.EncodeToPNG());
            }
            finally{camera.targetTexture=previous;camera.transform.position=position;camera.orthographicSize=size;RenderTexture.active=active;RenderTexture.ReleaseTemporary(texture);Object.Destroy(image);}
        }
    }
}
