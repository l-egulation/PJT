using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.CombatLab;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class FrameSwordDashComparisonTests
    {
        CombatArena arena;
        string repository;
        [UnitySetUp] public IEnumerator SetUp()
        {
            repository = FramePresetSourceTests.CopyRepository();
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; arena = Object.FindAnyObjectByType<CombatArena>(); Bind(arena);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<SwordDashComparisonWindow>().Where(w => w.name == "FrameComparisonTestWindow")) window.Close();
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (Directory.Exists(repository)) Directory.Delete(repository, true);
        }
        static void Bind(CombatArena target) => target.characterAnimations = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
        MeleeFrameDefinition Frame(float delta) => FramePresetSourceTests.Candidate(repository, delta);
        SwordDashVersion ApplyAndCapture(MeleeFrameDefinition frame)
        {
            Assert.That(arena.TryApplySwordFrameTiming(frame, false, out var error), Is.True, error); var version = SwordDashVersion.Capture(arena);
            Assert.That(version.FrameTiming, Is.SameAs(frame)); Assert.That(version.FixedSword.ContentHash, Is.EqualTo(frame.SourceAction.ContentHash)); return version;
        }
        void Restored(SwordDashVersion prior)
        {
            Assert.That(arena.ManualSimulationActive, Is.False); Assert.That(arena.FrameSwordTiming, Is.SameAs(prior.FrameTiming)); Assert.That(arena.BasicSwordFirst.ContentHash, Is.EqualTo(prior.Sword.ContentHash));
            Assert.That(arena.Movement.ContentHash, Is.EqualTo(prior.Movement.ContentHash)); Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.casts, Is.Empty); Assert.That(arena.Player.Position, Is.EqualTo(arena.Player.spawn));
        }
        [Test] public void FramePairSharesRecordedInputsAndRestoresTheOriginalFrameAndUnderlyingFixedSource()
        {
            var a = ApplyAndCapture(Frame(0)); var data = arena.Movement.ToData(); data.dashSpeed = 8;
            Assert.That(arena.TryApplyMovementAndReset(data, out var error), Is.True, error); var b = ApplyAndCapture(Frame(.1f));
            arena.Player.selected = 1; arena.Player.weapons[0].StatSlots[0] = 3; arena.enemies[1].aggressive = true;
            var pair = SwordDashComparison.Run(arena, a, b);
            Assert.That(pair.A.Version.FrameTiming.ContentHash, Is.EqualTo(a.FrameTiming.ContentHash)); Assert.That(pair.B.Version.FrameTiming.ContentHash, Is.EqualTo(b.FrameTiming.ContentHash));
            Assert.That(pair.A.HealthLost, Is.EqualTo(10)); Assert.That(pair.B.HealthLost, Is.EqualTo(10)); Assert.That(pair.B.FirstHitTick, Is.GreaterThan(pair.A.FirstHitTick)); Assert.That(pair.B.Travel, Is.LessThan(pair.A.Travel));
            Assert.That(pair.A.AttackInputs, Is.SameAs(pair.B.AttackInputs)); Assert.That(pair.A.WallInputs, Is.SameAs(pair.B.WallInputs));
            Restored(b); Assert.That(arena.Player.selected, Is.EqualTo(1)); Assert.That(arena.Player.weapons[0].StatSlots[0], Is.EqualTo(3)); Assert.That(arena.enemies[1].aggressive, Is.True);
            Assert.That(arena.TryRestoreSwordFixedTiming(false, out error), Is.True, error); Assert.That(arena.BasicSwordFirst.ContentHash, Is.EqualTo(b.FixedSword.ContentHash));
        }
        [Test] public void FixedAndFrameVersionsUseOneScenarioAndRestoreFixedMode()
        {
            var numeric = SwordDashVersion.Capture(arena); var frame = Frame(.06f); var framed = new SwordDashVersion(frame.Action, numeric.Movement, frame);
            var pair = SwordDashComparison.Run(arena, numeric, framed);
            Assert.That(pair.A.Version.FrameTiming, Is.Null); Assert.That(pair.B.Version.FrameTiming, Is.SameAs(frame)); Assert.That(pair.B.FirstHitTick, Is.GreaterThan(pair.A.FirstHitTick));
            Assert.That(pair.A.AttackInputs, Is.SameAs(pair.B.AttackInputs)); Restored(numeric);
            Assert.Throws<ArgumentException>(() => new SwordDashVersion(numeric.Sword, numeric.Movement, frame));
        }
        [Test] public void FailureEnteringBRestoresFrameModeMovementEquipmentAndOwner()
        {
            var prior = ApplyAndCapture(Frame(.03f)); var aFrame = Frame(0); var bFrame = Frame(.1f);
            var a = new SwordDashVersion(aFrame.Action, prior.Movement, aFrame); var b = new SwordDashVersion(bFrame.Action, prior.Movement, bFrame);
            arena.Player.selected = 1; arena.Player.weapons[1].StatSlots[0] = 4; int resets = 0;
            Action fail = () => { if (++resets == 4) throw new InvalidOperationException("B reset failure"); }; arena.ResetCompleted += fail;
            try { Assert.Throws<InvalidOperationException>(() => SwordDashComparison.Run(arena, a, b)); }
            finally { arena.ResetCompleted -= fail; }
            Assert.That(resets, Is.GreaterThan(4)); Restored(prior); Assert.That(arena.Player.selected, Is.EqualTo(1)); Assert.That(arena.Player.weapons[1].StatSlots[0], Is.EqualTo(4));
        }
        [Test] public void InvalidFrameArtBindingRejectsBeforeResetOrAnyCapturedModeChanges()
        {
            var prior = ApplyAndCapture(Frame(.03f)); var source = prior.FrameTiming.SourceMotion.ToDocument(); source.steps[0].seconds += .01f;
            var otherMotion = new FixedAttackMotion(source); var invalid = MeleeFrameCompiler.CompileFirst(prior.FixedSword, otherMotion, MeleeFrameCompiler.ProjectFirst(prior.FixedSword, otherMotion, 2.5f), 2.5f);
            var bad = new SwordDashVersion(invalid.Action, prior.Movement, invalid); int resets = 0; Action count = () => resets++; arena.ResetCompleted += count;
            arena.Player.health = 42; arena.Player.Position = new Vector2(7, 0);
            try {
                Assert.Throws<InvalidOperationException>(() => SwordDashComparison.Run(arena, prior, bad)); Assert.That(resets, Is.Zero); Assert.That(arena.FrameSwordTiming, Is.SameAs(prior.FrameTiming)); Assert.That(arena.Player.health, Is.EqualTo(42)); Assert.That(arena.Player.Position.x, Is.EqualTo(7));
                var set = arena.characterAnimations.sets.Single(s => s.form == "base_sword"); set.Find("attack1").sprites = new Sprite[1];
                Assert.Throws<InvalidOperationException>(() => SwordDashComparison.Run(arena, prior, prior)); Assert.That(resets, Is.Zero); Assert.That(arena.FrameSwordTiming, Is.SameAs(prior.FrameTiming));
            }
            finally { arena.ResetCompleted -= count; }
        }
        [Test] public void SavedFrameAndInputFilesDriveTheSameLoadedSamplesForBothVersions()
        {
            var candidate = Frame(.04f); string preset = Path.Combine(repository, "frame.json"), input = Path.Combine(repository, "inputs.json");
            FramePresetSource.Save(preset, null, repository, candidate); var loaded = FramePresetSource.Load(preset, repository, out _); var prior = ApplyAndCapture(loaded);
            SwordDashInputSource.Save(input, null, SwordDashInputSourceTests.Inputs(true)); var samples = SwordDashInputSource.Load(input, out _);
            var pair = SwordDashComparison.Run(arena, prior, prior, samples);
            foreach (var result in new[] { pair.A, pair.B }) {
                Assert.That(result.AttackInputs, Is.SameAs(samples.Attack)); Assert.That(result.WallInputs, Is.SameAs(samples.Wall)); Assert.That(result.HealthLost, Is.Zero); Assert.That(result.FirstHitTick, Is.EqualTo(-1)); Assert.That(result.Travel, Is.Zero.Within(.001f));
            }
            Restored(prior);
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowKeepsCompleteFrameCapturesAcrossPlayModeAndShowsMeasuredComparison()
        {
            var window = ScriptableObject.CreateInstance<SwordDashComparisonWindow>(); window.name = "FrameComparisonTestWindow"; window.Show(); window.CreateGUI(); yield return null;
            var a = Frame(0); var b = Frame(.08f); ApplyAndCapture(a); Click(window, "captureA"); ApplyAndCapture(b); Click(window, "captureB");
            Assert.That(window.rootVisualElement.Q<Label>("versions").text, Does.Contain(a.ContentHash.Substring(0, 12)).And.Contain(b.ContentHash.Substring(0, 12)));
            yield return new ExitPlayMode(); LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; arena = Object.FindAnyObjectByType<CombatArena>(); Bind(arena);
            window = Resources.FindObjectsOfTypeAll<SwordDashComparisonWindow>().Single(w => w.name == "FrameComparisonTestWindow"); window.CreateGUI(); yield return null;
            Assert.That(window.rootVisualElement.Q<Label>("versions").text, Does.Contain(a.ContentHash.Substring(0, 12)).And.Contain(b.ContentHash.Substring(0, 12)));
            Click(window, "compare"); Assert.That(window.rootVisualElement.Q<Label>("status").text, Does.Contain("비교 완료")); Assert.That(window.rootVisualElement.Q<Label>("results").text, Does.Contain("동일 입력 재생").And.Contain("HP 감소 10"));
            Assert.That(arena.FrameSwordTiming, Is.Null, "The fresh runtime's fixed mode is restored after frame comparison."); Assert.That(arena.ManualSimulationActive, Is.False);
        }
        static void Click(SwordDashComparisonWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True, name);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
    }
}
