using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class RandomStageSwordHitTests
    {
        CombatArena.ManualSimulation simulation;
        [UnityTearDown] public IEnumerator Cleanup(){simulation?.Dispose();yield return new ExitPlayMode();}
        [UnityTest] public IEnumerator OpenRandomStageSwordChoiceHitsGeneratedMonsterWithApprovedA()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindFirstObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindFirstObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            Assert.That(SceneManager.GetActiveScene().path,Is.EqualTo(Chapter1MapSelector.ProceduralScene));
            var arena=world.arena;var player=arena.Player;player.UseTestInput=true;
            var offers=world.GetComponent<Chapter1StartingBlueprints>();
            Assert.That(offers,Is.Not.Null);player.Position=offers.OfferPosition(0);
            Assert.That(offers.TryChoose(0),Is.True);Assert.That(player.Form.id,Is.EqualTo("base_sword"));
            foreach(var enemy in arena.enemies)enemy.aggressive=false;
            var targets=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(p=>p.GetType().Name=="Stage1VillageMonsterPresentation").ToArray();
            Assert.That(targets.Length,Is.GreaterThan(0),"Random Stage must use its actual village monster presentation.");
            var target=targets.Select(p=>p.GetComponent<FieldMouseEnemy>()).First(a=>a.FlatPath(a.Position+Vector2.left*1.9f));
            var original=target.Position;
            player.Position=original+Vector2.left*1.9f;player.velocity=Vector2.zero;player.SyncFacing(1);
            Physics2D.SyncTransforms();simulation=arena.AcquireManualSimulation();
            for(int i=0;i<12;i++){simulation.Step(1f/120);arena.Presentation.Draw(1f/120);foreach(var p in targets)p.GetType().GetMethod("Draw").Invoke(p,null);}
            Assert.That(player.grounded,Is.True,"Use the generated stage floor, not an isolated fixture floor.");
            float health=target.health;
            string output=Path.GetFullPath("../random-stage-sword-hit");Directory.CreateDirectory(output);
            for(int i=0;i<82;i++)
            {
                simulation.Step(1f/120,new CombatInput(pressed:i==0||i==41?CombatButtons.Basic:CombatButtons.None));
                arena.Presentation.Draw(1f/120);foreach(var p in targets)p.GetType().GetMethod("Draw").Invoke(p,null);
                if(i==27)
                {
                    Assert.That(target.ReceivedHitCount,Is.EqualTo(1));
                    var effects=Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Basic sword A contact"&&r.enabled).ToArray();
                    Assert.That(effects.Length,Is.GreaterThan(0));Assert.That(effects.All(r=>!r.flipY),Is.True);
                    Capture(world.View,player.Position,Path.Combine(output,"random-stage-A.png"));
                }
            }
            Assert.That(target.ReceivedHitCount,Is.EqualTo(2));
            Assert.That(Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Any(r=>r.name=="Basic sword A contact"&&r.enabled&&r.flipY),Is.True);
            Assert.That(health-target.health,Is.InRange(18f,22f));
            var particles=target.GetComponentInChildren<ParticleSystem>();var values=new ParticleSystem.Particle[64];int count=particles.GetParticles(values);
            Assert.That(count,Is.GreaterThan(0));
            for(int i=0;i<count;i++)Assert.That(values[i].startColor.b,Is.LessThanOrEqualTo(57),"Sword hits use the dark ink palette.");
            File.WriteAllText(Path.Combine(output,"verification.txt"),"Scene: "+SceneManager.GetActiveScene().path+"\nUnity: "+Application.unityVersion+"\nStarting blueprint: base_sword\nGenerated monster: "+target.name+"\nNative stage floor, existing monster spawn\nAccepted hits: 2\nDamage: "+(health-target.health)+"\nFirst upward / second downward: passed\n");
        }
        static void Capture(Camera camera,Vector2 player,string path)
        {
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var oldPosition=camera.transform.position;float oldSize=camera.orthographicSize;var oldRect=camera.rect;
            var rt=new RenderTexture(960,540,24);var pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
            try
            {
                camera.transform.position=new Vector3(player.x+1.1f,player.y+1.4f,-10);camera.orthographicSize=3.1f;camera.rect=new Rect(0,0,1,1);camera.targetTexture=rt;
                camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=oldTarget;camera.transform.position=oldPosition;camera.orthographicSize=oldSize;camera.rect=oldRect;RenderTexture.active=oldActive;Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);}
        }
    }
}
