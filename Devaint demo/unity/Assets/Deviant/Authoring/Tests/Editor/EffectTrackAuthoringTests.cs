using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.CombatLab;
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
    public class EffectTrackAuthoringTests
    {
        string repository;
        [SetUp] public void SetUp() => repository = BasicMeleeSourceTests.CopyRepository();
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            foreach (var window in Resources.FindObjectsOfTypeAll<FiniteActionTrackWindow>().Where(value => value.name == "EffectTrackTestWindow")) window.Close();
            if (Directory.Exists(repository)) Directory.Delete(repository, true);
        }
        [Test] public void SelectedWholeActionSavePreservesOtherThreeAndGeneratedStartupCompilation()
        {
            var original = FiniteActionTrackSource.Load(repository, out var hash); var guard = original.Single(value => value.Id == ActionTrackDefinition.GuardId);
            var document = guard.ToDocument(); document.timeline.markers.Single(marker => marker.id == "guard-end").offset = 1.25f;
            var numeric = guard.ToNumericData(); numeric.guardHp = 45; numeric.guardReduction = .25f; var edited = guard.Recompile(document, numeric); FiniteActionTrackSource.Save(repository, hash, new[] { edited });
            var reloaded = FiniteActionTrackSource.Load(repository, out _);
            foreach (var source in original) Assert.That(reloaded.Single(value => value.Id == source.Id).ContentHash, Is.EqualTo(source.Id == guard.Id ? edited.ContentHash : source.ContentHash));
            BasicSwordSource.Regenerate(repository); var generated = BasicMeleeSourceTests.Generated(repository);
            Assert.DoesNotThrow(() => FiniteActionTrackSource.ValidateGenerated(repository, generated));
            var fresh = new ActionTrackDefinition(generated.forms.SelectMany(form => form.specials).Single(value => value.id == guard.Id));
            Assert.That(fresh.Defense.Duration, Is.EqualTo(1.25f)); Assert.That(fresh.Execution.HasOwnerGuard, Is.True);
            Assert.That(fresh.Execution.Effect.Hp, Is.EqualTo(45)); Assert.That(fresh.Execution.Effect.Reduction, Is.EqualTo(.25f));
            var stale = generated.forms.SelectMany(form => form.specials).Single(value => value.id == ActionTrackDefinition.WaveId);
            stale.actionTracks.timeline.markers.Single(marker => marker.id == "wave-end").offset += .01f;
            Assert.Throws<ArgumentException>(() => FiniteActionTrackSource.ValidateGenerated(repository, generated));
        }
        [Test] public void WavePreviewUsesExistingEffectOriginAndGuardHasNoFabricatedHitGeometry()
        {
            var definitions = FiniteActionTrackSource.Load(repository, out _); var media = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var wave = definitions.Single(value => value.Id == ActionTrackDefinition.WaveId); var preview = ActionGeometryPreviewModel.Compile(wave, 0, media, _ => null);
            var first = preview.Sample(wave.Activation, 1, 0); Assert.That(first.Hitbox.center, Is.EqualTo(new Vector2(1.15625f, 1)));
            var later = preview.Sample(wave.Activation + .25f, 1, 0); Assert.That(later.Hitbox.center.x, Is.EqualTo(5.15625f));
            Assert.That(preview.Sample(wave.Activation + .25f, -1, 0).Hitbox.center.x, Is.EqualTo(-5.15625f));
            Assert.That(preview.Sample(0, 1, 0).HitActive, Is.False); Assert.That(preview.Sample(wave.Activation, 1, 0).Hitbox.center, Is.EqualTo(first.Hitbox.center));
            var numeric = wave.ToNumericData(); numeric.waveSpeed = 8; numeric.waveDistance = 2; numeric.waveSpawnX = 2; numeric.waveSpawnY = 1.25f; var edited = wave.Recompile(wave.ToDocument(), numeric);
            var numericPreview = ActionGeometryPreviewModel.Compile(edited, 0, media, _ => null); var numericSample = numericPreview.Sample(edited.Activation + .1f, 1, 0); Assert.That(numericSample.Hitbox.center.x, Is.EqualTo(2.8f).Within(.000001f)); Assert.That(numericSample.Hitbox.center.y, Is.EqualTo(1.25f)); Assert.That(numericPreview.Sample(0, 1, 0).HitActive, Is.False);
            var guard = definitions.Single(value => value.Id == ActionTrackDefinition.GuardId); var defense = ActionGeometryPreviewModel.Compile(guard, 0, media, _ => null);
            var sample = defense.Sample(guard.Defense.Start + 1, 1, 0); Assert.That(sample.HitActive, Is.False); Assert.That(sample.Hitbox, Is.EqualTo(default(Rect)));
            Assert.That(defense.End, Is.EqualTo(guard.Defense.End)); Assert.That(defense.SelectedHitId, Is.EqualTo("guard"));
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator ActualWindowSelectsEditsWaveAndGuardWithoutRecompileOrSourceMutation()
        {
            var window = ScriptableObject.CreateInstance<FiniteActionTrackWindow>(); window.name = "EffectTrackTestWindow"; window.Show(); window.CreateGUI(); window.LoadRepository(repository); yield return null;
            byte[] before = File.ReadAllBytes(Path.Combine(repository, FiniteActionTrackSource.RelativePath)); var root = window.rootVisualElement;
            var action = root.Q<DropdownField>("action"); Assert.That(action.choices, Is.EqualTo(ActionTrackDefinition.SupportedIds)); action.value = ActionTrackDefinition.WaveId;
            root.Q<FloatField>("frame-windup").value += .03f; root.Q<FloatField>("offset-wave-end").value = .75f;
            root.Q<FloatField>("box-wave-height").value = 1.5f;
            root.Q<FloatField>("damage-wave").value = 53; root.Q<FloatField>("wave-speed").value = 8; root.Q<FloatField>("wave-distance").value = 2; root.Q<FloatField>("wave-spawn-x").value = 2;
            var wave = window.CompileCandidate().Single(value => value.Id == ActionTrackDefinition.WaveId);
            Assert.That(wave.Activation, Is.EqualTo(.23f).Within(.000001f)); Assert.That(wave.Effect.Duration, Is.EqualTo(.75f));
            Assert.That(wave.Execution.Hits[0].Damage, Is.EqualTo(53)); Assert.That(wave.Execution.Effect.Speed, Is.EqualTo(8)); Assert.That(wave.Execution.Effect.Distance, Is.EqualTo(2)); Assert.That(wave.Execution.Effect.X, Is.EqualTo(2));
            root.Q<Slider>("geometry-time").value = .4f; var preview = root.Q<ActionGeometryPreview>(); Assert.That(preview.CurrentSample.HitActive, Is.True);
            action.value = ActionTrackDefinition.GuardId; Assert.That(root.Q<DropdownField>("hit").enabledSelf, Is.False);
            root.Q<FloatField>("offset-guard-end").value = 1.25f; Assert.That(window.CompileCandidate().Single(value => value.Id == ActionTrackDefinition.GuardId).Defense.Duration, Is.EqualTo(1.25f));
            root.Q<FloatField>("guard-hp").value = 50; root.Q<FloatField>("guard-reduction").value = .25f; Assert.That(window.CompileCandidate().Single(value => value.Id == ActionTrackDefinition.GuardId).Execution.Effect.Hp, Is.EqualTo(50));
            Assert.That(preview.CurrentSample.HitActive, Is.False); Assert.That(root.Q<FloatField>("box-guard-width"), Is.Null);
            root.Q<FloatField>("offset-guard-end").value = -1; Assert.That(root.Q<Button>("saveSource").enabledSelf, Is.False); Assert.That(preview.Model, Is.Null);
            Assert.That(File.ReadAllBytes(Path.Combine(repository, FiniteActionTrackSource.RelativePath)), Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator ActualWaveAndGuardComparisonReplaysSameInputAndRestoresUnselectedActions()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var a = EffectTrackVersion.Capture(arena);
            var unselected = arena.GetActionTracks(ActionTrackDefinition.MovementId);
            var b = new EffectTrackVersion(a.Actions.Select(source => { var document = source.ToDocument(); document.timeline.frames[0].seconds += .03f; var numeric = source.ToNumericData();
                if (source.Defense != null) { document.timeline.markers.Single(marker => marker.id == "guard-end").offset = .25f; numeric.guardHp = 50; numeric.guardReduction = .25f; }
                else { numeric.hits[0].damage = 53; numeric.waveSpeed = 12; numeric.waveDistance = 8; numeric.waveSpawnX = 2; }
                return source.Recompile(document, numeric); }), a.Movement);
            var result = EffectTrackComparison.Run(arena, a, b);
            for (int index = 0; index < result.A.Count; index++) Assert.That(result.A[index].Inputs.ContentHash, Is.EqualTo(result.B[index].Inputs.ContentHash));
            Assert.That(result.A[0].TargetHealthLost, Is.EqualTo(40)); Assert.That(result.A[0].TargetHits, Is.EqualTo(1));
            Assert.That(result.B[0].TargetHealthLost, Is.EqualTo(53)); Assert.That(result.B[0].TargetHits, Is.EqualTo(1));
            Assert.That(result.A[1].PlayerHealthLost, Is.EqualTo(30).Within(.00001f)); Assert.That(result.B[1].PlayerHealthLost, Is.EqualTo(100));
            Assert.That(result.A[2].PlayerHealthLost, Is.EqualTo(10)); Assert.That(arena.GetActionTracks(ActionTrackDefinition.MovementId), Is.SameAs(unselected));
            Assert.That(arena.GetActionTracks(ActionTrackDefinition.GuardId), Is.SameAs(a.Actions.Single(value => value.Id == ActionTrackDefinition.GuardId)));
        }
    }
}
