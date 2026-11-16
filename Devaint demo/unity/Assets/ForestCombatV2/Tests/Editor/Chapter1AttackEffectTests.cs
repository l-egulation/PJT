using System.Collections;
using System.Linq;
using System.IO;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1AttackEffectTests
    {
        ForestCombatWorld world;
        CombatArena arena;
        CombatArena.ManualSimulation simulation;
        [UnitySetUp] public IEnumerator Setup()
        {
            LegacySceneFixture.Open("Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            world=Object.FindFirstObjectByType<ForestCombatWorld>();Assert.That(world.Ready,Is.True);
            arena=world.arena;arena.Player.UseTestInput=true;
            foreach(var e in arena.enemies)e.aggressive=false;
            simulation=arena.AcquireManualSimulation();
            var floor=new GameObject("effects isolated flat ground");floor.AddComponent<ArenaSurface>();
            floor.transform.position=new Vector3(-500,-.5f,0);floor.AddComponent<BoxCollider2D>().size=new Vector2(100,1);
            Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator Teardown()
        {simulation?.Dispose();simulation=null;if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();}
        void Advance(float seconds)
        {
            for(int i=0;i<Mathf.RoundToInt(seconds/.01f);i++)
            {
                simulation.Step(.01f);
                foreach(var p in Object.FindObjectsByType<Chapter1MonsterPresentation>(FindObjectsSortMode.None))p.Draw(.01f);
            }
        }
        void Capture(string name,FieldMouseEnemy actor)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var camera=world.View;var position=camera.transform.position;float size=camera.orthographicSize;
            var target=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;
            var texture=new RenderTexture(768,432,24);var pixels=new Texture2D(768,432,TextureFormat.RGB24,false);
            try
            {
                camera.transform.position=new Vector3(actor.Position.x,actor.Position.y+1.4f,-10);
                camera.orthographicSize=2.7f;camera.rect=new Rect(0,0,1,1);camera.targetTexture=texture;
                camera.Render();RenderTexture.active=texture;pixels.ReadPixels(new Rect(0,0,768,432),0,0);pixels.Apply();
                string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../outputs/monster-effects-runtime"));
                Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=target;camera.transform.position=position;camera.orthographicSize=size;camera.rect=rect;
                RenderTexture.active=active;Object.DestroyImmediate(pixels);Object.DestroyImmediate(texture);
            }
        }
        FieldMouseEnemy Place(string species,int facing)
        {
            foreach(var e in arena.enemies){e.aggressive=false;e.ResetEnemy();}
            arena.Player.ResetPlayer();
            var p=Object.FindObjectsByType<Chapter1MonsterPresentation>(FindObjectsSortMode.None).First(p=>p.species==species);
            var actor=(FieldMouseEnemy)p.actor;actor.spawn=new Vector2(-500,.003f);actor.ResetEnemy();actor.SyncFacing(facing);
            arena.Player.Position=actor.Position+Vector2.right*facing*(species=="05_boar"?4:2);
            arena.Player.velocity=Vector2.zero;Physics2D.SyncTransforms();Advance(.03f);
            return actor;
        }
        [UnityTest] public IEnumerator ApprovedEffectsFollowAttackContactAndFacingAndResetWithoutDealingDamage()
        {
            foreach(string species in new[]{"01_field_mouse","02_mole","05_boar"})foreach(int facing in new[]{1,-1})
            {
                var actor=Place(species,facing);var p=actor.GetComponent<Chapter1MonsterPresentation>();
                var fx=p.attackEffects;Assert.That(fx,Is.Not.Null,species);
                if(species!="05_boar")
                {
                    float gap=.25f;
                    arena.Player.Position=new Vector2(actor.Position.x+actor.offset.x*facing+
                        facing*(actor.size.x/2+gap+arena.Player.size.x/2)-arena.Player.offset.x,.003f);
                    Physics2D.SyncTransforms();
                }
                actor.aggressive=true;Advance(.02f);Assert.That(actor.Brain.Executing,Is.True,species);
                Advance(actor.Definition.prepare-.04f);Assert.That(fx.VisibleEffects,Is.Zero,"No early effect");
                Advance(.06f);Assert.That(fx.VisibleEffects,Is.GreaterThan(0),species+" contact effect");
                var effectRenderers=fx.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled).ToArray();
                Assert.That(effectRenderers.All(r=>r.flipX==(facing<0)),Is.True,"Both directions mirror around the foot origin");
                Assert.That(fx.GetComponentsInChildren<SpriteRenderer>().Where(r=>r!=actor.visual).All(r=>!r.GetComponent<Collider2D>()),Is.True,"Effect sprites never create damage or physics colliders");
                float hp=arena.Player.health;Vector2 position=actor.Position;var size=actor.bodyCollider.size;
                for(int n=0;n<8;n++)p.Draw(0);
                Assert.That(arena.Player.health,Is.EqualTo(hp));Assert.That(actor.Position,Is.EqualTo(position));
                Assert.That(actor.bodyCollider.size,Is.EqualTo(size));Assert.That(arena.projectiles.Count,Is.Zero);
                Advance(.04f);Capture(species+"_"+facing,actor);
                actor.CancelAttack();p.Draw(0);Assert.That(fx.VisibleEffects,Is.Zero);Assert.That(fx.VisibleAfterimages,Is.Zero);
                arena.ResetTest();p.Draw(0);Assert.That(fx.VisibleEffects,Is.Zero);Assert.That(fx.VisibleAfterimages,Is.Zero);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator BoarDustAndAnimationAfterimagesStayAtHistoricalWorldPositionsAndClearOnHit()
        {
            var actor=Place("05_boar",1);var p=actor.GetComponent<Chapter1MonsterPresentation>();var fx=p.attackEffects;
            actor.aggressive=true;Advance(.02f);Advance(actor.Definition.prepare+.1f);
            Assert.That(fx.DustEmissions,Is.EqualTo(1));Assert.That(fx.VisibleAfterimages,Is.GreaterThan(0));
            Assert.That(world.monsterAttackEffects.Find("05_boar").afterimages.Contains(fx.FirstAfterimageSprite),Is.True);
            Assert.That(fx.FirstAfterimagePosition.x,Is.LessThan(actor.Position.x));
            var dust=fx.FirstDustPosition;Advance(.08f);
            Capture("boar_afterimages",actor);
            Assert.That(fx.FirstDustPosition,Is.EqualTo(dust),"Dust stays at rear-foot push-off, not attached to moving body");
            actor.Receive(1,arena.Player,-1,false,null);p.Draw(0);
            Assert.That(fx.VisibleAfterimages,Is.Zero);Assert.That(fx.VisibleEffects,Is.Zero);
            Assert.That(actor.Brain.Executing,Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator SelectorSpawnsChosenSpeciesRejectsUnsupportedFloorAndKeepsResetPlacement()
        {
            var settings=world.GetComponent<Chapter1MonsterTestSettings>();Assert.That(settings,Is.Not.Null);
            foreach(Chapter1TestSpecies species in new[]{Chapter1TestSpecies.FieldMouse,Chapter1TestSpecies.Mole,Chapter1TestSpecies.Boar})
            {
                int count=arena.enemies.Count;
                Assert.That(settings.TryPlace(species,new Vector2(-500,.1f),out var actor),Is.True,settings.Status);
                Assert.That(arena.enemies.Count,Is.EqualTo(count+1));Assert.That(actor.aggressive,Is.False);
                Assert.That(actor.GetComponent<Chapter1MonsterPresentation>().species,Is.EqualTo(Chapter1MonsterTestSettings.SpeciesId(species)));
                Assert.That(actor.GetComponent<Chapter1AttackEffects>(),Is.Not.Null);
                var spawn=actor.spawn;actor.Position+=Vector2.right;arena.ResetTest();
                Assert.That(actor.Position,Is.EqualTo(spawn));world.RemoveMonster(actor);
                Assert.That(arena.enemies.Count,Is.EqualTo(count));Assert.That(actor.gameObject.activeSelf,Is.False);
            }
            int before=arena.enemies.Count;
            Assert.That(settings.TryPlace(Chapter1TestSpecies.Boar,new Vector2(-600,1),out _),Is.False);
            Assert.That(arena.enemies.Count,Is.EqualTo(before));
            yield return null;
        }
        [UnityTest] public IEnumerator BoarTrailRemainsReadableBehindBodyAcrossRepeatedRenderFrames()
        {
            foreach(int facing in new[]{1,-1})
            {
                var actor=Place("05_boar",facing);
                var p=actor.GetComponent<Chapter1MonsterPresentation>();
                actor.aggressive=true;Advance(.02f);Advance(actor.Definition.prepare+.2f);
                // Test actual translucent output, not just whether a snapshot was allocated.
                var trail=p.attackEffects.GetComponentsInChildren<SpriteRenderer>()
                    .Where(r=>r.enabled && r.name.StartsWith("Historical pose") &&
                        (actor.Position.x-r.transform.position.x)*facing>.5f && r.color.a>=.45f).ToArray();
                Assert.That(trail.Length,Is.GreaterThan(0),"A readable historical silhouette must clear the opaque body in both directions");
                var positions=trail.Select(r=>r.transform.position).ToArray();
                for(int i=0;i<6;i++)p.Draw(0);
                for(int i=0;i<trail.Length;i++)
                {
                    Assert.That(trail[i].enabled,Is.True);
                    Assert.That(trail[i].transform.position,Is.EqualTo(positions[i]));
                }
                Capture("boar_readable_trail_"+facing,actor);
                actor.CancelAttack();p.Draw(0);
                Assert.That(p.attackEffects.VisibleAfterimages,Is.Zero);
            }
            yield return null;
        }
        [Test] public void InspectorPlacementOverridesOnlyTestPopulationAndPreservesWorldCoordinateConversion()
        {
            var root=new GameObject("placement configuration test");
            try
            {
                var settings=root.AddComponent<Chapter1MonsterTestSettings>();
                var data=new ForestData{monsters=new[]{new MonsterKind{id="05_boar"}},actors=new[]{new MonsterPlacement{kind=0,x=32,y=64}}};
                Assert.That(settings.BuildPlacements(data),Is.SameAs(data.actors));
                settings.useCustomPlacements=true;Assert.That(settings.BuildPlacements(data),Is.Empty);
                settings.placements.Add(new Chapter1TestPlacement{species=Chapter1TestSpecies.Boar,feet=new Vector2(3,-2)});
                var placement=settings.BuildPlacements(data).Single();
                Assert.That(placement.kind,Is.Zero);Assert.That(placement.x,Is.EqualTo(96));Assert.That(placement.y,Is.EqualTo(64));
                Assert.That(data.actors[0].x,Is.EqualTo(32),"Authored map is not rewritten");
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
