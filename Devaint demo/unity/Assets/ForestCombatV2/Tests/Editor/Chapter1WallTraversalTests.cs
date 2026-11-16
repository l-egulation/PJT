using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1WallTraversalTests
    {
        // Height a fresh wall's one-second stamina reliably climbs with up input (7.5 units/s).
        const float WallStaminaClimb=7;
        [UnityTest] public IEnumerator WallControlsAndCollisions()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=false;
            yield return new EnterPlayMode(); yield return null;
            var arena=Object.FindAnyObjectByType<CombatArena>(); var player=arena.Player;
            foreach(var enemy in arena.enemies)if(enemy)enemy.aggressive=false;
            var wall=new GameObject("Wall control test solid");
            wall.transform.position=new Vector3(204,200,0);
            var collider=wall.AddComponent<BoxCollider2D>(); collider.size=new Vector2(2,80);
            var surface=wall.AddComponent<ArenaSurface>();
            using(var driver=arena.AcquireManualSimulation())
            {
                void Reset() {
                    driver.Reset();player.Position=new Vector2(202.34f,200);player.velocity=Vector2.zero;
                    player.SyncFacing(1);Physics2D.SyncTransforms();player.grounded=false;
                }
                foreach(float height in new[]{.5f,player.size.y})
                {
                    collider.size=new Vector2(2,height);wall.transform.position=new Vector3(204,200+height/2,0);
                    Reset();driver.Step(1f/120,new CombatInput(1,climbAxis:1));
                    Assert.That(player.WallClimbing,Is.False,"Low wall height="+height);
                    driver.Step(1f/120,new CombatInput(1,CombatButtons.Jump,climbAxis:1));
                    Assert.That(player.WallJumpInFlight,Is.False);Assert.That(player.jumps.Used,Is.EqualTo(1));
                }
                collider.size=new Vector2(2,80);wall.transform.position=new Vector3(204,200,0);
                Reset();driver.Step(1f/120,new CombatInput(climbAxis:1));
                Assert.That(player.WallClimbing&&!player.WallSliding,Is.True);
                driver.Step(1f/120,new CombatInput());
                Assert.That(player.WallClimbing,Is.False);Assert.That(player.WallSliding,Is.False);
                driver.Step(1f/120,new CombatInput(pressed:CombatButtons.Jump));
                Assert.That(player.WallJumpInFlight,Is.False);Assert.That(player.jumps.Used,Is.EqualTo(1));
                Reset();driver.Step(1f/120,new CombatInput(pressed:CombatButtons.Jump,climbAxis:1));
                Assert.That(player.WallJumpInFlight,Is.True);Assert.That(player.jumps.Used,Is.Zero);
                Reset();surface.oneWay=true;driver.Step(1f/120,new CombatInput(climbAxis:1));
                Assert.That(player.WallClimbing,Is.False);surface.oneWay=false;
                Reset();player.InputBlocked=true;driver.Step(1f/120,new CombatInput(climbAxis:1));
                Assert.That(player.WallClimbing,Is.False);player.InputBlocked=false;
            }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator WallClimbDetachAndKickVariants()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            LegacySceneFixture.Open(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&!world.Ready;i++)yield return null;
            Assert.That(world.Ready,Is.True);
            var arena=world.arena;var player=arena.Player;var movement=arena.Movement;
            foreach(var enemy in arena.enemies)if(enemy)enemy.aggressive=false;
            float wallX=world.Data.width/32f+60,wallY=100;
            var wall=new GameObject("Wall movement test solid");wall.transform.position=new Vector3(wallX,wallY,0);
            wall.AddComponent<BoxCollider2D>().size=new Vector2(2,80);wall.AddComponent<ArenaSurface>();
            using(var driver=arena.AcquireManualSimulation())
            {
                void Step(int axis=1,int up=0,CombatButtons button=CombatButtons.None)=>driver.Step(1f/120,new CombatInput(axis,button,climbAxis:up));
                void Place(float x,float y=100,float vy=0) {
                    driver.Reset();player.Position=new Vector2(x,y);player.velocity=new Vector2(0,vy);
                    player.SyncFacing(1);Physics2D.SyncTransforms();player.grounded=false;
                }
                void Climb() {Place(wallX-1.66f);Step(up:1);Assert.That(player.WallClimbing&&!player.WallSliding,Is.True);}
                Climb();Step();
                Assert.That(player.WallClimbing||player.WallSliding,Is.False,"Releasing up drops off; there is no slide");
                for(int i=0;i<72;i++)Step();
                Assert.That(player.WallClimbing||player.WallSliding,Is.False);Assert.That(player.VelocityY,Is.LessThan(0));
                Place(wallX-1.66f,vy:-1);Step();
                Assert.That(player.WallClimbing||player.WallSliding,Is.False,"Falling contact without up does not grab");
                Climb();int held=1;
                for(;held<200&&player.WallClimbing;held++)Step(up:1);
                Assert.That(held,Is.InRange(118,123),"The existing one-second wall stamina is preserved");
                Step(-1,1,CombatButtons.Jump);Assert.That(player.WallJumpInFlight,Is.True,"Up permits the spent-wall coyote kick");
                Climb();for(int i=0;i<7;i++)Step(-1);
                Step(-1,0,CombatButtons.Jump);Assert.That(player.WallJumpInFlight,Is.False,"Space alone cannot consume wall coyote");
                Climb();for(int i=0;i<7;i++)Step(-1);
                Step(-1,1,CombatButtons.Jump);Assert.That(player.WallJumpInFlight,Is.True);Assert.That(player.jumps.Used,Is.Zero);
                Climb();float touchX=player.Position.x;
                Place(touchX-(movement.WallJumpCheckDistance-.03f),vy:-1);Step(0,1,CombatButtons.Jump);
                Assert.That(player.WallJumpInFlight,Is.True,"Up+Space kicks just before contact");
                Place(touchX-(movement.WallJumpCheckDistance-.03f),vy:-1);Step(0,0,CombatButtons.Jump);
                Assert.That(player.WallJumpInFlight,Is.False);Assert.That(player.jumps.Used,Is.EqualTo(1));
                Place(touchX-(movement.WallJumpCheckDistance+.15f),vy:-1);Step(0,1,CombatButtons.Jump);
                Assert.That(player.WallJumpInFlight,Is.False);Assert.That(player.jumps.Used,Is.EqualTo(1));
                Place(touchX,vy:movement.FirstJumpVelocity);Step(0);
                Assert.That(player.WallClimbing,Is.False,"Jumping upward without up is not caught by the wall");
                float top=wallY+40;Place(wallX-1.66f,top-.5f);
                for(int i=0;i<90&&!player.grounded;i++)Step(0,1);
                Assert.That(player.grounded,Is.True);Assert.That(player.Position.y,Is.EqualTo(top).Within(.1f));
                var opposite=new GameObject("Opposite wall movement solid");opposite.transform.position=new Vector3(wallX-7,wallY,0);
                opposite.AddComponent<BoxCollider2D>().size=new Vector2(2,80);opposite.AddComponent<ArenaSurface>();
                Climb();for(int i=0;i<100;i++)Step(up:1);
                Step(-1,1,CombatButtons.Jump);
                for(int i=0;i<90&&!player.WallClimbing;i++)Step(-1,1);
                Assert.That(player.WallClimbing&&!player.WallSliding,Is.True);Assert.That(player.VelocityY,Is.GreaterThan(0));
                Step(0);Assert.That(player.WallClimbing||player.WallSliding,Is.False);
                Step(1,1,CombatButtons.Jump);
                for(int i=0;i<90&&!player.WallClimbing;i++)Step(1,1);
                Assert.That(player.WallClimbing,Is.True);
                for(int i=0;i<100;i++)Step(up:1);
                Assert.That(player.WallClimbing,Is.True,"Another wall refills the previous wall's stamina");
                Object.Destroy(opposite);
            }
            Object.Destroy(wall);yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator AlternatingWallsUseBufferedSpaceWithoutAirJumpCharges()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            LegacySceneFixture.Open(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&!world.Ready;i++)yield return null;
            AssertAlternatingWalls(world);
            yield return new ExitPlayMode();
        }
        static void AssertAlternatingWalls(ForestCombatWorld world)
        {
            foreach(var enemy in world.arena.enemies)if(enemy)enemy.aggressive=false;
            var player=world.arena.Player;
            // Compact passages contain room doors and landings. They cannot promise the
            // uninterrupted walls this input/buffer test needs; the block map tests cover real corridors.
            const float left=200,right=205,bottom=200;
            var fixture=new GameObject("Uninterrupted alternating-wall fixture");
            foreach(float x in new[]{left-1,right+1})
            {
                var wall=new GameObject("Wall");wall.transform.SetParent(fixture.transform,false);
                wall.transform.position=new Vector2(x,bottom+40);
                wall.AddComponent<BoxCollider2D>().size=new Vector2(2,80);wall.AddComponent<ArenaSurface>();
            }
            Physics2D.SyncTransforms();
            try
            {
            using(var driver=world.arena.AcquireManualSimulation())
            {
                driver.Reset();player.SyncFacing(-1);
                player.Position=new Vector2(left+player.size.x/2+player.offset.x+.01f,bottom+12);
                player.velocity=Vector2.zero;Physics2D.SyncTransforms();player.grounded=false;
                driver.Step(1f/120,new CombatInput(-1,climbAxis:1));
                Assert.That(player.WallClimbing,Is.True);
                float startY=player.Position.y,startTime=world.arena.Now,hp=player.health;
                driver.Step(1f/120,new CombatInput(1,CombatButtons.Jump,climbAxis:1));
                int direction=1,bounces=0;
                for(int tick=0;tick<900&&bounces<8;tick++)
                {
                    if(player.WallClimbing)
                    {
                        driver.Step(1f/120,new CombatInput(-direction,CombatButtons.Jump,climbAxis:1));
                        Assert.That(player.VelocityX*direction,Is.LessThan(-.1f));Assert.That(player.VelocityY,Is.GreaterThan(19));
                        Assert.That(player.jumps.Used,Is.Zero);direction=-direction;bounces++;continue;
                    }
                    driver.Step(1f/120,new CombatInput(direction,climbAxis:1));
                }
                Assert.That(bounces,Is.EqualTo(8));Assert.That(player.Position.y-startY,Is.GreaterThan(20));
                Assert.That(world.arena.Now-startTime,Is.LessThan(3.6f));Assert.That(player.health,Is.EqualTo(hp));
                for(int tick=0;tick<100&&!player.WallClimbing;tick++)driver.Step(1f/120,new CombatInput(direction,climbAxis:1));
                Assert.That(player.WallSliding,Is.False);Assert.That(player.VelocityY,Is.GreaterThan(0));
                driver.Step(1f/120,new CombatInput(direction));
                Assert.That(player.WallClimbing||player.WallSliding,Is.False,"Releasing up drops off the wall");
                driver.Step(1f/120,new CombatInput(direction,CombatButtons.Jump));
                Assert.That(player.WallJumpInFlight,Is.False);Assert.That(player.jumps.Used,Is.EqualTo(1));
            }
            }
            finally{Object.DestroyImmediate(fixture);}
        }


    }
}
