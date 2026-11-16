using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.CombatLab.Tests
{
    public sealed class LightingLabGraphicsTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Lighting render fixture").AddComponent<LightingLabComposition>();
            yield return new EnterPlayMode(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static int Changed(Color32[] a, Color32[] b) => a.Zip(b, (x, y) => Mathf.Abs(x.r - y.r) + Mathf.Abs(x.g - y.g) + Mathf.Abs(x.b - y.b) > 6).Count(value => value);
        static void Render(LightingLabComposition lab) { lab.SamplePresentation(); lab.View.Render(); }
        static Color32[] Read(RenderTexture rt, string capture = null)
        {
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
                if (capture != null)
                {
                    string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../captures/lighting")); Directory.CreateDirectory(folder);
                    File.WriteAllBytes(Path.Combine(folder, capture + ".png"), texture.EncodeToPNG());
                }
                return texture.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.Destroy(texture); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ZoneDistanceBlendChangesActualAmbientPixelsAndRestoresTheOutsideState()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var rt = new RenderTexture(960, 540, 24); rt.Create(); lab.View.targetTexture = rt;
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                driver.Reset(); lab.SetLights(false); lab.SetZones(true); var zone = lab.ZoneDefinitions[0];
                lab.Arena.enemies[0].Position = new Vector2(7.5f, 0);
                var probe = lab.View.WorldToViewportPoint(new Vector2(-6, 3)); int pixel = Mathf.RoundToInt(probe.y * rt.height) * rt.width + Mathf.RoundToInt(probe.x * rt.width);
                yield return null; Render(lab); var outside = Read(rt, "zone-outside");
                lab.Arena.Player.Position = new Vector2(zone.Left + zone.BlendWidth * .5f, 0); Physics2D.SyncTransforms();
                for (int i = 0; i < 35; i++) driver.Step(.02f);
                yield return null; Render(lab); var half = Read(rt, "zone-half-blend");
                lab.Arena.Player.Position = new Vector2(zone.Left + zone.BlendWidth + .5f, 0); Physics2D.SyncTransforms();
                for (int i = 0; i < 35; i++) driver.Step(.02f);
                yield return null; Render(lab); var full = Read(rt, "zone-core");
                Assert.That(half[pixel].r, Is.GreaterThan(outside[pixel].r + 3)); Assert.That(full[pixel].r, Is.GreaterThan(half[pixel].r + 3));
                lab.Arena.Player.Position = new Vector2(zone.Left - zone.ExitHysteresis - 1, 0); Physics2D.SyncTransforms();
                for (int i = 0; i < 35; i++) driver.Step(.02f);
                yield return null; Render(lab); var restored = Read(rt, "zone-restored-outside");
                Assert.That(restored[pixel].r, Is.EqualTo(outside[pixel].r).Within(1));
                Assert.That(restored[pixel].g, Is.EqualTo(outside[pixel].g).Within(1)); Assert.That(restored[pixel].b, Is.EqualTo(outside[pixel].b).Within(1));
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator Actual2DRendererUsesNormalsAndBothActorShadowCasters()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); Assert.That(lab.Ready, Is.True);
            var rt = new RenderTexture(960, 540, 24) { filterMode = FilterMode.Point }; rt.Create(); lab.View.targetTexture = rt;
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                yield return null; Render(lab); var normal = Read(rt, "indoor-normal-shadow");
                foreach (var binding in lab.SpriteBindings)
                    Assert.That(binding.GetComponent<SpriteRenderer>().sprite.GetSecondaryTextureCount(), Is.EqualTo(1), "The displayed normal frame must carry its secondary texture.");
                foreach (var light in lab.GetComponentsInChildren<Light2D>())
                    if (light.lightType != Light2D.LightType.Global) Assert.That(light.normalMapQuality, Is.EqualTo(Light2D.NormalMapQuality.Accurate));
                Assert.That(normal.Count(pixel => pixel.r > 30 || pixel.g > 30 || pixel.b > 30), Is.GreaterThan(1000));
                lab.SetNormals(false); yield return null; Render(lab); var flat = Read(rt, "indoor-flat");
                foreach (var binding in lab.SpriteBindings)
                {
                    var sprite = binding.GetComponent<SpriteRenderer>().sprite;
                    var secondary = new SecondarySpriteTexture[sprite.GetSecondaryTextureCount()]; sprite.GetSecondaryTextures(secondary);
                    var flatNormal = secondary.Single(texture => texture.name == "_NormalMap").texture.GetPixel(0, 0);
                    Assert.That(flatNormal.r, Is.EqualTo(.5f).Within(.01f)); Assert.That(flatNormal.g, Is.EqualTo(.5f).Within(.01f)); Assert.That(flatNormal.b, Is.EqualTo(1));
                }
                Assert.That(Changed(normal, flat), Is.GreaterThan(10), "The normal-map pass must change real lit Sprite pixels.");
                lab.SetNormals(true); yield return null; Render(lab); var shadows = Read(rt);
                for (int i = 0; i < lab.SpriteBindings.Count; i++)
                {
                    var binding = lab.SpriteBindings[i]; binding.ShadowsEnabled = false;
                    yield return null; Render(lab); var without = Read(rt, i == 0 ? "without-player-shadow" : "without-monster-shadow");
                    Assert.That(Changed(shadows, without), Is.GreaterThan(10), "Each actor must cast a visible shadow, not only the static walls.");
                    binding.ShadowsEnabled = true; yield return null; Render(lab); shadows = Read(rt);
                }
                lab.SetSunset(true); for (int i = 0; i < 45; i++) driver.Step(.02f);
                yield return null; Render(lab); var sunset = Read(rt, "sunset");
                Assert.That(Changed(normal, sunset), Is.GreaterThan(1000));
                Assert.That(lab.GetComponentsInChildren<SpriteShadowSource>().Sum(source => source.ShapeUploads), Is.GreaterThan(0));
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator CachedFrameShapesSurviveAnimationAndFlipsWithoutRepeatedMeshUploads()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var rt = new RenderTexture(320, 180, 24); rt.Create(); lab.View.targetTexture = rt;
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                yield return null; Render(lab);
                driver.Reset(); driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Basic));
                Assert.That(lab.Arena.Player.currentCast, Is.Not.Null);
                yield return null; Render(lab);
                var sources = lab.SpriteBindings[0].GetComponentsInChildren<SpriteShadowSource>();
                Assert.That(sources.Length, Is.EqualTo(2), "One cached caster per authored shape.");
                int warm = sources.Sum(source => source.ShapeUploads); Assert.That(warm, Is.EqualTo(2));
                for (int i = 0; i < 4; i++)
                {
                    driver.Reset(); lab.Arena.Player.SyncFacing(i % 2 == 0 ? -1 : 1);
                    lab.SetNormals(i % 2 == 0); yield return null; Render(lab);
                    driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Basic)); yield return null; Render(lab);
                }
                Assert.That(sources.Sum(source => source.ShapeUploads), Is.EqualTo(warm), "Switch cached casters; never rebuild a mesh for every displayed animation frame.");
                Assert.That(sources.All(source => !source.MissingFrame), Is.True);
                foreach (var binding in lab.SpriteBindings) Assert.That(binding.MissingFrame, Is.False);
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator AcceptedMeleeMovesTheLightSocketAndChangesTheRenderedRoom()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var rt = new RenderTexture(960, 540, 24); rt.Create(); lab.View.targetTexture = rt;
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                driver.Reset(); yield return null; Render(lab); var before = Read(rt);
                var light = lab.Lantern.GetComponentInChildren<LightEmitterView>(); var initial = light.transform.position;
                driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Basic));
                for (int i = 0; i < 14; i++) driver.Step(.02f);
                Assert.That(lab.Lantern.State.AcceptedAttacks, Is.EqualTo(1));
                Assert.That(Vector3.Distance(initial, light.transform.position), Is.GreaterThan(.05f), "The actual Light2D must follow the swinging socket, not remain at the ceiling anchor.");
                yield return null; Render(lab); var after = Read(rt, "melee-swing");
                Assert.That(Changed(before, after), Is.GreaterThan(100));
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ChainLinksAndCurvedRopeHaveDistinctVisiblePixelsAndStayAttached()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var rt = new RenderTexture(960, 540, 24); rt.Create(); lab.View.targetTexture = rt;
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                driver.Reset(); lab.SetConnectorStyle(HangingConnectorStyle.Chain); yield return null; Render(lab); var chain = Read(rt, "chain-links");
                lab.SetConnectorStyle(HangingConnectorStyle.Rope); yield return null; Render(lab); var rope = Read(rt, "rope");
                Assert.That(Changed(chain, rope), Is.GreaterThan(20), "Switching styles must replace the solid bar with real link/rope pixels.");
                driver.Step(.02f, new CombatInput(pressed: CombatButtons.Weapon1Basic)); for (int i = 0; i < 15; i++) driver.Step(.02f);
                yield return null; Render(lab); Read(rt, "rope-bending");
                Assert.That(Mathf.Abs(lab.Suspension.Bend), Is.GreaterThan(.001f));
                Assert.That(Vector3.Distance(lab.Suspension.GetCurveWorldPoint(0), lab.Suspension.StartSocket.position), Is.LessThan(1e-5));
                Assert.That(Vector3.Distance(lab.Suspension.GetCurveWorldPoint(lab.Suspension.CurvePointCount - 1), lab.Suspension.EndSocket.position), Is.LessThan(1e-5));
                lab.SetConnectorStyle(HangingConnectorStyle.Chain); yield return null; Render(lab); Read(rt, "chain-bending");
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator SoftShadowsProduceIntermediatePenumbraWithoutChangingSpritesOrCombat()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var rt = new RenderTexture(960, 540, 24); rt.Create(); lab.View.targetTexture = rt;
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                driver.Reset();
                var emitter = lab.Lantern.GetComponentInChildren<LightEmitterView>(); var light = emitter.Output;
                foreach (var other in lab.GetComponentsInChildren<Light2D>())
                    if (other != light && other.lightType != Light2D.LightType.Global) other.enabled = false;
                float softness = light.shadowSoftness;
                Assert.That(softness, Is.GreaterThan(0)); Assert.That(light.shadowSoftnessFalloffIntensity, Is.EqualTo(.5f)); Assert.That(light.falloffIntensity, Is.EqualTo(.6f));
                lab.SamplePresentation();
                var sprite = lab.Arena.Player.visual.sprite; var material = lab.Arena.Player.visual.sharedMaterial;
                float now = lab.Arena.Now, health = lab.Arena.Player.health;
                // URP's penumbra extends outward from the fully shadowed core. Use
                // ambient-only and unshadowed references, not an assumption that a
                // soft shadow must brighten pixels inside the old hard silhouette.
                float intensity = light.intensity; light.intensity = 0;
                yield return null; Render(lab); var ambientOnly = Read(rt, "shadow-ambient-reference");
                light.intensity = intensity; light.shadowIntensity = 1;
                light.shadowsEnabled = false; yield return null; Render(lab); var clear = Read(rt, "shadow-unoccluded-reference");
                light.shadowsEnabled = true; light.shadowSoftness = 0; yield return null; Render(lab); var hard = Read(rt, "hard-shadow-boundary");
                light.shadowSoftness = softness; yield return null; Render(lab); var soft = Read(rt, "soft-shadow-boundary");
                int partial = 0;
                for (int i = 0; i < soft.Length; i++)
                {
                    float lit = Luminance(clear[i]), dark = Luminance(ambientOnly[i]), hardValue = Luminance(hard[i]), value = Luminance(soft[i]);
                    bool hardEndpoint = Mathf.Abs(hardValue - lit) < 1.5f || Mathf.Abs(hardValue - dark) < 1.5f;
                    if (lit - dark > 8 && hardEndpoint && value > dark + 2 && value < lit - 2) partial++;
                }
                Assert.That(partial, Is.GreaterThan(20), "Shadow edges must contain intermediate light levels, not only a moved binary silhouette.");
                Assert.That(Changed(hard, soft), Is.GreaterThan(20));
                Assert.That(lab.Arena.Player.visual.sprite, Is.SameAs(sprite)); Assert.That(lab.Arena.Player.visual.sharedMaterial, Is.SameAs(material));
                Assert.That(material.GetFloat("_OutlineStrength"), Is.EqualTo(1));
                Assert.That(lab.Arena.Now, Is.EqualTo(now)); Assert.That(lab.Arena.Player.health, Is.EqualTo(health));
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }
        static float Luminance(Color32 color) => color.r * .2126f + color.g * .7152f + color.b * .0722f;
    }
}
