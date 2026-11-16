using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class MeadowWorkbenchTests
    {
        CombatArena arena;
        PlaytestEnvironment workbench;
        [UnitySetUp] public IEnumerator Setup()
        {
            LegacySceneFixture.Open("Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity");
            var world=Object.FindObjectsByType<MonoBehaviour>().Single(b=>b.GetType().FullName=="ForestCombatV2.ForestCombatWorld");
            world.GetType().GetField("enablePlaytestWorkbench").SetValue(world,true);
            yield return new EnterPlayMode();yield return null;yield return new WaitForFixedUpdate();
            arena=Object.FindAnyObjectByType<CombatArena>();
            workbench=Object.FindAnyObjectByType<PlaytestEnvironment>();
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(workbench)workbench.SetOpen(false);if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();}
        [UnityTest] public IEnumerator ApprovedMeadowHasFiveSupportedOrdinaryEnemiesPerGroup()
        {
            Assert.That(workbench,Is.Not.Null);Assert.That(arena.enemies.Count,Is.EqualTo(20));
            for(int g=1;g<=4;g++)Assert.That(arena.enemies.Count(e=>e.name.StartsWith("무리 "+g+" ·")),Is.EqualTo(5));
            foreach(var enemy in arena.enemies)
            {
                Assert.That(enemy.isBoss,Is.False);
                Assert.That(arena.SolidOverlap(enemy.Center,enemy.size-Vector2.one*.02f),Is.False,enemy.name+" body must not spawn inside terrain");
                Assert.That(enemy.ProbeGround(),Is.True,enemy.name+" needs actual ground support");
            }
            Assert.That(arena.UnavailableTestForms.Count,Is.EqualTo(0),"The pinned resource selection includes all heavy weapon art");
            Assert.That(arena.Player.visual.sprite,Is.Not.Null);
            yield return null;
        }
        [UnityTest] public IEnumerator MeadowSettingsPauseAndApplyIndependentWeaponSpecs()
        {
            Assert.That(workbench,Is.Not.Null);workbench.SetOpen(true);float now=arena.Now;
            yield return new WaitForSecondsRealtime(.1f);Assert.That(arena.Now,Is.EqualTo(now));
            Assert.That(arena.ApplyTestWeaponTuning(0,"2","3","1.5","1.25",out var error),Is.True,error);
            Assert.That(arena.Player.weapons[0].Speed,Is.EqualTo(1.5));
            Assert.That(arena.Player.weapons[1].Speed,Is.EqualTo(1));
            arena.ResetTest();Assert.That(arena.Player.weapons[0].Speed,Is.EqualTo(1.5));
            Assert.That(arena.Player.weapons[0].Cooldown.Period,Is.EqualTo(1.25));
        }
    }
}
