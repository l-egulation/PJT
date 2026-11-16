using System.Collections;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class PlayerDeathPresentationTests
    {
        [UnityTest]
        public IEnumerator ChapterOutputKeepsHitFeedbackWaitsForBlackAndReleasesResultOnTop()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene("Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity");
            yield return new EnterPlayMode();
            CombatArena arena = null;
            for (int frame = 0; frame < 600; frame++)
            {
                yield return null; arena = Object.FindAnyObjectByType<CombatArena>();
                if (arena && arena.Player && arena.GetComponent<PlayerDeathPresentation>()) break;
            }
            Assert.That(arena && arena.Player && arena.GetComponent<PlayerDeathPresentation>(), Is.True);
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
                var output = PlayerDeathPresentation.Attach(arena);
                Assert.That(PlayerDeathPresentation.Attach(arena), Is.SameAs(output));
                var feedback = arena.FeelFeedback;
                Assert.That(feedback, Is.Not.Null);
                bool effectsEnabled = feedback.EffectsEnabled;
                arena.Player.Die(); Assert.That(output.Active, Is.False, "Alive cleanup is not a death.");
                arena.Player.health = 0; arena.Player.Die();
                Assert.That(output.Active, Is.True); Assert.That(output.FadeOpacity, Is.Zero);
                arena.AdvanceFeedbackAccent(.055f);
                Assert.That(output.KickPixels.X, Is.Not.Zero);
                using (var pause = arena.Controls.OpenScope("death presentation pause"))
                {
                    var request = pause.PauseSimulation();
                    float before = output.FadeOpacity;
                    arena.AdvanceFeedbackAccent(2);
                    Assert.That(output.FadeOpacity, Is.EqualTo(before));
                    Assert.That(output.ReadyForResult, Is.False); request.Dispose();
                }
                float previous = 0;
                for (int i = 0; i < 230; i++)
                {
                    driver.Step(.02f, default); arena.AdvanceFeedbackAccent(.02f);
                    Assert.That(output.FadeOpacity, Is.InRange(previous, 1)); previous = output.FadeOpacity;
                }
                Assert.That(output.ReadyForResult, Is.True); Assert.That(output.FadeOpacity, Is.EqualTo(1));
                Assert.That(output.KickPixels.X, Is.Zero); Assert.That(feedback.enabled, Is.True);
                Assert.That(feedback.EffectsEnabled, Is.EqualTo(effectsEnabled));
                Assert.That(output.OverlayOrder, Is.EqualTo(110)); output.RevealResult(); Assert.That(output.OverlayOrder, Is.EqualTo(90));
                var black = output.GetComponentInChildren<Image>(); Assert.That(black.raycastTarget, Is.False);
                driver.Reset(); Assert.That(output.Active, Is.False); Assert.That(output.FadeOpacity, Is.Zero);
                Assert.That(output.OverlayOrder, Is.EqualTo(110));
                arena.Player.health = 0; arena.Player.Die(); arena.AdvanceFeedbackAccent(.055f);
                output.enabled = false; Assert.That(output.FadeOpacity, Is.Zero); Assert.That(output.KickPixels.X, Is.Zero);
                output.enabled = true; driver.Reset();
            }
            yield return new ExitPlayMode();
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
