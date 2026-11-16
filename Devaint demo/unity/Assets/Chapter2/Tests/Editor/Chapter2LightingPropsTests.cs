using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Chapter2.Editor;
using Deviant.Combat.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    public sealed class Chapter2LightingPropsTests
    {
        // v26 지하수로 화풍(부품이 배치된 경우)은 창·시험 조명 소품을 쓰지 않는다(2026-11-12 사용자 결정). 예전 화풍에서만 확인한다.
        static void SkipUnderModernSewer()
        {
            if (Resources.Load<Texture2D>(Chapter2WaterwayArt.ModernRoot + "terrain-atlas")) Assert.Ignore("v26 sewer look has no windows or trial lighting props.");
        }
        [UnityTest] public IEnumerator ExistingWaterwayWindowsHaveVisibleLightShafts()
        {
            EditorSceneManager.playModeStartScene = null; Chapter2Tools.Create();
            Object.FindAnyObjectByType<Chapter2World>().seed = 17;
            SkipUnderModernSewer();
            yield return new EnterPlayMode();
            for (int i = 0; i < 120 && !Object.FindAnyObjectByType<Chapter2World>().Ready; i++) yield return null;
            var world = Object.FindAnyObjectByType<Chapter2World>();
            var frames = world.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == "Waterway · window-frame").ToArray();
            var lights = world.GetComponentsInChildren<WindowLightView>();
            Assert.That(frames.Length, Is.GreaterThan(0), "This seed must exercise actual waterway windows.");
            Assert.That(lights.Length, Is.EqualTo(frames.Length), "Existing city/frame sprites alone do not create window light.");
            foreach (var light in lights)
            {
                Assert.That(light.FootprintVertexCount, Is.GreaterThanOrEqualTo(3));
                Assert.That(light.ShadowEdgeCount, Is.EqualTo(24), "All six authored grille bars must block the shaft.");
                Assert.That(light.ReceiverCount, Is.GreaterThan(0), "A shaft alone must not stand in for actual wall illumination.");
            }
            using (var driver = world.arena.AcquireManualSimulation())
            {
                world.arena.Player.Position = (Vector2)lights[0].transform.position + new Vector2(1, -3);
                world.CameraStep(0, true); yield return null; yield return null; yield return null;
                long on = 0, surface = 0, off = 0;
                Capture(world.sceneCamera, "window-on", pixels => on = pixels.Sum(p => (long)p.r + p.g + p.b));
                foreach (var light in lights) light.SetShaftVisible(false);
                Capture(world.sceneCamera, "window-surface", pixels => surface = pixels.Sum(p => (long)p.r + p.g + p.b));
                foreach (var light in lights) light.SetEnabled(false);
                Capture(world.sceneCamera, "window-off", pixels => off = pixels.Sum(p => (long)p.r + p.g + p.b));
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("C2_LIGHTING_CAPTURE")) && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Assert.That(on, Is.GreaterThan(surface + 2000), "The native game image must contain a visible shaft.");
                    Assert.That(surface, Is.GreaterThan(off + 100), "Turning off the decorative shaft must leave actual wall lighting.");
                    Debug.Log("WATERWAY_WINDOW_NATIVE_ENERGY on=" + on + " surface=" + surface + " off=" + off);
                }
            }
            yield return new ExitPlayMode();
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [Test] public void ImportedKitKeepsWholeTextureUvs()
        {
            foreach (var id in new[] { "lantern-housing", "lantern-mount", "chain-face", "chain-edge", "torch-hardware", "flame", "contact-glint" })
            {
                var sprite = Resources.Load<Sprite>("Chapter2Lighting/" + id);
                Debug.Log("LIGHTING_IMPORT " + id + " texture=" + sprite.texture.width + "x" + sprite.texture.height + " rect=" + sprite.rect + " uv=" + string.Join(",", sprite.uv.Select(p => p.ToString("F3"))));
                Assert.That(sprite.rect.width, Is.EqualTo(sprite.texture.width));
                Assert.That(sprite.rect.height, Is.EqualTo(sprite.texture.height));
                Assert.That(sprite.uv.Min(p => p.x), Is.EqualTo(0).Within(.001f));
                Assert.That(sprite.uv.Max(p => p.x), Is.EqualTo(1).Within(.001f));
                Assert.That(sprite.uv.Min(p => p.y), Is.EqualTo(0).Within(.001f));
                Assert.That(sprite.uv.Max(p => p.y), Is.EqualTo(1).Within(.001f));
            }
        }
        [UnityTest] public IEnumerator TrialPropsRespondToRealBodyAndMeleeWithoutBlockingOrDamage()
        {
            EditorSceneManager.playModeStartScene = null;
            Chapter2Tools.Create();
            Object.FindAnyObjectByType<Chapter2World>().seed = 17;
            SkipUnderModernSewer();
            yield return new EnterPlayMode();
            for (int i = 0; i < 120 && !Object.FindAnyObjectByType<Chapter2World>().Ready; i++) yield return null;
            var world = Object.FindAnyObjectByType<Chapter2World>();
            Assert.That(world.Ready, Is.True);
            var props = world.GetComponent<Chapter2LightingProps>();
            Assert.That(props.Lanterns.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(props.TorchCount, Is.GreaterThanOrEqualTo(1));
            // Native renderer uploads for new SpriteRenderers complete across frames.
            // Rendering immediately in the startup coroutine can sample a previous sprite texture.
            yield return null; yield return null; yield return null;
            world.CameraStep(0, true);
            Capture(world.sceneCamera, "start-with-props");
            props.enabled = false; Capture(world.sceneCamera, "start-before-props"); props.enabled = true;
            var lamp = props.Lanterns[0];
            Assert.That(lamp.GetComponentsInChildren<Collider2D>(), Is.Empty, "The prop must not become terrain or a hurtbox.");
            var player = world.arena.Player;
            using (var driver = world.arena.AcquireManualSimulation())
            {
                driver.Reset();
                player.Position = lamp.ContactCenter - new Vector2(2.4f, 1); player.velocity = Vector2.zero;
                Physics2D.SyncTransforms(); float health = player.health;
                for (int i = 0; i < 75; i++) driver.Step(1f / 120, new CombatInput(1));
                Assert.That(lamp.BodyContacts, Is.GreaterThan(0), "Actual motor movement should shake the lantern.");
                Assert.That(player.Position.x, Is.GreaterThan(lamp.transform.position.x + .5f), "Player passes through the non-blocking lantern.");
                Assert.That(player.health, Is.EqualTo(health));
                using (var pause = world.arena.Controls.OpenScope("lighting trial pause check"))
                {
                    pause.PauseSimulation(); float angle = lamp.State.Angle;
                    Assert.Throws<InvalidOperationException>(() => driver.Step(.1f)); Assert.That(lamp.State.Angle, Is.EqualTo(angle));
                }
                driver.Reset();
                player.Position = lamp.ContactCenter - new Vector2(1.3f, 1.5f); player.velocity = Vector2.zero; player.SyncFacing(1);
                Physics2D.SyncTransforms();
                driver.Step(1f / 120, new CombatInput(pressed: CombatButtons.Weapon1Basic));
                for (int i = 0; i < 85; i++) driver.Step(1f / 120);
                Assert.That(lamp.State.AcceptedAttacks, Is.GreaterThan(0), "Real active melee sweeps reach the trial lamp.");
                Assert.That(player.health, Is.EqualTo(health));
                driver.Reset(); Assert.That(lamp.State.Angle, Is.Zero); Assert.That(lamp.BodyContacts, Is.Zero);
                // Same actual movement with art/observation enabled vs disabled.
                var start = world.Layout.spawn.Vector;
                player.Position = start; player.velocity = Vector2.zero;
                for (int i = 0; i < 60; i++) driver.Step(1f / 120, new CombatInput(1));
                var withProps = player.Position;
                driver.Reset(); props.enabled = false; player.Position = start; player.velocity = Vector2.zero;
                for (int i = 0; i < 60; i++) driver.Step(1f / 120, new CombatInput(1));
                Assert.That(Vector2.Distance(player.Position, withProps), Is.LessThan(.001f), "Decorative props must not alter movement.");
                props.enabled = true; driver.Reset();
                world.arena.Player.Position = world.Layout.spawn.Vector;
                for (int i = 0; i < 30; i++) driver.Step(1f / 120);
                world.CameraStep(0, true);
                yield return null; yield return null; yield return null;
                Capture(world.sceneCamera, "start-with-props");
                props.enabled = false; Capture(world.sceneCamera, "start-before-props"); props.enabled = true;
                world.WarpToRoom("F01");
                for (int i = 0; i < 30; i++) driver.Step(1f / 120);
                world.CameraStep(0, true);
                yield return null; yield return null; yield return null;
                Capture(world.sceneCamera, "combat-with-props");
            }
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator BodyContactEmitsSmallMovingSparksFromBothSidesAndResets()
        {
            EditorSceneManager.playModeStartScene = null; Chapter2Tools.Create();
            Object.FindAnyObjectByType<Chapter2World>().seed = 17;
            SkipUnderModernSewer();
            yield return new EnterPlayMode();
            for (int i = 0; i < 120 && !Object.FindAnyObjectByType<Chapter2World>().Ready; i++) yield return null;
            var world = Object.FindAnyObjectByType<Chapter2World>();
            var lamp = world.GetComponent<Chapter2LightingProps>().Lanterns[0];
            var flash = lamp.GetComponentInChildren<LanternContactFlash>();
            var player = world.arena.Player; float health = player.health;
            Assert.That(flash.transform.Find("Contact white rim"), Is.Null, "The rejected outline overlay must not be instantiated.");
            Assert.That(flash.Visible, Is.False, "initial emitted=" + flash.EmittedCount + " body contacts=" + lamp.BodyContacts);
            yield return null; yield return null; yield return null;
            using (var driver = world.arena.AcquireManualSimulation())
            {
                for (int direction = 1; direction >= -1; direction -= 2)
                {
                    driver.Reset(); player.Position = lamp.ContactCenter - new Vector2(direction * 2.4f, 1);
                    player.velocity = Vector2.zero; player.SyncFacing(direction); Physics2D.SyncTransforms();
                    for (int i = 0; i < 100 && flash.EmittedCount == 0; i++) driver.Step(1f / 120, new CombatInput(direction));
                    Assert.That(flash.EmittedCount, Is.EqualTo(1)); Assert.That(flash.Visible, Is.True);
                    Assert.That(flash.LastWasMelee, Is.False);
                    Assert.That((flash.LastContactPoint.x - lamp.transform.position.x) * direction, Is.LessThan(-.2f), "Flash belongs to the incoming body's contact edge.");
                    for (int i = 0; i < 3; i++) driver.Step(1f / 120);
                    Assert.That(flash.EmittedCount, Is.EqualTo(1), "Maintained overlap must not retrigger the white flash.");
                    world.CameraStep(0, true);
                    using (var pause = world.arena.Controls.OpenScope("contact sparks pause"))
                    {
                        pause.PauseSimulation(); Assert.Throws<InvalidOperationException>(() => driver.Step(.1f));
                        yield return null; yield return null; yield return null;
                        Assert.That(flash.Visible, Is.True, "Rendering while paused cannot age the flash.");
                    }
                    int whiteOn = Capture(world.sceneCamera, direction == 1 ? "contact-left-on" : "contact-right-on");
                    if (direction == 1)
                    {
                        flash.enabled = false;
                        int whiteOff = Capture(world.sceneCamera, "contact-left-off");
                        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("C2_LIGHTING_CAPTURE")) && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                            Assert.That(whiteOn, Is.GreaterThan(whiteOff + 25), "A few isolated pixels are not readable body-contact feedback at 960x540.");
                        flash.enabled = true;
                    }
                    else
                    {
                        // Capture real simulation progression for a human-readable 960x540 motion review.
                        var effect = flash.GetComponentInChildren<MeshRenderer>();
                        var whiteArea = new int[14];
                        float firstDistance = Vector2.Distance(effect.bounds.center, flash.LastContactPoint);
                        for (int frame = 0; frame < whiteArea.Length; frame++)
                        {
                            world.CameraStep(0, true);
                            yield return null; yield return null;
                            int lit = Capture(world.sceneCamera, "body-motion-" + frame.ToString("D2"));
                            bool wasVisible = effect.enabled; effect.enabled = false;
                            whiteArea[frame] = lit - Capture(world.sceneCamera, null);
                            effect.enabled = wasVisible;
                            if (frame == 3) Assert.That(Vector2.Distance(effect.bounds.center, flash.LastContactPoint), Is.GreaterThan(firstDistance + .025f), "Sparks must leave the world contact point instead of clinging to the swinging housing.");
                            for (int tick = 0; tick < 3; tick++) driver.Step(1f / 120);
                        }
                        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("C2_LIGHTING_CAPTURE")) && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        {
                            Assert.That(whiteArea.Take(4).Max(), Is.GreaterThan(30), "Body feedback must remain readable at the native game resolution.");
                            Assert.That(whiteArea.Last(), Is.Zero, "The sparks must expire.");
                            Debug.Log("CONTACT_SPARK_NATIVE_WHITE_AREA " + string.Join(",", whiteArea));
                        }
                    }
                    for (int i = 0; i < 30; i++) driver.Step(1f / 120);
                    Assert.That(flash.Visible, Is.False, "The short contact cue expires on the shared simulation clock.");
                    Assert.That(player.health, Is.EqualTo(health));
                }
                driver.Reset(); Assert.That(flash.Visible, Is.False); Assert.That(flash.EmittedCount, Is.Zero);
            }
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator RealMeleeMustEmitOneVisibleLanternFlash()
        {
            EditorSceneManager.playModeStartScene = null; Chapter2Tools.Create();
            Object.FindAnyObjectByType<Chapter2World>().seed = 17;
            SkipUnderModernSewer();
            yield return new EnterPlayMode();
            for (int i = 0; i < 120 && !Object.FindAnyObjectByType<Chapter2World>().Ready; i++) yield return null;
            var world = Object.FindAnyObjectByType<Chapter2World>();
            var lamp = world.GetComponent<Chapter2LightingProps>().Lanterns[0];
            var flash = lamp.GetComponentInChildren<LanternContactFlash>();
            using (var driver = world.arena.AcquireManualSimulation())
            {
                foreach (var button in new[] { CombatButtons.Weapon1Basic, CombatButtons.Weapon2Basic })
                {
                driver.Reset(); var player = world.arena.Player;
                player.Position = lamp.ContactCenter - new Vector2(2.25f, 1.5f);
                player.velocity = Vector2.zero; player.SyncFacing(1); Physics2D.SyncTransforms();
                float health = player.health;
                driver.Step(1f / 120, new CombatInput(pressed: button));
                for (int i = 0; i < 80 && lamp.State.AcceptedAttacks == 0; i++) driver.Step(1f / 120);
                Assert.That(lamp.State.AcceptedAttacks, Is.EqualTo(1), "The real melee sweep must reach the lantern.");
                Assert.That(lamp.BodyContacts, Is.Zero, "This regression must hit by weapon without masking the missing melee feedback with a body bump.");
                Assert.That(flash.EmittedCount, Is.EqualTo(1), "A real accepted melee hit needs visible feedback, not only pendulum movement.");
                Assert.That(flash.LastWasMelee, Is.True, "Weapon hits need the stronger impact profile.");
                Assert.That(flash.Visible, Is.True); Assert.That(player.health, Is.EqualTo(health));
                world.CameraStep(0, true); yield return null; yield return null; yield return null;
                Capture(world.sceneCamera, button == CombatButtons.Weapon1Basic ? "melee-q-impact" : "melee-w-impact");
                var settings = JsonUtility.FromJson<Chapter2PropsData>(Resources.Load<TextAsset>("Chapter2Lighting/Settings").text).contactFlash;
                if (button == CombatButtons.Weapon1Basic)
                {
                    var effect = flash.GetComponentInChildren<MeshRenderer>();
                    var whiteArea = new int[14];
                    for (int frame = 0; frame < whiteArea.Length; frame++)
                    {
                        world.CameraStep(0, true); yield return null; yield return null;
                        int lit = Capture(world.sceneCamera, "melee-motion-" + frame.ToString("D2"));
                        bool wasVisible = effect.enabled; effect.enabled = false;
                        whiteArea[frame] = lit - Capture(world.sceneCamera, null); effect.enabled = wasVisible;
                        for (int tick = 0; tick < 3; tick++) driver.Step(1f / 120);
                    }
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("C2_LIGHTING_CAPTURE")) && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        Assert.That(whiteArea.Take(4).Max(), Is.GreaterThan(80), "The weapon impact needs a clear flash and substantial streaks at native resolution.");
                        Assert.That(whiteArea.Last(), Is.Zero);
                        Debug.Log("CONTACT_MELEE_NATIVE_WHITE_AREA " + string.Join(",", whiteArea));
                    }
                    Assert.That(flash.Visible, Is.False);
                    Assert.That(flash.EmittedCount, Is.EqualTo(1));
                    Assert.That(settings.melee.sparks, Is.GreaterThan(settings.body.sparks));
                    Assert.That(settings.melee.flashDuration, Is.GreaterThan(0));
                    continue;
                }
                for (int i = 0; i < 8; i++) driver.Step(1f / 120);
                Assert.That(flash.EmittedCount, Is.EqualTo(1), "One attack's repeated active sweep samples cannot restart the flash.");
                for (int i = 0; i < 5; i++) driver.Step(1f / 120);
                Assert.That(flash.Visible, Is.True, "Moving attack sparks must remain readable through the first 100ms of normal playback.");
                yield return null; yield return null;
                Capture(world.sceneCamera, button == CombatButtons.Weapon1Basic ? "melee-q-visible-120ms" : "melee-w-visible-120ms");
                }
            }
            yield return new ExitPlayMode();
        }
        static int Capture(Camera camera, string label, Action<Color32[]> inspect = null)
        {
            var directory = Environment.GetEnvironmentVariable("C2_LIGHTING_CAPTURE");
            if (string.IsNullOrEmpty(directory) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return 0;
            Directory.CreateDirectory(directory);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(960, 540, 24);
            var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); texture.Apply();
                inspect?.Invoke(texture.GetPixels32());
                Assert.That(texture.GetPixels32().Select(p => (int)p.r << 16 | (int)p.g << 8 | p.b).Distinct().Count(), Is.GreaterThan(20));
                if (label == "start-with-props")
                {
                    var trial = Object.FindAnyObjectByType<Chapter2LightingProps>().transform.Find("Waterway lighting trial");
                    int visibleFlames = 0;
                    foreach (var flame in trial.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == "Separate flame"))
                    {
                        var point = camera.WorldToViewportPoint(flame.bounds.center);
                        if (point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) continue;
                        int amber = 0, x = Mathf.RoundToInt(point.x * 960), y = Mathf.RoundToInt(point.y * 540);
                        for (int dy = -4; dy <= 4; dy++) for (int dx = -3; dx <= 3; dx++)
                        {
                            var color = texture.GetPixel(Mathf.Clamp(x + dx, 0, 959), Mathf.Clamp(y + dy, 0, 539));
                            if (color.r > .55f && color.r > color.g * 1.15f && color.g > color.b * 1.2f) amber++;
                        }
                        Assert.That(amber, Is.GreaterThan(2), "The native image must show the actual flame texture, not a stale gray chain texture.");
                        visibleFlames++;
                    }
                    Assert.That(visibleFlames, Is.GreaterThanOrEqualTo(2));
                }
                if (label != null) File.WriteAllBytes(Path.Combine(directory, label + ".png"), texture.EncodeToPNG());
                return texture.GetPixels32().Count(p => p.r > 225 && p.g > 225 && p.b > 225);
            }
            finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(texture); }
        }
    }
}
