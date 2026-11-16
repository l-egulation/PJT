using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Game.Diagnostics;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1HitboxTests
    {
        CombatArena arena;
        CombatDiagnostics diagnostics;
        CombatArena.ManualSimulation simulation;
        readonly List<CombatDiagnostics.AttackHitbox> boxes=new List<CombatDiagnostics.AttackHitbox>();
        [UnitySetUp] public IEnumerator Setup()
        {
            LegacySceneFixture.Open(Chapter1MapSelector.Ver2Scene);
            yield return new EnterPlayMode();yield return null;yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();arena=world.arena;diagnostics=world.diagnostics;
            foreach(var enemy in arena.enemies)enemy.aggressive=false;
            arena.Player.UseTestInput=true;simulation=arena.AcquireManualSimulation();
            diagnostics.showDebugUI=true;arena.showBoxes=true;
        }
        [UnityTearDown] public IEnumerator Teardown(){simulation?.Dispose();if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();}
        void Observe()=>diagnostics.CollectAttackHitboxes(boxes);
        [UnityTest] public IEnumerator ProjectilesUseActualRadiusFollowMovementAndDisappearOnReleaseOrF2Off()
        {
            var origin=arena.Player.Center+Vector2.up*5;
            var shot=PlayerProjectile.Spawn(arena,arena.Player,origin,Vector2.right,
                new RangedData{radius=.23f,distance=10},1);
            var bomb=BombProjectile.Spawn(arena,arena.Player,origin+Vector2.right,Vector2.zero,
                new BombData{radius=.31f},1);
            arena.FireEnemyProjectile(arena.enemies[0],origin+Vector2.right*3,Vector2.zero,1);
            var enemyShot=arena.projectiles.Last();
            Observe();
            Assert.That(boxes.Count,Is.EqualTo(3));
            Assert.That(boxes.Single(b=>b.Source==shot).Radius,Is.EqualTo(shot.Definition.Radius));
            Assert.That(boxes.Single(b=>b.Source==bomb).Radius,Is.EqualTo(bomb.Definition.Radius));
            Assert.That(boxes.Single(b=>b.Source==enemyShot).Bounds.size.x,Is.EqualTo(.25f));
            shot.Position+=Vector2.right*2;Observe();
            Assert.That((Vector2)boxes.Single(b=>b.Source==shot).Bounds.center,Is.EqualTo(shot.Position));
            arena.showBoxes=false;Observe();Assert.That(boxes,Is.Empty);
            arena.showBoxes=true;shot.Release();bomb.Release();enemyShot.Release();Observe();Assert.That(boxes,Is.Empty);
            yield return null;
        }
        [UnityTest] public IEnumerator SpeciesAttackBoxesUseRealGeometryAndStopAfterActiveWindowOrCancellation()
        {
            while(arena.Now<4)simulation.Step(.01f);
            var species=arena.enemies.OfType<FieldMouseEnemy>().ToArray();Assert.That(species.Length,Is.GreaterThan(0));
            foreach(var actor in species)
            {
                actor.ResetEnemy();
                var d=actor.Definition;
                var observation=new MouseObservation{position=actor.Position,spawn=actor.Position,targetPosition=actor.Position+Vector2.right*4,
                    detected=true,visible=true,pathToTarget=true,canAttack=true,insideStartDistance=true};
                void Begin(double at){actor.Brain.Reset();actor.Brain.Tick(at,observation,true);actor.Brain.BeginAttack(at);}
                Begin(arena.Now);Observe();
                Assert.That(boxes.Single(b=>b.Source==actor).Kind,Is.EqualTo(CombatDiagnostics.HitboxKind.EnemyPrepare));
                Begin(arena.Now-d.prepare-d.active/2);actor.SyncFacing(-1);Observe();
                var box=boxes.Single(b=>b.Source==actor);
                Assert.That(box.Kind,Is.EqualTo(CombatDiagnostics.HitboxKind.EnemyAttack));
                Assert.That(box.Bounds,Is.EqualTo(actor.AttackBounds));
                Begin(arena.Now-d.prepare-d.active-.01);Observe();Assert.That(boxes.Any(b=>b.Source==actor),Is.False);
                Begin(arena.Now-d.prepare-d.active/2);actor.CancelAttack();Observe();Assert.That(boxes.Any(b=>b.Source==actor),Is.False);
            }
            yield return null;
        }
    }
}
