using System;
using System.Collections;
using System.Collections.Generic;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.CombatLab.Tests
{
    public sealed class WindowLightIntegrationTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Window integration fixture").AddComponent<LightingLabComposition>();
            yield return new EnterPlayMode(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator AWindowUsesAnAngledFreeformOverTheEntireOpeningAndClipsToTheRoom()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var view = lab.Window;
            Assert.That(view.Emitter.Output.lightType, Is.EqualTo(Light2D.LightType.Freeform)); Assert.That(view.FootprintVertexCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(view.Emitter.NativeContributionEnabled, Is.False); Assert.That(view.Emitter.Output.intensity, Is.Zero);
            Assert.That(view.Emitter.Output.shadowsEnabled, Is.False); Assert.That(view.Emitter.ComputedIntensity, Is.GreaterThan(0));
            Assert.That(view.ContainsWorldPoint(new Vector2(-.5f, .8f)), Is.True);
            Assert.That(view.ContainsWorldPoint(new Vector2(-.5f, 4.8f)), Is.False);
            for (int i = 0; i < view.FootprintVertexCount; i++)
            {
                Vector3 p = view.GetFootprintWorldPoint(i);
                Assert.That(p.x, Is.InRange(-8.0001f, 8.0001f)); Assert.That(p.y, Is.InRange(-.5001f, 6.0001f));
            }
            Assert.That(view.GetComponentsInChildren<Collider2D>(), Is.Empty); Assert.That(view.GetComponentsInChildren<Rigidbody2D>(), Is.Empty);
            yield return null;
        }
        [UnityTest] public IEnumerator RepeatedPresentationDoesNotRebuildAndAngleChangesDoNotTouchCombat()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var view = lab.Window;
            using (var driver = lab.Arena.AcquireManualSimulation())
            {
                driver.Reset(); int builds = view.RebuildCount; float now = lab.Arena.Now, health = lab.Arena.Player.health; var position = lab.Arena.Player.Position;
                for (int i = 0; i < 100; i++) lab.SamplePresentation(); Assert.That(view.RebuildCount, Is.EqualTo(builds));
                lab.SetWindowAngle(25); Assert.That(view.RebuildCount, Is.EqualTo(builds + 1));
                Assert.That(view.ContainsWorldPoint(new Vector2(-.5f, .8f)), Is.False); Assert.That(view.ContainsWorldPoint(new Vector2(-.5f, 5)), Is.True);
                lab.SetWindowAngle(25); Assert.That(view.RebuildCount, Is.EqualTo(builds + 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => lab.SetWindowAngle(float.NaN)); Assert.That(view.AngleDegrees, Is.EqualTo(25));
                Assert.That(lab.Arena.Now, Is.EqualTo(now)); Assert.That(lab.Arena.Player.health, Is.EqualTo(health)); Assert.That(lab.Arena.Player.Position, Is.EqualTo(position));
                lab.SetPaused(true); yield return null; lab.SetWindowAngle(-45); lab.SamplePresentation(); Assert.That(lab.Arena.Now, Is.EqualTo(now));
                lab.SetPaused(false);
            }
        }
        [UnityTest] public IEnumerator LightSwitchesAndComponentLifetimeDoNotLeaveAnOrphanedShaft()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var view = lab.Window;
            lab.SetLights(false); view.SampleNow(); Assert.That(view.Emitter.Output.enabled, Is.False);
            foreach (var renderer in view.GetComponentsInChildren<MeshRenderer>()) Assert.That(renderer.enabled, Is.False);
            lab.SetWindowAngle(-60); Assert.That(view.Emitter.Output.enabled, Is.False, "An angle rebuild cannot revive disabled lighting.");
            lab.SetLights(true); Assert.That(view.Emitter.Output.enabled, Is.True);
            view.SetShaftVisible(false); foreach (var renderer in view.GetComponentsInChildren<MeshRenderer>()) Assert.That(renderer.enabled, Is.False);
            Assert.That(view.Emitter.Output.enabled, Is.True, "The real illumination must not depend on the decorative shaft.");
            view.enabled = false; Assert.That(view.Emitter.Output.enabled, Is.False);
            view.enabled = true; Assert.That(view.Emitter.Output.enabled, Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator ParallelParametersAreReceiverScopedAndTrackActorPoseWithoutAWindowPointLight()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var window = lab.Window;
            using (var driver = lab.Arena.AcquireManualSimulation())
            {
                driver.Reset(); lab.SamplePresentation(); Assert.That(window.ShadowEdgeCount, Is.GreaterThan(0)); Assert.That(window.ShadowEdgeOverflow, Is.False);
                Assert.That(window.ReceiverCount, Is.GreaterThan(3)); Assert.That(window.Emitter.Output.enabled, Is.True);
                var block = new MaterialPropertyBlock(); lab.Arena.Player.visual.GetPropertyBlock(block);
                Assert.That(block.GetVectorArray("_WindowData")[0].x, Is.EqualTo(1)); Assert.That(block.GetVectorArray("_WindowShadow")[0].z, Is.GreaterThan(0));
                var direction = block.GetVectorArray("_WindowToLight")[0];
                lab.Arena.Player.Position += Vector2.right * 2; lab.SamplePresentation(); lab.Arena.Player.visual.GetPropertyBlock(block);
                Assert.That(block.GetVectorArray("_WindowToLight")[0], Is.EqualTo(direction), "Moving a receiver cannot rotate the incoming light vector toward a point.");
                window.SetEnabled(false); lab.Arena.Player.visual.GetPropertyBlock(block); Assert.That(block.GetVectorArray("_WindowData")[0].x, Is.Zero);
                Assert.That(window.Emitter.Output.intensity, Is.Zero);
                window.SetEnabled(true); lab.SamplePresentation(); Assert.That(window.Emitter.Output.intensity, Is.Zero);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ExceedingTheDirectionalEdgeBudgetDisablesTheWindowInsteadOfFallingBackToAPoint()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var view = lab.Window;
            var template = Resources.Load<LightingBindings>("Lighting/Bindings").shadowTemplate;
            var sources = new List<SpriteShadowSource>();
            for (int i = 0; i < 33; i++)
            {
                var renderer = new GameObject("Window budget caster " + i).AddComponent<SpriteRenderer>(); renderer.transform.SetParent(lab.transform, false);
                renderer.sprite = lab.Arena.TechnicalSprite; renderer.sharedMaterial = lab.Arena.spriteMaterial;
                var source = Object.Instantiate(template, renderer.transform, false).GetComponent<SpriteShadowSource>();
                source.Configure(renderer, new[] { new SpriteShadowFrame { sprite = renderer.sprite, vertices = LightingLabComposition.Box(.5f, .5f), edges = LightingLabComposition.Loop(4) } });
                sources.Add(source);
            }
            LogAssert.Expect(LogType.Warning, "Directional window edge budget exceeded; disabling this window instead of dropping shadow casters.");
            view.SetShadowCasters(sources); Assert.That(view.ShadowEdgeOverflow, Is.True);
            var block = new MaterialPropertyBlock(); lab.Arena.Player.visual.GetPropertyBlock(block); Assert.That(block.GetVectorArray("_WindowData")[0].x, Is.Zero);
            Assert.That(view.Emitter.Output.intensity, Is.Zero); Assert.That(view.Emitter.Output.shadowsEnabled, Is.False);
            view.SetShadowCasters(new SpriteShadowSource[0]); Assert.That(view.ShadowEdgeOverflow, Is.False);
            lab.Arena.Player.visual.GetPropertyBlock(block); Assert.That(block.GetVectorArray("_WindowData")[0].x, Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator WindowsComposeWithoutOverwritingOtherOwnersAndCleanUpIndependently()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var sprite = lab.Arena.Player.visual;
            var block = new MaterialPropertyBlock(); sprite.GetPropertyBlock(block); block.SetFloat("_UnrelatedLightingTest", 37); sprite.SetPropertyBlock(block);
            using (var driver = lab.Arena.AcquireManualSimulation())
            {
                float now = lab.Arena.Now; var position = lab.Arena.Player.Position;
                lab.SetSecondWindow(true); lab.SamplePresentation(); sprite.GetPropertyBlock(block);
                Assert.That(block.GetInteger("_WindowCount"), Is.EqualTo(2)); Assert.That(block.GetVectorArray("_WindowData")[1].x, Is.EqualTo(1));
                lab.Window.enabled = false; sprite.GetPropertyBlock(block);
                Assert.That(block.GetVectorArray("_WindowData")[0].x, Is.Zero); Assert.That(block.GetVectorArray("_WindowData")[1].x, Is.EqualTo(1));
                lab.Window.enabled = true; lab.Window.RemoveReceiver(sprite); sprite.GetPropertyBlock(block);
                Assert.That(block.GetInteger("_WindowCount"), Is.EqualTo(1)); Assert.That(block.GetVectorArray("_WindowData")[0].x, Is.EqualTo(1));
                lab.Window.AddReceiver(sprite); lab.Window.AddReceiver(sprite); Assert.That(sprite.GetComponent<WindowLightReceiver>().WindowCount, Is.EqualTo(2));
                lab.SetWindowOpening(1); lab.SetWindowAngle(0);
                Assert.That(lab.Window.ContainsWorldPoint(new Vector2(1, 3)), Is.False);
                Assert.That(lab.Window.ContainsWorldPoint(new Vector2(1, 3.5f)), Is.True);
                lab.SetWindowOpening(2); Assert.That(lab.Arena.Now, Is.EqualTo(now)); Assert.That(lab.Arena.Player.Position, Is.EqualTo(position));
                Assert.Throws<ArgumentNullException>(() => lab.Window.SetAperture(null));
                sprite.GetPropertyBlock(block); Assert.That(block.GetFloat("_UnrelatedLightingTest"), Is.EqualTo(37));
                Object.Destroy(lab.SecondWindow); yield return null; sprite.GetPropertyBlock(block);
                Assert.That(block.GetInteger("_WindowCount"), Is.EqualTo(1)); Assert.That(sprite.GetComponent<WindowLightReceiver>().WindowCount, Is.EqualTo(1));
            }
        }
        [UnityTest] public IEnumerator ReceiverCapacityRejectsExtraWindowsWithoutDisturbingExistingBindings()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var sprite = lab.Arena.Player.visual;
            var bindings = Resources.Load<LightingBindings>("Lighting/Bindings");
            var settings = new Deviant.Definitions.WindowLightDefinition(new Deviant.Definitions.WindowLightData {
                angleDegrees = 0, length = 10, edgeFeather = .1f, beamOpacity = .1f, endFade = 1 });
            WindowLightView rejected = null;
            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject("Capacity window " + i); root.transform.SetParent(lab.transform, false);
                var emitter = Object.Instantiate(bindings.windowTemplate, root.transform); emitter.Bind(lab.Window.Emitter.Definition, 123);
                var view = root.AddComponent<WindowLightView>(); view.Bind(new Rect(-1, -1, 2, 2), new Rect(-8, -4, 16, 10), settings, emitter, bindings.windowBeamShader, 0, -25);
                if (i < 2) view.AddReceiver(sprite); else rejected = view;
            }
            Assert.Throws<InvalidOperationException>(() => rejected.AddReceiver(sprite)); Assert.That(rejected.ReceiverCount, Is.Zero);
            Assert.That(sprite.GetComponent<WindowLightReceiver>().WindowCount, Is.EqualTo(4));
            var block = new MaterialPropertyBlock(); sprite.GetPropertyBlock(block); Assert.That(block.GetInteger("_WindowCount"), Is.EqualTo(4));
            yield return null;
        }
        [UnityTest] public IEnumerator InvalidPointCookiesKeepThePreviousMaskAndWindowCookiesAreRejected()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var emitter = lab.Lantern.GetComponentInChildren<LightEmitterView>();
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp };
            var full = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * .5f);
            var cropped = Sprite.Create(texture, new Rect(0, 0, 4, 8), Vector2.one * .5f);
            try
            {
                emitter.SetTransmissionMask(full); Assert.That(emitter.Output.lightCookieSprite, Is.SameAs(full));
                Assert.Throws<ArgumentException>(() => emitter.SetTransmissionMask(cropped));
                Assert.That(emitter.TransmissionMask, Is.SameAs(full)); Assert.That(emitter.Output.lightCookieSprite, Is.SameAs(full));
                Assert.Throws<InvalidOperationException>(() => lab.Window.Emitter.SetTransmissionMask(full));
                emitter.SetTransmissionMask(null); Assert.That(emitter.Output.lightCookieSprite, Is.Null);
            }
            finally { Object.Destroy(full); Object.Destroy(cropped); Object.Destroy(texture); }
            yield return null;
        }
        [UnityTest] public IEnumerator ArtBindingsCaptureCoordinatesAndReceiverReuseLeavesNoOldWindowContribution()
        {
            var lab = Object.FindAnyObjectByType<LightingLabComposition>(); var sprite = lab.Arena.Player.visual;
            var art = ScriptableObject.CreateInstance<WindowApertureArt>();
            art.openings = new[] { new WindowOpeningPiece { vertices = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(0, 1) } } };
            try
            {
                var shape = art.Capture(); art.openings[0].vertices[0] = Vector2.one * 500;
                Assert.That(shape.GetVertex(0, 0).X, Is.EqualTo(-1));
                lab.Window.SetAperture(shape); lab.SetSecondWindow(true);
                var receiver = sprite.GetComponent<WindowLightReceiver>(); var block = new MaterialPropertyBlock();
                receiver.enabled = false; sprite.GetPropertyBlock(block); Assert.That(block.GetInteger("_WindowCount"), Is.Zero);
                receiver.enabled = true; sprite.GetPropertyBlock(block); Assert.That(block.GetInteger("_WindowCount"), Is.EqualTo(2));
                lab.Window.RemoveReceiver(sprite); lab.SecondWindow.RemoveReceiver(sprite);
                sprite.GetPropertyBlock(block); Assert.That(block.GetInteger("_WindowCount"), Is.Zero);
                lab.SecondWindow.AddReceiver(sprite); sprite.GetPropertyBlock(block);
                Assert.That(block.GetInteger("_WindowCount"), Is.EqualTo(1)); Assert.That(receiver.WindowCount, Is.EqualTo(1));
            }
            finally { Object.Destroy(art); }
            yield return null;
        }
    }
}
