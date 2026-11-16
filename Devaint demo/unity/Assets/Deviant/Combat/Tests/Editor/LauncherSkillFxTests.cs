using System;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class LauncherSkillFxTests
    {
        [Serializable] class ClipData { public string name; public bool loop; public int loopFrom = -1; public float[] seconds; public string[] frames; }
        [Serializable] class Catalog { public string format; public ClipData[] clips; }

        static readonly string[] Expected =
        {
            "ball_pop", "ball_bullet", "ball_hit",
            "laser_pop", "laser_hold", "laser_head", "laser_hit", "laser_beam", "laser_wall_hit", "laser_wall_hold",
            "shotgun_pellet", "shotgun_pop", "shotgun_dust", "shotgun_wind", "shotgun_hit",
            "grenade_shell", "grenade_smoke", "grenade_pop", "grenade_frag", "grenade_main_air", "grenade_main_ground", "grenade_frag_air", "grenade_frag_ground",
            "charge_orb1", "charge_orb2", "charge_orb3", "charge_suck", "charge_up", "charge_shot1", "charge_shot2", "charge_shot3", "charge_pop_big",
            "charge_burst1", "charge_burst2", "charge_burst3",
        };

        [Test, Category("RequiresArt")] public void EveryApprovedClipLoadsAtGamePixelsWithItsPivotInside()
        {
            var text = Resources.Load<TextAsset>("LauncherSkillFx/clips");
            Assert.That(text, Is.Not.Null);
            var catalog = JsonUtility.FromJson<Catalog>(text.text);
            Assert.That(catalog.format, Is.EqualTo("deviant-launcher-skill-fx-v1"));
            Assert.That(catalog.clips.Select(c => c.name).ToArray(), Is.EquivalentTo(Expected));
            foreach (var clip in catalog.clips)
            {
                Assert.That(clip.frames.Length, Is.EqualTo(clip.seconds.Length), clip.name);
                Assert.That(clip.seconds.All(s => s > 0), Is.True, clip.name);
                foreach (var path in clip.frames)
                {
                    var sprite = Resources.Load<Sprite>(path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32), path);
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), path);
                    var pivot = sprite.pivot / sprite.rect.size;
                    Assert.That(pivot.x, Is.InRange(0f, 1f), path); Assert.That(pivot.y, Is.InRange(0f, 1f), path);
                }
            }
            var bullet = catalog.clips.Single(c => c.name == "ball_bullet");
            Assert.That(bullet.loop, Is.True); Assert.That(bullet.loopFrom, Is.EqualTo(1), "the launch frame plays once, then the flight frames repeat");
            foreach (string pfx in new[] { "grenade_main_air", "grenade_main_ground", "grenade_frag_air", "grenade_frag_ground", "charge_burst1", "charge_burst2", "charge_burst3" })
                Assert.That(catalog.clips.Single(c => c.name == pfx).seconds.All(s => Mathf.Abs(s - 1f / 30) < .0001f), Is.True, pfx + " is a 30fps pixelfx bake");
            Assert.That(catalog.clips.Single(c => c.name == "laser_beam").loop, Is.True);
        }

        // 2026-11-08: the bullet body was left where the shot was created and never followed the flight.
        [Test] public void LauncherBulletBodyFollowsTheFlyingShot()
        {
            var runtime = typeof(LauncherSkillFx).Assembly;
            var visualType = runtime.GetType("Deviant.Combat.LauncherProjectileVisual");
            var bookType = runtime.GetType("Deviant.Combat.LauncherFxFlipbook");
            Assert.That(visualType, Is.Not.Null); Assert.That(bookType, Is.Not.Null);
            var shot = new GameObject("shot"); var body = new GameObject("body");
            try
            {
                shot.transform.localScale = Vector3.one * .3f;
                var visual = shot.AddComponent(visualType); var book = body.AddComponent(bookType);
                const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                visualType.GetField("Body", any).SetValue(visual, book);
                visualType.GetField("Spawn", any).SetValue(visual, Vector2.zero);
                visualType.GetField("Gap", any).SetValue(visual, .3f);
                visualType.GetField("DrawOffset", any).SetValue(visual, new Vector2(0, -.125f));
                var late = visualType.GetMethod("LateUpdate", any);

                shot.transform.position = new Vector3(.1f, 0, 0); late.Invoke(visual, null);
                Assert.That((bool)bookType.GetField("Visible", any).GetValue(book), Is.False, "still inside the drawn barrel");
                shot.transform.position = new Vector3(6, 1, 0); late.Invoke(visual, null);
                Assert.That(body.transform.position.x, Is.EqualTo(6).Within(1e-5f), "the body moves with the shot");
                Assert.That(body.transform.position.y, Is.EqualTo(.875f).Within(1e-5f), "drawn on the barrel axis");
                Assert.That(body.transform.localScale, Is.EqualTo(Vector3.one), "the shot's hit-radius scale is not inherited");
                Assert.That((bool)bookType.GetField("Visible", any).GetValue(book), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(shot); UnityEngine.Object.DestroyImmediate(body); }
        }

        [Test] public void ChargeShotStepFollowsTheReleasedRadius()
        {
            Assert.That(LauncherSkillFx.ChargeStep(.15f, .3f), Is.EqualTo(1));
            Assert.That(LauncherSkillFx.ChargeStep(.225f, .3f), Is.EqualTo(2));
            Assert.That(LauncherSkillFx.ChargeStep(.3f, .3f), Is.EqualTo(3));
        }

        [Test] public void ThreeChargeStepsSplitTheHoldIntoEqualBands()
        {
            var steps = new ChargeDefinition("launcher_basic_special", .2f, .8f, 20, 40, .5f, steps: 3);
            Assert.That(steps.Resolve(.1f).Damage, Is.EqualTo(20), "an early release fires the first step at the minimum hold");
            Assert.That(steps.Resolve(.39f).Damage, Is.EqualTo(20));
            Assert.That(steps.Resolve(.41f).Damage, Is.EqualTo(30));
            Assert.That(steps.Resolve(.59f).Damage, Is.EqualTo(30));
            Assert.That(steps.Resolve(.61f).Damage, Is.EqualTo(40));
            Assert.That(steps.Resolve(.8f).Damage, Is.EqualTo(40));
            Assert.That(steps.Resolve(.5f).ReleaseTime, Is.EqualTo(.5f).Within(1e-6f), "the release time itself is not stepped");
            var smooth = new ChargeDefinition("launcher_basic_special", .2f, .8f, 20, 40, .5f);
            Assert.That(smooth.Resolve(.5f).Damage, Is.EqualTo(30).Within(1e-4f));
            Assert.That(smooth.Resolve(.35f).Damage, Is.EqualTo(25).Within(1e-4f), "without steps the damage still grows continuously");
            Assert.That(steps.ContentHash, Is.Not.EqualTo(smooth.ContentHash));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChargeDefinition("x", .2f, .8f, 20, 40, .5f, steps: 1));
        }
    
        // 2026-11-10: the four launcher ink specials stop reusing the light standing shot (attack_single). Each plays its own
        // body motion with its own gun (640x360 cells, feet at (220,327), PPU 106.667) and a muzzle point per frame.
        [Test] public void LauncherSpecialsPlayTheirOwnGunMotions()
        {
            var media = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var combat = JsonUtility.FromJson<Deviant.Combat.Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var registry = new MotionSourceRegistry(media.motionRegistry, combat);
            foreach (var (action, form, clipName, seconds) in new[] {
                ("launcher_basic_special", "launcher", "attack_charge_shot", .5f), ("launcher_range_special", "launcher_laser", "attack_laser_beam", .6f),
                ("launcher_move_special", "launcher_shotgun", "attack_blast_jump", .8f), ("launcher_followup_special", "launcher_grenade", "attack_kneel_reload", 1f) })
            {
                var binding = registry.Actions.Single(a => a.Key.ActionId == action && a.Key.Kind == MotionActionKind.Special);
                Assert.That(binding.MotionId, Is.EqualTo(form + "/" + clipName), action);
                Assert.That(binding.IsFallback, Is.False, action);
                var clip = media.sets.Single(s => s.form == form).Find(clipName);
                Assert.That(clip.seconds.Sum(), Is.EqualTo(seconds).Within(.001f), clipName);
                Assert.That(clip.muzzleX.Length, Is.EqualTo(clip.frames.Length), clipName);
                Assert.That(clip.muzzleY.Length, Is.EqualTo(clip.frames.Length), clipName);
                for (int i = 0; i < clip.frames.Length; i++)
                {
                    var sprite = Resources.Load<Sprite>(clip.frames[i]);
                    Assert.That(sprite, Is.Not.Null, clip.frames[i]);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(320f / 3).Within(.001f), clip.frames[i]);
                    Assert.That(clip.muzzleX[i], Is.InRange(.4f, 2.2f), clip.frames[i] + " muzzle in front of the body");
                    Assert.That(clip.muzzleY[i], Is.InRange(.2f, 2.2f), clip.frames[i]);
                }
            }
            Assert.That(registry.Actions.Single(a => a.Key.ActionId == "launcher_basic_0" && a.Key.Variant == 1).MotionId, Is.EqualTo("launcher_laser/attack_single"), "the basic ball shot keeps its light shot");
        }
}
}
