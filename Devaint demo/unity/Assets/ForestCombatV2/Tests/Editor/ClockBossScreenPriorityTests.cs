using System;
using System.Collections;
using Deviant.Combat;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class ClockBossScreenPriorityTests
    {
        IDisposable profile;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            profile?.Dispose(); profile = null;
        }

        [UnityTest] public IEnumerator IntroEnrageAndFinalPhaseReleaseScreenOwnershipOnSkipAndDisable()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            profile = Chapter1ProfileHost.UseTemporaryStore(new MemoryProfileStore());
            EditorSceneManager.LoadSceneInPlayMode(Chapter1MapSelector.ProceduralScene, new LoadSceneParameters(LoadSceneMode.Single));
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int frame = 0; frame < 1200 && (!world || !world.Ready); frame++) {
                yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>();
            }
            Assert.That(world && world.Ready, Is.True);
            yield return null;
            var arena = world.arena; var encounter = world.GetComponent<ClockExecutionerEncounter>(); var boss = encounter.Boss;
            using (var simulation = arena.AcquireManualSimulation()) {
                arena.Player.Position = new Vector2(encounter.Room.x + 3, boss.Floor);
                arena.Player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                encounter.BeginIntro();
                Assert.That(arena.CombatScreenEffectsAllowed, Is.False);
                Assert.That(encounter.TrySkip(), Is.True);
                Assert.That(arena.CombatScreenEffectsAllowed, Is.True);
                boss.health = boss.EnrageHealth + 1;
                boss.Receive(2, arena.Player, 1, false, null, true); boss.Step(0);
                Assert.That(encounter.CutscenePlaying, Is.True);
                Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                Assert.That(arena.CombatScreenEffectsAllowed, Is.False);
                encounter.AdvanceCutscene(encounter.CutsceneDuration + .01f);
                Assert.That(arena.CombatScreenEffectsAllowed, Is.True);
                boss.health = boss.FinalPhaseHealth + 1;
                boss.Receive(2, arena.Player, 1, false, null, true); boss.Step(0);
                Assert.That(boss.FinalPhase, Is.True);
                Assert.That(arena.CombatScreenEffectsAllowed, Is.False);
                encounter.enabled = false;
                Assert.That(arena.CombatScreenEffectsAllowed, Is.True, "disabling an encounter cannot leave the next fight suppressed");
            }
        }
    }
}
