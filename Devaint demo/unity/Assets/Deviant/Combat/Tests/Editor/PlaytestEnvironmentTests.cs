using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Tests
{
    public class PlaytestEnvironmentTests
    {
        CombatArena arena;
        MonoBehaviour workbench;
        void Open(bool value)=>workbench.GetType().GetMethod("SetOpen").Invoke(workbench,new object[]{value});
        [UnitySetUp] public IEnumerator Setup()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=false;
            yield return new EnterPlayMode();yield return null;
            arena=Object.FindAnyObjectByType<CombatArena>();
            workbench=(MonoBehaviour)arena.GetComponent("PlaytestEnvironment");
            Assert.That(workbench,Is.Not.Null);arena.Player.UseTestInput=true;
        }
        [UnityTearDown] public IEnumerator Cleanup(){Open(false);if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();}
        [UnityTest] public IEnumerator NumericRunSpeedAppliesToMovementAndRejectsInvalidValuesWithoutReset()
        {
            Assert.That(arena.Movement.RunSpeed,Is.EqualTo(7.5f));
            var before=arena.Movement.ToData();
            Open(true);
            Assert.That(arena.TryApplyTestRunSpeed("9.25",out var error),Is.True,error);
            Assert.That(arena.Movement.RunSpeed,Is.EqualTo(9.25f));
            Assert.That(arena.Movement.DashSpeed,Is.EqualTo(before.dashSpeed));
            Assert.That(arena.Movement.FirstJumpVelocity,Is.EqualTo(before.firstJumpVelocity));
            Assert.That(arena.Movement.WallJumpHorizontalSpeed,Is.EqualTo(before.wallJump.horizontalSpeed));
            Open(false);
            yield return null; // Let the input scope transition clear its paused samples.
            arena.Player.UseTestInput=true;
            foreach(var enemy in arena.enemies)enemy.aggressive=false;
            using(var simulation=arena.AcquireManualSimulation())
            {
                simulation.Step(.01f);
                arena.Player.axis=1;float x=arena.Player.Position.x;
                for(int i=0;i<20;i++)simulation.Step(.01f,new CombatInput(1));
                Assert.That(arena.Player.Position.x-x,Is.EqualTo(9.25f*.2f).Within(.04f));
                var position=arena.Player.Position;
                foreach(string invalid in new[]{"NaN","Infinity","-1","abc",""})
                {
                    Assert.That(arena.TryApplyTestRunSpeed(invalid,out error),Is.False,invalid);
                    Assert.That(arena.Movement.RunSpeed,Is.EqualTo(9.25f));
                    Assert.That(arena.Player.Position,Is.EqualTo(position),"Invalid text must not reset combat.");
                }
            }
            arena.ResetTest();Assert.That(arena.Movement.RunSpeed,Is.EqualTo(9.25f));
            yield return null;
        }
        [UnityTest] public IEnumerator DefaultGroupsAreFiveOrdinaryEnemiesEachAndResetKeepsTheirSpawns()
        {
            Assert.That(arena.enemies.Count,Is.EqualTo(20));
            Assert.That(arena.enemies.All(e=>!e.isBoss),Is.True);
            for(int g=1;g<=4;g++)Assert.That(arena.enemies.Count(e=>e.name.StartsWith("무리 "+g+" ·")),Is.EqualTo(5));
            var starts=arena.enemies.Select(e=>e.spawn).ToArray();
            Assert.That(starts[5].x-starts[0].x,Is.EqualTo(32).Within(.001));
            arena.ResetTest();
            for(int i=0;i<starts.Length;i++)Assert.That(arena.enemies[i].Position,Is.EqualTo(starts[i]));
            yield return null;
        }
        [UnityTest] public IEnumerator SettingsPauseCombatAndDoNotReleaseAnotherOwnersPause()
        {
            Open(true);float now=arena.Now;
            Assert.That(arena.Player.EditingAllowed,Is.True);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(arena.Now,Is.EqualTo(now));
            using(var other=arena.Controls.OpenScope("other pause"))
            {
                other.PauseSimulation();Open(false);
                Assert.That(arena.Controls.Current.SimulationPaused,Is.True);
            }
            Assert.That(arena.Controls.Current.SimulationPaused,Is.False);
            float timeout=Time.realtimeSinceStartup+5;
            while(arena.Now<=now){Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout));yield return null;}
        }
        [UnityTest] public IEnumerator WeaponSpecsRemainPerSlotAndSurviveResetAndSpecialChange()
        {
            Open(true);
            Assert.That(arena.ApplyTestWeaponTuning(0,"2","3","1.5","1.25",out var error),Is.True,error);
            var w=arena.Player.weapons[0];
            Assert.That(w.BasicBonus,Is.EqualTo(1));Assert.That(w.SpecialBonus,Is.EqualTo(2));
            Assert.That(w.Speed,Is.EqualTo(1.5));Assert.That(w.Cooldown.Period,Is.EqualTo(1.25));
            Assert.That(arena.Player.weapons[1].Speed,Is.EqualTo(1));
            arena.Player.Reconfigure(0,w.FormIndex,1);
            Assert.That(w.SpecialIndex,Is.EqualTo(1));Assert.That(w.Cooldown.Period,Is.EqualTo(1.25));
            arena.ResetTest();Assert.That(w.Speed,Is.EqualTo(1.5));Assert.That(w.Cooldown.Period,Is.EqualTo(1.25));
            Assert.That(arena.ApplyTestWeaponTuning(0,"NaN","1","1","-1",out error),Is.False);
            Assert.That(w.BasicBonus,Is.EqualTo(1));
            Assert.That(arena.ApplyTestWeaponTuning(0,"1","1","0","-1",out error),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator RegroupReplacesRatherThanAccumulatesAndRejectsOverlappingGroups()
        {
            Open(true);
            var apply=workbench.GetType().GetMethod("ApplyGroups");
            Assert.That(apply.Invoke(workbench,new object[]{"3","2","32"}),Is.EqualTo(true));
            Assert.That(arena.enemies.Count,Is.EqualTo(6));
            Assert.That(apply.Invoke(workbench,new object[]{"3","2","32"}),Is.EqualTo(true));
            Assert.That(arena.enemies.Count,Is.EqualTo(6));
            Assert.That(apply.Invoke(workbench,new object[]{"8","2","20"}),Is.EqualTo(false));
            Assert.That(arena.enemies.Count,Is.EqualTo(6));
            yield return null;
        }
        [UnityTest] public IEnumerator WeaponSpeedTrialsUseCurrentTimingAndFollowFormAcrossSlots()
        {
            Open(true);
            var ids=new[]{"greatsword","hammer","whip"};
            var scales=new[]{.738f,.8175f,1.3f};
            var recoveries=new[]{.2f,.2f,0f};
            var totals=new[]{1607.1429f,1228.8421f,1300f};
            for(int i=0;i<ids.Length;i++)
            {
                int index=System.Array.FindIndex(arena.catalog.forms,f=>f.id==ids[i]);
                var form=arena.catalog.forms[index];
                arena.Player.Reconfigure(0,index,0);
                var weapon=arena.Player.weapons[0];
                Assert.That(weapon.Speed,Is.EqualTo(scales[i]).Within(.00001f));
                float damage=form.basics.Sum(b=>b.damage), duration=form.basics.Sum(b=>b.duration);
                Assert.That(damage/(duration/weapon.Speed+recoveries[i])*60,Is.EqualTo(totals[i]).Within(.01f));
                Assert.That(weapon.BasicBonus,Is.Zero,"Trial changes timing only");
                Assert.That(arena.Player.weapons[1].Speed,Is.EqualTo(1));
                using(var simulation=arena.AcquireManualSimulation())
                {
                    Open(false);
                    simulation.Step(.02f,new CombatInput(0,CombatButtons.Basic,false));
                    for(int hit=0;hit<form.basics.Length;hit++)
                    {
                        var cast=arena.Player.currentCast;
                        Assert.That(cast,Is.Not.Null);
                        Assert.That(arena.Player.BasicIndex,Is.EqualTo(hit));
                        float extra=hit==form.basics.Length-1 ? recoveries[i] : 0;
                        Assert.That(cast.MotionDuration,Is.EqualTo(form.basics[hit].duration/scales[i]+extra).Within(.00001f));
                        Assert.That(cast.ImpactTime,Is.EqualTo(form.basics[hit].impact/scales[i]).Within(.00001f));
                        Assert.That(cast.Definition.Hits[0].Active,Is.EqualTo(form.basics[hit].active/scales[i]).Within(.00001f));
                        Assert.That(cast.Definition.Hits[0].Damage,Is.EqualTo(form.basics[hit].damage));
                        float end=cast.EndTime;
                        int steps=0;
                        while(arena.Player.currentCast==cast)
                        {
                            Assert.That(++steps,Is.LessThan(150));
                            simulation.Step(.01f,new CombatInput(0,CombatButtons.Basic,false));
                        }
                        Assert.That(arena.Now,Is.GreaterThanOrEqualTo(end-.00001f),"Next basic must wait through final recovery");
                    }
                    Open(true);arena.ResetTest();
                }
                Assert.That(weapon.Speed,Is.EqualTo(scales[i]).Within(.00001f));
            }
            int whip=System.Array.FindIndex(arena.catalog.forms,f=>f.id=="whip");
            int sword=System.Array.FindIndex(arena.catalog.forms,f=>f.id=="base_sword");
            arena.Player.Reconfigure(0,sword,0);arena.Player.Reconfigure(1,whip,0);
            Assert.That(arena.Player.weapons[0].Speed,Is.EqualTo(1));
            Assert.That(arena.Player.weapons[1].Speed,Is.EqualTo(1.3f).Within(.00001f));
            Assert.That(arena.ApplyTestWeaponTuning(1,"1","1","2","-1",out var error),Is.True,error);
            arena.Player.weapons[1].StatSlots[0]=3;
            Assert.That(arena.Player.weapons[1].Speed,Is.EqualTo(1.3f*2*1.1f).Within(.00001f));
            yield return null;
        }
        [UnityTest] public IEnumerator KeyboardPanelTogglesOnceAndResetWorksAfterDeath()
        {
            var settings=InputSystem.settings;var background=settings.backgroundBehavior;
            var editorInput=settings.editorInputBehaviorInPlayMode;
            Keyboard keyboard=null;arena.Player.UseTestInput=false;
            try
            {
                settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard=InputSystem.AddDevice<Keyboard>();
                var adapter=arena.Player.GetComponent<CombatKeyboardInput>();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F1));InputSystem.Update();keyboard.MakeCurrent();
                workbench.SendMessage("Update");adapter.SendMessage("Update");
                Assert.That(arena.TestConfigurationOpen,Is.True);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F1));InputSystem.Update();
                adapter.SendMessage("Update");workbench.SendMessage("Update");
                Assert.That(arena.TestConfigurationOpen,Is.False);
                Assert.That(arena.showPanel,Is.False);
                Assert.That(arena.Controls.Current.SimulationPaused,Is.False);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                arena.Player.health=0;
                Assert.That(arena.Player.Dead,Is.True);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F5));InputSystem.Update();
                adapter.SendMessage("Update");
                Assert.That(arena.Player.Dead,Is.False,"F5 must still reset after player death");
                Assert.That(arena.Player.health,Is.EqualTo(arena.Player.maxHealth));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            }
            finally
            {
                if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior=background;settings.editorInputBehaviorInPlayMode=editorInput;
                arena.Player.UseTestInput=true;
            }
            yield return null;
        }
    }
}
