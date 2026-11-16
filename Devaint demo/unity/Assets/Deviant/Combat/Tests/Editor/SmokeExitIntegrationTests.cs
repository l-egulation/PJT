using System;
using System.Collections;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace Deviant.Combat.Tests
{
    public class SmokeExitIntegrationTests
    {
        [UnityTest] public IEnumerator GameClearsSmokeAtExitAndExpiryWithoutClearingBasicStealth() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabClearsSmokeAtExitAndExpiryWithoutClearingBasicStealth() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=false;
            yield return new EnterPlayMode(); yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var p=arena.Player;
            var floor=new GameObject("SmokeExitFloor");floor.transform.position=new Vector3(0,999.5f,0);floor.AddComponent<ArenaSurface>();floor.AddComponent<BoxCollider2D>().size=new Vector2(100,1);
            try { using(var driver=arena.AcquireManualSimulation()) {
                driver.Reset();foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.Position=new Vector2(40,1000);}
                Assert.That(p.TryReconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="dagger"),1,out var error),Is.True,error);
                p.Position=new Vector2(0,1000);p.velocity=Vector2.zero;p.grounded=true;Physics2D.SyncTransforms();
                var source=JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(p.Special));source.activation=.125f;source.effect.duration=.5f;source.end=1;
                var cast=CombatCast.Create(arena,p,source,p.Weapon,false);p.currentCast=cast;
                driver.Step(.125f);driver.Step(.0625f);Assert.That(p.Hidden,Is.True);
                p.Position=new Vector2(8,1000);Physics2D.SyncTransforms();driver.Step(.001f);
                Assert.That(p.smokeUntil,Is.Zero,"Leaving an active smoke region clears only that concealment source on the current tick.");Assert.That(p.Hidden,Is.False);
                p.Position=new Vector2(0,1000);Physics2D.SyncTransforms();driver.Step(.001f);Assert.That(p.Hidden,Is.True);
                p.stealthUntil=10;p.Position=new Vector2(8,1000);Physics2D.SyncTransforms();driver.Step(.001f);Assert.That(p.smokeUntil,Is.Zero);Assert.That(p.Hidden,Is.True);Assert.That(p.stealthUntil,Is.EqualTo(10));
                p.Position=new Vector2(0,1000);Physics2D.SyncTransforms();driver.Step(.001f);p.stealthUntil=0;
                driver.Step(.625f-arena.Now);driver.Step(.001f);Assert.That(p.smokeUntil,Is.Zero,"Expired smoke cannot leave a refresh grace.");Assert.That(p.Hidden,Is.False);
            }} finally { Object.Destroy(floor); }
        }
    }
}
