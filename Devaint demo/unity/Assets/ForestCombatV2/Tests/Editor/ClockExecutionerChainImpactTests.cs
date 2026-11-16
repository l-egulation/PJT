using System.Collections;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class ClockExecutionerChainImpactTests
    {
        ForestCombatWorld world;
        ClockExecutionerBoss boss;
        CombatPlayer player;
        Vector2 Contact=>new Vector2(boss.RoomLeft+15,boss.Floor);
        Vector2 Away=>new Vector2(boss.RoomLeft-4,boss.Floor+2);

        [UnitySetUp]public IEnumerator LoadStage()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            var source=Object.FindAnyObjectByType<ForestCombatWorld>();source.proceduralSeed=42;source.arena.loadCharacterArt=false;
            yield return new EnterPlayMode();
            world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int frame=0;frame<600&&(!world||!world.Ready);frame++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);yield return null;
            var encounter=world.GetComponent<ClockExecutionerEncounter>();encounter.enabled=false;
            boss=encounter.Boss;player=world.arena.Player;
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
        void Place(Vector2 position)
        {
            player.Position=position;player.velocity=Vector2.zero;Physics2D.SyncTransforms();
        }
        void Begin(CombatArena.ManualSimulation driver,float speed=1)
        {
            driver.Reset();
            foreach(var enemy in world.arena.enemies)if(enemy)enemy.aggressive=false;
            player.UseTestInput=true;player.maxHealth=player.health=1000;player.shield=0;
            player.guardUntil=0;player.guardReduction=0;
            boss.phaseOneSpeed=speed;boss.BeginFight();
            boss.ForcedChainAnchor=Contact+Vector2.up;Place(Contact);
            boss.BeginAttack(ClockBossAttack.ChainSlash);
            Assert.That(boss.PatternSpeed,Is.EqualTo(speed).Within(.00001f));
        }
        void AdvanceTo(CombatArena.ManualSimulation driver,float age,Vector2? position=null)
        {
            int steps=0;
            while(boss.Age<age)
            {
                Assert.That(++steps,Is.LessThan(3000),"The pattern clock must advance.");
                if(position.HasValue)Place(position.Value);
                driver.Step(Mathf.Min(1f/120,(age-boss.Age)/boss.PatternSpeed+.000001f));
            }
            if(position.HasValue)Place(position.Value);
            // Combat advances its clock after the hit phase. Sample the newly reached boundary explicitly.
            boss.Step(0);
        }
        float AssertSingleImpact(float before)
        {
            var variance=CombatDamageVariance.Settings;
            Assert.That(before-player.health,Is.InRange(ClockExecutionerBoss.ChainDamage*variance.minimum-.001f,ClockExecutionerBoss.ChainDamage*variance.maximum+.001f),"One impact applies the configured per-hit damage variance.");
            return player.health;
        }
        [UnityTest]public IEnumerator StatusOnlyStunDoesNotBlockStartingOrAdvancingTheBossPattern()
        {
            using(var driver=world.arena.AcquireManualSimulation())
            {
                driver.Reset(); world.arena.EnableSketchEquipment(42);
                boss.BeginFight(); boss.stunUntil=world.arena.Now+3; boss.rootUntil=world.arena.Now+3;
                boss.BeginAttack(ClockBossAttack.ChainSlash);
                Assert.That(boss.CurrentAttack,Is.EqualTo(ClockBossAttack.ChainSlash));
                Assert.That(boss.stunUntil,Is.GreaterThan(world.arena.Now),"status remains available to ink conditions");
                float age=boss.Age; driver.Step(.1f);
                Assert.That(boss.Age,Is.GreaterThan(age),"the boss pattern clock keeps moving");
            }
            yield break;
        }
        [UnityTest]public IEnumerator DamageOccursOnlyAtTheFarEdgeImpactAtEveryPatternSpeed()
        {
            using(var driver=world.arena.AcquireManualSimulation())
            foreach(float speed in new[]{.85f,1f,1.25f})
            {
                Begin(driver,speed);float hp=player.health,impact=ClockExecutionerBoss.ChainImpactAt(0);
                AdvanceTo(driver,impact-.001f,Contact);
                Assert.That(player.health,Is.EqualTo(hp),"A chain extending through the player is harmless before impact.");
                Assert.That(boss.ActiveBoxes.Count,Is.Zero);
                AdvanceTo(driver,impact+.001f,Contact);
                float after=AssertSingleImpact(hp);
                Assert.That(boss.ActiveBoxes.Count,Is.EqualTo(1));
                boss.Step(0);Assert.That(player.health,Is.EqualTo(after),"Sampling the impact again cannot repeat damage.");
                Assert.That(boss.ActiveBoxes.Count,Is.Zero);
                Place(Away);boss.Step(0);Place(Contact);
                AdvanceTo(driver,impact+.1f,Contact);
                Assert.That(player.health,Is.EqualTo(after),"Reentering the embedded chain is harmless.");
            }
            yield break;
        }
        [UnityTest]public IEnumerator MissingEveryImpactThenTouchingTheHeldAndRetractingChainsIsHarmless()
        {
            using(var driver=world.arena.AcquireManualSimulation())
            {
                Begin(driver);float hp=player.health;
                float lastImpact=ClockExecutionerBoss.ChainImpactAt(boss.ChainLineCount-1);
                AdvanceTo(driver,lastImpact+.01f,Away);
                Assert.That(player.health,Is.EqualTo(hp),"Each missed impact is consumed.");
                Assert.That(boss.ChainReach(ClockExecutionerBoss.ChainImpact+.1f),Is.EqualTo(1));
                AdvanceTo(driver,lastImpact+.1f,Contact);
                Assert.That(player.health,Is.EqualTo(hp),"Entering a fully extended chain after its impact cannot damage the player.");
                AdvanceTo(driver,lastImpact+boss.ChainHold+ClockExecutionerBoss.ChainRetract/2,Contact);
                Assert.That(player.health,Is.EqualTo(hp),"Retraction is harmless too.");
                Assert.That(boss.ActiveBoxes.Count,Is.Zero);
            }
            yield break;
        }
        [UnityTest]public IEnumerator EmbeddedChainsHoldForOneCombatSecondAtEveryPatternSpeed()
        {
            using(var driver=world.arena.AcquireManualSimulation())
            foreach(float speed in new[]{.85f,1f,1.0625f,1.25f})
            {
                Begin(driver,speed);float hp=player.health;Assert.That(boss.ChainHold,Is.EqualTo(1f),"The runtime reads the canonical one-second timing.");
                int last=boss.ChainLineCount-1;float impact=ClockExecutionerBoss.ChainImpactAt(last);
                AdvanceTo(driver,impact+.001f,Away);
                float holdEnd=impact+1f*speed;
                AdvanceTo(driver,holdEnd-.001f*speed,Contact);
                Assert.That(boss.ChainReach(ClockExecutionerBoss.ChainLineAge(last,boss.Age)),Is.EqualTo(1),"The embedded chain must stay fully extended until one combat second has passed.");
                Assert.That(boss.CurrentAttack,Is.EqualTo(ClockBossAttack.ChainSlash));
                Assert.That(player.health,Is.EqualTo(hp),"The longer hold does not add contact damage.");
                AdvanceTo(driver,holdEnd+.001f*speed,Contact);
                float retract=boss.ChainReach(ClockExecutionerBoss.ChainLineAge(last,boss.Age));
                Assert.That(retract,Is.GreaterThan(0).And.LessThan(1),"Retraction begins just after the one-second hold at every phase/enrage speed.");
                AdvanceTo(driver,holdEnd+ClockExecutionerBoss.ChainRetract+.01f,Contact);
                Assert.That(boss.ChainReach(ClockExecutionerBoss.ChainLineAge(last,boss.Age)),Is.Zero);
                Assert.That(player.health,Is.EqualTo(hp));
                AdvanceTo(driver,boss.ChainSlashDuration+.01f,Away);
                Assert.That(boss.CurrentAttack,Is.EqualTo(ClockBossAttack.None),"Recovery starts after the full hold and retraction.");
            }
            yield break;
        }
        [UnityTest]public IEnumerator FinalPhaseAccelerationPreservesTheRemainingEmbeddedHold()
        {
            using(var driver=world.arena.AcquireManualSimulation())
            {
                Begin(driver);
                boss.Receive(boss.health-(boss.FinalPhaseHealth+1),player,1,false,null,true);
                Assert.That(world.GetComponent<ClockExecutionerEncounter>().TrySkip(),Is.True);
                boss.BeginAttack(ClockBossAttack.ChainSlash);
                float impact=ClockExecutionerBoss.ChainImpactAt(0);
                AdvanceTo(driver,impact+.4f,Away);
                float age=boss.Age;
                boss.Receive(1,player,1,false,null,true);
                Assert.That(world.GetComponent<ClockExecutionerEncounter>().TrySkip(),Is.True,"the final phase roars");
                Assert.That(boss.PatternSpeed,Is.EqualTo(1.2f).Within(.00001f));
                Assert.That(boss.Age,Is.EqualTo(age).Within(.00001f));
                AdvanceTo(driver,age+.59f*1.2f,Away);
                Assert.That(boss.ChainReach(ClockExecutionerBoss.ChainLineAge(0,boss.Age),0),Is.EqualTo(1).Within(.00001f),"the remaining real-time hold must not shorten");
                AdvanceTo(driver,age+.61f*1.2f,Away);
                Assert.That(boss.ChainReach(ClockExecutionerBoss.ChainLineAge(0,boss.Age),0),Is.LessThan(1),"retraction begins after one combat second total");
                Assert.That(player.health,Is.EqualTo(1000));
            }
            yield break;
        }
        [UnityTest]public IEnumerator ARealDashAtImpactDoesNotDeferDamageUntilTheDashEnds()
        {
            using(var driver=world.arena.AcquireManualSimulation())
            {
                Begin(driver);float hp=player.health,impact=ClockExecutionerBoss.ChainImpactAt(0);
                AdvanceTo(driver,impact-.03f,Contact);
                driver.Step(.001f,new CombatInput(0,CombatButtons.Dash));
                Assert.That(player.Dashing,Is.True);
                AdvanceTo(driver,impact+.001f,Contact);
                Assert.That(player.health,Is.EqualTo(hp),"The impact respects dash protection.");
                player.CancelAction();Assert.That(player.Dashing,Is.False);
                AdvanceTo(driver,impact+.1f,Contact);
                Assert.That(player.health,Is.EqualTo(hp),"Ending the dash on the embedded chain must not replay its impact.");
            }
            yield break;
        }
        [UnityTest]public IEnumerator ACoarseStepProcessesTheImpactOnceAndANewAttackGetsFreshImpacts()
        {
            using(var driver=world.arena.AcquireManualSimulation())
            {
                Begin(driver);float hp=player.health,impact=ClockExecutionerBoss.ChainImpactAt(0);
                AdvanceTo(driver,impact-.02f,Contact);
                driver.Step(.1f);Place(Contact);boss.Step(0);
                float after=AssertSingleImpact(hp);
                boss.Step(0);Assert.That(player.health,Is.EqualTo(after));
                boss.CancelAttack();boss.BeginAttack(ClockBossAttack.ChainSlash);Place(Contact);
                AdvanceTo(driver,impact+.001f,Contact);
                AssertSingleImpact(after);
            }
            yield break;
        }
    }
}
