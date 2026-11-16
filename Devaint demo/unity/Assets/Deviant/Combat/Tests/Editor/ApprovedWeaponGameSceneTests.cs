using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Deviant.Combat.Contracts;
using Object=UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    [Category("RequiresArt")]
    public class ApprovedWeaponGameSceneTests
    {
        CombatArena.ManualSimulation simulation;
        [UnityTearDown] public IEnumerator Teardown(){simulation?.Dispose();if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();}
        [UnityTest] public IEnumerator ForestVer2SelectsAllThirteenAppearancesAndFiresRangedEffects()
        {
            LegacySceneFixture.Open("Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1ForestVer2.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            var arena=Object.FindFirstObjectByType<CombatArena>();Assert.That(arena,Is.Not.Null);
            Assert.That(arena.characterAnimations.sets.Length,Is.EqualTo(13));
            foreach(var enemy in arena.enemies)enemy.aggressive=false;
            var player=arena.Player;player.UseTestInput=true;simulation=arena.AcquireManualSimulation();
            foreach(var set in arena.characterAnimations.sets)
            {
                simulation.Reset();
                player.Reconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id==set.gameplayForm),Mathf.Max(0,set.launcherMode));
                for(int i=0;i<5;i++){simulation.Step(.02f);arena.Presentation.Draw(.02f);}
                Assert.That(arena.Presentation.CurrentForm,Is.EqualTo(set.form));
                Assert.That(player.visual.transform.localScale,Is.EqualTo(Vector3.one));
                if(set.form=="shuriken" || set.form=="bomb")
                {
                    // Exercise the approved sprites in the actual current game map.
                    foreach(var enemy in arena.enemies)enemy.Position=new Vector2(100,0);
                    simulation.Step(.001f,new CombatInput(pressed:CombatButtons.Basic));
                    for(int i=0;i<12;i++){simulation.Step(.01f);arena.Presentation.Draw(.01f);}
                    if(set.form=="shuriken")
                    {
                        Assert.That(arena.PlayerShotCount,Is.EqualTo(1));
                        Assert.That(arena.playerShots[0].GetComponentInChildren<ApprovedThrownVisual>(),Is.Not.Null);
                        simulation.Step(.001f,new CombatInput(pressed:CombatButtons.Basic));
                        for(int i=0;i<28;i++){simulation.Step(.005f);arena.Presentation.Draw(.005f);}
                        Assert.That(arena.Presentation.CurrentClip,Is.EqualTo("attack2"));
                        for(int i=0;i<160 && arena.Presentation.CurrentClip=="attack2";i++){simulation.Step(.005f);arena.Presentation.Draw(.005f);}
                        Assert.That(arena.PlayerShotCount,Is.EqualTo(2));
                        Assert.That(arena.Presentation.CurrentClip,Is.EqualTo("recover2"));
                    }
                    else
                    {
                        for(int i=0;i<10;i++){simulation.Step(.01f);arena.Presentation.Draw(.01f);}
                        Assert.That(arena.bombs.Count,Is.EqualTo(1));
                        Assert.That(arena.bombs[0].GetComponentInChildren<ApprovedThrownVisual>(),Is.Not.Null);
                        Assert.That(arena.bombs[0].damage,Is.EqualTo(24));
                    }
                    continue;
                }
                if(set.launcherMode<0)continue;
                simulation.Step(.01f,new CombatInput(pressed:CombatButtons.Basic));
                for(int i=0;i<22;i++){simulation.Step(.01f);arena.Presentation.Draw(.01f);arena.RangedPresentation?.Tick();}
                Assert.That(arena.Presentation.CurrentBodyClipName,Is.EqualTo("attack_single"));
                CollectionAssert.Contains(set.Find("attack_single").sprites,player.visual.sprite);
                // Every ink fires the ball as its basic attack, so each gun shows the ball muzzle flash.
                Assert.That(arena.RangedPresentation.ActiveCount,Is.GreaterThan(0),set.form);Assert.That(arena.lasers.Count,Is.Zero,set.form);
            }
        }
    }
}
