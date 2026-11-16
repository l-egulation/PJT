using System;
using System.Collections;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class AudioCueCombatIntegrationTests
    {
        sealed class AttackClock : ICueClock
        {
            readonly CombatArena arena;
            readonly float started;
            public AttackClock(CombatArena arena, float started) { this.arena = arena; this.started = started; }
            public float ReadElapsed() => arena.Now - started;
        }
        [UnityTest] public IEnumerator GameInjectedAudioFailureCannotChangeCombat() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabInjectedAudioFailureCannotChangeCombat() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var clip = AudioClip.Create("CombatAudioCueFixtureClip", 24000, 1, 48000, false);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    float[] expected = null;
                    foreach (string mode in new[] { "none", "normal", "cancel", "missing", "disabled", "destroyed", "loop-owner" })
                    {
                        Setup(arena, driver); var observations = new List<float>(); var target = arena.enemies[0]; float health = target.health;
                        using (var factory = new AudioCueFactory()) using (var cues = new CuePlayer(factory)) using (var owner = cues.OpenScope("injected attack observation"))
                        {
                            var definition = new AudioCueDefinition("CombatAudioCueFixture-" + mode, "fixture.pcm", clip.samples, clip.frequency, mode == "loop-owner");
                            if (mode != "missing") factory.Publish(definition, clip);
                            ICueHandle handle = null; AudioSource source = null; AudioCueOutputBinding output = null; bool observed = false;
                            for (int tick = 0; tick < 60; tick++)
                            {
                                driver.Step(.02f, new CombatInput(0, tick == 0 ? CombatButtons.Basic : tick == 30 ? CombatButtons.Dash : CombatButtons.None));
                                // This isolated consumer observes an already-started
                                // cast; it cannot create hits, resources or time.
                                var cast = arena.Player.currentCast;
                                if (mode != "none" && handle == null && cast)
                                {
                                    handle = owner.Play(definition, new CueOccurrence(cast.ExecutionId, definition.Id),
                                        new CueAnchor(arena.Player.Position.x, arena.Player.Position.y, arena.Player.facing), new AttackClock(arena, cast.StartedAt));
                                    if (mode != "missing")
                                    {
                                        output = GameObject.Find(definition.Id).GetComponent<AudioCueOutputBinding>(); source = output.Source;
                                        Assert.That(source.clip, Is.SameAs(clip)); Assert.That(source.enabled, Is.True); observed = true;
                                        Assert.That(output.GetComponent<Rigidbody2D>(), Is.Null); Assert.That(output.GetComponent<Collider2D>(), Is.Null);
                                    }
                                }
                                handle?.Poll();
                                if (tick == 3 && handle != null)
                                {
                                    if (mode == "cancel") handle.Cancel();
                                    if (mode == "disabled") output.enabled = false;
                                    if (mode == "destroyed") Object.DestroyImmediate(source);
                                    if (mode == "loop-owner") owner.Dispose();
                                    if (mode == "normal")
                                    {
                                        Assert.That(source.isPlaying, Is.True); handle.Poll(); Assert.That(source.isPlaying, Is.False);
                                        Assert.That(source.timeSamples, Is.InRange(1, clip.samples - 1));
                                    }
                                    else handle.Poll();
                                }
                                Snapshot(arena, target, observations);
                            }
                            Assert.That(health - target.health, Is.EqualTo(10), mode);
                            if (expected == null) expected = observations.ToArray();
                            else Assert.That(observations.ToArray(), Is.EqualTo(expected), "Audio mode=" + mode);
                            Assert.That(cues.ActiveCount, Is.Zero); Assert.That(factory.LiveOutputCount, Is.Zero);
                            if (mode == "none") Assert.That(handle, Is.Null);
                            else
                            {
                                Assert.That(handle, Is.Not.Null);
                                Assert.That(handle.Status, Is.EqualTo(mode == "normal" ? CueStatus.Completed : mode == "missing" || mode == "disabled" || mode == "destroyed" ? CueStatus.Failed : CueStatus.Cancelled), mode);
                                if (mode != "missing") Assert.That(observed, Is.True);
                                if (source) { Assert.That(source.isPlaying, Is.False); Assert.That(source.enabled, Is.False); Assert.That(source.clip, Is.Null); }
                            }
                        }
                    }
                }
            }
            finally { if (clip) Object.Destroy(clip); }
        }
        static void Snapshot(CombatArena arena, TestEnemy target, List<float> values)
        {
            var player = arena.Player;
            values.Add(arena.Now); values.Add(target.health); values.Add(target.ReceivedHitCount);
            values.Add(player.Position.x); values.Add(player.Position.y); values.Add(player.DashRemaining);
            values.Add(player.Weapon.Cooldown.Available); values.Add(player.Weapon.Cooldown.NextReady);
            values.Add(player.currentCast ? player.currentCast.Age : -1); values.Add(player.BasicIndex);
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            arena.Player.selected = 0; arena.Player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "base_sword"), 0);
            arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1); arena.enemies[0].Position = new Vector2(3, 0); Physics2D.SyncTransforms();
        }
    }
}
