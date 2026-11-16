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
    public class DirectMeleeSpecialStartupTests
    {
        string repository;
        [SetUp] public void SetUp() => repository = BasicMeleeSourceTests.CopyRepository();
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        [UnityTest] public IEnumerator GameNewInitializationUsesSavedGeneratedDirectNumericVersion() => Exercise(CombatCompositionProfile.Game);
        [UnityTest] public IEnumerator LabNewInitializationUsesSavedGeneratedDirectNumericVersion() => Exercise(CombatCompositionProfile.Lab);
        IEnumerator Exercise(CombatCompositionProfile profile)
        {
            var values = DirectMeleeSpecialSource.Load(repository, out var hash); var numeric = values[0].ToBasicData(); numeric.damage = 42; numeric.impact = .35f; numeric.duration = .85f;
            var candidate = values[0].Recompile(numeric); DirectMeleeSpecialSource.Save(repository, hash, new[] { candidate }); BasicSwordSource.Regenerate(repository);
            var generated = BasicMeleeSourceTests.Generated(repository); DirectMeleeSpecialSource.ValidateGenerated(repository, generated);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var root = new GameObject("DirectNumericFreshArena"); var arena = root.AddComponent<CombatArena>(); var camera = new GameObject("DirectNumericFreshCamera").AddComponent<Camera>();
            var floor = new GameObject("DirectNumericFreshFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            try {
                bool created = false;
                var configuration = new CombatRuntimeConfiguration { Profile = profile, Catalog = generated,
                    Loadout = Deviant.Equipment.WeaponResources.CreateLoadout(),
                    Locomotion = JsonUtility.FromJson<LocomotionData>(Resources.Load<TextAsset>("PlayerLocomotion").text),
                    Animations = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text), Camera = camera,
                    PlayerSpawn = Vector2.zero, PlayerSize = new Vector2(.9375f, 2), PlayerOffset = new Vector2(.1875f, 1), SecondaryFormIndex = 0,
                    PrepareEnvironment = runtime => { created = true; Assert.That(runtime.Player, Is.Null); Assert.That(runtime.GetDirectMeleeSpecial(candidate.Id).ContentHash, Is.EqualTo(candidate.ContentHash)); },
                    Enemies = new[] { new CombatEnemySpawn("DirectNumericFreshTarget", 0, new Vector2(2, 0), new Vector2(2, 2), Vector2.up, 1000, false, false, 0) } };
                Assert.That(arena.Initialize(configuration), Is.True); Assert.That(created, Is.True);
                using (var driver = arena.AcquireManualSimulation()) {
                    driver.Reset(); arena.Player.selected = 0; foreach (var weapon in arena.Player.weapons) weapon.StatSlots.Clear();
                    int form = Array.FindIndex(arena.catalog.forms, value => value.id == candidate.FormId); Assert.That(arena.Player.TryReconfigure(0, form, 0, out var error), Is.True, error);
                    arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1); Physics2D.SyncTransforms(); driver.Step(.02f, new CombatInput(0, CombatButtons.Special));
                    var cast = arena.Player.currentCast; Assert.That(cast.DirectMeleeSpecial.ContentHash, Is.EqualTo(candidate.ContentHash)); Assert.That(cast.ImpactTime, Is.EqualTo(.35f));
                    for (int tick = 0; tick < 70; tick++) driver.Step(.02f);
                    Assert.That(arena.enemies[0].health, Is.EqualTo(958)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));
                }
            }
            finally { Object.Destroy(root); Object.Destroy(camera.gameObject); Object.Destroy(floor); }
        }
    }
}
