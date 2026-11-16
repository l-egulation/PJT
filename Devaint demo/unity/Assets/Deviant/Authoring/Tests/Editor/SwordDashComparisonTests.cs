using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
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
    public class SwordDashComparisonTests
    {
        CombatArena arena;
        [UnitySetUp] public IEnumerator SetUp()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            arena = Object.FindAnyObjectByType<CombatArena>();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<SwordDashComparisonWindow>().Where(w => w.name == "ComparisonTestWindow")) window.Close();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        [Test] public void EqualInputsReproduceAndEditedTimingRangeAndMovementChangeMeasuredResults()
        {
            var a = SwordDashVersion.Capture(arena);
            string catalog = JsonUtility.ToJson(arena.catalog);
            arena.Player.weapons[0].StatSlots[0] = 3;
            arena.Player.weapons[1].StatSlots[1] = 4;
            arena.Player.selected = 1; arena.enemies[1].aggressive = true;
            var equal = SwordDashComparison.Run(arena, a, a);
            Assert.That(Values(equal.A), Is.EqualTo(Values(equal.B)));
            Assert.That(equal.A.HealthLost, Is.EqualTo(10));
            Assert.That(equal.A.Travel, Is.GreaterThan(2.9f));
            Assert.That(equal.A.WallTravel, Is.InRange(.3f, .36f));
            Assert.That(equal.A.AttackInputs.ScenarioId, Is.EqualTo(SwordDashComparison.ScenarioId + "/attack"));
            Assert.That(equal.A.AttackInputs.StepSeconds, Is.EqualTo(SwordDashComparison.Step));
            Assert.That(equal.A.AttackInputs.Inputs.Count, Is.EqualTo(60)); Assert.That(equal.A.WallInputs.Inputs.Count, Is.EqualTo(20));
            Assert.That(equal.A.AttackInputs.Inputs[0].Pressed, Is.EqualTo(CombatButtons.Basic));
            Assert.That(equal.A.AttackInputs.Inputs[30].Pressed, Is.EqualTo(CombatButtons.Dash));
            Assert.That(equal.A.WallInputs.Inputs[0].Pressed, Is.EqualTo(CombatButtons.Dash));
            Assert.That(equal.B.AttackInputs, Is.SameAs(equal.A.AttackInputs)); Assert.That(equal.B.WallInputs, Is.SameAs(equal.A.WallInputs));
            var basic = a.Sword.ToData(); basic.impact = .3f;
            BasicMeleeDefinition.TryCreate(a.Sword.Id, basic, out var sword, out _);
            var data = a.Movement.ToData(); data.dashSpeed = 8;
            LocomotionProfile.TryCreate(data, out var movement, out _);
            var pair = SwordDashComparison.Run(arena, a, new SwordDashVersion(sword, movement));
            Assert.That(pair.B.FirstHitTick, Is.GreaterThan(pair.A.FirstHitTick));
            Assert.That(pair.B.Travel, Is.LessThan(pair.A.Travel));
            Assert.That(pair.B.WallTravel, Is.EqualTo(pair.A.WallTravel).Within(.001f));
            basic.box.width = .2f; basic.box.x = .3f;
            BasicMeleeDefinition.TryCreate(a.Sword.Id, basic, out sword, out _);
            var range = SwordDashComparison.Run(arena, a, new SwordDashVersion(sword, movement));
            Assert.That(range.B.HealthLost, Is.Zero); Assert.That(range.B.FirstHitTick, Is.EqualTo(-1));
            basic = a.Sword.ToData(); basic.damage = 0;
            Assert.That(BasicMeleeDefinition.TryCreate(a.Sword.Id, basic, out sword, out _), Is.True);
            var zero = SwordDashComparison.Run(arena, a, new SwordDashVersion(sword, movement));
            Assert.That(zero.B.HealthLost, Is.Zero);
            Assert.That(zero.B.FirstHitTick, Is.EqualTo(zero.A.FirstHitTick), "An accepted zero-damage hit is still a hit.");
            Assert.That(arena.Player.selected, Is.EqualTo(1));
            Assert.That(arena.Player.weapons[0].StatSlots[0], Is.EqualTo(3));
            Assert.That(arena.Player.weapons[1].StatSlots[1], Is.EqualTo(4));
            Assert.That(arena.enemies[1].aggressive, Is.True);
            AssertRestored(a); Assert.That(JsonUtility.ToJson(arena.catalog), Is.EqualTo(catalog));
        }
        [Test] public void FailureReleasesOnlyItsOwnerAndRestoresDefinitionsAndEquipment()
        {
            var a = SwordDashVersion.Capture(arena);
            arena.Player.weapons[0].StatSlots[0] = 1;
            using (var owner = arena.AcquireManualSimulation())
            {
                Assert.Throws<InvalidOperationException>(() => SwordDashComparison.Run(arena, a, a));
                Assert.That(arena.ManualSimulationActive, Is.True);
                Assert.That(arena.Player.weapons[0].StatSlots[0], Is.EqualTo(1));
            }
            int resets = 0;
            Action failOnce = () => { if (++resets == 3) throw new InvalidOperationException("fixture failure"); };
            arena.ResetCompleted += failOnce;
            try { Assert.Throws<InvalidOperationException>(() => SwordDashComparison.Run(arena, a, a)); }
            finally { arena.ResetCompleted -= failOnce; }
            Assert.That(resets, Is.GreaterThan(3));
            Assert.That(arena.Player.weapons[0].StatSlots[0], Is.EqualTo(1));
            AssertRestored(a);
            var other = new GameObject("OtherCombatSession"); other.SetActive(false); other.AddComponent<CombatArena>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => SwordDashComparison.Run(arena, a, a));
                AssertRestored(a);
            }
            finally { Object.Destroy(other); }
        }
        [Test] public void ExistingPauseRejectsComparisonBeforeResetOrEquipmentChanges()
        {
            var a = SwordDashVersion.Capture(arena);
            arena.Player.health = 42; arena.Player.Position = new Vector2(7, 0); arena.Player.weapons[0].StatSlots[0] = 3;
            int resets = 0; Action count = () => resets++;
            using (var owner = arena.Controls.OpenScope("external pause"))
            {
                var pause = owner.PauseSimulation(); arena.ResetCompleted += count;
                try { Assert.Throws<InvalidOperationException>(() => SwordDashComparison.Run(arena, a, a)); }
                finally { arena.ResetCompleted -= count; }
                Assert.That(resets, Is.Zero); Assert.That(arena.Player.health, Is.EqualTo(42));
                Assert.That(arena.Player.Position.x, Is.EqualTo(7)); Assert.That(arena.Player.weapons[0].StatSlots[0], Is.EqualTo(3));
                Assert.That(arena.ManualSimulationActive, Is.False); Assert.That(pause.IsActive, Is.True);
                Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
            }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowCapturesRuntimeVersionsAndShowsMeasuredComparison()
        {
            var window = ScriptableObject.CreateInstance<SwordDashComparisonWindow>(); window.name = "ComparisonTestWindow";
            window.Show(); window.CreateGUI(); yield return null;
            var root = window.rootVisualElement;
            Assert.That(root.Q<Button>("compare").enabledSelf, Is.False);
            Click(root, "captureA");
            var edited = arena.Movement.ToData(); edited.dashSpeed = 8;
            Assert.That(arena.TryApplyMovementAndReset(edited, out var error), Is.True, error);
            var expected = arena.Movement.ContentHash;
            Click(root, "captureB"); Click(root, "compare");
            Assert.That(root.Q<Label>("status").text, Does.Contain("비교 완료"));
            Assert.That(root.Q<Label>("results").text, Does.Contain("B−A").And.Contain("동일 입력 재생").And.Contain("60틱").And.Contain("20틱"));
            Assert.That(root.Q<Label>("versions").text, Does.Contain(expected.Substring(0, 12)));
            Assert.That(arena.Movement.ContentHash, Is.EqualTo(expected));
            Assert.That(arena.ManualSimulationActive, Is.False);
        }
        [Test] public void SavedInputsReloadAndDriveBothVersionsInsteadOfTheDefaultScript()
        {
            string directory = Path.Combine(Path.GetTempPath(), "deviant-input-replay-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            var version = SwordDashVersion.Capture(arena);
            try
            {
                string path = Path.Combine(directory, "idle.json"); var idle = SwordDashInputSourceTests.Inputs(true);
                SwordDashInputSource.Save(path, null, idle); var loaded = SwordDashInputSource.Load(path, out _);
                var pair = SwordDashComparison.Run(arena, version, version, loaded);
                Assert.That(pair.A.AttackInputs, Is.SameAs(loaded.Attack)); Assert.That(pair.B.AttackInputs, Is.SameAs(loaded.Attack));
                Assert.That(pair.A.WallInputs, Is.SameAs(loaded.Wall)); Assert.That(pair.B.WallInputs, Is.SameAs(loaded.Wall));
                foreach (var result in new[] { pair.A, pair.B })
                {
                    Assert.That(result.HealthLost, Is.Zero); Assert.That(result.FirstHitTick, Is.EqualTo(-1));
                    Assert.That(result.Travel, Is.EqualTo(0).Within(.001f)); Assert.That(result.WallTravel, Is.EqualTo(0).Within(.001f));
                }
                AssertRestored(version);
            }
            finally { Directory.Delete(directory, true); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowImportsExportsAndKeepsItsSelectionWhenFileValidationFails()
        {
            string directory = Path.Combine(Path.GetTempPath(), "deviant-input-window-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            var window = ScriptableObject.CreateInstance<SwordDashComparisonWindow>(); window.name = "ComparisonTestWindow";
            try
            {
                string source = Path.Combine(directory, "idle.json"); var idle = SwordDashInputSourceTests.Inputs(true); SwordDashInputSource.Save(source, null, idle);
                window.Show(); window.CreateGUI(); yield return null; var root = window.rootVisualElement;
                Assert.That(root.Q<Button>("importInputs"), Is.Not.Null); Assert.That(root.Q<Button>("exportInputs").enabledSelf, Is.False);
                window.LoadInputs(source); Assert.That(root.Q<Label>("inputs").text, Does.Contain("불러온 기록"));
                Click(root, "captureA"); Click(root, "captureB"); Click(root, "compare");
                Assert.That(root.Q<Label>("results").text, Does.Contain("적중 없음").And.Contain("이동 0"));
                string exported = Path.Combine(directory, "exported.json"); string hash = window.SaveInputs(exported, null);
                Assert.That(SwordDashInputSource.Load(exported, out _).Attack.ContentHash, Is.EqualTo(idle.Attack.ContentHash));
                File.WriteAllText(exported, "external edit"); Assert.Throws<IOException>(() => window.SaveInputs(exported, hash)); Assert.That(File.ReadAllText(exported), Is.EqualTo("external edit"));
                File.WriteAllText(source, "{}"); Assert.Throws<ArgumentException>(() => window.LoadInputs(source));
                Assert.That(root.Q<Label>("inputs").text, Does.Contain(idle.Attack.ContentHash.Substring(0, 12)));
                window.ClearInputs(); Assert.That(root.Q<Label>("inputs").text, Does.Contain("기본 시나리오")); Assert.That(root.Q<Button>("exportInputs").enabledSelf, Is.False);
                Click(root, "compare"); Assert.That(root.Q<Label>("results").text, Does.Contain("HP 감소 10"));
            }
            finally { window.Close(); Directory.Delete(directory, true); }
        }
        void AssertRestored(SwordDashVersion version)
        {
            Assert.That(arena.ManualSimulationActive, Is.False);
            Assert.That(arena.BasicSwordFirst.ContentHash, Is.EqualTo(version.Sword.ContentHash));
            Assert.That(arena.Movement.ContentHash, Is.EqualTo(version.Movement.ContentHash));
            Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.casts, Is.Empty);
            Assert.That(arena.Player.Position, Is.EqualTo(arena.Player.spawn));
        }
        static float[] Values(SwordDashMetrics m) => new[] { m.HealthLost, m.FirstHitTick, m.Travel, m.WallTravel };
        static void Click(VisualElement root, string name)
        {
            var button = root.Q<Button>(name); Assert.That(button.enabledSelf, Is.True);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
    }
}
