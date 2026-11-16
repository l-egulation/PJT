using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class PlayerHurtScreenFeedbackTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        [UnityTest] public IEnumerator ChapterOneDamageOnlyGlowsScreen()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            yield return VerifyScene("Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity");
        }

        [UnityTest] public IEnumerator ChapterTwoDamageOnlyGlowsScreen()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            yield return VerifyScene("Assets/Chapter2/Scenes/Chapter2Procedural.unity");
        }

        static void PreparePlayMode()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static IEnumerator VerifyScene(string scene)
        {
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
            CombatArena arena = null;
            float deadline = Time.realtimeSinceStartup + 45;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                arena = Object.FindAnyObjectByType<CombatArena>();
                if (arena && arena.Player && arena.FeelFeedback && arena.enemies.Any(enemy => enemy && !enemy.isBoss)) break;
            }
            Assert.That(arena && arena.Player && arena.FeelFeedback, Is.True, "Current gameplay scene initializes");
            using (arena.AcquireManualSimulation())
            {
                var player = arena.Player;
                var enemy = arena.enemies.First(value => value && !value.isBoss);
                var feedback = arena.FeelFeedback;
                var probe = player.GetComponent<Chapter1PlayerHitFeedback>();
                Assert.That(probe, Is.Not.Null);
                foreach (var target in arena.enemies) if (target) target.aggressive = false;
                feedback.ResetFeedback();
                var tint = new Color(.35f, .72f, .84f, .7f);
                player.visual.color = tint;
                int spriteCount = player.visual.GetComponentsInChildren<SpriteRenderer>().Length;
                float health = player.health;
                Assert.That(player.Receive(5, enemy, -1, false, null, bypass: true), Is.True);
                feedback.TickPresentation(0); probe.Sample();
                Assert.That(player.health, Is.LessThan(health), "The real damage path still applies health damage");
                Assert.That(feedback.PlayerHurtVignetteActive, Is.True);
                Assert.That(feedback.PlayerHurtOpacity, Is.Zero.Within(.001f), "Glow rises instead of starting opaque");
                feedback.TickPresentation(.15f); probe.Sample();
                Assert.That(feedback.PlayerHurtOpacity, Is.EqualTo(.5f).Within(.001f));
                Assert.That(probe.FlashStrength, Is.EqualTo(1).Within(.001f));
                Assert.That(player.visual.color, Is.EqualTo(tint), "Preserve the body's current tint and alpha");
                Assert.That(player.visual.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(spriteCount), "No flash or afterimage sprite");
                Assert.That(feedback.CurrentCameraOffset, Is.EqualTo(Vector3.zero));
                Assert.That(feedback.HitStopped, Is.False);
                var texture = (Texture2D)typeof(CombatFeelFeedback).GetField("vignetteTexture", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(feedback);
                Assert.That(texture.GetPixel(32, 32).a, Is.Zero, "Center stays clear");
                var edge = texture.GetPixel(0, 32);
                Assert.That(edge.a, Is.EqualTo(.5f).Within(.01f));
                Assert.That(edge.r, Is.GreaterThan(edge.g), "Outer edge is red");

                feedback.TickPresentation(.375f);
                Assert.That(feedback.PlayerHurtOpacity, Is.EqualTo(.375f).Within(.001f), "Same quadratic fade as healing");
                float faded = feedback.PlayerHurtOpacity;
                feedback.TickPresentation(0);
                Assert.That(feedback.PlayerHurtOpacity, Is.EqualTo(faded), "Zero presentation delta does not advance the glow");
                Assert.That(player.Receive(5, enemy, -1, false, null, bypass: true), Is.True);
                feedback.TickPresentation(0); probe.Sample();
                Assert.That(feedback.PlayerHurtOpacity, Is.EqualTo(.5f).Within(.001f), "Repeated damage restores peak brightness");
                Assert.That(player.visual.color, Is.EqualTo(tint));
                feedback.TickPresentation(.751f); probe.Sample();
                Assert.That(feedback.PlayerHurtVignetteActive, Is.False);
                Assert.That(probe.FlashStrength, Is.Zero);

                // Frequent hits during the rise must not restart it from zero.
                Assert.That(player.Receive(5, enemy, -1, false, null, bypass: true), Is.True);
                feedback.TickPresentation(0); feedback.TickPresentation(.05f);
                float rising = feedback.PlayerHurtOpacity;
                Assert.That(player.Receive(5, enemy, -1, false, null, bypass: true), Is.True);
                feedback.TickPresentation(0);
                Assert.That(feedback.PlayerHurtOpacity, Is.GreaterThanOrEqualTo(rising));
                feedback.TickPresentation(.1f);
                Assert.That(feedback.PlayerHurtOpacity, Is.EqualTo(.5f).Within(.001f));
                feedback.ResetFeedback(); probe.Sample();
                Assert.That(feedback.PlayerHurtVignetteActive, Is.False);
                Assert.That(probe.FlashStrength, Is.Zero);

                player.EncounterProtection = () => true;
                Assert.That(player.Receive(5, enemy, -1, false, null), Is.False);
                feedback.TickPresentation(0);
                Assert.That(feedback.PlayerHurtVignetteActive, Is.False, "Rejected hits do not glow");
                player.EncounterProtection = null;
                typeof(CombatPlayer).GetField("respawnProtect", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, -10f);
                player.shield = 50; player.guardUntil = arena.Now + 10; player.guardReduction = 0;
                health = player.health;
                Assert.That(player.Receive(5, enemy, -1, false, null), Is.True);
                feedback.TickPresentation(0); probe.Sample();
                Assert.That(player.health, Is.EqualTo(health));
                Assert.That(feedback.PlayerHurtVignetteActive, Is.False, "Fully absorbed hits do not glow");
                Assert.That(player.visual.color, Is.EqualTo(tint));
                enemy.Receive(5, player, 1, false, null, bypass: true);
                feedback.TickPresentation(0);
                Assert.That(feedback.PlayerHurtVignetteActive, Is.False, "Damage dealt to enemies is separate");
            }
        }

        [Test] public void InvalidHurtEnvelopeSettingsAreRejected()
        {
            var settings = CombatFeelSettings.Load(); settings.Validate();
            settings.playerHurtRiseSeconds = settings.playerHurtSeconds;
            Assert.Throws<System.ArgumentException>(settings.Validate);
            settings = CombatFeelSettings.Load(); settings.playerHurtRiseSeconds = float.NaN;
            Assert.Throws<System.ArgumentException>(settings.Validate);
            settings = CombatFeelSettings.Load(); settings.playerHurtOpacity = .51f;
            Assert.Throws<System.ArgumentException>(settings.Validate);
        }
    }
}
