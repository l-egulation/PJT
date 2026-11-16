using System;
using System.Collections;
using Deviant.Control.Contracts;
using Deviant.Integration;
using Deviant.Game.Diagnostics;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class PlayerDeathFeedbackTests
    {
        sealed class OptionsPreview:EditorWindow
        {
            public CombatDiagnostics source;
            void OnGUI(){if(source)source.DrawEquipmentPanel(new Rect(0,0,position.width,position.height));}
        }
        [UnityTest,Category("RequiresGraphics")]public IEnumerator ExistingF1PanelRendersTheOutputOptionsWithoutChangingTimeOrDamage()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=true;
            yield return new EnterPlayMode();yield return null;yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var bridge=arena.GetComponent<HitFeedbackBridge>();var workbench=Object.FindAnyObjectByType<PlaytestEnvironment>();
            Assert.That(bridge.Ready,Is.True,bridge.LastError);Assert.That(workbench,Is.Not.Null);
            var diagnostics=Object.FindAnyObjectByType<CombatDiagnostics>();var preview=ScriptableObject.CreateInstance<OptionsPreview>();
            preview.source=diagnostics;preview.position=new Rect(80,80,440,700);diagnostics.showPanel=true;preview.Show();
            int drawn=0;Action probe=()=>drawn++;arena.DrawFeedbackOptions+=probe;
            try
            {
                workbench.SetOpen(true);float now=arena.Now,hp=arena.Player.health;
                double until=EditorApplication.timeSinceStartup+3;
                while(drawn==0&&EditorApplication.timeSinceStartup<until){preview.Repaint();yield return null;}
                Assert.That(drawn,Is.GreaterThan(0),"The existing panel must invoke the explicit presentation port in a real IMGUI frame.");
                foreach(float intensity in new[]{0f,.5f,1f})
                {bridge.SetMotionIntensity(intensity);Assert.That(bridge.motionIntensity,Is.EqualTo(intensity));Assert.That(arena.Now,Is.EqualTo(now));Assert.That(arena.Player.health,Is.EqualTo(hp));Assert.That(arena.Controls.Current.SimulationPaused,Is.True);}
                workbench.SetOpen(false);Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1));
            }
            finally{arena.DrawFeedbackOptions-=probe;workbench.SetOpen(false);preview.Close();}
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();Time.timeScale=1;}
        [UnityTest]public IEnumerator LabOnlyPlayerDeathSlowsAndLocalTimeNeverWritesGlobalTime()=>Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity",false);
        [UnityTest]public IEnumerator GamePlayerDeathAccentSurvivesRunChildCleanupThenReturnsTime()=>Exercise("Assets/Deviant/Game/Scenes/deviant.unity",true);
        static IEnumerator Exercise(string scene,bool globalTime)
        {
            LegacySceneFixture.Open(scene);Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=false;
            yield return new EnterPlayMode();yield return null;yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>();var player=arena.Player;var enemy=arena.enemies[0];
            using(var driver=arena.AcquireManualSimulation())
            {
                driver.Reset();foreach(var target in arena.enemies)target.aggressive=false;
                var bridge=HitFeedbackBridge.Attach(arena);Assert.That(bridge.Ready,Is.True,bridge.LastError);
                player.Die();Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1),"Calling cleanup while alive is not an actual player death.");
                driver.Reset();
                enemy.gameObject.AddComponent<HitFeedbackTarget>().importantDeath=true;enemy.health=1;
                Assert.That(enemy.Receive(100,player,1,false,null),Is.True);bridge.Sample();
                Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1),"No normal, boss, or formerly important monster kill can trigger slow motion.");
                using(var other=arena.Controls.OpenScope("foreign slow"))using(var menu=arena.Controls.OpenScope("foreign pause"))
                {
                    var foreign=((IFeedbackTimeScope)other).RequestSlow(.5f,2);
                    Assert.That(player.Receive(1000,enemy,-1,false,null),Is.True);Assert.That(player.Dead,Is.True);
                    Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(.35f));Assert.That(Time.timeScale,Is.EqualTo(globalTime ? .35f : 1));
                    arena.AdvanceFeedbackAccent(.05f);player.Die();
                    var pause=menu.PauseSimulation();arena.AdvanceFeedbackAccent(.5f);Assert.That(arena.Controls.Current.SimulationScale,Is.Zero);
                    pause.Dispose();Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(.35f));
                    arena.AdvanceFeedbackAccent(.11f);Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(.5f),"Repeated Die must not extend the one death accent or cancel another time owner.");
                    player.Die();Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(.5f));
                    foreign.Dispose();Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1));
                }
                driver.Reset();player.health=1;player.Fall();Assert.That(player.Dead,Is.True);Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(.35f),"Lethal falls use the same native death event.");
                using(var external=arena.Controls.OpenScope("pause retained through reset"))
                {
                    var pause=external.PauseSimulation();driver.Reset();Assert.That(arena.Controls.Current.SimulationPaused,Is.True);pause.Dispose();Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1));
                }
                Action resetAtDeath=()=>arena.ResetTest();player.Died+=resetAtDeath;
                try{Assert.That(player.Receive(1000,enemy,-1,false,null),Is.True);}
                finally{player.Died-=resetAtDeath;}
                Assert.That(player.Dead,Is.False);Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1),"A synchronous death reset must not leave the previous life's slow request.");
                bridge.enabled=false;Assert.That(arena.Controls.Current.SimulationScale,Is.EqualTo(1));
            }
        }
    }
}
