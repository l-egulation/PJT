using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class AirJumpVisualTests
    {
        const float Tick = .005f;
        CombatArena arena;
        CombatArena.ManualSimulation driver;
        CombatPlayer Player => arena.Player;

        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            arena = new GameObject("Air jump visual regression").AddComponent<CombatArena>();
            var content = CombatContentLoader.Load(null, true);
            content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Air jump camera").AddComponent<Camera>();
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.PlayerSpawn = new Vector2(0, 200.003f);
            content.PlayerSize = new Vector2(.9375f, 2);
            content.PlayerOffset = new Vector2(.1875f, 1);
            content.PlayerSprite = Resources.Load<Sprite>("ApprovedTraversal/jump/base_sword/F03");
            content.Enemies = Array.Empty<CombatEnemySpawn>();
            content.BindFeedback = null;
            Assert.That(arena.Initialize(content), Is.True);
            var floor = new GameObject("Air jump regression floor");
            floor.transform.position = new Vector3(0, 199.5f, 0);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            floor.AddComponent<ArenaSurface>();
            Physics2D.SyncTransforms();
            driver = arena.AcquireManualSimulation();
            Player.UseTestInput = true;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            driver?.Dispose();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        void Step(CombatButtons buttons = CombatButtons.None, int climb = 0)
        {
            driver.Step(Tick, new CombatInput(0, buttons, climbAxis: climb));
            arena.Presentation.Draw(Tick);
        }

        void BeginSecondJump()
        {
            driver.Reset();
            Player.Position = new Vector2(0, 200.003f);
            Player.velocity = Vector2.zero;
            Player.grounded = true;
            Physics2D.SyncTransforms();
            Step(CombatButtons.Jump);
            Assert.That(arena.Presentation.CurrentClip, Is.Not.EqualTo("air_jump"), "First jump keeps its ordinary clip");
            Step();
            Step(CombatButtons.Jump);
            Assert.That(Player.jumps.Used, Is.EqualTo(2));
            Assert.That(Player.AirJumpStartedAt, Is.EqualTo(arena.Now - Tick).Within(.000001f));
        }

        [UnityTest] public IEnumerator SecondJumpSelectsTheFourApprovedPosesAndExpiresAtTheirDuration()
        {
            var catalog = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("ApprovedTraversal/AirJump").text);
            var clip = catalog.sets.Single(set => set.form == "base_sword").Find("air_jump");
            clip.Load();
            Assert.That(clip.Duration, Is.EqualTo(.18f).Within(.00001f));
            BeginSecondJump();
            while (arena.Now - Player.AirJumpStartedAt < clip.Duration)
            {
                Assert.That(arena.Presentation.CurrentClip, Is.EqualTo("air_jump"));
                Assert.That(Player.visual.sprite, Is.SameAs(clip.At(arena.Now - Player.AirJumpStartedAt, false)));
                Step();
            }
            Assert.That(arena.Presentation.CurrentClip, Is.Not.EqualTo("air_jump"), "Ordinary airborne animation resumes after 0.18 seconds");
            yield return null;
        }

        [UnityTest] public IEnumerator LandingDashAttackAndWallGrabTakeOwnershipFromTheAirJump()
        {
            BeginSecondJump();
            Player.Position = new Vector2(0, 200.003f);
            Player.velocity = Vector2.zero; Player.grounded = true;
            arena.Presentation.Draw(Tick);
            Assert.That(arena.Presentation.CurrentClip, Is.Not.EqualTo("air_jump"), "Landing owns its pose immediately");

            BeginSecondJump(); Step(CombatButtons.Dash);
            Assert.That(Player.Dashing, Is.True);
            Assert.That(arena.Presentation.CurrentClip, Is.Not.EqualTo("air_jump"), "Dash owns its pose");

            BeginSecondJump(); Step(CombatButtons.Basic);
            Assert.That(Player.currentCast, Is.Not.Null);
            Assert.That(arena.Presentation.CurrentClip, Is.Not.EqualTo("air_jump"), "An admitted attack owns its pose");

            BeginSecondJump();
            var wall = new GameObject("Air jump interruption wall");
            wall.transform.position = new Vector3(4, 200, 0);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(2, 80);
            wall.AddComponent<ArenaSurface>();
            Player.Position = new Vector2(2.34f, 202); Player.velocity = Vector2.zero;
            Physics2D.SyncTransforms(); Step(climb: 1);
            Assert.That(Player.WallClimbing, Is.True);
            Assert.That(arena.Presentation.CurrentClip, Is.Not.EqualTo("air_jump"), "Wall traversal owns its pose");
            yield return null;
        }

        [UnityTest] public IEnumerator AcceptedJumpsClearLedgeRecoveryAndRejectedJumpKeepsItUntilBufferedLanding()
        {
            var ledge = typeof(CombatPlayer).GetProperty("WallLedgeStartedAt", BindingFlags.Instance | BindingFlags.Public);
            driver.Reset(); Player.grounded = true;
            ledge.SetValue(Player, arena.Now);
            Step(CombatButtons.Jump);
            Assert.That(float.IsNegativeInfinity(Player.WallLedgeStartedAt), Is.True, "First jump must not be redrawn as ledge recovery");
            Step(); ledge.SetValue(Player, arena.Now);
            Step(CombatButtons.Jump);
            Assert.That(float.IsNegativeInfinity(Player.WallLedgeStartedAt), Is.True, "Second jump also owns its pose immediately");
            Step(); float prior = arena.Now; ledge.SetValue(Player, prior);
            Step(CombatButtons.Jump);
            Assert.That(Player.WallLedgeStartedAt, Is.EqualTo(prior), "An exhausted jump does not cancel the active recovery");
            Player.Landed();
            Assert.That(Player.jumps.Used, Is.EqualTo(1), "The buffered jump launches on landing");
            Assert.That(float.IsNegativeInfinity(Player.WallLedgeStartedAt), Is.True, "A buffered launch owns its pose too");
            yield return null;
        }
    }
}
