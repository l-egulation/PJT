using System;
using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.CombatLab;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class FiniteActionTrackComparisonTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator BothActualSpecialsUseIdenticalInputRecordsAndRestoreTheAppliedPair()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var prior = FiniteActionTrackVersion.Capture(arena);
            var candidate = new FiniteActionTrackVersion(prior.Actions.Select(value => {
                var document = value.ToDocument(); document.timeline.frames[0].seconds += .03f; return FiniteActionTrackSourceTests.NumericEdit(value.Recompile(document));
            }), prior.Movement);
            var result = FiniteActionTrackComparison.Run(arena, prior, candidate);
            Assert.That(result.A.Count, Is.EqualTo(5)); Assert.That(result.B.Count, Is.EqualTo(5));
            for (int index = 0; index < result.A.Count; index++) {
                Assert.That(result.A[index].Inputs.ContentHash, Is.EqualTo(result.B[index].Inputs.ContentHash));
                Assert.That(result.A[index].Inputs.TickCount, Is.EqualTo(FiniteActionTrackComparison.Ticks));
            }
            Assert.That(result.A[0].HealthLost, Is.EqualTo(40)); Assert.That(result.A[0].Travel, Is.EqualTo(4).Within(.001f));
            Assert.That(result.A[1].Travel, Is.LessThan(result.A[0].Travel));
            foreach (int index in new[] { 2, 3, 4 }) { Assert.That(result.A[index].HealthLost, Is.EqualTo(50)); Assert.That(result.A[index].ReceivedHits, Is.EqualTo(2)); }
            Assert.That(result.B[0].FirstHitTick, Is.GreaterThan(result.A[0].FirstHitTick));
            Assert.That(result.B[0].HealthLost, Is.EqualTo(47)); Assert.That(result.B[0].Travel, Is.EqualTo(2.4f).Within(.001f));
            foreach (int index in new[] { 2, 3, 4 }) { Assert.That(result.B[index].HealthLost, Is.EqualTo(65)); Assert.That(result.B[index].ReceivedHits, Is.EqualTo(2)); }
            Assert.That(arena.GetActionTracks(ActionTrackDefinition.MovementId), Is.SameAs(prior.Actions[0]));
            Assert.That(arena.GetActionTracks(ActionTrackDefinition.FollowUpId), Is.SameAs(prior.Actions[1]));
            Assert.That(arena.ManualSimulationActive, Is.False); Assert.That(arena.SimulationFaulted, Is.False);
        }
        [Test] public void FiniteSpatialSelectionScrubsMotionAndIndependentTailWithoutStartingACombat()
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var media = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var source = catalog.forms.SelectMany(form => form.specials).Single(skill => skill.id == ActionTrackDefinition.MovementId);
            var tracks = new ActionTrackDefinition(source); var model = ActionGeometryPreviewModel.Compile(tracks, 0, media, _ => null);
            Assert.That(model.Sample(tracks.Motion.Start, 1, 0).Root.x, Is.Zero);
            Assert.That(model.Sample(tracks.Motion.End, 1, 0).Root.x, Is.EqualTo(4).Within(.000001f));
            Assert.That(model.Sample(tracks.Motion.End, -1, 0).Root.x, Is.EqualTo(-4).Within(.000001f));
            Assert.That(model.Sample(0, 1, 0).Root.x, Is.Zero, "A backwards scrub samples, it does not reverse/fire a runner.");
            Assert.That(model.Sample(tracks.Hits[0].Start, 1, 0).HitActive, Is.True); Assert.That(model.Sample(tracks.Hits[0].End, 1, 0).HitActive, Is.False);
            source = catalog.forms.SelectMany(form => form.specials).Single(skill => skill.id == ActionTrackDefinition.FollowUpId);
            var follow = new ActionTrackDefinition(source); var document = follow.ToDocument(); document.timeline.markers.Single(marker => marker.id == "echo-start").offset = .6f;
            var edited = follow.Recompile(document); var tail = ActionGeometryPreviewModel.Compile(edited, 1, media, _ => null);
            var sample = tail.Sample(edited.Hits[1].Start, 1, 0);
            Assert.That(sample.HitActive, Is.True); Assert.That(sample.FrameIndex, Is.EqualTo(-1), "No body art is invented after body end.");
            Assert.That(sample.Hitbox.center, Is.EqualTo(new Vector2(1.421875f, 1))); Assert.That(tail.End, Is.EqualTo(edited.TailEnd));
            Assert.That(tail.SelectedHitId, Is.EqualTo("echo"));
        }
    }
}
