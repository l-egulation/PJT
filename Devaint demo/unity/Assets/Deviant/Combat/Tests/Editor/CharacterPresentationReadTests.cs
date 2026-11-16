using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class CharacterPresentationReadTests
    {
        [Test] public void CharacterConsumerHasNoNativeActorOrCastSurfaceAndNoLegacyDrawCaptureOverloads()
        {
            var type = typeof(CharacterPresenter);
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                foreach (var parameter in method.GetParameters())
                    Assert.That(parameter.ParameterType == typeof(CombatArena) || parameter.ParameterType == typeof(CombatPlayer) || parameter.ParameterType == typeof(CombatCast), Is.False, method.Name);
            Assert.That(type.GetMethod("CaptureAttack"), Is.Null);
            Assert.That(type.GetMethod("Draw").GetParameters().Length, Is.EqualTo(1));
            string root = Path.Combine(Application.dataPath, "Deviant/Combat/Runtime");
            string presenter = File.ReadAllText(Path.Combine(root, "CharacterPresenter.cs"));
            string cues = File.ReadAllText(Path.Combine(root, "Unity/BasicAttackCueBridge.cs"));
            foreach (string forbidden in new[] { "currentCast", "arena.Player", "CombatPlayer", "CombatCast", ".Weapon", ".health" })
            { Assert.That(presenter, Does.Not.Contain(forbidden)); Assert.That(cues, Does.Not.Contain(forbidden)); }
        }
        [UnityTest] public IEnumerator GameDetachedPresentationFactsTrackExistingOwnersWithoutAdvancing() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabDetachedPresentationFactsTrackExistingOwnersWithoutAdvancing() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
                player.selected = 0; Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0, out var error), Is.True, error);
                player.Position = new Vector2(0, 1000); player.axis = -1; player.velocity = new Vector2(2, -3); player.grounded = false; player.SyncFacing(-1);
                Assert.That(arena.ReadModel.TryGetSession(out var session), Is.True);
                Assert.That(arena.ReadModel.TryReadPlayer(session, out var idle), Is.True);
                Assert.That(idle.Presentation.Axis, Is.EqualTo(-1)); Assert.That(idle.Presentation.BasicIndex, Is.EqualTo(-1));
                Assert.That(idle.Presentation.HasAction, Is.False); Assert.That(idle.Presentation.DashDuration, Is.EqualTo(arena.Movement.DashDuration));
                driver.Step(.02f, new CombatInput(-1, CombatButtons.Basic)); var cast = player.currentCast;
                Assert.That(arena.ReadModel.TryReadPlayer(session, out var active), Is.True);
                Assert.That(active.Presentation.HasAction && active.Presentation.IsBasicAction, Is.True);
                Assert.That(active.Presentation.DynamicBodyMapping, Is.False); Assert.That(active.Action.ExecutionId, Is.EqualTo(cast.ExecutionId));
                Assert.That(active.Presentation.ImpactTime, Is.EqualTo(cast.ImpactTime)); Assert.That(active.Presentation.MotionDuration, Is.EqualTo(cast.MotionDuration));
                float now = arena.Now; long tick = arena.SimulationTickCount, sequence = player.NextInputSequence;
                player.Position = new Vector2(9, 1002); player.axis = 1;
                Assert.That(active.Kinematics.X, Is.Not.EqualTo(player.Position.x)); Assert.That(active.Presentation.Axis, Is.EqualTo(-1));
                for (int i = 0; i < 32; i++) arena.ReadModel.TryReadPlayer(session, out _);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 2048; i++) arena.ReadModel.TryReadPlayer(session, out _);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.SimulationTickCount, Is.EqualTo(tick)); Assert.That(player.NextInputSequence, Is.EqualTo(sequence + 1));
                player.CancelAction(); driver.Reset();
                Assert.That(arena.ReadModel.TryReadPlayer(session, out _), Is.False);
                Assert.That(arena.ReadModel.TryGetSession(out session), Is.True);
                Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "rapier"), 0, out error), Is.True, error);
                player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.SyncFacing(1);
                arena.enemies[0].Position = new Vector2(2, 1000); arena.enemies[0].velocity = Vector2.zero; Physics2D.SyncTransforms();
                driver.Step(.02f, new CombatInput(0, CombatButtons.Special, true));
                Assert.That(player.currentCast, Is.Not.Null, "The existing nearest-target charged admission must actually start.");
                Assert.That(player.currentCast.ChargedMelee, Is.Not.Null);
                Assert.That(arena.ReadModel.TryReadPlayer(session, out var charged), Is.True);
                Assert.That(charged.Presentation.HasAction, Is.True); Assert.That(charged.Presentation.IsBasicAction, Is.False); Assert.That(charged.Presentation.DynamicBodyMapping, Is.True);
                Assert.That(charged.Presentation.ImpactTime, Is.EqualTo(player.currentCast.ImpactTime));
            }
        }
    }
}
