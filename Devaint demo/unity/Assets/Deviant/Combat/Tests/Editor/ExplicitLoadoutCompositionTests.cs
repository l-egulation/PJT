using System;
using System.Collections;
using Deviant.Combat.Contracts;
using Deviant.Equipment;
using Deviant.Equipment.Contracts;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class ExplicitLoadoutCompositionTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator ExplicitEquipmentOwnerAndPlayerViewsRemainTheSameAcrossDeathAndDevelopmentReset()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var root = new GameObject("ExplicitLoadoutRuntime"); var arena = root.AddComponent<CombatArena>(); var camera = new GameObject("ExplicitLoadoutCamera").AddComponent<Camera>();
            try
            {
                var loadout = WeaponResources.CreateLoadout(); var a = loadout.GetWeapon(0); var b = loadout.GetWeapon(1);
                var content = CombatContentLoader.Load(null, false); content.Profile = CombatCompositionProfile.Game; content.Camera = camera;
                content.Loadout = loadout; content.Enemies = Array.Empty<CombatEnemySpawn>(); content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
                Assert.That(arena.Initialize(content), Is.True); var player = arena.Player; var view = player.weapons;
                Assert.That(player.weapons[0].InstanceId, Is.EqualTo(a.InstanceId)); Assert.That(player.weapons[1].InstanceId, Is.EqualTo(b.InstanceId));
                a.Stats.SetSlot(0, StatInkKind.SpecialDamage); Assert.That(player.weapons[0].StatSlots[0], Is.EqualTo(2));
                int died = 0; player.Died += () => { died++; Assert.That(player.currentCast, Is.Null); Assert.That(player.CurrentInput.Pressed, Is.EqualTo(CombatButtons.None)); };
                using (var driver = arena.AcquireManualSimulation())
                {
                    player.health = 0; player.Die(); Assert.That(died, Is.EqualTo(1)); driver.Reset();
                    Assert.That(arena.Player, Is.SameAs(player)); Assert.That(player.weapons, Is.SameAs(view));
                    Assert.That(player.weapons[0].InstanceId, Is.EqualTo(a.InstanceId)); Assert.That(player.weapons[1].InstanceId, Is.EqualTo(b.InstanceId));
                    Assert.That(player.weapons[0].StatSlots[0], Is.EqualTo(2)); driver.Step(.02f); Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { if (arena.Player) Object.Destroy(arena.Player.gameObject); Object.Destroy(root); Object.Destroy(camera.gameObject); }
        }
        [UnityTest] public IEnumerator MissingEquipmentOwnerRejectsBeforeAnyActorOrEnvironmentIsCreated()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var root = new GameObject("MissingLoadoutRuntime"); var arena = root.AddComponent<CombatArena>(); var camera = new GameObject("MissingLoadoutCamera").AddComponent<Camera>();
            try
            {
                var content = CombatContentLoader.Load(null, false); content.Profile = CombatCompositionProfile.Game; content.Camera = camera; content.Enemies = Array.Empty<CombatEnemySpawn>();
                bool environment = false; content.PrepareEnvironment = _ => environment = true;
                Assert.Catch(() => arena.Initialize(content)); Assert.That(environment, Is.False); Assert.That(arena.Player, Is.Null); Assert.That(arena.enemies, Is.Empty);
            }
            finally { Object.Destroy(root); Object.Destroy(camera.gameObject); }
        }
    }
}
