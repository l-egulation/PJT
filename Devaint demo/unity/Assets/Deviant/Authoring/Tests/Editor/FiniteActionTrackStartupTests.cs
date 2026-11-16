using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class FiniteActionTrackStartupTests
    {
        string repository;
        [SetUp] public void SetUp() => repository = BasicMeleeSourceTests.CopyRepository();
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        [UnityTest] public IEnumerator GameFreshInitializeCompilesSavedGeneratedTracksBeforeEnvironmentCallbacks() => Exercise(CombatCompositionProfile.Game);
        [UnityTest] public IEnumerator LabFreshInitializeCompilesSavedGeneratedTracksBeforeEnvironmentCallbacks() => Exercise(CombatCompositionProfile.Lab);
        static CharacterAnimationCatalog Animations() => JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
        static LocomotionData Locomotion() => JsonUtility.FromJson<LocomotionData>(Resources.Load<TextAsset>("PlayerLocomotion").text);
        CombatRuntimeConfiguration Configuration(Catalog catalog, Camera camera, CombatCompositionProfile profile, Action<CombatArena> environment)
            => new CombatRuntimeConfiguration {
                Profile = profile, Catalog = catalog, Locomotion = Locomotion(), Animations = Animations(), Camera = camera,
                Loadout = Deviant.Equipment.WeaponResources.CreateLoadout(),
                PlayerSpawn = Vector2.zero, PlayerSize = new Vector2(.9375f, 2), PlayerOffset = new Vector2(.1875f, 1),
                SecondaryFormIndex = 0, PrepareEnvironment = environment,
                Enemies = new[] { new CombatEnemySpawn("FiniteStartupTarget", 0, new Vector2(2, 0), new Vector2(2, 2), Vector2.up, 1000, false, false, 0) }
            };
        IEnumerator Exercise(CombatCompositionProfile profile)
        {
            var original = FiniteActionTrackSource.Load(repository, out var hash); var edited = FiniteActionTrackSourceTests.Edit(original).Select(FiniteActionTrackSourceTests.NumericEdit).ToArray();
            FiniteActionTrackSource.Save(repository, hash, edited); BasicSwordSource.Regenerate(repository);
            var generated = BasicMeleeSourceTests.Generated(repository); FiniteActionTrackSource.ValidateGenerated(repository, generated);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var root = new GameObject("FiniteFreshRuntime"); var camera = new GameObject("FiniteFreshCamera").AddComponent<Camera>();
            var floor = new GameObject("FiniteStartupFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            var arena = root.AddComponent<CombatArena>(); bool called = false;
            try
            {
                Assert.That(arena.Initialize(Configuration(generated, camera, profile, runtime => {
                    called = true; Assert.That(runtime.Player, Is.Null); Assert.That(runtime.ActionTrackCount, Is.EqualTo(ActionTrackDefinition.SupportedIds.Count));
                    Assert.That(runtime.GetActionTracks(ActionTrackDefinition.MovementId).ContentHash, Is.EqualTo(edited[0].ContentHash));
                    foreach (var value in edited) Assert.That(runtime.GetActionTracks(value.Id).ContentHash, Is.EqualTo(value.ContentHash));
                })), Is.True);
                Assert.That(called, Is.True); Assert.That(arena.CompositionProfile, Is.EqualTo(profile));
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); arena.Player.selected = 0; foreach (var weapon in arena.Player.weapons) weapon.StatSlots.Clear();
                    int form = Array.FindIndex(arena.catalog.forms, value => value.id == "base_sword");
                    Assert.That(arena.Player.TryReconfigure(0, form, 3, out var error), Is.True, error);
                    arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1); Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var cast = arena.Player.currentCast;
                    Assert.That(cast.ActionTracks.ContentHash, Is.EqualTo(edited[1].ContentHash)); Assert.That(cast.ImpactTime, Is.EqualTo(.23f).Within(.000001f));
                    for (int tick = 0; tick < 60; tick++) driver.Step(.02f);
                    Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(2)); Assert.That(arena.enemies[0].health, Is.EqualTo(935));
                    driver.Reset(); Assert.That(arena.Player.TryReconfigure(0, form, 2, out error), Is.True, error); arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1); arena.enemies[0].Position = new Vector2(40, 0); Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); for (int tick = 0; tick < 40; tick++) driver.Step(.02f); Assert.That(arena.Player.Position.x, Is.EqualTo(2.4f).Within(.0001f));
                    driver.Reset(); Assert.That(arena.Player.TryReconfigure(0, form, 1, out error), Is.True, error); arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1); arena.enemies[0].Position = new Vector2(5, 0); Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); for (int tick = 0; tick < 60; tick++) driver.Step(.02f); Assert.That(arena.enemies[0].health, Is.EqualTo(953));
                    driver.Reset(); int shield = Array.FindIndex(arena.catalog.forms, value => value.id == "shield"); Assert.That(arena.Player.TryReconfigure(0, shield, 1, out error), Is.True, error); arena.Player.Position = Vector2.zero; Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); for (int tick = 0; tick < 7; tick++) driver.Step(.02f); Assert.That(arena.Player.shield, Is.EqualTo(45));
                    float health = arena.Player.health; Assert.That(arena.Player.Receive(100, arena.enemies[0], -1, false, null), Is.True); Assert.That(arena.Player.health, Is.EqualTo(health - 30).Within(.00001f));
                }
                // Invalid generated graphs abort before the environment callback or actors.
                var invalid = BasicMeleeSourceTests.Generated(repository); invalid.forms.SelectMany(form => form.specials).Single(skill => skill.id == ActionTrackDefinition.MovementId).actionTracks.timeline.frames[0].seconds = 0;
                var rejected = new GameObject("FiniteRejectedStartup").AddComponent<CombatArena>(); bool invalidCalled = false;
                try {
                    LogAssert.Expect(LogType.Error, "Invalid generated finite action tracks: A stable step ID and positive finite duration are required.");
                    Assert.That(rejected.Initialize(Configuration(invalid, camera, profile, _ => invalidCalled = true)), Is.False);
                    Assert.That(invalidCalled, Is.False); Assert.That(rejected.Player, Is.Null);
                }
                finally { Object.Destroy(rejected.gameObject); }
                invalid = BasicMeleeSourceTests.Generated(repository); invalid.forms.SelectMany(form => form.specials).Single(skill => skill.id == ActionTrackDefinition.GuardId).effect.reduction = 2;
                rejected = new GameObject("FiniteRejectedNumericStartup").AddComponent<CombatArena>(); invalidCalled = false;
                try {
                    LogAssert.Expect(LogType.Error, "Invalid generated finite action tracks: Only the migrated finite melee policies without charge, recast, missile or invulnerability are supported.");
                    Assert.That(rejected.Initialize(Configuration(invalid, camera, profile, _ => invalidCalled = true)), Is.False); Assert.That(invalidCalled, Is.False); Assert.That(rejected.Player, Is.Null);
                }
                finally { Object.Destroy(rejected.gameObject); }
            }
            finally { Object.Destroy(root); Object.Destroy(camera.gameObject); Object.Destroy(floor); }
        }
    }
}
