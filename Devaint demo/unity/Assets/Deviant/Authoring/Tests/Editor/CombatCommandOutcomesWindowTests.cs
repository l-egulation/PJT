using System;
using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class CombatCommandOutcomesWindowTests
    {
        [Test] public void WindowLayoutContainsOnlyBindingAndPollingControls()
        {
            var window = ScriptableObject.CreateInstance<CombatCommandOutcomesWindow>();
            try
            {
                window.CreateGUI();
                Assert.That(window.rootVisualElement.Q<Button>("bind"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Button>("poll"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Button>("stop"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Query<Button>().ToList().Count, Is.EqualTo(3));
                Assert.That(window.OutcomeCount, Is.Zero); Assert.That(window.LostOutcomes, Is.Zero);
                Assert.Throws<ArgumentOutOfRangeException>(() => window.OutcomeAt(0));
            }
            finally { Object.DestroyImmediate(window); }
        }
        [UnityTest] public IEnumerator WindowPollsActualOutcomesWithoutAcquiringControlsAndDoesNotRebindAfterReset()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var window = ScriptableObject.CreateInstance<CombatCommandOutcomesWindow>();
            try
            {
                window.CreateGUI();
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 1000); }
                    arena.Player.Position = new Vector2(0, 1000); window.Bind(arena);
                    int before = window.OutcomeCount; float time = arena.Now; long tick = arena.SimulationTickCount;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic | CombatButtons.Dash)); window.Poll();
                    var outcomes = Enumerable.Range(before, window.OutcomeCount - before).Select(window.OutcomeAt).ToArray();
                    Assert.That(outcomes.Any(o => o.Command.Kind == CombatCommandKind.Dash && o.State == CombatCommandState.Started), Is.True);
                    Assert.That(outcomes.Any(o => o.Command.Kind == CombatCommandKind.Basic && o.State == CombatCommandState.Rejected), Is.True);
                    var saved = outcomes.Single(o => o.Command.Kind == CombatCommandKind.Dash && o.State == CombatCommandState.Started);
                    int retained = window.OutcomeCount; time = arena.Now; tick = arena.SimulationTickCount;
                    for (int i = 0; i < 8; i++) window.Poll();
                    Assert.That(window.OutcomeCount, Is.EqualTo(retained)); Assert.That(arena.Now, Is.EqualTo(time)); Assert.That(arena.SimulationTickCount, Is.EqualTo(tick));
                    Assert.That(arena.Controls.Current.SimulationPaused, Is.False); Assert.That(arena.Player.InputBlocked, Is.False);
                    Assert.That(window.rootVisualElement.Q<Label>("outcomes").text, Does.Contain("Started").And.Contain("Rejected"));
                    driver.Reset(); window.Poll(); Assert.That(window.OutcomeCount, Is.EqualTo(retained));
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Jump)); window.Poll(); Assert.That(window.OutcomeCount, Is.EqualTo(retained));
                    window.Bind(arena); window.Poll(); Assert.That(window.OutcomeCount, Is.GreaterThan(0));
                    Assert.That(saved.Command.Actor.Session.Equals(arena.Player.InputSession), Is.False);
                    Assert.That(saved.State, Is.EqualTo(CombatCommandState.Started));
                    window.Bind(null); Assert.That(window.OutcomeCount, Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(window); }
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
