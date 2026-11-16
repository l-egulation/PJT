using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
namespace Deviant.Chapter2.Tests
{
    public sealed class Chapter2MonsterArtTests
    {
        [Test] public void FiveApprovedMonstersHaveAlignedSingleHitFrames()
        {
            var catalog=Chapter2MonsterArt.Load();Assert.That(catalog.monsters.Length,Is.EqualTo(5));
            foreach(var id in new[]{"K4","B3","M3","R-selected","H3"}) {
                var monster=catalog.Find(id);Assert.That(monster,Is.Not.Null,id);
                foreach(var motion in new[]{"idle","walk","attack","hit"}) {
                    var clip=monster.Find(motion);Assert.That(clip,Is.Not.Null,id+"/"+motion);
                    Assert.That(clip.frames.Length,Is.EqualTo(clip.milliseconds.Length));Assert.That(clip.milliseconds.All(ms=>ms>0));
                    foreach(var sprite in clip.frames) {
                        Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(512,384)));Assert.That(sprite.pivot,Is.EqualTo(new Vector2(320,124)).Using(Vector2Comparer.Instance));
                        Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
                    }
                }
                var death=monster.Find("death");Assert.That(death.frames.Single(),Is.SameAs(monster.Find("idle").frames[0]));
                var debris=Resources.Load<Deviant.Presentation.Unity.DebrisCatalog>("HitFeedback/Generated/Debris");
                var bake=debris.Find(death.frames[0]);Assert.That(bake,Is.Not.Null,id+" shatter body");Assert.That(bake.IsCurrentSource,Is.True);
                Assert.That(bake.pieces.Length,Is.GreaterThan(1));
                var hit=monster.Find("hit");Assert.That(hit.frames.Length,Is.EqualTo(1));Assert.That(hit.DurationMs,Is.EqualTo(150));Assert.That(hit.loop,Is.False);
            }
        }
        [Test] public void Chapter2MappingLeavesEliteAndChapter1ContentUnchanged()
        {
            Assert.That(Chapter2MonsterArt.ArtId("01_thorn-root"),Is.EqualTo("M3"));
            Assert.That(Chapter2MonsterArt.ArtId("02_broom-stalker"),Is.EqualTo("K4"));
            Assert.That(Chapter2MonsterArt.ArtId("03_gutter-spitter"),Is.EqualTo("R-selected"));
            Assert.That(Chapter2MonsterArt.ArtId("draft_06_stationary_turret"),Is.EqualTo("H3"));
            Assert.That(Chapter2MonsterArt.ArtId("draft_07_heavy_charger"),Is.EqualTo("B3"));
            Assert.That(Chapter2MonsterArt.ArtId("04_well-warden"),Is.Null);
        }
        [TestCase("K4")] [TestCase("B3")] [TestCase("M3")] [TestCase("R-selected")] [TestCase("H3")]
        public void SingleHitReturnsToIdleAtUnchangedScaleAndPivot(string id)
        {
            var node=new GameObject("Chapter2 hit playback test");
            try {
                var renderer=node.AddComponent<SpriteRenderer>();var player=node.AddComponent<Deviant.Stage1Monsters.Stage1MonsterPlayer>();
                player.catalog=Chapter2MonsterArt.Load();player.monsterId=id;player.Initialize();var scale=node.transform.localScale;
                Assert.That(player.Play("hit"),Is.True);var hit=renderer.sprite;
                player.Advance(.149f);Assert.That(renderer.sprite,Is.SameAs(hit));Assert.That(player.Motion,Is.EqualTo("hit"));
                player.Advance(.002f);Assert.That(player.Motion,Is.EqualTo("idle"));
                Assert.That(node.transform.localScale,Is.EqualTo(scale));Assert.That(renderer.sprite.pivot,Is.EqualTo(hit.pivot));
                player.faceRight=true;player.SampleAt(0);Assert.That(renderer.flipX,Is.True);
                player.Play("hit");player.SampleAt(0);Assert.That(renderer.sprite,Is.SameAs(hit));
            } finally {Object.DestroyImmediate(node);}
        }
        [UnityEngine.TestTools.UnityTest] public System.Collections.IEnumerator ChapterTwoAcceptedHitsUseAllFiveSelectedPoses()
        {
            Deviant.Chapter2.Editor.Chapter2Tools.Create();
            yield return new UnityEngine.TestTools.EnterPlayMode();
            Chapter2World world=null;float deadline=Time.realtimeSinceStartup+30;
            do {yield return null;world=Object.FindAnyObjectByType<Chapter2World>();}while((!world||!world.Ready)&&Time.realtimeSinceStartup<deadline);
            Assert.That(world&&world.Ready,Is.True,"2챕터 실제 씬 초기화");
            world.arena.Player.UseTestInput=true;
            foreach(var id in new[]{"K4","B3","M3","R-selected","H3"}) {
                var entry=world.Layout.actors.First(a=>Chapter2MonsterArt.ArtId(a.species)==id);
                world.WarpToRoom(entry.roomId);var actor=world.Actors[entry.id];actor.aggressive=false;
                using(var manual=world.arena.AcquireManualSimulation()) {
                    var size=actor.size;var scale=actor.visual.transform.localScale;
                    Assert.That(actor.Receive(1,world.arena.Player,1,false,null,true),Is.True,id+" accepted hit");
                    yield return null;yield return null;
                    Assert.That(actor.visual.sprite,Is.SameAs(Chapter2MonsterArt.Load().Find(id).Find("hit").frames[0]),id+" hit pose");
                    Assert.That(actor.size,Is.EqualTo(size));Assert.That(actor.visual.transform.localScale,Is.EqualTo(scale));
                    for(int i=0;i<60;i++)manual.Step(1f/120);yield return null;
                    Assert.That(actor.visual.sprite,Is.Not.SameAs(Chapter2MonsterArt.Load().Find(id).Find("hit").frames[0]),id+" recovers");
                    var bridge=world.arena.GetComponent<Deviant.Integration.HitFeedbackBridge>();Assert.That(bridge.Ready,Is.True);
                    int before=bridge.ActiveDebris;
                    Assert.That(actor.Receive(99999,world.arena.Player,1,false,null,true),Is.True,id+" lethal hit");
                    yield return null;yield return null;
                    for(int i=0;i<16;i++)manual.Step(1f/120);yield return null;
                    Assert.That(bridge.ActiveDebris,Is.GreaterThan(before),id+" shared physical shatter");
                    Assert.That(actor.visual.enabled,Is.False,id+" body replaced by fragments");
                    int once=bridge.ActiveDebris;yield return null;yield return null;
                    Assert.That(bridge.ActiveDebris,Is.EqualTo(once),id+" no duplicate shatter");
                }
            }
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }
        sealed class Vector2Comparer : System.Collections.Generic.IEqualityComparer<Vector2>
        {
            public static readonly Vector2Comparer Instance=new Vector2Comparer();
            public bool Equals(Vector2 x,Vector2 y)=>Vector2.Distance(x,y)<.01f;
            public int GetHashCode(Vector2 v)=>0;
        }
    }
}
