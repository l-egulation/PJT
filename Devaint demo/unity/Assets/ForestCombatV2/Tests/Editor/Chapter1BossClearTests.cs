using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1BossClearTests
    {
        [UnityTearDown]public IEnumerator Cleanup()
        {
            DevFeatures.ForceForTests(null);
            if(Application.isPlaying)yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator DebugTravelIntoTheExistingBossRoomPreservesTheRunAndLoadout()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int frame=0;frame<600&&(!world||!world.Ready);frame++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);yield return null;yield return null;
            foreach(var enemy in world.arena.enemies)if(enemy)enemy.aggressive=false;
            var selector=Object.FindAnyObjectByType<Chapter1MapSelector>();Assert.That(selector,Is.Not.Null);
            var player=world.arena.Player;var offers=world.GetComponent<Chapter1StartingBlueprints>();
            // The village departure now starts the run unarmed (no start-room offers); pick two only where they are offered.
            if(!offers.Complete)
            {
                player.Position=offers.OfferPosition(0);Assert.That(offers.TryChoose(0),Is.True);
                player.Position=offers.OfferPosition(1);Assert.That(offers.TryChoose(1),Is.True);
            }
            player.health=player.maxHealth-10;
            var before=player.Position;float health=player.health;
            int seed=world.Data.generationSeed,enemies=world.arena.enemies.Count;
            var scene=SceneManager.GetActiveScene().handle;
            var run=world.GetComponent<Chapter1RunBinding>().Run;
            var weapons=player.weapons.Select(w=>w.InstanceId).ToArray();
            var clock=world.GetComponent<ClockExecutionerEncounter>();float bossHealth=clock.Boss.health;
            var inputSettings=InputSystem.settings;var background=inputSettings.backgroundBehavior;var editorInput=inputSettings.editorInputBehaviorInPlayMode;
            inputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
            try
            {
                void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();}
                DevFeatures.ForceForTests(false);Keys(Key.F10);
                Assert.That(selector.HandleKeyboard(),Is.False);Assert.That(player.Position,Is.EqualTo(before),"Release builds ignore the development shortcut.");
                DevFeatures.ForceForTests(true);Keys();
                using(var pause=world.arena.Controls.OpenScope("F10 pause fixture"))
                {
                    pause.PauseSimulation();Keys(Key.F10);Assert.That(selector.HandleKeyboard(),Is.False);
                    Assert.That(player.Position,Is.EqualTo(before));Keys();
                }
                player.velocity=new Vector2(7,-12);Keys(Key.F10);
                Assert.That(selector.HandleKeyboard(),Is.False,"Legacy F10 is removed.");
                Assert.That(selector.gameObject,Is.Not.EqualTo(world.gameObject),"The selector lives on a separate scene root.");
                Object.DestroyImmediate(selector.gameObject);
                var menu=world.GetComponent<DebugGameMenu>();Assert.That(menu.Open(),Is.True);
                menu.SelectCategory(0);
                Assert.That(menu.SelectedLabel,Is.EqualTo("현재 런 보스방으로 이동"),"The actual console button remains enabled without a legacy selector");
                using(var foreignPause=world.arena.Controls.OpenScope("external pause during menu travel"))
                {
                    foreignPause.PauseSimulation();
                    foreignPause.BlockInput(Deviant.Control.Contracts.InputChannels.Combat);
                    Assert.That(menu.ExecuteSelected(),Is.True,"Console travel is allowed while paused and input-blocked");
                    Assert.That(player.Position.x,Is.EqualTo(clock.Room.x+3).Within(.01f));
                    Assert.IsTrue(world.arena.Controls.Current.SimulationPaused);
                    Assert.IsTrue(world.arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat));
                }
                Assert.That(menu.ExecuteSelected(),Is.True,"Execute the same command the console button dispatches.");
                Assert.That(world.arena.Controls.Current.SimulationPaused,Is.True,"The menu stays paused after travel.");
                menu.Close();Keys();
                var enclosure=world.GetComponent<Chapter1BossRoomEnclosure>();
                Assert.That(world.GetComponent<Chapter1BossEncounter>().PlayerInBossRoom,Is.True);
                Assert.That(player.Position.x,Is.EqualTo(clock.Room.x+3).Within(.01f));
                Assert.That(player.Position.y,Is.EqualTo(clock.Boss.Floor+.02f).Within(.01f));
                Assert.That(player.velocity,Is.EqualTo(Vector2.zero));Assert.That(player.ProbeGround(),Is.True);
                Assert.That(enclosure.Holding&&enclosure.EntryShut,Is.True);
                Assert.That(world.FixedCameraCenter,Is.EqualTo((Vector2?)enclosure.CameraCenter));
                Assert.That(SceneManager.GetActiveScene().handle,Is.EqualTo(scene));Assert.That(Object.FindAnyObjectByType<ForestCombatWorld>(),Is.SameAs(world));
                Assert.That(world.Data.generationSeed,Is.EqualTo(seed));Assert.That(world.clockBossTestOnly,Is.False);
                Assert.That(world.GetComponent<Chapter1RunBinding>().Run,Is.SameAs(run));
                Assert.That(player.weapons.Select(w=>w.InstanceId),Is.EqualTo(weapons));Assert.That(offers.Complete,Is.True);
                Assert.That(player.health,Is.EqualTo(health));Assert.That(world.arena.enemies.Count,Is.EqualTo(enemies));Assert.That(clock.Boss.health,Is.EqualTo(bossHealth));
                float deadline=Time.realtimeSinceStartup+6;
                while(Time.realtimeSinceStartup<deadline&&!clock.IntroPlaying&&!clock.Boss.Started)yield return null;
                Assert.That(clock.IntroPlaying||clock.Boss.Started,Is.True,"The normal boss introduction follows the warp. Player="+player.Position);
                if(clock.IntroPlaying)clock.AdvanceIntro(clock.IntroDuration+.01f);Assert.That(clock.Boss.Started,Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);DevFeatures.ForceForTests(null);
                inputSettings.backgroundBehavior=background;inputSettings.editorInputBehaviorInPlayMode=editorInput;
            }
        }
        [UnityTest] public IEnumerator BossRoomHoldsTheClockExecutionerAndItsDefeatOpensTheChapterExit()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int frame = 0; frame < 600 && (!world || !world.Ready); frame++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            yield return null;

            var encounter = world.GetComponent<Chapter1BossEncounter>();
            var binding = world.GetComponent<Chapter1RunBinding>();
            Assert.That(encounter, Is.Not.Null);
            var boss = encounter.Boss;
            Assert.That(boss, Is.Not.Null);
            Assert.That(boss.isBoss, Is.True);
            var clock = world.GetComponent<ClockExecutionerEncounter>();
            Assert.That(clock, Is.Not.Null, "the random stage runs the stationary clock executioner fight");
            Assert.That(boss, Is.SameAs(clock.Boss), "the chapter exit follows the clock executioner");
            Assert.That(boss.maxHealth, Is.EqualTo(clock.provisionalHealth));
            Assert.That(boss.name, Is.EqualTo("시계탑 집행자"));
            Assert.That(world.arena.enemies.Count(e => e && e.isBoss), Is.EqualTo(1), "no boss dummy is spawned beside it");
            Assert.That(Chapter1EnemyRole.Of(boss), Is.EqualTo(Chapter1EnemyRole.Boss));
            var room = world.Data.rooms.Single(r => r.kind == "boss");
            Assert.That(boss.Position.x, Is.InRange((float)room.x, (float)(room.x + room.w)), "boss stands in the final boss room");

            var player = world.arena.Player;
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(boss.aggressive, Is.False, "the boss waits until the player enters its room");

            player.Position = encounter.ExitPosition;
            yield return null;
            Assert.That(encounter.PlayerInBossRoom, Is.True);
            var enclosure = world.GetComponent<Chapter1BossRoomEnclosure>();
            Assert.That(room.w, Is.EqualTo(Chapter1ProceduralMap.BossRoomWidth), "every boss room is one 30-unit camera view");
            Assert.That(enclosure.Holding, Is.True, "entering the boss room closes its entry");
            Assert.That(world.FixedCameraCenter, Is.EqualTo((Vector2?)enclosure.CameraCenter), "the boss room camera is fixed");
            // Entering starts the walk-in and the entry cutscene; the fight begins once the bars leave.
            for (int frame = 0; frame < 300 && !clock.IntroPlaying; frame++) yield return null;
            Assert.That(clock.IntroPlaying, Is.True, "entering the room plays the entry cutscene");
            clock.AdvanceIntro(clock.IntroDuration + .01f);
            Assert.That(boss.aggressive && ((ClockExecutionerBoss)boss).Started, Is.True);
            Assert.That(encounter.ExitOpen, Is.False);
            Assert.That(encounter.TryUseExit(), Is.False, "the exit stays closed while the boss lives");
            Assert.That(binding.Run.Ended, Is.False);

            player.health = player.maxHealth = 100000;
            // A real accepted hit plays the defeat outro (setting health alone skips it).
            boss.Receive(boss.health + 1, player, 1, false, null, true);
            for (int frame = 0; frame < 60 && !binding.Run.BossDefeated; frame++) yield return null;
            Assert.That(binding.Run.BossDefeated, Is.True);
            // Boss gold credits at once and its brief floor visual may end during the outro below.
            var coins=world.GetComponent<Chapter1Rewards>().Pickups.Where(p=>p.Kind==Chapter1RewardKind.Gold).ToArray();
            // The boss weapon choice waits for the defeat outro, which keeps playing with the player held still (QA 2026-11-13).
            var sketchRun = binding.Sketch;
            Assert.That(clock.DeathCutscene, Is.True, "the boss collapse plays first");
            Assert.That(world.arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.True, "the player holds still");
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False, "the collapse is not paused behind a choice");
            if (sketchRun)
            {
                Assert.That(binding.BossOfferPending, Is.True); Assert.That(sketchRun.State.PendingOffer, Is.Null, "no choice covers the collapse");
                player.Position = encounter.ExitPosition; Assert.That(encounter.TryUseExit(), Is.False, "the exit waits for the choice");
            }
            float outroDeadline = Time.realtimeSinceStartup + clock.CutsceneDuration + 2;
            while (clock.DeathCutscene && Time.realtimeSinceStartup < outroDeadline) yield return null;
            yield return null;
            Assert.That(clock.DeathCutscene, Is.False); Assert.That(binding.BossOfferPending, Is.False);
            if (sketchRun) { Assert.That(sketchRun.State.PendingOffer, Is.Not.Null, "the boss weapon choice opens after the outro"); Assert.That(sketchRun.State.PendingIsBoss, Is.True); }
            Assert.That(enclosure.Holding && world.FixedCameraCenter == enclosure.CameraCenter, Is.True, "the camera stays fixed after the boss falls");
            Assert.That(encounter.ExitOpen, Is.True);
            yield return new WaitForSecondsRealtime(.8f);
            world.RefreshCamera(0,true);
            ApprovedVillageIntegrationTests.Capture(world.View,"boss-defeated");
            Assert.That(coins.Sum(p=>p.GoldAmount),Is.GreaterThanOrEqualTo(300),"boss gold has a brief floor visual");
            foreach(var coin in coins){Assert.That(coin.Collected,Is.True);Assert.That(coin.TryCollect(),Is.False);}
            Assert.That(binding.Run.Economy.Gold,Is.GreaterThanOrEqualTo(300),"boss gold is credited before using the exit");

            player.Position = encounter.ExitPosition + Vector2.left * 6;
            Assert.That(encounter.TryUseExit(), Is.False, "the exit needs the player beside it");
            player.Position = encounter.ExitPosition;
            // Banked sketch rewards are chosen before leaving the stage (rewards.md 받아 둔 보상): choose each one.
            var sketch=world.GetComponent<Chapter1RunBinding>().Sketch;
            for(int pick=0;pick<20&&sketch&&sketch.State!=null&&sketch.State.MustOpenBeforeExit;pick++)
            {if(sketch.State.PendingOffer==null)sketch.State.OpenBanked();sketch.State.Choose(0);}
            Assert.That(encounter.TryUseExit(), Is.True);
            Assert.That(encounter.ExitOpening,Is.True);
            Assert.That(binding.ShowingResult,Is.False,"result waits for portcullis animation");
            Assert.That(encounter.TryUseExit(),Is.False,"do not open twice");
            var continuedRun=binding.Run;
            float exitDeadline=Time.realtimeSinceStartup+8;
            while(encounter&&!encounter.ExitOpened&&Time.realtimeSinceStartup<exitDeadline)yield return null;
            Assert.That(encounter&&encounter.ExitOpened,Is.True);
            ApprovedVillageIntegrationTests.Capture(world.View,"boss-sewer-open");
            Assert.That(binding.ShowingResult,Is.False,"chapter one does not show a run result");
            while(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Chapter2/Scenes/Chapter2Procedural.unity"&&Time.realtimeSinceStartup<exitDeadline)yield return null;
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,Is.EqualTo("Assets/Chapter2/Scenes/Chapter2Procedural.unity"));
            Assert.That(Chapter1RunHost.Instance.Flow.Current,Is.SameAs(continuedRun));
            Assert.That(continuedRun.Ended,Is.False);Assert.That(continuedRun.Chapter,Is.EqualTo(2));
            yield return new ExitPlayMode();
        }
    }
}
