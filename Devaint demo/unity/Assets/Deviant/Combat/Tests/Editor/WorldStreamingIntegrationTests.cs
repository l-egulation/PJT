using System;
using System.Collections;
using System.Linq;
using Deviant.World.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class WorldStreamingIntegrationTests
    {
        [UnityTest] public IEnumerator ForestConsumesFrozenPlansAndReusesViewsWithoutChangingCommonCombat()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var composition = arena.GetComponent("GameComposition");
            var forest = (MonoBehaviour)composition.GetType().GetField("forest").GetValue(composition);
            var ensure = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), forest, forest.GetType().GetMethod("EnsureChunks"));
            using (var driver = arena.AcquireManualSimulation())
            {
                ensure(4.6875f); var first = Window(forest); var priorViews = Chunks(forest);
                Assert.That(first.Placements.Select(p => p.Index), Is.EqualTo(new[] { 0, 1, 2 }));
                var original = priorViews.Single(c => Index(c) == 0);
                var ground = (Tilemap)original.GetType().GetField("ground").GetValue(original);
                var bounds = ground.cellBounds; var tiles = ground.GetTilesBlock(bounds);
                Assert.That(tiles.Any(t => t != null), Is.True, "The source chunk must contain actual authored tiles.");
                ensure(520); var distant = Window(forest); var recycled = Chunks(forest);
                Assert.That(distant.Placements.Select(p => p.Index), Is.EqualTo(new[] { 7, 8, 9, 10 }));
                Assert.That(recycled.Length, Is.EqualTo(distant.Placements.Count));
                Assert.That(recycled.Count(c => priorViews.Contains(c)), Is.EqualTo(priorViews.Length), "Existing views are reused before allocating another.");
                foreach (var placement in distant.Placements)
                {
                    var chunk = recycled.Single(c => Index(c) == placement.Index);
                    Assert.That(chunk.transform.localPosition.x, Is.EqualTo(placement.WorldX));
                    Assert.That(placement.ModuleIndex, Is.EqualTo(placement.Index % 8));
                }
                var repeated = (Tilemap)recycled.Single(c => Index(c) == 8).GetType().GetField("ground").GetValue(recycled.Single(c => Index(c) == 8));
                Assert.That(repeated.cellBounds, Is.EqualTo(bounds)); Assert.That(repeated.GetTilesBlock(bounds), Is.EqualTo(tiles));
                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.MaxValue })
                    Assert.Throws<ArgumentOutOfRangeException>(() => ensure(invalid));
                Assert.That(Window(forest), Is.SameAs(distant)); Assert.That(Chunks(forest), Is.EquivalentTo(recycled));
                ensure(4.6875f); Assert.That(Chunks(forest).Length, Is.EqualTo(3));
                Assert.That(first.Placements.Select(p => p.Index), Is.EqualTo(new[] { 0, 1, 2 }), "Historical plans cannot change when views are recycled.");
                Assert.That(arena.Player, Is.SameAs(player));
                player.UseTestInput = true; player.ClearInputState(); driver.Step(.02f);
                Assert.That(arena.SimulationFaulted, Is.False);
            }
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static StreamingWindow Window(MonoBehaviour forest) => (StreamingWindow)forest.GetType().GetProperty("CurrentWindow").GetValue(forest);
        static Component[] Chunks(MonoBehaviour forest) => forest.GetComponentsInChildren<Transform>()
            .Select(t => t.GetComponent("InkForestChunk")).Where(c => c != null && c.gameObject.activeInHierarchy).ToArray();
        static int Index(Component chunk) => (int)chunk.GetType().GetField("index").GetValue(chunk);
    }
}
