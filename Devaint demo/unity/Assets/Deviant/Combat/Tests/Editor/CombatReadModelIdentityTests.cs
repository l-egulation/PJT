using System;
using System.Collections;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class CombatReadModelIdentityTests
    {
        [UnityTest] public IEnumerator NativeBindingsRejectAnotherArenaAndDestroyedActorsWithoutFindingReplacements()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("FirstReadArena").AddComponent<CombatArena>();
            new GameObject("SecondReadArena").AddComponent<CombatArena>();
            yield return new EnterPlayMode(); yield return null;
            var arenas = Object.FindObjectsByType<CombatArena>(FindObjectsSortMode.None);
            foreach (var arena in arenas)
            {
                var content = CombatContentLoader.Load(null, false);
                content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
                content.Profile = CombatCompositionProfile.Game; content.Camera = new GameObject("ReadCamera").AddComponent<Camera>();
                content.PlayerSpawn = new Vector2(100, 1000); content.Enemies = Array.Empty<CombatEnemySpawn>();
                content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
                Assert.That(arena.Initialize(content), Is.True);
            }
            var first = arenas[0]; var second = arenas[1]; var reads = first.ReadModel;
            Assert.That(first.TryBindActor(first.Player, out var firstPlayer), Is.True);
            Assert.That(second.TryBindActor(second.Player, out var secondPlayer), Is.True);
            Assert.That(first.TryBindActor(second.Player, out _), Is.False);
            Assert.That(reads.TryReadActor(secondPlayer, out _), Is.False);
            Assert.That(reads.TryReadPlayer(secondPlayer.Session, out _), Is.False);
            Assert.That(second.ReadModel.TryReadActor(firstPlayer, out _), Is.False);
            first.SpawnDecoy(first.Player, new Vector2(1, 1000), new EffectData { hp = 10, duration = 3 });
            var decoy = first.decoys[0]; Assert.That(first.TryBindActor(decoy, out var handle), Is.True);
            Object.DestroyImmediate(decoy.gameObject);
            Assert.That(reads.TryReadActor(handle, out _), Is.False);
            Assert.That(first.TryBindActor(decoy, out _), Is.False);
            Assert.That(reads.TryReadActor(new CombatActorHandle(firstPlayer.Session, second.Player.ActorId), out _), Is.False);
            var prior = reads;
            Object.DestroyImmediate(first);
            Assert.That(prior.TryGetSession(out _), Is.False);
            Assert.That(prior.TryReadPlayer(firstPlayer.Session, out _), Is.False);
            Assert.That(second.ReadModel.TryReadActor(secondPlayer, out _), Is.True, "Destroying one session cannot invalidate another arena.");
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
