using System;
using System.Collections;
using Deviant.Control.Contracts;
using Deviant.Equipment;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class StagedCombatInitializationTests
    {
        CombatArena arena;
        int completed;
        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Staged combat").AddComponent<CombatArena>();
            yield return new EnterPlayMode();
            arena = Object.FindAnyObjectByType<CombatArena>(); arena.loadCharacterArt = false; completed = 0;
            arena.Initialized += Completed;
        }
        void Completed() { completed++; }
        CombatRuntimeConfiguration Content()
        {
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game; content.Loadout = WeaponResources.CreateLoadout();
            content.Camera = new GameObject("Staged camera").AddComponent<Camera>();
            content.PlayerSpawn = new Vector2(0, 1000); content.PlayerSize = new Vector2(1, 2); content.PlayerOffset = Vector2.up;
            var enemies = new CombatEnemySpawn[24];
            for (int i = 0; i < enemies.Length; i++) enemies[i] = new CombatEnemySpawn("Enemy " + i, 1, new Vector2(i, 1000), Vector2.one * 2, Vector2.up, 100, false, false, 0);
            content.Enemies = enemies;
            content.PrepareEnvironment = BuildFloor; return content;
        }
        void BuildFloor(CombatArena owner)
        {
            var floor = new GameObject("Staged floor"); floor.transform.position = new Vector2(0, 999.5f);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
        }
        [UnityTest] public IEnumerator BatchesStayPausedAndPublishOnlyAfterEveryEnemyIsReady()
        {
            var steps = arena.InitializeInFrames(Content()); int frames = 0; bool partial = false;
            try
            {
                while (steps.MoveNext())
                {
                    frames++; partial |= arena.Player && arena.enemies.Count > 0 && arena.enemies.Count < 24;
                    Assert.That(arena.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Unconfigured));
                    Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                    Assert.That(arena.Controls.Current.Blocks(InputChannels.Combat | InputChannels.Interaction), Is.True);
                    Assert.That(completed, Is.Zero); Assert.That(arena.SimulationTickCount, Is.Zero);
                    Assert.Throws<InvalidOperationException>(RejectManualWhileInitializing);
                    yield return null;
                }
            }
            finally { ((IDisposable)steps).Dispose(); }
            Assert.That(frames, Is.GreaterThan(3)); Assert.That(partial, Is.True);
            Assert.That(arena.enemies.Count, Is.EqualTo(24)); Assert.That(completed, Is.EqualTo(1));
            Assert.That(arena.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Game));
            Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
            Assert.That(arena.Controls.Current.Blocks(InputChannels.Combat | InputChannels.Interaction), Is.False);
            using (var manual = arena.AcquireManualSimulation()) manual.Step(.01f);
            Assert.That(arena.SimulationTickCount, Is.EqualTo(1));
        }
        void RejectManualWhileInitializing() { using (arena.AcquireManualSimulation()) { } }
        [UnityTest] public IEnumerator StagedMediaPublishesTheSameCompleteCatalogAsSynchronousLoading()
        {
            CheckMediaCatalogs(); yield return null;
        }
        static void CheckMediaCatalogs()
        {
            var expected = CombatContentLoader.Load(null, false);
            CombatRuntimeConfiguration actual = null; int published = 0;
            var loading = CombatContentLoader.LoadInFrames(null, false, content => { actual = content; published++; });
            while (loading.MoveNext()) Assert.That(actual, Is.Null, "partial media must not be published");
            Assert.That(published, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(actual.Catalog), Is.EqualTo(JsonUtility.ToJson(expected.Catalog)));
            Assert.That(JsonUtility.ToJson(actual.Animations), Is.EqualTo(JsonUtility.ToJson(expected.Animations)));
            Assert.That(actual.Animations.motionSprites.Keys, Is.EquivalentTo(expected.Animations.motionSprites.Keys));
        }
        [UnityTest] public IEnumerator CancelledPartialCompositionReleasesItsPauseAndCannotRunCombat()
        {
            var steps = arena.InitializeInFrames(Content());
            try { while (steps.MoveNext() && !arena.Player) { } }
            finally { ((IDisposable)steps).Dispose(); }
            Assert.That(arena.Player, Is.Not.Null); Assert.That(completed, Is.Zero);
            Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
            Assert.That(arena.Controls.Current.Blocks(InputChannels.Combat | InputChannels.Interaction), Is.False);
            Assert.Throws<InvalidOperationException>(RejectManualWhileInitializing);
            yield return null; yield return null;
            Assert.That(arena.SimulationTickCount, Is.Zero);
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
