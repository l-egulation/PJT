using System;
using System.Collections;
using System.Linq;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Presentation.Tests
{
    public sealed class HitFeedbackMediaTests
    {
        sealed class Clock:ICueClock {public float time;public float ReadElapsed()=>time;}
        static HitFeedbackSettings Settings()=>new HitFeedbackSettings(JsonUtility.FromJson<HitFeedbackData>(Resources.Load<TextAsset>("HitFeedback/HitFeedback").text));
        [Test]public void OrdinaryHitsDrawFromTheOtherRecipesAndCriticalHitsUseTheCriticalOne()
        {
            var settings=Settings();
            Assert.That(settings.CriticalRecipe,Is.EqualTo("hit_blunt"));
            Assert.That(settings.OrdinaryRecipeKeys.Count,Is.EqualTo(43));
            Assert.That(settings.OrdinaryRecipeKeys,Does.Not.Contain("hit_blunt"));
            var state=UnityEngine.Random.state;var seen=new System.Collections.Generic.HashSet<string>();
            for(uint seed=0;seed<4096;seed++)
            {
                string key=settings.PickRecipe(seed,false);
                Assert.That(settings.OrdinaryRecipeKeys,Does.Contain(key));
                Assert.That(settings.PickRecipe(seed,false),Is.EqualTo(key),"The same hit always draws the same picture.");
                Assert.That(settings.PickRecipe(seed,true),Is.EqualTo("hit_blunt"));
                seen.Add(key);
            }
            Assert.That(seen,Is.EquivalentTo(settings.OrdinaryRecipeKeys),"Every ordinary recipe is reachable.");
            Assert.That(UnityEngine.Random.state,Is.EqualTo(state),"Picking never consumes the game's random stream.");
        }
        [Test]public void GeneratedCatalogContainsEveryLicensedPrefabWithValidExternallySampledMedia()
        {
            var settings=Settings();var bindings=Resources.Load<HitFeedbackBindings>("HitFeedback/Generated/Bindings");
            Assert.That(bindings,Is.Not.Null,"Run HitFeedbackAssetBuilder.Build.");
            Assert.That(bindings.prefabs.Select(b=>b.key),Is.EquivalentTo(settings.RecipeKeys));
            Assert.That(bindings.prefabs.Length,Is.EqualTo(44));
            // Generated output sits under Resources and ships in the player: no prefab outside the recipes may remain.
            var generated=UnityEditor.AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Deviant/Presentation/Resources/HitFeedback/Generated"})
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath).Select(System.IO.Path.GetFileNameWithoutExtension);
            Assert.That(generated,Is.EquivalentTo(settings.RecipeKeys));
            foreach(var binding in bindings.prefabs)
            {
                Assert.That(binding.prefab,Is.Not.Null,binding.key);
                Assert.DoesNotThrow(()=>PrefabCueFactory.Validate(binding.prefab),binding.key);
                Assert.That(binding.duration,Is.GreaterThan(0),binding.key);
            }
        }
        [UnityTearDown]public IEnumerator TearDown(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [Test]public void KickUsesBoundedAccentTimeAndDoesNotAccumulateBaseline()
        {
            var mixer=new CameraKickMixer(4,3);mixer.Add(1,0,2,.1f);mixer.Add(1,0,2,.1f);
            Assert.That(mixer.ReadPixels().X,Is.EqualTo(3));mixer.Advance(.05f);Assert.That(mixer.ReadPixels().X,Is.EqualTo(1).Within(.0001));
            mixer.Intensity=0;Assert.That(mixer.ReadPixels().X,Is.Zero);mixer.Advance(.05f);Assert.That(mixer.ActiveCount,Is.Zero);
        }
        [UnityTest]public IEnumerator LicensedPrefabSupportsHoldRewindPoolAndCueCleanup()
        {
            yield return new EnterPlayMode();
            var root=new GameObject("Prefab fixture");var bindings=Resources.Load<HitFeedbackBindings>("HitFeedback/Generated/Bindings");
            Assert.That(bindings,Is.Not.Null,"Run HitFeedbackAssetBuilder.Build in the licensed local project.");
            var settings=Settings();
            using(var factory=new PrefabCueFactory(root.transform,bindings,2,8192))
            {
                var definition=factory.Definition("hit_slash");var captured=factory.Capture("hit_slash",0,.35f,109);
                CueViewResult result=null;captured.Begin(definition,new CueAnchor(1,2,1),r=>result=r);Assert.That(result.Error,Is.Null);
                var view=result.View;view.Sample(0);
                for(int i=1;i<=10;i++)view.Sample(i*.01f);
                var systems=root.GetComponentsInChildren<ParticleSystem>();Assert.That(systems.Length,Is.GreaterThan(0));
                int count=systems.Sum(p=>p.particleCount);Assert.That(count,Is.GreaterThan(0));
                yield return null;yield return null;
                Assert.That(systems.Sum(p=>p.particleCount),Is.EqualTo(count),"Unity Update must not advance the manually sampled effect.");
                view.Sample(.1f);Assert.That(systems.Sum(p=>p.particleCount),Is.EqualTo(count));
                view.Sample(.02f);view.Sample(.1f);Assert.That(systems.Sum(p=>p.particleCount),Is.EqualTo(count),"Same seed reconstructs a rewound sample.");
                view.Sample(1.5f);view.Sample(2);Assert.That(systems.Sum(p=>p.particleCount),Is.Zero,"Advancing past the non-looping tail must not restart its burst.");
                view.Dispose();view.Dispose();Assert.That(factory.ActiveCount,Is.Zero);
                using(var player=new CuePlayer(captured))using(var scope=player.OpenScope("fixture"))
                {
                    var clock=new Clock();var handle=scope.Play(definition,new CueOccurrence(Guid.NewGuid(),definition.Id),new CueAnchor(0,0,1),clock);
                    clock.time=.1f;player.Tick();Assert.That(handle.Status,Is.EqualTo(CueStatus.Playing));
                    clock.time=definition.Duration+.01f;player.Tick();Assert.That(handle.Status,Is.EqualTo(CueStatus.Completed));Assert.That(factory.ActiveCount,Is.Zero);
                }
                Assert.That(root.transform.childCount,Is.EqualTo(1),"The returned instance is reused rather than accumulated.");
            }
            Object.Destroy(root);
        }
        [UnityTest]public IEnumerator PaperStyleUpdatesPausedMediaAndKeepsOriginalsAndOtherFactoriesIndependent()
        {
            yield return new EnterPlayMode();
            var root=new GameObject("Paper style fixture");var otherRoot=new GameObject("Independent paper fixture");
            var bindings=Resources.Load<HitFeedbackBindings>("HitFeedback/Generated/Bindings");Assert.That(bindings,Is.Not.Null);
            using(var factory=new PrefabCueFactory(root.transform,bindings,2,8192))
            using(var other=new PrefabCueFactory(otherRoot.transform,bindings,2,8192))
            {
                factory.PaperInk=other.PaperInk=true;factory.PaperOutlineColor=Color.red;
                CueViewResult result=null,otherResult=null;
                factory.Capture("hit_slash",0,.35f,109).Begin(factory.Definition("hit_slash"),new CueAnchor(0,0,1),r=>result=r);
                other.Capture("hit_slash",0,.35f,109).Begin(other.Definition("hit_slash"),new CueAnchor(0,0,1),r=>otherResult=r);
                using(var view=result.View)using(var otherView=otherResult.View)
                {
                    view.Sample(.1f);otherView.Sample(.1f);
                    var renderer=root.GetComponentInChildren<ParticleSystemRenderer>();var material=renderer.sharedMaterial;
                    var otherMaterial=otherRoot.GetComponentInChildren<ParticleSystemRenderer>().sharedMaterial;
                    Assert.That(material.GetColor("_OutlineColor"),Is.EqualTo(Color.red));
                    int count=root.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount);
                    factory.PaperOutlineColor=Color.blue;view.Sample(.1f);
                    Assert.That(renderer.sharedMaterial,Is.SameAs(material),"Editing style reuses the cached runtime material.");
                    Assert.That(material.GetColor("_OutlineColor"),Is.EqualTo(Color.blue));
                    Assert.That(Vector4.Distance(otherMaterial.GetColor("_OutlineColor"),other.PaperOutlineColor),Is.LessThan(.001f));
                    Assert.That(root.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount),Is.EqualTo(count),"Changing style must not advance paused media.");
                    Assert.Throws<ArgumentOutOfRangeException>(()=>factory.PaperOutlineColor=new Color(float.NaN,0,0));
                    Assert.That(factory.PaperOutlineColor,Is.EqualTo(Color.blue));
                    factory.PaperInk=false;view.Sample(.1f);
                    Assert.That(renderer.sharedMaterial.shader.name,Is.EqualTo("Deviant/HitFeedback/Particle"));
                    factory.PaperInk=true;view.Sample(.1f);Assert.That(renderer.sharedMaterial,Is.SameAs(material));
                }
                foreach(var renderer in bindings.prefabs.Single(b=>b.key=="hit_slash").prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    foreach(var material in renderer.sharedMaterials)Assert.That(material.shader.name,Is.EqualTo("Deviant/HitFeedback/Particle"));
                CueViewResult reused=null;factory.Capture("hit_slash",0,.35f,109).Begin(factory.Definition("hit_slash"),new CueAnchor(0,0,1),r=>reused=r);
                using(var view=reused.View){view.Sample(.1f);Assert.That(root.GetComponentInChildren<ParticleSystemRenderer>().sharedMaterial.GetColor("_OutlineColor"),Is.EqualTo(Color.blue));}
                Assert.That(root.transform.childCount,Is.EqualTo(1));
            }
            Object.Destroy(root);Object.Destroy(otherRoot);
        }
        [UnityTest]public IEnumerator RecipeScaleReachesAllLicensedParticlesAndIsResetOnPoolReuse()
        {
            yield return new EnterPlayMode();
            var root=new GameObject("Recipe scale fixture");var bindings=Resources.Load<HitFeedbackBindings>("HitFeedback/Generated/Bindings");var settings=Settings();
            Assert.That(bindings,Is.Not.Null,"Run HitFeedbackAssetBuilder.Build in the licensed local project.");
            try
            {
                using(var factory=new PrefabCueFactory(root.transform,bindings,2,8192))
                foreach(var key in settings.RecipeKeys)
                {
                    GameObject pooled=null;
                    foreach(float scale in new[]{settings.Scale(key)*.1f,settings.Scale(key)})
                    {
                        CueViewResult result=null;
                        factory.Capture(key,0,scale,109).Begin(factory.Definition(key),new CueAnchor(0,0,1),r=>result=r);
                        Assert.That(result.Error,Is.Null);
                        using(var view=result.View)
                        {
                            view.Sample(.1f);
                            var instance=root.transform.Cast<Transform>().Single(t=>t.gameObject.activeSelf).gameObject;
                            if(pooled)Assert.That(instance,Is.SameAs(pooled),"Scale changes must apply to the reused instance.");
                            pooled=instance;
                            Assert.That(instance.transform.localScale,Is.EqualTo(Vector3.one*scale));
                            var systems=instance.GetComponentsInChildren<ParticleSystem>(true);
                            Assert.That(systems.Sum(p=>p.particleCount),Is.GreaterThan(0),key);
                            foreach(var system in systems)
                            {
                                Assert.That(system.main.scalingMode,Is.EqualTo(ParticleSystemScalingMode.Hierarchy),key+" must scale particle size and motion with its output root.");
                                Assert.That(system.transform.lossyScale.x,Is.EqualTo(scale).Within(.00001f),key);
                                Assert.That(system.transform.lossyScale.y,Is.EqualTo(scale).Within(.00001f),key);
                                Assert.That(system.transform.lossyScale.z,Is.EqualTo(scale).Within(.00001f),key);
                            }
                        }
                        Assert.That(factory.ActiveCount,Is.Zero);
                    }
                }
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator LocalDebrisCollidesWithRegisteredFloorAndDoesNotAdvanceAnotherWorld()
        {
            yield return new EnterPlayMode();
            var root=new GameObject("Debris fixture");var floor=new GameObject("Explicit floor");floor.transform.position=new Vector3(0,-.5f,0);
            var collider=floor.AddComponent<BoxCollider2D>();collider.size=new Vector2(20,1);
            var sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),Vector2.one*.5f,Texture2D.whiteTexture.width);
            var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.1f,-.1f),new Vector3(.1f,-.1f),new Vector3(.1f,.1f),new Vector3(-.1f,.1f)};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();
            var bake=ScriptableObject.CreateInstance<DebrisBakeAsset>();bake.source=sprite;bake.sourceRect=sprite.rect;bake.sourcePivot=sprite.pivot;bake.pixelsPerUnit=sprite.pixelsPerUnit;bake.pieces=new[]{new DebrisPiece{center=Vector2.zero,mesh=mesh,mirroredMesh=mesh,hull=new[]{new Vector2(-.1f,-.1f),new Vector2(.1f,-.1f),new Vector2(.1f,.1f),new Vector2(-.1f,.1f)},area=.04f}};
            var shader=Shader.Find("Deviant/HitFeedback/Particle");
            using(var a=new DebrisWorld(root.transform,shader,Settings(),.02f,0))using(var b=new DebrisWorld(root.transform,shader,Settings(),.02f,0))
            {
                Assert.That(a.RegisterSurface(collider,false),Is.True);Assert.That(a.Spawn(bake,new Vector3(0,1,0),Quaternion.identity,Vector3.one,false,Vector2.zero,0,30),Is.True);
                for(int i=1;i<=200;i++)a.AdvanceTo(i*.02f);
                Assert.That(a.ActiveCount,Is.EqualTo(1));Assert.That(b.SimulatedThrough,Is.Zero);
                var display=root.transform.GetChild(0);Assert.That(display.position.y,Is.GreaterThan(.07f).And.LessThan(.2f),"Body lands on the real copied floor geometry.");
                Assert.That(display.GetComponent<Collider2D>(),Is.Null);float x=display.position.x;
                a.FootSweep(new Vector2(x-1,0),new Vector2(x+1,0),1,4,.02f);a.AdvanceTo(4.02f);a.AdvanceTo(4.04f);
                Assert.That(display.position.x,Is.GreaterThan(x));a.Reset(0);Assert.That(a.ActiveCount,Is.Zero);
            }
            Object.Destroy(root);Object.Destroy(floor);Object.Destroy(bake);Object.Destroy(mesh);Object.Destroy(sprite);
        }
        [UnityTest]public IEnumerator MeleeStrikeLaunchesOverlappedDebrisOncePerSwingAndCorpseIsFaded()
        {
            yield return new EnterPlayMode();
            var root=new GameObject("Debris strike fixture");var floor=new GameObject("Explicit floor");floor.transform.position=new Vector3(0,-.5f,0);
            var collider=floor.AddComponent<BoxCollider2D>();collider.size=new Vector2(40,1);
            var sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),Vector2.one*.5f,Texture2D.whiteTexture.width);
            var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.1f,-.1f),new Vector3(.1f,-.1f),new Vector3(.1f,.1f),new Vector3(-.1f,.1f)};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();
            var bake=ScriptableObject.CreateInstance<DebrisBakeAsset>();bake.source=sprite;bake.sourceRect=sprite.rect;bake.sourcePivot=sprite.pivot;bake.pixelsPerUnit=sprite.pixelsPerUnit;bake.pieces=new[]{new DebrisPiece{center=Vector2.zero,mesh=mesh,mirroredMesh=mesh,hull=new[]{new Vector2(-.1f,-.1f),new Vector2(.1f,-.1f),new Vector2(.1f,.1f),new Vector2(-.1f,.1f)},area=.04f}};
            var settings=Settings();
            Assert.That(settings.DebrisAlpha,Is.GreaterThan(0).And.LessThan(1),"Corpse debris is drawn slightly faded.");
            Assert.That(settings.DebrisDesaturate,Is.GreaterThan(0).And.LessThanOrEqualTo(1));
            Assert.That(settings.DebrisStrikeSpeed,Is.GreaterThan(0));
            using(var world=new DebrisWorld(root.transform,Shader.Find("Deviant/HitFeedback/Particle"),settings,.02f,0))
            {
                Assert.That(world.RegisterSurface(collider,false),Is.True);Assert.That(world.Spawn(bake,new Vector3(0,1,0),Quaternion.identity,Vector3.one,false,Vector2.zero,0,30),Is.True);
                for(int i=1;i<=200;i++)world.AdvanceTo(i*.02f);
                var display=root.transform.GetChild(0);var block=new MaterialPropertyBlock();display.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetColor("_Color").a,Is.EqualTo(settings.DebrisAlpha).Within(.0001f));
                Assert.That(block.GetFloat("_Desaturate"),Is.EqualTo(settings.DebrisDesaturate).Within(.0001f));
                Vector3 rest=display.position;var swing=Guid.NewGuid();
                // A box away from the piece does nothing.
                world.Strike(new Vector2(rest.x+5,rest.y),new Vector2(1,1),1,swing,0,4);world.AdvanceTo(4.02f);
                Assert.That(world.StrikeCount,Is.Zero);
                world.Strike(new Vector2(rest.x,rest.y),new Vector2(2,2),-1,swing,0,4.02f);world.AdvanceTo(4.04f);
                Assert.That(world.StrikeCount,Is.EqualTo(1));
                // The same swing keeps sampling its box on later steps without relaunching the piece.
                world.Strike(new Vector2(rest.x,rest.y),new Vector2(20,20),-1,swing,0,4.04f);world.AdvanceTo(4.06f);
                Assert.That(world.StrikeCount,Is.EqualTo(1));
                for(int i=1;i<=10;i++)world.AdvanceTo(4.06f+i*.02f);
                Assert.That(display.position.x,Is.LessThan(rest.x-.3f),"A left swing launches the piece left.");
                Assert.That(display.position.y,Is.GreaterThan(rest.y+.3f),"The piece flies up off the floor.");
                // The next hit of the combo may launch it again; a blocked point may not.
                world.Strike(display.position,new Vector2(2,2),1,swing,1,4.26f,_=>false);world.AdvanceTo(4.28f);
                Assert.That(world.StrikeCount,Is.EqualTo(1));
                world.Strike(display.position,new Vector2(2,2),1,swing,1,4.28f);world.AdvanceTo(4.30f);
                Assert.That(world.StrikeCount,Is.EqualTo(2));
            }
            Object.Destroy(root);Object.Destroy(floor);Object.Destroy(bake);Object.Destroy(mesh);Object.Destroy(sprite);
        }
        [UnityTest]public IEnumerator RegisteredCompositeInputsResolveToOneActualGeometryProxy()
        {
            yield return new EnterPlayMode();
            var root=new GameObject("Composite proxy fixture");var terrain=new GameObject("Explicit merged source");
            terrain.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;
            var composite=terrain.AddComponent<CompositeCollider2D>();composite.geometryType=CompositeCollider2D.GeometryType.Polygons;composite.generationType=CompositeCollider2D.GenerationType.Manual;
            var box=terrain.AddComponent<BoxCollider2D>();box.size=new Vector2(20,1);box.compositeOperation=Collider2D.CompositeOperation.Merge;composite.GenerateGeometry();
            using(var world=new DebrisWorld(root.transform,Shader.Find("Deviant/HitFeedback/Particle"),Settings(),.02f,0))
            {
                Assert.That(world.RegisterSurface(box,false),Is.True,world.LastError);
                Assert.That(world.RegisterSurface(composite,false),Is.True,world.LastError);
                Assert.That(world.SurfaceCount,Is.EqualTo(1),"Input colliders and their composite cannot become duplicate local terrain.");
            }
            Object.Destroy(root);Object.Destroy(terrain);
        }
    }
}
