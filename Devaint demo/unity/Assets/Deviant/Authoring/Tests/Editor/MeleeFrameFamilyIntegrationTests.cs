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
    public class MeleeFrameFamilyIntegrationTests
    {
        string repository;
        MeleeFrameWindow window;
        [SetUp] public void SetUp() { repository = BasicMeleeSourceTests.CopyRepository(); }
        [UnityTearDown] public IEnumerator TearDown() { if (window) window.Close(); if (Application.isPlaying) yield return new ExitPlayMode(); if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        static MeleeFrameSourceDocument Project(MeleeFrameDefinition source) => source.Family == MeleeFrameFamily.Basic
            ? MeleeFrameCompiler.ProjectBasic(source.NumericAction, source.FormId, source.InputSpeed, source.Media) : MeleeFrameCompiler.ProjectDirect(source.NumericDirect, source.Media);
        static MeleeFrameDefinition Compile(MeleeFrameDefinition source, MeleeFrameSourceDocument document) => source.Family == MeleeFrameFamily.Basic
            ? MeleeFrameCompiler.CompileBasic(source.NumericAction, source.FormId, source.InputSpeed, source.Media, document) : MeleeFrameCompiler.CompileDirect(source.NumericDirect, source.Media, document);
        [Test] public void CompleteTwentyFamilyProjectsEveryExistingMediaIncludingZeroSourceContactAndRoundTrips()
        {
            var catalog = MeleeFrameSource.Load(repository, out _); Assert.That(catalog.Actions.Count, Is.EqualTo(20));
            foreach (var source in catalog.Actions)
            {
                var document = source.Id == FixedAttackMotion.FirstSwordAction
                    ? MeleeFrameCompiler.CompileFirst(source.NumericAction, FrameMotionSource.Load(repository, out _).FixedBinding,
                        MeleeFrameCompiler.ProjectFirst(source.NumericAction, FrameMotionSource.Load(repository, out _).FixedBinding, source.InputSpeed), source.InputSpeed).ToDocument()
                    : Project(source);
                var compiled = Compile(source, document); Assert.That(compiled.Action.Impact, Is.EqualTo(source.Action.Impact).Within(.000001f), source.Id);
                Assert.That(compiled.Action.Duration, Is.EqualTo(source.Action.Duration).Within(.000001f), source.Id); Assert.That(compiled.Action.Active, Is.EqualTo(source.Action.Active).Within(.000001f), source.Id);
                Assert.That(compiled.Timeline.Frames.Select(frame => frame.Id), Is.EqualTo(source.Media.StepIds));
                foreach (float speed in new[] { 1f, 1.5f }) { var action = compiled.CompileForStart(speed); var body = compiled.CompileMotion(speed); Assert.That(body.Impact, Is.EqualTo(action.Impact)); Assert.That(body.End, Is.EqualTo(action.End)); }
                var copy = Compile(source, JsonUtility.FromJson<MeleeFrameSourceDocument>(JsonUtility.ToJson(document))); Assert.That(copy.ContentHash, Is.EqualTo(compiled.ContentHash));
            }
        }
        [Test] public void CanonicalSaveAndNumericGuardsRespectOneActiveTimeSourceAndFileHashes()
        {
            var catalog = MeleeFrameSource.Load(repository, out var hash); var source = catalog.Actions.Single(value => value.Id == "base_sword_basic_1");
            var document = catalog.ToDocument(); var projected = Project(source); projected.timeline.frames[0].seconds += .03f;
            document.actions[Array.FindIndex(document.actions, value => value.actionId == source.Id)] = projected;
            hash = MeleeFrameSource.Save(repository, hash, document); var fresh = MeleeFrameSource.Load(repository, out var reload);
            Assert.That(reload, Is.EqualTo(hash)); Assert.That(fresh.Actions.Single(value => value.Id == source.Id).Action.Impact, Is.EqualTo(source.Action.Impact + .03f).Within(.000001f));
            var numeric = BasicMeleeSource.Load(repository, out var numericHash); var hit = numeric.Single(value => value.Id == source.Id); hit.hit.impact += .01f;
            Assert.Throws<ArgumentException>(() => BasicMeleeSource.Save(repository, numericHash, numeric));
            numeric = BasicMeleeSource.Load(repository, out numericHash); numeric.Single(value => value.Id == source.Id).hit.damage += 5; Assert.DoesNotThrow(() => BasicMeleeSource.Save(repository, numericHash, numeric));
            File.AppendAllText(Path.Combine(repository, MeleeFrameSource.RelativePath), "\n"); Assert.Throws<IOException>(() => MeleeFrameSource.Save(repository, hash, document));
        }
        [Test] public void FirstNumericSourceUsesTheLatestExplicitModeAndKeepsHashSafeDamageEdits()
        {
            string path = Path.Combine(repository, FrameMotionSource.ActionPath);
            var numeric = BasicSwordSource.Load(path, out var numericHash); var candidate = FrameMotionSource.ReadCandidate(repository, out var frameHash);
            FrameMotionSource.Save(repository, frameHash, candidate); byte[] before = File.ReadAllBytes(path);
            numeric.hit.impact += .01f;
            Assert.Throws<ArgumentException>(() => BasicSwordSource.Save(repository, numericHash, numeric)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            numeric = BasicSwordSource.Load(path, out numericHash); numeric.hit.damage += 1;
            Assert.DoesNotThrow(() => BasicSwordSource.Save(repository, numericHash, numeric));
            numeric = BasicSwordSource.Load(path, out numericHash); numeric.hit.duration += .01f;
            File.Delete(Path.Combine(repository, AttackMotionSource.RelativePath)); before = File.ReadAllBytes(path);
            Assert.Catch<IOException>(() => BasicSwordSource.Save(repository, numericHash, numeric)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator GameSharedFirstWeightsUpdateFixedDirectButPinLocalOverrides() => SharedFirstWeights("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabSharedFirstWeightsUpdateFixedDirectButPinLocalOverrides() => SharedFirstWeights("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        IEnumerator SharedFirstWeights(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            using (var driver = arena.AcquireManualSimulation())
            {
                var basicStart = typeof(CombatPlayer).GetMethod("BeginBasic", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var specialStart = typeof(CombatPlayer).GetMethod("BeginSpecial", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Action<string> prepare = form => {
                    driver.Reset(); player.selected = 0;
                    foreach (var target in arena.enemies) { target.aggressive = false; target.Position = new Vector2(35, 1000); }
                    Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, value => value.id == form), 0, out var error), Is.True, error);
                    foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
                    player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; Physics2D.SyncTransforms();
                };
                prepare("base_sword"); specialStart.Invoke(player, new object[] { default(CombatCommandKey), -1f });
                Assert.That(player.currentCast.DirectMeleeSpecial.Id, Is.EqualTo("base_sword_default"));
                string oldDirectBody = arena.Presentation.CurrentAttackMotionVersion;
                string oldFixedMedia = arena.GetMeleeFrame("base_sword_default", false).Media.FixedTiming.ContentHash;
                Assert.That(arena.TryGetBasicSwordMotion(out var firstMotion, out var error), Is.True, error);
                var local = arena.GetMeleeFrame("hammer_basic_0", true); var document = Project(local); document.timeline.frames[0].seconds += .03f;
                var candidate = Compile(local, document); Assert.That(arena.TryApplyMeleeFrames(new[] { candidate }, false, out error), Is.True, error);
                prepare("hammer"); basicStart.Invoke(player, new object[] { default(CombatCommandKey), 0, -1f });
                var running = player.currentCast; Assert.That(running.FrameTimingVersion, Is.EqualTo(candidate.ContentHash));
                string pinnedBody = arena.Presentation.CurrentAttackMotionVersion; var cueMapping = running.FrameTiming.CueMapping;
                var weights = firstMotion.ToDocument(); weights.steps[0].seconds += .05f;
                Assert.That(arena.TryApplyBasicSwordMotion(weights, false, out error), Is.True, error);
                var retained = arena.GetMeleeFrame(local.Id, true);
                Assert.That(retained.ContentHash, Is.EqualTo(candidate.ContentHash)); Assert.That(retained.CueMapping.ContentHash, Is.EqualTo(cueMapping.ContentHash));
                Assert.That(retained.Timeline.Frames.Select(value => value.Seconds), Is.EqualTo(candidate.Timeline.Frames.Select(value => value.Seconds)));
                Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(pinnedBody)); Assert.That(player.currentCast, Is.SameAs(running));
                Assert.That(arena.GetMeleeFrame("base_sword_default", false).Media.FixedTiming.ContentHash, Is.Not.EqualTo(oldFixedMedia));
                prepare("base_sword"); specialStart.Invoke(player, new object[] { default(CombatCommandKey), -1f });
                Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Not.EqualTo(oldDirectBody));
                string state = arena.MeleeFrameVersion, binding = arena.MotionBindingHash; running = player.currentCast; float now = arena.Now;
                Assert.That(arena.TryGetBasicSwordMotion(out var accepted, out error), Is.True, error); var structural = arena.MotionRegistry.ToDocument(); structural.motions.Single(value => value.id == "hammer/attack1").steps[0].id += "-changed";
                int resets = 0; Action count = () => resets++; arena.ResetCompleted += count;
                try { Assert.That(arena.TryApplyMotionRegistry(structural, true, out error), Is.False); }
                finally { arena.ResetCompleted -= count; }
                Assert.That(resets, Is.Zero); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(player.currentCast, Is.SameAs(running));
                Assert.That(arena.MeleeFrameVersion, Is.EqualTo(state)); Assert.That(arena.MotionBindingHash, Is.EqualTo(binding));
                Assert.That(arena.TryGetBasicSwordMotion(out var afterFailure, out error), Is.True, error); Assert.That(afterFailure.ContentHash, Is.EqualTo(accepted.ContentHash));
            }
        }
        [UnityTest] public IEnumerator GameActivationRejectsFrameResetAppliesBeforeAnyPublication() => ActivationResetAdmission("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabActivationRejectsFrameResetAppliesBeforeAnyPublication() => ActivationResetAdmission("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        IEnumerator ActivationResetAdmission(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var floor = new GameObject("FrameResetAdmissionFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    var original = arena.MeleeFrameState; var basics = original.Actions.Where(value => value.Family == MeleeFrameFamily.Basic).Select(value => value.NumericAction).ToArray();
                    var direct = original.Actions.Where(value => value.Family == MeleeFrameFamily.Direct).Select(value => value.NumericDirect).ToArray();
                    Assert.That(arena.TryGetBasicSwordMotion(out var motion, out var error), Is.True, error);
                    var registry = arena.MotionRegistry; var first = basics.Single(value => value.Id == FixedAttackMotion.FirstSwordAction);
                    var firstFrame = MeleeFrameCompiler.CompileFirst(first, motion, MeleeFrameCompiler.ProjectFirst(first, motion, Array.Find(arena.catalog.forms, value => value.id == "base_sword").inputSpeed), Array.Find(arena.catalog.forms, value => value.id == "base_sword").inputSpeed);
                    var other = original.Actions.Single(value => value.Id == "base_sword_basic_1"); var document = Project(other); document.timeline.frames[0].seconds += .03f; var frame = Compile(other, document);
                    var firstNumeric = first.ToData(); firstNumeric.damage += 1;
                    var otherNumeric = other.NumericAction.ToData(); otherNumeric.damage += 1;
                    Assert.That(BasicMeleeDefinition.TryCreate(other.Id, otherNumeric, out var changedOther, out error), Is.True, error);
                    var directNumeric = direct[0].ToBasicData(); directNumeric.damage += 1; var changedDirect = direct[0].Recompile(directNumeric);
                    var changedMotion = motion.ToDocument(); changedMotion.steps[0].seconds += .01f;
                    var changedRegistry = registry.ToDocument(); changedRegistry.motions.Single(value => value.id == "rapier/attack1").steps[0].seconds += .01f;
                    for (int operation = 0; operation < 9; operation++)
                    {
                        Assert.That(arena.TryApplyMeleeFrameState(basics, direct, motion, registry, null, original.ToDocument(), false, out error), Is.True, error);
                        if (operation == 3) Assert.That(arena.TryApplySwordFrameTiming(firstFrame, false, out error), Is.True, error);
                        driver.Reset(); player.selected = 0;
                        foreach (var target in arena.enemies) { target.aggressive = false; target.Position = new Vector2(35, 1000); }
                        Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, value => value.id == "base_sword"), 0, out error), Is.True, error);
                        foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
                        player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; Physics2D.SyncTransforms();
                        bool observed = false, accepted = true; int resets = 0; string rejected = null; Action count = () => resets++;
                        Application.LogCallback activate = (message, stack, type) => {
                            if (observed || !message.Contains("activate " + FixedAttackMotion.FirstSwordAction)) return;
                            observed = true; string state = arena.MeleeFrameVersion, binding = arena.MotionBindingHash, media = JsonUtility.ToJson(arena.characterAnimations);
                            var delegated = arena.FrameSwordTiming; var running = player.currentCast; float now = arena.Now; long tick = arena.SimulationTickCount;
                            switch (operation)
                            {
                                case 0: accepted = arena.TryApplyMeleeFrames(new[] { frame }, true, out rejected); break;
                                case 1: accepted = arena.TryApplyMeleeFrameState(basics, direct, motion, registry, firstFrame, original.ToDocument(), true, out rejected); break;
                                case 2: accepted = arena.TryApplySwordFrameTiming(firstFrame, true, out rejected); break;
                                case 3: accepted = arena.TryRestoreSwordFixedTiming(true, out rejected); break;
                                case 4: accepted = arena.TryApplyBasicSwordMotion(changedMotion, true, out rejected); break;
                                case 5: accepted = arena.TryApplyMotionRegistry(changedRegistry, true, out rejected); break;
                                case 6: accepted = arena.TryApplyBasicSwordFirst(firstNumeric, true, out rejected); break;
                                case 7: accepted = arena.TryApplyBasicMelee(new[] { changedOther }, true, out rejected); break;
                                default: accepted = arena.TryApplyDirectMeleeSpecials(new[] { changedDirect }, true, out rejected); break;
                            }
                            Assert.That(accepted, Is.False, "Reset apply " + operation); Assert.That(rejected, Is.Not.Null.And.Not.Empty);
                            Assert.That(arena.MeleeFrameVersion, Is.EqualTo(state)); Assert.That(arena.MotionBindingHash, Is.EqualTo(binding)); Assert.That(JsonUtility.ToJson(arena.characterAnimations), Is.EqualTo(media));
                            Assert.That(arena.FrameSwordTiming, Is.SameAs(delegated)); Assert.That(player.currentCast, Is.SameAs(running)); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.SimulationTickCount, Is.EqualTo(tick));
                        };
                        arena.ResetCompleted += count; Application.logMessageReceived += activate;
                        try { for (int step = 0; step < 40 && !observed; step++) driver.Step(.02f, new CombatInput(0, step == 0 ? CombatButtons.Basic : CombatButtons.None)); }
                        finally { Application.logMessageReceived -= activate; arena.ResetCompleted -= count; }
                        Assert.That(observed, Is.True, "Native activation " + operation); Assert.That(accepted, Is.False); Assert.That(resets, Is.Zero);
                        Assert.That(arena.SimulationFaulted, Is.False); long completed = arena.SimulationTickCount; driver.Step(.02f); Assert.That(arena.SimulationTickCount, Is.EqualTo(completed + 1));
                    }
                }
            }
            finally { Object.DestroyImmediate(floor); }
        }
        [UnityTest] public IEnumerator GameFramesPinRunningCastAndPublishWholeStateAtomically() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabFramesPinRunningCastAndPublishWholeStateAtomically() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var floor = new GameObject("MeleeFrameFamilyFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); foreach (var target in arena.enemies) { target.aggressive = false; target.Position = new Vector2(35, 1000); }
                    player.selected = 0; Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "base_sword"), 0, out var error), Is.True, error);
                    player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; Physics2D.SyncTransforms();
                    var numeric = arena.GetBasicMelee("base_sword", 1); var original = arena.MeleeFrameState; var originalMotion = arena.FrameSwordTiming;
                    var source = arena.GetMeleeFrame(numeric.Id, true); var document = Project(source); document.timeline.frames[0].seconds += .03f; var candidate = Compile(source, document);
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); var active = player.currentCast;
                    Assert.That(arena.TryApplyMeleeFrames(new[] { candidate }, false, out error), Is.True, error); Assert.That(active.FrameTiming, Is.Null); Assert.That(active.ImpactTime, Is.EqualTo(.2f));
                    var changed = numeric.ToData(); changed.damage += 5; Assert.That(BasicMeleeDefinition.TryCreate(numeric.Id, changed, out var changedNumeric, out error), Is.True, error);
                    Assert.That(arena.TryApplyBasicMelee(new[] { changedNumeric }, false, out error), Is.True, error);
                    player.CancelAction(); var reflected = typeof(CombatPlayer).GetMethod("BeginBasic", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    reflected.Invoke(player, new object[] { default(CombatCommandKey), 1, -1f }); var next = player.currentCast;
                    Assert.That(next.FrameTiming, Is.Not.Null); Assert.That(next.ImpactTime, Is.EqualTo(candidate.Action.Impact).Within(.000001f)); Assert.That(next.Definition.Hits[0].Damage, Is.EqualTo(changedNumeric.Damage));
                    var allBasics = arena.MeleeFrameState.Actions.Where(value => value.Family == MeleeFrameFamily.Basic).Select(value => value.NumericAction).ToArray();
                    var direct = arena.MeleeFrameState.Actions.Where(value => value.Family == MeleeFrameFamily.Direct).Select(value => value.NumericDirect).ToArray();
                    var stateA = arena.MeleeFrameState; var numericB = allBasics.Select(value => value.Id == numeric.Id ? numeric : value).ToArray();
                    var sourceB = original.ToDocument(); var first = stateA.Find(new MotionActionKey(FixedAttackMotion.FirstSwordAction, MotionActionKind.Basic)).NumericAction;
                    int resets = 0; Action coherent = () => { resets++; Assert.That(arena.GetBasicMelee("base_sword", 1).ContentHash, Is.EqualTo(numeric.ContentHash)); Assert.That(arena.GetMeleeFrame(numeric.Id, true).Mode, Is.EqualTo(MeleeTimingMode.CombatTimeFixed)); };
                    arena.ResetCompleted += coherent;
                    try { Assert.That(arena.TryApplyMeleeFrameState(numericB, direct, originalMotion?.SourceMotion ?? FrameMotionSource.Load(repository, out _).FixedBinding,
                        arena.MotionRegistry, originalMotion, sourceB, true, out error), Is.True, error); }
                    finally { arena.ResetCompleted -= coherent; }
                    Assert.That(resets, Is.EqualTo(1));
                    Assert.That(arena.TryApplyMeleeFrameState(allBasics, direct, FrameMotionSource.Load(repository, out _).FixedBinding, arena.MotionRegistry, originalMotion, stateA.ToDocument(), true, out error), Is.True, error);
                    Assert.That(arena.GetMeleeFrame(numeric.Id, true).Mode, Is.EqualTo(MeleeTimingMode.FrameTime));
                }
            }
            finally { Object.DestroyImmediate(floor); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ActualWindowSelectsAllTwentyAndEditsIndependentFrameAuthority()
        {
            window = ScriptableObject.CreateInstance<MeleeFrameWindow>(); window.Show(); window.CreateGUI(); window.LoadRepository(repository); yield return null;
            var root = window.rootVisualElement; var selector = root.Q<DropdownField>("action"); Assert.That(selector.choices.Count, Is.EqualTo(20));
            foreach (string id in selector.choices.ToArray()) { selector.value = id; Assert.That(root.Q<Label>("state").text, Does.Not.StartWith("Invalid"), id); }
            selector.value = "base_sword_basic_1"; Click("convert"); var field = root.Query<FloatField>().ToList().First(value => value.name.StartsWith("frame-", StringComparison.Ordinal));
            float before = field.value; field.value += .03f; Assert.That(window.Candidate.Action.Impact, Is.EqualTo(.23f).Within(.000001f));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null; Assert.That(window.Candidate.Action.Impact, Is.EqualTo(.2f).Within(.000001f));
            Assert.That(File.ReadAllText(Path.Combine(repository, MeleeFrameSource.RelativePath)), Does.Contain("\"mode\": 0"));
        }
        void Click(string name) { var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True, name); using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); } }
    }
}
