using System.Collections;
using System.Linq;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1RoomClearHealingTests
    {
        [UnityTest] public IEnumerator EveryEnemyMustDieAndEachRoomHealsOnlyOnceWithoutExceedingMaximum()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            Assert.That(world && world.Ready,Is.True);
            var healing=world.GetComponent<Chapter1RoomClearHealing>();
            Assert.That(healing,Is.Not.Null,"normal run installs room clear healing");
            var fx=world.GetComponent<Chapter1RoomClearHealFx>();
            Assert.That(fx,Is.Not.Null,"room clear healing has its green body and screen effect");
            var player=world.arena.Player;
            using(var simulation=world.arena.AcquireManualSimulation())
            {
                foreach(var enemy in world.arena.enemies)if(enemy)enemy.aggressive=false;
                player.maxHealth=100;player.health=20;
                healing.Tick();Assert.That(player.health,Is.EqualTo(20),"empty/noncombat rooms grant nothing");
                foreach(var kind in new[]{"combat","elite","boss"})
                {
                    var room=PopulatedRoom(world,kind,false);
                    var enemies=Members(world,room);
                    float before=player.health;
                    for(int i=0;i<enemies.Length-1;i++)enemies[i].health=0;
                    healing.Tick();Assert.That(player.health,Is.EqualTo(before),"a surviving room member prevents clear");
                    enemies[enemies.Length-1].Position=player.Position; // Chasing outside does not change encounter membership.
                    enemies[enemies.Length-1].health=0;
                    int played=fx.Played;
                    healing.Tick();Assert.That(player.health,Is.EqualTo(before+(kind=="boss"?30:10)),kind);
                    Assert.That(fx.Played,Is.EqualTo(played+1),"each healed clear plays the effect once");Assert.That(fx.GlowActive,Is.True);
                    healing.Tick();Assert.That(player.health,Is.EqualTo(before+(kind=="boss"?30:10)),"no repeated reward");
                    Assert.That(fx.Played,Is.EqualTo(played+1),"no repeated effect");
                }
                var remaining=PopulatedRoom(world,"combat",true);
                player.health=97;foreach(var enemy in Members(world,remaining))enemy.health=0;
                healing.Tick();Assert.That(player.health,Is.EqualTo(100),"healing is capped by maximum health");
                player.health=90;healing.Tick();Assert.That(player.health,Is.EqualTo(90),"overheal does not leave a deferred reward");
                var another=PopulatedRoom(world,"combat",true);
                int beforeDeath=fx.Played;
                player.health=0;foreach(var enemy in Members(world,another))enemy.health=0;
                healing.Tick();Assert.That(player.health,Is.Zero,"room reward cannot revive the player");
                Assert.That(fx.Played,Is.EqualTo(beforeDeath),"no heal effect without healing");
            }
        }
        static TestEnemy[] Members(ForestCombatWorld world,ForestRoom room)
        {
            var bounds=new Rect(room.x,-(room.y+room.h),room.w,room.h);
            return world.arena.enemies.Where(e=>e && bounds.Contains(e.spawn+Vector2.up*.5f)).ToArray();
        }
        // Keep closures outside the iterator: EnterPlayMode cannot restore a captured display class.
        static ForestRoom PopulatedRoom(ForestCombatWorld world,string kind,bool requireAlive)
        {
            foreach(var room in world.Data.rooms)
                if(room.kind==kind)
                {
                    var members=Members(world,room);
                    if(members.Length>0 && (!requireAlive || members.Any(e=>!e.Dead)))return room;
                }
            Assert.Fail("No populated room of kind "+kind);return null;
        }
        [UnityTearDown] public IEnumerator TearDown(){if(Application.isPlaying)yield return new ExitPlayMode();}
    }
}
