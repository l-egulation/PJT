using System;
using System.Collections;
using System.IO;
using Deviant.Combat;
using Deviant.Definitions;
using Deviant.Equipment;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public sealed class RangedActionStartupTests
    {
        string repository;
        [SetUp]public void SetUp()=>repository=BasicMeleeSourceTests.CopyRepository();
        [UnityTearDown]public IEnumerator TearDown(){if(Application.isPlaying)yield return new ExitPlayMode();if(Directory.Exists(repository))Directory.Delete(repository,true);}
        [UnityTest]public IEnumerator GameFreshRuntimeCompilesSavedRangedCandidateBeforeActorCreation()=>Exercise(CombatCompositionProfile.Game);
        [UnityTest]public IEnumerator LabFreshRuntimeCompilesSavedRangedCandidateBeforeActorCreation()=>Exercise(CombatCompositionProfile.Lab);
        IEnumerator Exercise(CombatCompositionProfile profile)
        {
            var source=RangedActionSource.Load(repository);var numeric=source.Catalog.Read("launcher_move_special").ToNumericData();numeric.ranged.speed=16;numeric.damages[0]=7;
            var candidate=source.Catalog.Edit("launcher_move_special",numeric);RangedActionSource.Save(source,candidate,"launcher_move_special");BasicSwordSource.Regenerate(repository);
            var generated=BasicMeleeSourceTests.Generated(repository);RangedActionSource.ValidateGenerated(repository,generated);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            var root=new GameObject("RangedFreshRuntime");var arena=root.AddComponent<CombatArena>();var camera=new GameObject("RangedFreshCamera").AddComponent<Camera>();
            try
            {
                var configuration=CombatContentLoader.Load(null,false);configuration.Profile=profile;configuration.Camera=camera;configuration.Catalog=generated;
                configuration.Loadout=WeaponResources.CreateLoadout();configuration.Enemies=Array.Empty<CombatEnemySpawn>();configuration.PlayerSize=new Vector2(.9375f,2);configuration.PlayerOffset=new Vector2(.1875f,1);
                bool prepared=false;configuration.PrepareEnvironment=value=>{prepared=true;Assert.That(value.Player,Is.Null);Assert.That(value.GetRangedAction("launcher_move_special").ContentHash,Is.EqualTo(candidate.Read("launcher_move_special").ContentHash));};
                Assert.That(arena.Initialize(configuration),Is.True);Assert.That(prepared,Is.True);
                Assert.That(arena.GetRangedAction("launcher_move_special").Execution.Ranged.Speed,Is.EqualTo(16));
                Assert.That(arena.GetRangedAction("launcher_move_special").Execution.Hits[0].Damage,Is.EqualTo(7));
            }
            finally{if(arena.Player)Object.Destroy(arena.Player.gameObject);Object.Destroy(root);Object.Destroy(camera.gameObject);}
        }
    }
}
