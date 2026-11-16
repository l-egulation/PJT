using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.CombatLab.Tests
{
    public sealed class WindowLightGraphicsTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Window render fixture").AddComponent<LightingLabComposition>();
            yield return new EnterPlayMode(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static Color32[] Capture(LightingLabComposition lab, RenderTexture rt, string name)
        {
            lab.SamplePresentation(); lab.View.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../captures/lighting")); Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG()); return texture.GetPixels32();
            }
            finally { RenderTexture.active = old; Object.Destroy(texture); }
        }
        static float BluePatch(Camera camera, Color32[] pixels, RenderTexture rt, Vector2 world)
        {
            var viewport = camera.WorldToViewportPoint(world); int x = Mathf.RoundToInt(viewport.x * rt.width), y = Mathf.RoundToInt(viewport.y * rt.height); float total = 0;
            for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) total += pixels[(y + dy) * rt.width + x + dx].b;
            return total / 25;
        }
        static void IsolateWindow(LightingLabComposition lab)
        {
            foreach (var light in lab.GetComponentsInChildren<Light2D>()) if (light != lab.Window.Emitter.Output && light.lightType != Light2D.LightType.Global) light.enabled = false;
            foreach (var binding in lab.SpriteBindings) binding.ShadowsEnabled = false;
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ActualWindowIlluminationFollowsTheAngleEvenWithTheShaftHidden()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var rt = new RenderTexture(960, 540, 24); rt.Create(); lab.View.targetTexture = rt;
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                driver.Reset(); IsolateWindow(lab); lab.Window.SetShaftVisible(false); lab.Window.SetEnabled(false);
                yield return null; yield return null; var off = Capture(lab, rt, "window-off");
                lab.Window.SetEnabled(true); lab.SetWindowAngle(-30); yield return null; yield return null; var down = Capture(lab, rt, "window-angle-down-light-only");
                var lower = new Vector2(-.5f, .8f); var upper = new Vector2(-.5f, 5);
                Assert.That(BluePatch(lab.View, down, rt, lower) - BluePatch(lab.View, off, rt, lower), Is.GreaterThan(8), "A real Lit receiver must brighten without the shaft overlay.");
                Assert.That(Mathf.Abs(BluePatch(lab.View, down, rt, upper) - BluePatch(lab.View, off, rt, upper)), Is.LessThan(4), "The old radial light must not illuminate the opposite side.");
                lab.SetWindowAngle(25); yield return null; yield return null; var up = Capture(lab, rt, "window-angle-up-light-only");
                Assert.That(BluePatch(lab.View, up, rt, upper) - BluePatch(lab.View, off, rt, upper), Is.GreaterThan(8));
                Assert.That(Mathf.Abs(BluePatch(lab.View, up, rt, lower) - BluePatch(lab.View, off, rt, lower)), Is.LessThan(4));
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ShaftIsOptionalVisibleAndClippedToTheSameFiniteFootprint()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var rt = new RenderTexture(960, 540, 24); rt.Create(); lab.View.targetTexture = rt;
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                driver.Reset(); IsolateWindow(lab); lab.Window.SetShaftVisible(false); yield return null; yield return null;
                var lightOnly = Capture(lab, rt, "window-no-shaft"); lab.Window.SetShaftVisible(true); yield return null;
                var shaft = Capture(lab, rt, "window-area-shaft");
                Assert.That(lightOnly.Zip(shaft, (a, b) => Mathf.Abs(a.b - b.b) > 2).Count(changed => changed), Is.GreaterThan(100));
                var outside = new Vector2(-.5f, 5);
                Assert.That(BluePatch(lab.View, shaft, rt, outside), Is.EqualTo(BluePatch(lab.View, lightOnly, rt, outside)).Within(1));
                lab.SetWindowAngle(-55); yield return null; yield return null; Capture(lab, rt, "window-area-steep");
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }
        static SpriteShadowSource Blocker(LightingLabComposition lab, Vector2 at)
        {
            var root = new GameObject("Parallel window blocker"); root.transform.SetParent(lab.transform, false); root.transform.position = at;
            root.transform.localScale = new Vector3(.12f, .3f, 1);
            var sprite = root.AddComponent<SpriteRenderer>(); sprite.sprite = lab.Arena.TechnicalSprite; sprite.sharedMaterial = lab.Arena.spriteMaterial; sprite.sortingOrder = 5;
            var template = Resources.Load<LightingBindings>("Lighting/Bindings").shadowTemplate;
            var source = Object.Instantiate(template, root.transform, false).GetComponent<SpriteShadowSource>();
            source.Configure(sprite, new[] { new SpriteShadowFrame { sprite = sprite.sprite, vertices = LightingLabComposition.Box(.5f, .5f), edges = LightingLabComposition.Loop(4) } });
            return source;
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowShadowsStayParallelAcrossDifferentOccludersAndReceiverDistances()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var rt = new RenderTexture(960, 540, 24); rt.Create(); lab.View.targetTexture = rt;
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                driver.Reset(); IsolateWindow(lab); lab.SetWindowAngle(0); lab.Window.SetShaftVisible(false);
                var near = Blocker(lab, new Vector2(-2.5f, 2.45f)); var far = Blocker(lab, new Vector2(-.5f, 3.55f));
                lab.Window.SetShadowCasters(new SpriteShadowSource[0]); yield return null; yield return null;
                var clear = Capture(lab, rt, "parallel-window-clear");
                lab.Window.SetShadowCasters(new[] { near, far }); yield return null; yield return null;
                var shadows = Capture(lab, rt, "parallel-window-shadows");
                foreach (float x in new[] { 1.5f, 4.5f }) foreach (float y in new[] { 2.45f, 3.55f })
                {
                    var center = new Vector2(x, y); var outside = new Vector2(x, y + .3f);
                    Assert.That(BluePatch(lab.View, clear, rt, center) - BluePatch(lab.View, shadows, rt, center), Is.GreaterThan(12), "Each shadow center must follow the same horizontal direction at both receiver depths.");
                    Assert.That(Mathf.Abs(BluePatch(lab.View, clear, rt, outside) - BluePatch(lab.View, shadows, rt, outside)), Is.LessThan(3), "Shadows must not widen or fan toward a common point.");
                }
                lab.Window.SetShaftVisible(true); yield return null; Capture(lab, rt, "parallel-window-shaft-shadows");
                Assert.That(lab.Window.Emitter.Output.intensity, Is.Zero); Assert.That(lab.Window.Emitter.Output.shadowsEnabled, Is.False);
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator IdenticalNormalsAtDifferentPositionsReceiveTheSameWindowDirection()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var rt = new RenderTexture(960, 540, 24); rt.Create(); lab.View.targetTexture = rt;
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                driver.Reset(); IsolateWindow(lab); lab.SetWindowAngle(0); lab.Window.SetShaftVisible(false); lab.Window.SetShadowCasters(new SpriteShadowSource[0]);
                foreach (float x in new[] { 0f, 4f })
                {
                    var sprite = new GameObject("Constant-direction normal probe").AddComponent<SpriteRenderer>(); sprite.transform.SetParent(lab.transform, false);
                    sprite.transform.position = new Vector2(x, 3); sprite.transform.localScale = Vector3.one * .35f;
                    sprite.sprite = lab.Arena.TechnicalSprite; sprite.color = Color.gray; sprite.sharedMaterial = lab.Arena.spriteMaterial; sprite.sortingOrder = 15;
                    lab.Window.AddReceiver(sprite);
                }
                yield return null; yield return null; var pixels = Capture(lab, rt, "parallel-window-normal-direction");
                float a = BluePatch(lab.View, pixels, rt, new Vector2(0, 3)), b = BluePatch(lab.View, pixels, rt, new Vector2(4, 3));
                Assert.That(a, Is.GreaterThan(50)); Assert.That(a, Is.EqualTo(b).Within(2), "A receiver farther from the window must not get a different point-origin normal angle.");
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator PaneGapsAffectActualSurfaceLightAndTheShaftRatherThanOnlyDecorativeArt()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var rt = new RenderTexture(960, 540, 24); rt.Create(); lab.View.targetTexture = rt;
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                driver.Reset(); IsolateWindow(lab); lab.SetWindowAngle(0); lab.Window.SetShaftVisible(false); lab.Window.SetShadowCasters(new SpriteShadowSource[0]);
                lab.SetWindowOpening(0); yield return null; yield return null; var full = Capture(lab, rt, "opening-rectangle-surface");
                lab.SetWindowOpening(1); yield return null; yield return null; var panes = Capture(lab, rt, "opening-four-panes-surface");
                foreach (float x in new[] { 1f, 4f })
                {
                    var gap = new Vector2(x, 3); var open = new Vector2(x, 3.5f);
                    Assert.That(BluePatch(lab.View, full, rt, gap) - BluePatch(lab.View, panes, rt, gap), Is.GreaterThan(12));
                    Assert.That(BluePatch(lab.View, full, rt, open), Is.EqualTo(BluePatch(lab.View, panes, rt, open)).Within(2));
                }
                lab.Window.SetShaftVisible(true); yield return null; var shaft = Capture(lab, rt, "opening-four-panes-shaft");
                // The 5x5 patch can include the intended soft rim of the narrow bar.
                Assert.That(BluePatch(lab.View, shaft, rt, new Vector2(1, 3)), Is.EqualTo(BluePatch(lab.View, panes, rt, new Vector2(1, 3))).Within(3));
                Assert.That(BluePatch(lab.View, shaft, rt, new Vector2(1, 3.5f)), Is.GreaterThan(BluePatch(lab.View, panes, rt, new Vector2(1, 3.5f))));
                lab.SetWindowOpening(2); lab.SetWindowAngle(-45); yield return null; Capture(lab, rt, "opening-polygon-arch");
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator TwoWindowsAddOnTheSameSurfaceAndRemovingOnePreservesTheOther()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var rt = new RenderTexture(960, 540, 24); rt.Create(); lab.View.targetTexture = rt;
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                driver.Reset(); IsolateWindow(lab); lab.SetWindowAngle(0); lab.Window.SetShaftVisible(false); lab.SecondWindow.SetShaftVisible(false);
                lab.Window.SetShadowCasters(new SpriteShadowSource[0]); lab.SecondWindow.SetShadowCasters(new SpriteShadowSource[0]);
                lab.Window.SetEnabled(false); yield return null; yield return null; var off = Capture(lab, rt, "two-windows-off");
                lab.Window.SetEnabled(true); yield return null; var left = Capture(lab, rt, "two-windows-left-only");
                lab.SetSecondWindow(true); yield return null; var both = Capture(lab, rt, "two-windows-combined");
                lab.Window.enabled = false; yield return null; var right = Capture(lab, rt, "two-windows-right-only");
                var at = new Vector2(1, 3);
                float baseline = BluePatch(lab.View, off, rt, at), a = BluePatch(lab.View, left, rt, at), b = BluePatch(lab.View, right, rt, at), sum = BluePatch(lab.View, both, rt, at);
                Assert.That(a - baseline, Is.GreaterThan(8)); Assert.That(b - baseline, Is.GreaterThan(8));
                Assert.That(sum, Is.GreaterThan(Mathf.Max(a, b) + 5));
                lab.SecondWindow.RemoveReceiver(lab.GetComponentsInChildren<SpriteRenderer>().First(sprite => sprite.name == "Rear wall right"));
                yield return null; var removed = Capture(lab, rt, "two-windows-detached");
                Assert.That(BluePatch(lab.View, removed, rt, at), Is.EqualTo(baseline).Within(2));
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator APointTransmissionCookieClipsRealLanternLightAndCanBeRestored()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var rt = new RenderTexture(960, 540, 24); rt.Create(); lab.View.targetTexture = rt;
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            // An asymmetric non-rectangular aperture; alpha alone is not a URP point cookie.
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) pixels[y * 32 + x] = x > 16 && Mathf.Abs(y - 16) < x - 16 ? Color.white : Color.black;
            texture.SetPixels(pixels); texture.Apply(); var mask = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 32);
            using (var driver = lab.Arena.AcquireManualSimulation())
            try
            {
                driver.Reset(); lab.Window.SetEnabled(false); lab.SecondWindow.SetEnabled(false);
                var emitter = lab.Lantern.GetComponentInChildren<LightEmitterView>();
                foreach (var light in lab.GetComponentsInChildren<Light2D>()) if (light != emitter.Output && light.lightType != Light2D.LightType.Global) light.enabled = false;
                emitter.Output.shadowsEnabled = false; emitter.Output.enabled = false; yield return null; yield return null;
                var off = Capture(lab, rt, "lantern-cookie-off"); emitter.Output.enabled = true; yield return null;
                var clear = Capture(lab, rt, "lantern-cookie-clear");
                float now = lab.Arena.Now; float angle = lab.Lantern.State.Angle;
                emitter.SetTransmissionMask(mask); yield return null; yield return null; var clipped = Capture(lab, rt, "lantern-cookie-polygon");
                Assert.That(clear.Zip(clipped, (a, b) => a.r - b.r > 12).Count(value => value), Is.GreaterThan(1000), "A cookie must suppress actual point lighting, not only change the flame sprite.");
                Assert.That(clipped.Zip(off, (a, b) => a.r - b.r > 8).Count(value => value), Is.GreaterThan(100), "The open polygon must still transmit light, not black out the whole emitter.");
                emitter.SetTransmissionMask(null); yield return null; var restored = Capture(lab, rt, "lantern-cookie-restored");
                Assert.That(clear.Zip(restored, (a, b) => Mathf.Abs(a.r - b.r) > 2).Count(value => value), Is.LessThan(20));
                Assert.That(lab.Arena.Now, Is.EqualTo(now)); Assert.That(lab.Lantern.State.Angle, Is.EqualTo(angle));
                Assert.That(emitter.Output.lightType, Is.EqualTo(Light2D.LightType.Point));
            }
            finally { lab.View.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(mask); Object.Destroy(texture); }
        }
    }
}
