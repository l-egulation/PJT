using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class ScreenPresentationScopeTests
    {
        [Test] public void OverlappingOwnersAndRepeatedDisposeDoNotReleaseAnotherPresentation()
        {
            var go = new GameObject("Screen priority owners");
            try {
                var arena = go.AddComponent<CombatArena>();
                var first = arena.SuppressCombatScreenEffects();
                var second = arena.SuppressCombatScreenEffects();
                first.Dispose(); first.Dispose();
                Assert.That(arena.CombatScreenEffectsAllowed, Is.False);
                second.Dispose();
                Assert.That(arena.CombatScreenEffectsAllowed, Is.True);
                var next = arena.SuppressCombatScreenEffects();
                second.Dispose();
                Assert.That(arena.CombatScreenEffectsAllowed, Is.False);
                next.Dispose();
                Assert.That(arena.CombatScreenEffectsAllowed, Is.True);
            } finally { Object.DestroyImmediate(go); }
        }
    }

    public partial class WeaponBalanceReleaseTests
    {
        SpriteRenderer[] HammerOverlays() => Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Where(r => r.name == "Hammer impact darken" || r.name == "Hammer impact invert").ToArray();

        void StartScreenHammer()
        {
            Ready("hammer");
            arena.ViewCamera.orthographic = true;
            arena.ViewCamera.transform.position = new Vector3(0, 1001, -10);
            Assert.That(Player.TryReconfigure(0, Player.Weapon.FormIndex, 1, out var error), Is.True, error);
            driver.Step(.001f, new CombatInput(pressed: CombatButtons.Special));
            Assert.That(Player.currentCast, Is.Not.Null);
        }

        [UnityTest] public IEnumerator HammerBlackoutIsClearedWhileCombatIsPausedAndNeverReplaysAfterTheCutscene()
        {
            StartScreenHammer();
            float damage = Player.Form.specials[1].hits[0].damage;
            Advance(1.08f); yield return null;
            Assert.That(HammerOverlays().Any(r => r.name == "Hammer impact darken" && r.enabled), Is.True, "reproduce the real dark interval");
            Assert.That(Target.maxHealth - Target.health, Is.EqualTo(damage).Within(.01f), "presentation keeps the original one-hit damage");
            float frozen = arena.Now;
            using (var pause = arena.Controls.OpenScope("boss transition regression")) {
                pause.PauseSimulation();
                using (arena.SuppressCombatScreenEffects()) {
                    Assert.That(HammerOverlays().All(r => !r.enabled), Is.True, "clear synchronously, before another frame");
                    yield return null;
                    Assert.That(arena.FeelFeedback.CombatCamera.CurrentZoom, Is.EqualTo(1));
                }
                yield return null;
                Assert.That(arena.Now, Is.EqualTo(frozen), "the effect clock cannot advance during this reproduction");
                Assert.That(HammerOverlays().All(r => !r.enabled), Is.True, "the interrupted screen effect must not return at its old timestamp");
            }
            StartScreenHammer(); Advance(1.08f); yield return null;
            Assert.That(HammerOverlays().Any(r => r.name == "Hammer impact darken" && r.enabled), Is.True, "a new skill after the cutscene retains its normal effect");
        }

        [UnityTest] public IEnumerator HammerStartedInsidePresentationCannotReplayItsScreenEffectWhenTheOwnerReleases()
        {
            using (var presentation = arena.SuppressCombatScreenEffects()) {
                StartScreenHammer(); Advance(.7f); yield return null;
                Assert.That(HammerOverlays().All(r => !r.enabled), Is.True);
            }
            Advance(.38f); yield return null;
            Assert.That(HammerOverlays().Length, Is.EqualTo(2));
            Assert.That(HammerOverlays().All(r => !r.enabled), Is.True);
        }
    }
}
