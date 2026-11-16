using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using ForestCombatV2;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Stage1Monsters.Game.Tests
{
    public sealed class Stage1MonsterEffectTests
    {
        [Test] public void BrightStrokePixelsStayInsideTheActualAttackBoxes()
        {
            var data=JsonUtility.FromJson<VillageMonsterData>(File.ReadAllText("Assets/Deviant/Stage1Monsters/Game/Data/VillageMonsterContent.json"));
            var catalog=AssetDatabase.LoadAssetAtPath<Stage1MonsterEffectCatalog>(Deviant.Stage1Monsters.Game.Editor.Stage1MonsterEffectImporter.CatalogPath);
            foreach(string id in new[]{"01_thorn-root","04_well-warden"})
            {
                var spec=data.monsters.Single(s=>s.id==id);bool elite=id=="04_well-warden";
                var clip=catalog.Find(elite?"elite-claw-sweep":"thorn-slash");int strong=0;
                float left=-spec.definition.attackOffset[0]-spec.definition.attackSize[0]/2;
                float right=left+spec.definition.attackSize[0];
                float bottom=spec.definition.attackOffset[1]-spec.definition.attackSize[1]/2;
                float top=bottom+spec.definition.attackSize[1];
                const float nativeTolerance=1f/32;
                foreach(var sprite in clip.frames)
                {
                    var pixels=sprite.texture.GetPixels32();
                    for(int i=0;i<pixels.Length;i++)if(pixels[i].a>80)
                    {
                        float x=(i%sprite.texture.width+.5f-sprite.pivot.x)/32-(elite?.5f:0);
                        float y=(i/sprite.texture.width+.5f-sprite.pivot.y)/32;
                        Assert.That(x,Is.InRange(left-nativeTolerance,right+nativeTolerance),id+" bright stroke X");
                        Assert.That(y,Is.InRange(bottom-nativeTolerance,top+nativeTolerance),id+" bright stroke Y");strong++;
                    }
                }
                Assert.That(strong,Is.GreaterThan(0),id);
            }
        }
        [Test] public void NativeEffectsKeepPivotsThreeColorsAndExpandingSmokeFade()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<Stage1MonsterEffectCatalog>(Deviant.Stage1Monsters.Game.Editor.Stage1MonsterEffectImporter.CatalogPath);
            Assert.That(catalog,Is.Not.Null);Assert.That(catalog.clips.Length,Is.EqualTo(8));
            var smoke=catalog.Find("broom-dust");
            Assert.That(smoke.frames.Length,Is.EqualTo(16));
            int area=0;float center=float.PositiveInfinity;int opacity=256;
            for(int i=4;i<15;i++)
            {
                var sprite=smoke.frames[i];var pixels=sprite.texture.GetPixels32();
                var used=pixels.Where(p=>p.a>0).ToArray();
                int count=used.Length;float cx=0;for(int j=0;j<pixels.Length;j++)if(pixels[j].a>0)cx+=j%sprite.texture.width;
                cx/=count;int alpha=used[0].a;
                Assert.That(count,Is.GreaterThanOrEqualTo(area));Assert.That(cx,Is.LessThanOrEqualTo(center+.05f));
                Assert.That(alpha,Is.LessThan(opacity));Assert.That(used.All(p=>p.a==alpha),Is.True);
                Assert.That(used.Select(p=>(p.r<<16)|(p.g<<8)|p.b).Distinct().Count(),Is.LessThanOrEqualTo(3));
                Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
                Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));
                Assert.That(sprite.pivot,Is.EqualTo(new Vector2(112,16)));
                area=count;center=cx;opacity=alpha;
            }
            Assert.That(smoke.frames.Last().texture.GetPixels32().All(p=>p.a==0),Is.True);
        }

        [UnityTest] public IEnumerator EffectsAndBurrowUseCombatClockFixedWarningOneHitAndReset()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();yield return null;yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();Assert.That(world.Ready,Is.True);
            var arena=world.arena;arena.Player.UseTestInput=true;
            var enemies=arena.enemies.OfType<FieldMouseEnemy>().ToArray();
            foreach(var e in enemies){e.aggressive=false;e.CancelAttack();}
            var floor=new GameObject("Temporary effects verification floor");floor.transform.position=new Vector3(-500,-.5f,0);
            floor.AddComponent<ArenaSurface>();floor.AddComponent<BoxCollider2D>().size=new Vector2(100,1);Physics2D.SyncTransforms();
            foreach(var index in enemies.Select(e=>e.SurfaceIndex).Distinct())index.Rebuild(Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None));
            using(var simulation=arena.AcquireManualSimulation())
            {
                var presentations=world.GetComponentsInChildren<Stage1VillageMonsterPresentation>();
                void Advance(float seconds)
                { for(int i=0;i<Mathf.CeilToInt(seconds/.01f);i++){simulation.Step(.01f);foreach(var p in presentations)p.Draw();} }
                FieldMouseEnemy Place(string id,int facing)
                {
                    arena.ResetTest();foreach(var e in enemies){e.aggressive=false;e.CancelAttack();}
                    var actor=enemies.First(e=>e.Definition.id==id);actor.spawn=new Vector2(-500,.003f);actor.ResetEnemy();
                    actor.SyncFacing(facing);arena.Player.Position=new Vector2(-500+facing*3,.003f);
                    arena.Player.velocity=Vector2.zero;Physics2D.SyncTransforms();Advance(.12f);return actor;
                }
                foreach(int facing in new[]{-1,1})
                {
                    var broom=Place("02_broom-stalker",facing);
                    arena.Player.Position=broom.Position+new Vector2(facing*1.2f,0);Physics2D.SyncTransforms();
                    // The dust starts at the sweep; a held wind-up (prepareHoldExtra) moves it later by the same amount.
                    broom.aggressive=true;Advance(.83f+broom.GetComponent<Stage1VillageMonsterAttack>().HoldExtra);
                    var fx=broom.GetComponent<Stage1VillageMonsterEffects>();Assert.That(fx.EffectRenderer.enabled,Is.True);
                    Assert.That(fx.EffectRenderer.flipX,Is.EqualTo(facing>0));
                    Assert.That(fx.EffectRenderer.transform.position.x,Is.EqualTo(broom.Position.x).Within(.001));
                    var before=fx.EffectRenderer.sprite;var now=arena.Now;
                    using(var scope=arena.Controls.OpenScope("FX pause test"))
                    using(var pause=scope.PauseSimulation())
                    { yield return new WaitForSecondsRealtime(.05f);fx.Draw();Assert.That(arena.Now,Is.EqualTo(now));Assert.That(fx.EffectRenderer.sprite,Is.SameAs(before)); }
                    broom.CancelAttack();fx.Draw();Assert.That(fx.EffectRenderer.enabled,Is.False);
                }
                foreach(bool dodge in new[]{false,true})
                {
                    var actor=Place("04_well-warden",dodge?-1:1);
                    actor.aggressive=true;Advance(.12f);Assert.That(actor.Brain.Execution,Is.EqualTo(1));
                    actor.aggressive=false;actor.CancelAttack();Advance(4);
                    actor.aggressive=true;Advance(.05f);
                    var burrow=actor.GetComponent<Stage1EliteBurrow>();Assert.That(burrow.Active,Is.True,burrow.SelectionStatus+" brain="+actor.Brain.Execution+" state="+actor.Brain.State);
                    void Until(float age){Advance(Mathf.Max(0,age-actor.SpeciesAttackAge));}
                    Until(.7f);Assert.That(actor.Hidden,Is.True);Assert.That(actor.hurtCollider.enabled,Is.False);
                    Assert.That(actor.visual.enabled,Is.False);
                    Until(1.65f);Assert.That(burrow.Locked,Is.True);var target=burrow.Destination;
                    float health=arena.Player.health;Assert.That(health,Is.EqualTo(arena.Player.maxHealth));
                    var effect=actor.GetComponent<Stage1VillageMonsterEffects>();
                    var ink=actor.GetComponent<Stage1BurrowInk>();Assert.That(ink.WarningVisible,Is.True);
                    Assert.That(ink.WarningWidth,Is.EqualTo(burrow.DamageBounds.size.x).Within(.001));
                    if(dodge){arena.Player.Position+=Vector2.right*4;Physics2D.SyncTransforms();}
                    Until(2.2f);Assert.That(burrow.Destination,Is.EqualTo(target));Assert.That(burrow.Emerged,Is.True);
                    Assert.That(actor.Hidden,Is.False);Assert.That(actor.bodyCollider.enabled && actor.hurtCollider.enabled,Is.True);
                    Assert.That(actor.Position.x,Is.EqualTo(target.x).Within(.01));
                    Until(2.38f);Assert.That(burrow.Hits,Is.EqualTo(dodge?0:1));
                    Assert.That(arena.Player.health,Is.EqualTo(dodge?health:health-actor.Definition.damage).Within(.001));
                    actor.CancelAttack();effect.Draw();Assert.That(burrow.Active,Is.False);Assert.That(effect.EffectRenderer.enabled,Is.False);
                    Assert.That(burrow.TryGround(new Vector2(-560,0),out var unused),Is.False,"Never emerge outside supported ground.");
                }
                // Interrupt during melt and reset during underground concealment.
                var elite=Place("04_well-warden",1);elite.aggressive=true;Advance(.1f);
                elite.aggressive=false;elite.CancelAttack();Advance(4);elite.aggressive=true;Advance(.1f);
                var special=elite.GetComponent<Stage1EliteBurrow>();Assert.That(special.Active,Is.True);
                elite.Receive(1,arena.Player,-1,false,null);Advance(.02f);
                Assert.That(special.Active,Is.True,"Ordinary damage must not interrupt elite melt.");
                Assert.That(elite.staggerUntil,Is.LessThanOrEqualTo(arena.Now));
                elite.Receive(1,arena.Player,-1,false,new HitData {stun=.2f});Advance(.02f);
                Assert.That(special.Active,Is.False);Assert.That(elite.hurtCollider.enabled,Is.True);
                var actor2=Place("04_well-warden",1);actor2.aggressive=true;Advance(.1f);
                actor2.aggressive=false;actor2.CancelAttack();Advance(4);actor2.aggressive=true;Advance(.8f);
                Assert.That(actor2.Hidden,Is.True);arena.ResetTest();
                Assert.That(actor2.Hidden,Is.False);Assert.That(actor2.hurtCollider.enabled && actor2.bodyCollider.enabled,Is.True);
                Assert.That(actor2.GetComponent<Stage1EliteBurrow>().Active,Is.False);
                Debug.Log("STAGE1_EFFECT_GAMEPLAY_PASS: mirrors, pause, cancel, fixed warning, hit/dodge, single damage, safe ground, reset.");
            }
            Object.Destroy(floor);yield return new ExitPlayMode();
        }
        [UnityTearDown] public IEnumerator Cleanup() { if(Application.isPlaying)yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator EnlargedGutterShotHitsItsNewVerticalMarginAndLegacyShotMisses()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();yield return null;yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();Assert.That(world.Ready,Is.True);
            var arena=world.arena;foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.CancelAttack();}
            var target=new GameObject("Projectile margin target").AddComponent<CombatActor>();
            target.Configure(arena,new Vector2(500,1000.21f),new Vector2(.1f,.1f),Vector2.zero,false);
            target.isDecoy=true;target.health=target.maxHealth=100;
            using(var simulation=arena.AcquireManualSimulation())
            {
                Physics2D.SyncTransforms();var source=arena.enemies[0];
                var small=new GameObject("Legacy shot").AddComponent<ArenaProjectile>();
                small.Initialize(arena,source,new Vector2(499,1000),new Vector2(8,0),10,arena.Now+3);arena.projectiles.Add(small);
                small.Step(.25f);Assert.That(target.health,Is.EqualTo(100));small.Release();
                var spec=world.GetComponent<Stage1VillageMonsterContent>().Find("03_gutter-spitter");
                var large=new GameObject("Gutter shot").AddComponent<ArenaProjectile>();
                large.Initialize(arena,source,new Vector2(499,1000),new Vector2(8,0),10,arena.Now+3);arena.projectiles.Add(large);
                large.CollisionSize=new Vector2(spec.projectileSize[0],spec.projectileSize[1]);large.Step(.25f);
                Assert.That(target.health,Is.LessThan(100),"The newly visible vertical margin must collide.");
            }
            Object.Destroy(target.gameObject);yield return new ExitPlayMode();
        }
    }
}
