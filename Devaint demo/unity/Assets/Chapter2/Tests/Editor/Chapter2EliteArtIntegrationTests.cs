using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Chapter2.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    public sealed class Chapter2EliteArtIntegrationTests
    {
        [UnityTearDown] public IEnumerator LeavePlayModeAfterEachTest(){if(Application.isPlaying)yield return new ExitPlayMode();}

        [Test] public void ApprovedSpritesImportAtAuthoredFootPivotsWithoutFrameFitting()
        {
            var art=Chapter2EliteArt.Load();
            Assert.That(art.Find("teleport").frames.Length,Is.EqualTo(24));
            Assert.That(art.Find("combo").frames.Length,Is.EqualTo(52));
            Assert.That(art.Find("punch").frames.Length,Is.EqualTo(28));
            foreach(var item in art.sprites)
            {
                string path="Assets/Chapter2/Resources/"+item.path+".png";
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(sprite,Is.Not.Null,path);
                Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));
                Assert.That(sprite.pivot.x,Is.EqualTo(item.pivot[0]).Within(.01f),path);
                Assert.That(sprite.pivot.y,Is.EqualTo(item.canvas[1]-item.pivot[1]).Within(.01f),path);
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));
                Assert.That(importer.mipmapEnabled,Is.False);
                Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Compressed));
            }
        }
        [SetUp] public void OpenExistingScene()
        {
            if(Application.isPlaying)return;
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Chapter2Tools.ScenePath);
            var world=Object.FindAnyObjectByType<Chapter2World>();world.seed=17;world.family="current";
        }
        static IEnumerator WaitForWorld()
        {
            Chapter2World world;
            float end=Time.realtimeSinceStartup+45;
            while(Time.realtimeSinceStartup<end)
            {
                world=Object.FindAnyObjectByType<Chapter2World>();
                if(world&&world.Ready){yield return null;yield break;}
                yield return null;
            }
            Assert.Fail("Chapter2 scene failed to initialize");
        }
        static void Step(CombatArena.ManualSimulation sim,float seconds)
        {for(int i=0;i<Mathf.RoundToInt(seconds*120);i++){Physics2D.SyncTransforms();sim.Step(1f/120);}}
        static string Output(string name)
        {string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../captures"));Directory.CreateDirectory(folder);return Path.Combine(folder,name+".png");}
        static Texture2D Render(Camera camera,int width,int height)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;
            var target=RenderTexture.GetTemporary(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();return image;}
            finally{camera.rect=rect;camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);}
        }
        static void Capture(Chapter2EliteEnemy enemy,string name,float framing=3.4f)
        {
            var camera=enemy.arena.ViewCamera;var at=camera.transform.position;float size=camera.orthographicSize;
            try{camera.transform.position=new Vector3(enemy.Position.x,enemy.Position.y+1.5f,-10);camera.orthographicSize=framing;var image=Render(camera,1280,720);File.WriteAllBytes(Output(name),image.EncodeToPNG());Object.Destroy(image);}
            finally{camera.transform.position=at;camera.orthographicSize=size;}
        }
        [UnityTest] public IEnumerator MainScenePlaysAllThreeContactsAndPausesPresentationWithSimulation()
        {
            yield return new EnterPlayMode();yield return WaitForWorld();var world=Object.FindAnyObjectByType<Chapter2World>();world.WarpToRoom("E");
            var enemy=world.Elite;var arena=world.arena;var player=arena.Player;player.maxHealth=player.health=10000;
            using(var sim=arena.AcquireManualSimulation())
            {
                enemy.ShowRanges=false;
                enemy.BeginPattern(ElitePattern.Combo);Step(sim,1.08f/1.3f);enemy.Presentation.Sample();
                Assert.That(enemy.Presentation.CurrentClip,Is.EqualTo("combo"));
                Assert.That(enemy.Presentation.GroundDebrisCount,Is.GreaterThanOrEqualTo(8));
                Assert.That(enemy.Presentation.ActiveEffectCount,Is.GreaterThan(enemy.Presentation.GroundDebrisCount));
                Assert.That(enemy.visual.transform.position.y,Is.EqualTo(enemy.Position.y).Within(.001f));
                Assert.That(enemy.Presentation.BodyScale*88/32,Is.EqualTo(4.8f).Within(.001f),"idle and every attack keep the enlarged approved size");
                float scale=enemy.visual.transform.localScale.y;var sprite=enemy.visual.sprite;int effects=enemy.Presentation.ActiveEffectCount;
                enemy.Presentation.Sample();enemy.Presentation.Sample();
                Assert.That(enemy.visual.sprite,Is.SameAs(sprite));Assert.That(enemy.Presentation.ActiveEffectCount,Is.EqualTo(effects));
                Capture(enemy,"a3-combo-first-impact");
                Step(sim,1.5f/1.3f);enemy.Presentation.Sample();Assert.That(enemy.Presentation.GroundDebrisCount,Is.GreaterThanOrEqualTo(16));
                Capture(enemy,"a3-combo-third-impact");
                Step(sim,.5f/1.3f);enemy.Presentation.Sample();Assert.That(enemy.Presentation.CurrentClip,Is.EqualTo("combo"),"recovery continues during combat rest");
                enemy.BeginPattern(ElitePattern.Rock);Step(sim,1.47f);enemy.Presentation.Sample();
                Assert.That(enemy.visual.transform.localScale.y,Is.EqualTo(scale));
                Capture(enemy,"a3-rock-windup");
                Step(sim,.05f);enemy.Presentation.Sample();Capture(enemy,"a3-rock-impact");
                Assert.That(enemy.Presentation.CurrentClip,Is.EqualTo("punch"));
                enemy.StopEncounter();enemy.Presentation.Sample();Assert.That(enemy.Presentation.ActiveEffectCount,Is.Zero);Assert.That(enemy.FragmentCount,Is.Zero);
                world.WarpToRoom("S");player.Position=player.spawn;player.velocity=Vector2.zero;player.SyncFacing(1);Step(sim,.15f);
                enemy.aggressive=true;if(!arena.enemies.Contains(enemy))arena.enemies.Add(enemy);enemy.BeginPattern(ElitePattern.Teleport);Step(sim,.8f);enemy.Presentation.Sample();
                Assert.That(enemy.visual.enabled,Is.True);Assert.That(enemy.visual.transform.localScale.y,Is.EqualTo(scale));
                Capture(enemy,"a3-teleport-arrival");
                Step(sim,.25f);enemy.Presentation.Sample();Capture(enemy,"a3-teleport-hit");
                Assert.That(enemy.Presentation.ActiveEffectCount,Is.GreaterThan(0));
                enemy.health=0;sim.Step(1f/120);enemy.Presentation.Sample();
                Assert.That(enemy.visual.enabled,Is.False);Assert.That(enemy.Presentation.ActiveEffectCount,Is.GreaterThan(0));
                Step(sim,.7f);enemy.Presentation.Sample();Assert.That(enemy.Presentation.ActiveEffectCount,Is.Zero);
            }
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator FivePhysicalFragmentsKeepUniformFanAndVisibleWhiteOutline()
        {
            yield return new EnterPlayMode();yield return WaitForWorld();var world=Object.FindAnyObjectByType<Chapter2World>();world.WarpToRoom("E");
            var enemy=world.Elite;var player=world.arena.Player;player.maxHealth=player.health=10000;
            using(var sim=world.arena.AcquireManualSimulation())
            {
                var room=world.Layout.Room("E");enemy.Position=new Vector2(room.x+room.w/2,enemy.Position.y);
                player.Position=enemy.Position-Vector2.right*8;player.velocity=Vector2.zero;
                enemy.BeginPattern(ElitePattern.Rock);for(int n=0;n<600&&enemy.Age<enemy.Settings.rockFire+.01f;n++)Step(sim,1f/120);enemy.Presentation.Sample();
                Assert.That(enemy.FragmentCount,Is.EqualTo(5),"age="+enemy.Age+" pattern="+enemy.Pattern+" source="+enemy.RockPosition+" room="+world.CurrentRoom);var speeds=enemy.FragmentVelocities;
                float center=Mathf.Atan2(enemy.RockAim.y,enemy.RockAim.x)*Mathf.Rad2Deg;
                for(int i=0;i<5;i++)
                {
                    Assert.That(speeds[i].magnitude,Is.EqualTo(30).Within(.001f));
                    float degrees=Mathf.Atan2(speeds[i].y,speeds[i].x)*Mathf.Rad2Deg;
                    Assert.That(Mathf.DeltaAngle(center,degrees),Is.EqualTo(-30+15*i).Within(.01f));
                    var view=enemy.FragmentViews[i];
                    Assert.That(view.sharedMaterial.GetColor("_OutlineColor"),Is.EqualTo(Color.white));
                    Assert.That(view.sharedMaterial,Is.Not.SameAs(enemy.visual.sharedMaterial));
                    Assert.That(Mathf.DeltaAngle(view.transform.eulerAngles.z+enemy.Presentation.Art.fragmentAxes[i],degrees),Is.EqualTo(0).Within(.01f));
                }
                var expected=enemy.Presentation.Art.rockCenterOffsetPx;float unit=enemy.Presentation.BodyScale/32;
                Assert.That(enemy.RockPosition.y-enemy.Position.y,Is.EqualTo(expected[1]*unit).Within(.001f));
                Assert.That(Mathf.Abs(enemy.RockPosition.x-enemy.Position.x),Is.EqualTo(expected[0]*unit).Within(.001f));
                var views=enemy.FragmentViews;var original=views.Select(v=>v.transform.position).ToArray();
                player.Position+=Vector2.right*15;Step(sim,.05f);
                Assert.That(enemy.FragmentVelocities,Is.EqualTo(speeds),"moving target must not steer or add gravity");
                for(int i=0;i<5;i++)Assert.That(Vector2.Distance(views[i].transform.position,original[i]+(Vector3)(speeds[i]*.05f)),Is.LessThan(.002f));
                Step(sim,.025f);enemy.Presentation.Sample();Capture(enemy,"a3-five-fragment-fan",4.5f);
                // Render only one actual projectile on an isolated layer. White pixels must exist outside its source silhouette.
                var view0=views[2];int layer=view0.gameObject.layer;view0.gameObject.layer=30;
                var node=new GameObject("A3 outline verification camera");var camera=node.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.5f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.08f,.1f);camera.cullingMask=1<<30;camera.transform.position=view0.transform.position+Vector3.back*10;
                var material=view0.sharedMaterial;material.SetFloat("_OutlineStrength",1);var outlined=Render(camera,256,256);material.SetFloat("_OutlineStrength",0);var plain=Render(camera,256,256);material.SetFloat("_OutlineStrength",1);
                var a=outlined.GetPixels32();var b=plain.GetPixels32();int added=0;
                for(int i=0;i<a.Length;i++)if(a[i].r>235&&a[i].g>235&&a[i].b>235&&b[i].r<220)added++;
                File.WriteAllBytes(Output("a3-white-fragment"),outlined.EncodeToPNG());
                Assert.That(added,Is.GreaterThan(100),"white border must actually render, not just exist as a material setting");
                Object.Destroy(outlined);Object.Destroy(plain);view0.gameObject.layer=layer;Object.Destroy(node);
                enemy.StopEncounter();Assert.That(enemy.FragmentCount,Is.Zero);
            }
            yield return new ExitPlayMode();
        }
    }
}
