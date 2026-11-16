using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class JumpGravitySourceIntegrationTests
    {
        string directory, source, generated;
        LocomotionWindow window;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "deviant-jump-source-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            source = Path.Combine(directory, "player.json"); generated = Path.Combine(directory, "generated.json"); File.WriteAllText(source, Resources.Load<TextAsset>("PlayerLocomotion").text);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (window) window.Close(); if (Application.isPlaying) yield return new ExitPlayMode(); if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        [Test] public void SavingReloadingAndGeneratedTextAssetCompileTheSameCompleteSchemaTwoProfile()
        {
            var draft = LocomotionSource.Load(source, out var hash); draft.firstJumpVelocity = 18; draft.secondJumpVelocity = 22; draft.riseGravity = 72; draft.fallGravity = 110;
            hash = LocomotionSource.Save(source, hash, draft); LocomotionSource.Export(source, hash, generated);
            Assert.That(File.ReadAllBytes(generated), Is.EqualTo(File.ReadAllBytes(source)));
            var fresh = LocomotionSource.Load(generated, out _); var profile = LocomotionSource.Validate(fresh);
            Assert.That(profile.ToData().schemaVersion, Is.EqualTo(2)); Assert.That(profile.FirstJumpVelocity, Is.EqualTo(18)); Assert.That(profile.SecondJumpVelocity, Is.EqualTo(22));
            Assert.That(profile.RiseGravity, Is.EqualTo(72)); Assert.That(profile.FallGravity, Is.EqualTo(110));
            var legacy = profile.ToData(); legacy.schemaVersion = 1; Assert.Throws<ArgumentException>(() => LocomotionSource.Validate(legacy));
        }
        [UnityTest] public IEnumerator GameResetAppliesOneGravityVersionToJumpsActorsAndRecoil() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabResetAppliesOneGravityVersionToJumpsActorsAndRecoil() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var original = arena.Movement;
            var floor = new GameObject("JumpProfileFixtureFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); foreach (var inactiveEnemy in arena.enemies) { inactiveEnemy.aggressive = false; inactiveEnemy.Position = new Vector2(35, 1000); }
                    player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Jump | CombatButtons.Basic)); var running = player.currentCast;
                    var invalid = original.ToData(); invalid.riseGravity = 0; float now = arena.Now; var velocity = player.velocity;
                    Assert.That(arena.TryApplyMovementAndReset(invalid, out _), Is.False); Assert.That(arena.Movement, Is.SameAs(original)); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(player.velocity, Is.EqualTo(velocity)); Assert.That(player.currentCast, Is.SameAs(running));
                    var changed = original.ToData(); changed.firstJumpVelocity = 12; changed.secondJumpVelocity = 16; changed.riseGravity = 30; changed.fallGravity = 50;
                    Assert.That(arena.TryApplyMovementAndReset(changed, out var error), Is.True, error); Assert.That(player.currentCast, Is.Null); Assert.That(ReadPlayer(arena).Jump.Used, Is.Zero); Assert.That(player.velocity, Is.EqualTo(Vector2.zero));
                    var version = arena.Movement.ContentHash; changed.riseGravity = 999; Assert.That(arena.Movement.RiseGravity, Is.EqualTo(30));
                    player.Position = new Vector2(0, 1000); player.grounded = true; Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Jump)); Assert.That(player.VelocityY, Is.EqualTo(12 - 30 * .02f).Within(.00001f));
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Jump)); Assert.That(player.VelocityY, Is.EqualTo(16 - 30 * .02f).Within(.00001f)); Assert.That(ReadPlayer(arena).Jump.Used, Is.EqualTo(2));
                    var enemy = arena.enemies[0]; enemy.Position = new Vector2(30, 1010); enemy.VelocityY = 10; enemy.grounded = false; enemy.PhysicsStep(.02f, 0, false);
                    Assert.That(enemy.VelocityY, Is.EqualTo(10 - 30 * .02f).Within(.00001f)); enemy.VelocityY = -1; enemy.PhysicsStep(.02f, 0, false); Assert.That(enemy.VelocityY, Is.EqualTo(-1 - 50 * .02f).Within(.00001f));
                    player.CancelAction(); player.selected = 0; Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "launcher"), 2, out error), Is.True, error);
                    player.Position = new Vector2(0, 1010); player.velocity = Vector2.zero; player.grounded = false; Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var recoil = player.currentCast;
                    for (int i = 0; i < 50 && player.VelocityY <= 0; i++) driver.Step(.02f);
                    Assert.That(recoil, Is.Not.Null); Assert.That(player.VelocityY, Is.GreaterThan(0), "Actual airborne shotgun must begin the profile-derived hop.");
                    Assert.That(player.VelocityY, Is.EqualTo((float)Math.Sqrt(2 * arena.Movement.RiseGravity) - arena.Movement.RiseGravity * .02f).Within(.00001f),
                        "Ranged activation sets the profile hop before the same tick's ordinary actor gravity step.");
                    Assert.That(arena.Movement.ContentHash, Is.EqualTo(version));
                    Assert.That(arena.TryApplyMovementAndReset(arena.Movement.ToData(), out error), Is.True, error);
                    Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "bomb"), 2, out error), Is.True, error);
                    player.Position = new Vector2(0, 1010); player.velocity = Vector2.zero; player.grounded = false; Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var missile = player.currentCast;
                    for (int i = 0; i < 25; i++) driver.Step(.02f);
                    Assert.That(missile && missile.MissileFlying, Is.True); Assert.That(missile.TryMissileDetonate(), Is.True);
                    Assert.That(player.VelocityY, Is.EqualTo((float)Math.Sqrt(2 * arena.Movement.RiseGravity * missile.Definition.Missile.RecoilHeight)).Within(.00001f));
                    Assert.That(arena.TryApplyMovementAndReset(original.ToData(), out error), Is.True, error); Assert.That(arena.casts.Any(cast => cast && cast.owner == player), Is.False);
                }
            }
            finally { Object.DestroyImmediate(floor); }
        }
        static ActorReadSnapshot ReadPlayer(CombatArena arena)
        {
            Assert.That(arena.ReadModel.TryGetSession(out var session), Is.True);
            Assert.That(arena.ReadModel.TryReadPlayer(session, out var snapshot), Is.True); return snapshot;
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ActualWindowEditsFourFieldsUndoSavesAndReloadsWithoutApplying()
        {
            window = ScriptableObject.CreateInstance<LocomotionWindow>(); window.Show(); window.CreateGUI(); window.LoadSource(source, generated); yield return null;
            var root = window.rootVisualElement; string before = File.ReadAllText(source);
            var original = LocomotionSource.Load(source, out _);
            Undo.IncrementCurrentGroup();
            root.Q<FloatField>("firstJumpVelocity").value = 18; root.Q<FloatField>("secondJumpVelocity").value = 22; root.Q<FloatField>("riseGravity").value = 72; root.Q<FloatField>("fallGravity").value = 110;
            Assert.That(File.ReadAllText(source), Is.EqualTo(before)); Assert.That(root.Q<Label>("diff").text, Does.Contain("상승 중력"));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
            Assert.That(root.Q<FloatField>("firstJumpVelocity").value, Is.EqualTo(20)); Assert.That(root.Q<FloatField>("secondJumpVelocity").value, Is.EqualTo(20));
            Assert.That(root.Q<FloatField>("riseGravity").value, Is.EqualTo(66.6666667f)); Assert.That(root.Q<FloatField>("fallGravity").value, Is.EqualTo(104.1666667f));
            Assert.That(File.ReadAllText(source), Is.EqualTo(before)); Assert.That(File.Exists(generated), Is.False, "Undo must not write source or generated files.");
            Undo.IncrementCurrentGroup();
            root.Q<FloatField>("firstJumpVelocity").value = 18; root.Q<FloatField>("secondJumpVelocity").value = 22; root.Q<FloatField>("riseGravity").value = 72; root.Q<FloatField>("fallGravity").value = 110;
            Click("save"); Click("reload"); var fresh = LocomotionSource.Load(source, out _); Assert.That(fresh.firstJumpVelocity, Is.EqualTo(18)); Assert.That(fresh.secondJumpVelocity, Is.EqualTo(22)); Assert.That(fresh.riseGravity, Is.EqualTo(72)); Assert.That(fresh.fallGravity, Is.EqualTo(110));
            Assert.That(File.ReadAllBytes(generated), Is.EqualTo(File.ReadAllBytes(source)));
            Assert.That(fresh.runSpeed, Is.EqualTo(original.runSpeed)); Assert.That(fresh.dashSpeed, Is.EqualTo(original.dashSpeed)); Assert.That(fresh.dashDuration, Is.EqualTo(original.dashDuration));
            root.Q<FloatField>("riseGravity").value = 0; Assert.That(root.Q<Button>("save").enabledSelf, Is.False); Assert.That(root.Q<Button>("apply").enabledSelf, Is.False);
        }
        void Click(string name) { var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True, name + ": " + window.rootVisualElement.Q<Label>("state").text); using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); } }
    }
}
