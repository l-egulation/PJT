using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class CombatDamageVarianceTests
    {
        [TestCase(0, 90)] [TestCase(.5f, 100)] [TestCase(1, 110)]
        public void BoundsComeFromRuntimeData(float sample, float expected)
            => Assert.That(CombatDamageVariance.Settings.Apply(100, sample), Is.EqualTo(expected).Within(.0001f));

        [Test] public void PlayerReceivedDamageVarianceIsDisabledInRuntimeData()
            => Assert.That(CombatDamageVariance.Settings.varyPlayerReceivedDamage, Is.False);

        [TestCase(9.49f,"9")] [TestCase(9.5f,"10")] [TestCase(10.5f,"11")]
        [TestCase(.49f,"0")] [TestCase(.5f,"1")] [TestCase(0,"0")]
        public void DisplayRoundsActualDamageToAnInteger(float damage,string expected)
            => Assert.That(CombatDamageVariance.Format(damage),Is.EqualTo(expected));

        [UnityTearDown] public IEnumerator Cleanup()
        { if(Application.isPlaying)yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator RandomStageAppliesOneRollAndReportsTheActualReducedDamage()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            var arena=world.arena;var player=arena.Player;
            foreach(var enemy in arena.enemies){enemy.aggressive=false;enemy.CancelAttack();}
            using(var driver=arena.AcquireManualSimulation())
            {
                var target=arena.enemies.First(e=>e&&!e.Dead&&!e.isBoss);
                target.maxHealth=target.health=10000;
                var savedRandom=Random.state;
                try
                {
                    Random.InitState(732);
                    float low=float.MaxValue,high=0;
                    var hits=new CombatAcceptedHit[8];
                    for(int i=0;i<128;i++)
                    {
                        arena.HitEvents.TryGetCursor(out var cursor);
                        float before=target.health;
                        Assert.That(target.Receive(10,player,1,false,null),Is.True);
                        float actual=before-target.health;
                        Assert.That(actual,Is.InRange(8.999f,11.001f));
                        low=Mathf.Min(low,actual);high=Mathf.Max(high,actual);
                        Assert.That(arena.HitEvents.TryRead(cursor,hits,out var page),Is.True);
                        Assert.That(page.Count,Is.EqualTo(1));
                        Assert.That(hits[0].Damage,Is.EqualTo(actual).Within(.001f));
                    }
                    Assert.That(high-low,Is.GreaterThan(1),"Consecutive hits must vary, not just their labels.");
                    player.guardUntil=0;player.shield=0;player.health=100;
                    Assert.That(player.Receive(20,target,-1,false,null),Is.True);
                    Assert.That(100-player.health,Is.EqualTo(20),"Enemy damage to the player has no variance multiplier.");
                    for(int i=0;i<32;i++)
                    {
                        player.health=100;
                        Assert.That(player.Receive(20,target,-1,false,null),Is.True);
                        Assert.That(100-player.health,Is.EqualTo(20));
                    }
                    player.health=100;player.guardUntil=arena.Now+10;player.guardReduction=.5f;player.shield=3;
                    Assert.That(player.Receive(20,target,-1,false,null),Is.True);
                    Assert.That(100-player.health,Is.EqualTo(7),"Fixed damage still passes through reduction and shield absorption.");
                    target.health=100;
                    Assert.That(target.Receive(0,player,1,false,null),Is.True);
                    Assert.That(target.health,Is.EqualTo(100));
                    Assert.That(target.Receive(100,player,1,false,null,true),Is.True);
                    Assert.That(target.Dead,Is.True,"Scripted forced execution remains exact.");
                    player.maxHealth=player.health=100;player.Fall();
                    Assert.That(player.health,Is.EqualTo(90),"Fall recovery retains its fixed 10% cost.");
                }
                finally {Random.state=savedRandom;}
            }
        }
    }
}
