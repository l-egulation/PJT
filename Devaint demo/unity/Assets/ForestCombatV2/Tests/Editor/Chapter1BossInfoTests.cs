using System.Collections;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // Health-based phases share the real stage and its skippable encounter presentation.
    public sealed class Chapter1BossInfoTests
    {
        Keyboard keyboard;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard); keyboard = null;
                InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        static void OpenSeededStage()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
        }

        static ForestCombatWorld ReadyWorld()
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            return world && world.Ready && world.GetComponent<ClockExecutionerEncounter>() ? world : null;
        }

        // Inside the boss room, past the entry, on its floor.
        static void EnterBossRoom(ForestCombatWorld world, ClockExecutionerEncounter encounter)
        {
            foreach (var enemy in world.arena.enemies) if (enemy && !enemy.isBoss) enemy.aggressive = false;
            var player = world.arena.Player;
            player.maxHealth = player.health = 100000;
            player.Position = new Vector2(encounter.Room.x + 3, encounter.Boss.Floor + .02f); player.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        void AddKeyboard()
        {
            var settings = InputSystem.settings;
            background = settings.backgroundBehavior; editorInput = settings.editorInputBehaviorInPlayMode;
            // The batch Editor has no focused Game View for this synthetic device.
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        }

        [UnityTest] public IEnumerator FSkipsTheEntryAndThePhaseTwoRoar()
        {
            OpenSeededStage();
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 600 && !ReadyWorld(); frame++) yield return null;
            var world = ReadyWorld(); Assert.That(world, Is.Not.Null);
            var encounter = world.GetComponent<ClockExecutionerEncounter>(); var boss = encounter.Boss;
            AddKeyboard();
            var skip = CombatKeyboardBindings.For(world.arena.Player).Interact;
            Assert.That(skip, Is.EqualTo(Key.F));

            EnterBossRoom(world, encounter);
            for (int frame = 0; frame < 120 && !encounter.SkipAvailable; frame++) yield return null;
            Assert.That(encounter.SkipAvailable && !boss.Started, Is.True, "entering the room starts the skippable entry");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(skip));
            for (int frame = 0; frame < 10 && !boss.Started; frame++) yield return null;
            Assert.That(boss.Started && !encounter.IntroPlaying && !encounter.SkipAvailable, Is.True, "F starts the fight at once");
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;

            boss.aggressive = false;
            boss.Receive(boss.health - boss.EnrageHealth + 1, world.arena.Player, 1, false, null, true);
            Assert.That(boss.PhaseTwo && encounter.CutscenePlaying && encounter.SkipAvailable, Is.True, "phase two opens its roar");
            Assert.That(boss.bossBarColor, Is.EqualTo(ClockExecutionerBoss.PhaseTwoBar));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(skip));
            for (int frame = 0; frame < 10 && encounter.CutscenePlaying; frame++) yield return null;
            Assert.That(encounter.CutscenePlaying, Is.False, "F ends the phase-two roar");
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.False, "the fight resumes");
            Assert.That(boss.PhaseTwo && !boss.Dead, Is.True);
        }

        [Test] public void SealPageEmphasisKeepsAllThreeIconCentresAligned()
        {
            var type=typeof(Chapter1SealChallenge).Assembly.GetType("ForestCombatV2.ApprovedWorldNotice");
            var bounds=type.GetMethod("SealPageBounds",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            for(int selected=0;selected<3;selected++)
            {
                var icons=new Rect[3];
                for(int i=0;i<3;i++)icons[i]=(Rect)bounds.Invoke(null,new object[]{i,3,i==selected});
                Assert.That(icons[0].center.y,Is.EqualTo(icons[1].center.y));
                Assert.That(icons[1].center.y,Is.EqualTo(icons[2].center.y));
                Assert.That(icons[1].center.x-icons[0].center.x,Is.EqualTo(icons[2].center.x-icons[1].center.x));
                Assert.That(icons[1].center.x,Is.EqualTo(480));
                Assert.That(icons[selected].width,Is.GreaterThan(icons[(selected+1)%3].width));
            }
        }

        [UnityTest] public IEnumerator EntryNoticeWaitsForTheFightAndRetainsTimeWhileMenusPause()
        {
            OpenSeededStage();
            yield return new EnterPlayMode();
            for(int frame=0;frame<600&&!ReadyWorld();frame++)yield return null;
            var world=ReadyWorld();Assert.That(world,Is.Not.Null);
            var encounter=world.GetComponent<ClockExecutionerEncounter>();
            var notice=world.GetComponent<Chapter1BossEncounter>();Assert.That(notice,Is.Not.Null);
            // The loaded Editor's first unscaled frame can exceed the whole intro. Hold only the
            // encounter's presentation Update and advance the real intro explicitly below.
            encounter.enabled=false;
            EnterBossRoom(world,encounter);encounter.BeginIntro();
            yield return null;yield return null;
            const System.Reflection.BindingFlags hidden=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var shown=typeof(Chapter1BossEncounter).GetField("introductionShown",hidden);
            var age=typeof(Chapter1BossEncounter).GetProperty("IntroductionAge",hidden);
            Assert.That(encounter.IntroPlaying,Is.True);
            Assert.That((bool)shown.GetValue(notice),Is.False,"the reveal cannot consume the battle-start notice");
            encounter.AdvanceIntro(encounter.IntroDuration+.01f);encounter.enabled=true;
            yield return null;yield return null;
            Assert.That(encounter.Boss.Started,Is.True);Assert.That((bool)shown.GetValue(notice),Is.True);
            using(var scope=world.arena.Controls.OpenScope("notice menu pause"))
            {
                scope.PauseSimulation();float before=(float)age.GetValue(notice);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That((float)age.GetValue(notice),Is.EqualTo(before).Within(.001f),"hidden menu time must not consume the notice");
            }
        }

        // The fight has no clock (user decision 2026-10-30): however long it runs, the player is not felled by time
        // and the boss's patterns keep their phase speed.
        [UnityTest] public IEnumerator TheFightHasNoClock()
        {
            OpenSeededStage();
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 600 && !ReadyWorld(); frame++) yield return null;
            var world = ReadyWorld(); Assert.That(world, Is.Not.Null);
            var encounter = world.GetComponent<ClockExecutionerEncounter>(); var boss = encounter.Boss;
            EnterBossRoom(world, encounter);
            for (int frame = 0; frame < 120 && !encounter.SkipAvailable; frame++) yield return null;
            Assert.That(encounter.TrySkip() && boss.Started, Is.True);
            boss.aggressive = false; // only time passes in this fight
            var player = world.arena.Player;
            using (var driver = world.arena.AcquireManualSimulation())
                while (boss.FightElapsed < 300 && !player.Dead) driver.Step(1f / 30);
            Assert.That(player.Dead, Is.False, "no time limit fells the player");
            boss.aggressive = true; boss.BeginAttack(ClockBossAttack.Needles);
            Assert.That(boss.PatternSpeed, Is.EqualTo(boss.phaseOneSpeed).Within(1e-5f), "elapsed time alone does not enrage the boss");
        }

        [UnityTest] public IEnumerator HealthThresholdsInterruptOnceAndFinalPhaseChainsAllPatternsWithoutRecovery()
        {
            OpenSeededStage();
            yield return new EnterPlayMode();
            for(int frame=0;frame<600&&!ReadyWorld();frame++)yield return null;
            var world=ReadyWorld();Assert.That(world,Is.Not.Null);
            var encounter=world.GetComponent<ClockExecutionerEncounter>();var boss=encounter.Boss;
            EnterBossRoom(world,encounter);
            for(int frame=0;frame<120&&!encounter.SkipAvailable;frame++)yield return null;
            Assert.That(encounter.TrySkip(),Is.True);
            using(var driver=world.arena.AcquireManualSimulation())
            {
                foreach(var enemy in world.arena.enemies)if(enemy)enemy.aggressive=false;
                boss.aggressive=true;
                Assert.That(boss.maxHealth,Is.EqualTo(1800));
                int roars=0;boss.PhaseTwoStarted+=()=>roars++;
                boss.BeginAttack(ClockBossAttack.Needles);
                boss.Receive(boss.maxHealth-boss.EnrageHealth-1,world.arena.Player,1,false,null,true);
                Assert.That(boss.health,Is.EqualTo(boss.EnrageHealth+1));Assert.That(boss.PhaseTwo,Is.False);
                boss.Receive(1,world.arena.Player,1,false,null,true);
                Assert.That(boss.PhaseTwo&&encounter.CutscenePlaying,Is.True);
                Assert.That(boss.CurrentAttack,Is.EqualTo(ClockBossAttack.None));
                Assert.That(boss.PendingHazards,Is.Zero);Assert.That(encounter.TrySkip(),Is.True);
                boss.BeginAttack(ClockBossAttack.LongFist);
                boss.Receive(boss.health-boss.FinalPhaseHealth-1,world.arena.Player,1,false,null,true);
                Assert.That(boss.health,Is.EqualTo(boss.FinalPhaseHealth+1));Assert.That(boss.FinalPhase,Is.False);
                driver.Step(.2f);float before=boss.Age;
                boss.Receive(1,world.arena.Player,1,false,null,true);
                Assert.That(boss.FinalPhase,Is.True);Assert.That(roars,Is.EqualTo(1));
                // The final phase roars like phase two (playtest 2026-11-04) and its bar turns black.
                Assert.That(encounter.CutscenePlaying&&encounter.SkipAvailable,Is.True,"the final phase has its own warning");
                Assert.That(boss.bossBarColor,Is.EqualTo(ClockExecutionerBoss.FinalPhaseBar));
                Assert.That(encounter.TrySkip(),Is.True);
                Assert.That(boss.PatternSpeed,Is.EqualTo(1.2f).Within(.00001f));
                Assert.That(boss.Age,Is.EqualTo(before).Within(.00001f),"accelerate without jumping the active attack");
                foreach(var attack in new[]{ClockBossAttack.Needles,ClockBossAttack.LongFist,ClockBossAttack.Mixer,ClockBossAttack.ChainSlash})
                {
                    boss.BeginAttack(attack);
                    float duration=attack==ClockBossAttack.Needles?ClockExecutionerBoss.NeedleDuration-ClockExecutionerBoss.SpecialRecovery
                        :attack==ClockBossAttack.LongFist?ClockExecutionerBoss.LongFistSequenceEnd
                        :attack==ClockBossAttack.Mixer?ClockExecutionerBoss.MixerTravelEnd+ClockExecutionerBoss.MixerReturn:boss.ChainSlashDuration;
                    float start=world.arena.Now;
                    while(boss.CurrentAttack==attack&&world.arena.Now-start<duration/1.2f+.1f)driver.Step(1f/120);
                    Assert.That(boss.CurrentAttack,Is.Not.EqualTo(attack),attack+" must finish without recovery");
                    Assert.That(boss.CurrentAttack,Is.Not.EqualTo(ClockBossAttack.None),"next pattern starts on the same tick");
                    Assert.That(world.arena.Now-start,Is.EqualTo(duration/1.2f).Within(.03f));
                }
                driver.Reset();
                Assert.That(boss.health,Is.EqualTo(1800));Assert.That(boss.PhaseTwo||boss.FinalPhase,Is.False);
                Assert.That(boss.PendingHazards,Is.Zero);
                boss.BeginFight();boss.Receive(boss.maxHealth-boss.FinalPhaseHealth,world.arena.Player,1,false,null,true);
                Assert.That(boss.PhaseTwo&&boss.FinalPhase,Is.True,"one large hit can cross both phases");
                Assert.That(encounter.CutscenePlaying,Is.True);Assert.That(roars,Is.EqualTo(2));
                Assert.That(encounter.TrySkip(),Is.True);Assert.That(encounter.CutscenePlaying,Is.False,"one roar for both phases");
                boss.Step(0);Assert.That(boss.CurrentAttack,Is.Not.EqualTo(ClockBossAttack.None));
                Assert.That(boss.PatternSpeed,Is.EqualTo(1.2f).Within(.00001f));
                driver.Reset();boss.BeginFight();boss.Receive(boss.maxHealth,world.arena.Player,1,false,null,true);
                Assert.That(boss.Dead,Is.True);Assert.That(boss.PhaseTwo||boss.FinalPhase,Is.False,"death takes priority over both phases");
            }
        }
    }
}
