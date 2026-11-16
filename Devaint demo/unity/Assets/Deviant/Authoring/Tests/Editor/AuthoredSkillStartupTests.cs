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
    public class AuthoredSkillStartupTests
    {
        string repository;
        [SetUp] public void SetUp() => repository = BasicMeleeSourceTests.CopyRepository();
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        [UnityTest] public IEnumerator GameFreshInitializeCompilesAllAuthoredSkillsBeforeActors() => Exercise(CombatCompositionProfile.Game);
        [UnityTest] public IEnumerator LabFreshInitializeCompilesAllAuthoredSkillsBeforeActors() => Exercise(CombatCompositionProfile.Lab);
        IEnumerator Exercise(CombatCompositionProfile profile)
        {
            var source = AuthoredSkillSource.Load(repository, out var hash).Single(value => value.Id == "whip_default"); var numeric = source.ToNumericData(); numeric.hits[0].damage = 21; var candidate = source.Recompile(numeric);
            AuthoredSkillSource.Save(repository, hash, new[] { candidate }); BasicSwordSource.Regenerate(repository); var catalog = BasicMeleeSourceTests.Generated(repository); AuthoredSkillSource.ValidateGenerated(repository, catalog);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var root = new GameObject("AuthoredFreshArena"); var arena = root.AddComponent<CombatArena>(); var camera = new GameObject("AuthoredFreshCamera").AddComponent<Camera>();
            var floor = new GameObject("AuthoredFreshFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            try {
                bool prepared = false; var configuration = new CombatRuntimeConfiguration { Profile = profile, Catalog = catalog, Camera = camera, Loadout = Deviant.Equipment.WeaponResources.CreateLoadout(),
                    Locomotion = JsonUtility.FromJson<LocomotionData>(Resources.Load<TextAsset>("PlayerLocomotion").text), Animations = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text),
                    PlayerSpawn = Vector2.zero, PlayerSize = new Vector2(.9375f, 2), PlayerOffset = new Vector2(.1875f, 1), SecondaryFormIndex = 0,
                    PrepareEnvironment = value => { prepared = true; Assert.That(value.Player, Is.Null); Assert.That(value.AuthoredSkillCount, Is.EqualTo(19)); Assert.That(value.GetAuthoredSkill(candidate.Id).ContentHash, Is.EqualTo(candidate.ContentHash)); },
                    Enemies = new[] { new CombatEnemySpawn("AuthoredFreshTarget", 0, new Vector2(2, 0), new Vector2(2, 2), Vector2.up, 1000, false, false, 0) } };
                Assert.That(arena.Initialize(configuration), Is.True); Assert.That(prepared, Is.True);
                using (var driver = arena.AcquireManualSimulation()) {
                    driver.Reset(); int form = Array.FindIndex(arena.catalog.forms, value => value.id == candidate.FormId); Assert.That(arena.Player.TryReconfigure(0, form, 0, out var error), Is.True, error); arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1); Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var cast = arena.Player.currentCast; Assert.That(cast.Definition.Authored.ContentHash, Is.EqualTo(candidate.ContentHash));
                    for (int tick = 0; tick < 80; tick++) driver.Step(.02f); Assert.That(arena.enemies[0].health, Is.EqualTo(979)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));
                }
            }
            finally { Object.Destroy(root); Object.Destroy(camera.gameObject); Object.Destroy(floor); }
        }
    }
}
